// Partial de 20260913150000_LevanteVariosRegistrosDiaPesajeUniformidad: SQL verbatim.
// V2 / Nueva = espejos en backend/sql/ (este commit); V1 / Prev = versiones anteriores para el Down.

namespace ZooSanMarino.Infrastructure.Migrations
{
    public partial class LevanteVariosRegistrosDiaPesajeUniformidad
    {
        private const string FnSeguimientoDiarioLevanteV2 = """
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- fn_seguimiento_diario_levante — grilla diaria CANÓNICA de levante (tipo_seguimiento='levante')
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- v2 (2026-09-13) — seg_dias_agrupado (solo flag ON), mismo criterio que
--   fn_seguimiento_diario_produccion v4 (plan
--   fase_de_desarrollo/levante_varios_registros_dia_pesaje_uniformidad_plan.md, L1/L2):
--   uniformidad/CV = último registro que la TRAE (un NULL posterior ya no tapa la medición),
--   desempate del «último» por c_id (los forms graban todo el día a mediodía y el ts empataba) y
--   peso/kcal/proteína = promedio de los registros que midieron (> 0). Flag OFF intacto.
--   Detalle junto al CTE. Espejo C#: SeguimientoDiarioLevanteCalculos.AgruparPorDia.
--   Corrige la cabecera de v1: los 3 semanales NO leen esta fn, leen la tabla cruda.
-- v1 (2026-09-05) — creación (plan
--   fase_de_desarrollo/seguimiento_produccion_multiples_registros_dia_plan.md, §5/S6-S7)
--
--   Antes de esta fn, NADA dedupeaba/agrupaba por día: `sp_recalcular_seguimiento_levante`,
--   `fn_indicadores_levante_postura`, `fn_reporte_semanal_levante_extras` y
--   `fn_resumen_semanal_ra_pesadas_levante` leían `seguimiento_diario_levante` cruda —lo
--   opuesto al bug de producción (que descartaba en silencio): acá TODO se sumaba/contaba
--   fila por fila. Con el índice único de siempre eso nunca se manifestó (nunca hubo 2 filas
--   el mismo lote+día); con el flag `companies.permite_multiples_seguimientos_diarios` ON
--   para LEVANTE, sin esta fn los 4 consumidores sobre-contarían "días con registro" y
--   `sp_recalcular_seguimiento_levante` calcularía `gr_ave_dia_h/m` (delta de peso día a día,
--   vía LAG) comparando dos registros del MISMO día como si fueran de días consecutivos.
--
--   Diseño (deliberadamente más simple que fn_seguimiento_diario_produccion — sin rama
--   LPP/legacy, sin saldo de aves acá: eso lo sigue calculando cada consumidor con SUS
--   propios acumuladores, ahora sobre una fila por día en vez de una fila por registro):
--   • SUMA de siempre (Postgres: SUM es asociativa) para lo aditivo — mortalidad, selección,
--     error de sexaje, consumo, traslados, venta de aves. El total semanal/acumulado NO
--     cambia si se agrupa por día antes: cambia lo que SÍ estaba roto, `COUNT(*)`/`dias`
--     (pasa de contar FILAS a contar DÍAS) y los deltas día-a-día (LAG) del SP.
--   • PROMEDIO simple para peso/uniformidad/CV/kcal/prot (mismo criterio que producción:
--     equivale a ponderar por aves vivas, que es un valor de DÍA constante ese día).
--   • Con UN solo registro el día (el caso de siempre, flag OFF en TODAS las demás empresas)
--     cada fórmula de abajo da exactamente el valor de esa fila — byte a byte igual a leer
--     la tabla cruda directamente. `seg_dias_dedup` (branch flag OFF) es DISTINCT ON por día:
--     con el índice único vigente nunca hay más de 1 fila, así que es un no-op verificable.
--
--   Consumidores: sp_recalcular_seguimiento_levante (grilla+saldo diario de
--   produccion_resultado_levante — modal «Cálculos», GET …/por-lote/{id}/resultado). Los 3 semanales
--   (fn_indicadores_levante_postura, fn_reporte_semanal_levante_extras,
--   fn_resumen_semanal_ra_pesadas_levante) leen seguimiento_diario_levante CRUDA y agrupan el pesaje
--   por día por su cuenta (PesajeSemanalLevanteCalculos).
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION fn_seguimiento_diario_levante(p_lote_id TEXT)
RETURNS TABLE (
    reg_date                    DATE,
    -- Timestamp representativo del día: el único registro si no hay agrupación (byte a byte
    -- igual al `fecha` crudo de siempre — necesario para aritmética que hoy resta timestamptz,
    -- p.ej. sp_recalcular_seguimiento_levante); MIN(fecha) del día si hay 2+ registros.
    fecha_ts                    TIMESTAMPTZ,
    reg_id                      BIGINT,
    mortalidad_hembras          INT,
    mortalidad_machos           INT,
    sel_h                       INT,
    sel_m                       INT,
    error_sexaje_hembras        INT,
    error_sexaje_machos         INT,
    consumo_kg_hembras          NUMERIC,
    consumo_kg_machos           NUMERIC,
    traslado_salida_hembras     INT,
    traslado_salida_machos      INT,
    traslado_ingreso_hembras    INT,
    traslado_ingreso_machos     INT,
    venta_aves_hembras          INT,
    venta_aves_machos           INT,
    peso_prom_hembras           DOUBLE PRECISION,
    peso_prom_machos            DOUBLE PRECISION,
    uniformidad_hembras         DOUBLE PRECISION,
    uniformidad_machos          DOUBLE PRECISION,
    cv_hembras                  DOUBLE PRECISION,
    cv_machos                   DOUBLE PRECISION,
    kcal_al_h                   DOUBLE PRECISION,
    prot_al_h                   DOUBLE PRECISION
)
LANGUAGE sql STABLE
AS $$
WITH ctx AS (
    SELECT COALESCE(comp.permite_multiples_seguimientos_diarios, false) AS permite_multiples
      FROM lotes l
      LEFT JOIN companies comp ON comp.id = l.company_id
     WHERE l.lote_id = NULLIF(p_lote_id, '')::int
       AND l.deleted_at IS NULL
),
crudos AS (
    SELECT sl.id::bigint                            AS c_id,
           sl.fecha                                  AS c_ts,
           COALESCE(sl.mortalidad_hembras, 0)        AS c_mort_h,
           COALESCE(sl.mortalidad_machos, 0)         AS c_mort_m,
           COALESCE(sl.sel_h, 0)                     AS c_sel_h,
           COALESCE(sl.sel_m, 0)                     AS c_sel_m,
           COALESCE(sl.error_sexaje_hembras, 0)      AS c_err_h,
           COALESCE(sl.error_sexaje_machos, 0)       AS c_err_m,
           COALESCE(sl.consumo_kg_hembras, 0)        AS c_cons_h,
           COALESCE(sl.consumo_kg_machos, 0)         AS c_cons_m,
           COALESCE(sl.traslado_salida_hembras, 0)   AS c_tras_sal_h,
           COALESCE(sl.traslado_salida_machos, 0)    AS c_tras_sal_m,
           COALESCE(sl.traslado_ingreso_hembras, 0)  AS c_tras_ing_h,
           COALESCE(sl.traslado_ingreso_machos, 0)   AS c_tras_ing_m,
           COALESCE(sl.venta_aves_hembras, 0)        AS c_venta_h,
           COALESCE(sl.venta_aves_machos, 0)         AS c_venta_m,
           sl.peso_prom_hembras                      AS c_peso_h,
           sl.peso_prom_machos                       AS c_peso_m,
           sl.uniformidad_hembras                    AS c_unif_h,
           sl.uniformidad_machos                     AS c_unif_m,
           sl.cv_hembras                             AS c_cv_h,
           sl.cv_machos                              AS c_cv_m,
           sl.kcal_al_h                               AS c_kcal_h,
           sl.prot_al_h                               AS c_prot_h,
           (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante'
       AND sl.lote_id = p_lote_id
),
seg_dias_dedup AS (
    SELECT DISTINCT ON (c.reg_date)
           c.reg_date, c.c_ts, c.c_id, c.c_mort_h, c.c_mort_m, c.c_sel_h, c.c_sel_m,
           c.c_err_h, c.c_err_m, c.c_cons_h, c.c_cons_m,
           c.c_tras_sal_h, c.c_tras_sal_m, c.c_tras_ing_h, c.c_tras_ing_m,
           c.c_venta_h, c.c_venta_m, c.c_peso_h, c.c_peso_m,
           c.c_unif_h, c.c_unif_m, c.c_cv_h, c.c_cv_m, c.c_kcal_h, c.c_prot_h
      FROM crudos c
     ORDER BY c.reg_date, c.c_ts
),
-- v2 (2026-09-13) — lo NO aditivo se agrupaba mal con 2+ registros el mismo día:
--   • peso por sexo, kcal, proteína → promedio de los registros que MIDIERON (> 0). Si ninguno midió,
--     el AVG de siempre (0 o NULL). Un 0 guardado como «no medido» partía el promedio a la mitad
--     (el caso de peso_huevo en producción: 60 g + un registro sin pesaje ⇒ 30 g).
--   • uniformidad / CV → el ÚLTIMO registro que la TRAE: un registro posterior sin la medición (NULL)
--     tapaba la del día.
--   • «último» = c_ts DESC, c_id DESC: los forms graban todos los registros del día al mediodía, así
--     que el timestamp solo empataba y el ganador no era determinista.
--   Con UN registro el día cada expresión devuelve el valor de esa fila (idéntico a v1).
seg_dias_agrupado AS (
    SELECT
        c.reg_date,
        MIN(c.c_ts)                                             AS c_ts,
        MIN(c.c_id)                                             AS c_id,
        SUM(c.c_mort_h)::int                                    AS c_mort_h,
        SUM(c.c_mort_m)::int                                    AS c_mort_m,
        SUM(c.c_sel_h)::int                                     AS c_sel_h,
        SUM(c.c_sel_m)::int                                     AS c_sel_m,
        SUM(c.c_err_h)::int                                     AS c_err_h,
        SUM(c.c_err_m)::int                                     AS c_err_m,
        SUM(c.c_cons_h)                                         AS c_cons_h,
        SUM(c.c_cons_m)                                         AS c_cons_m,
        SUM(c.c_tras_sal_h)::int                                AS c_tras_sal_h,
        SUM(c.c_tras_sal_m)::int                                AS c_tras_sal_m,
        SUM(c.c_tras_ing_h)::int                                AS c_tras_ing_h,
        SUM(c.c_tras_ing_m)::int                                AS c_tras_ing_m,
        SUM(c.c_venta_h)::int                                   AS c_venta_h,
        SUM(c.c_venta_m)::int                                   AS c_venta_m,
        COALESCE(AVG(c.c_peso_h) FILTER (WHERE c.c_peso_h > 0), AVG(c.c_peso_h)) AS c_peso_h,
        COALESCE(AVG(c.c_peso_m) FILTER (WHERE c.c_peso_m > 0), AVG(c.c_peso_m)) AS c_peso_m,
        -- Uniformidad/CV: mismo criterio que producción — gana el ÚLTIMO registro del día que la
        -- trae, NO se promedia (es una medición puntual, no un consumo acumulable).
        (array_agg(c.c_unif_h ORDER BY c.c_ts DESC, c.c_id DESC) FILTER (WHERE c.c_unif_h IS NOT NULL))[1] AS c_unif_h,
        (array_agg(c.c_unif_m ORDER BY c.c_ts DESC, c.c_id DESC) FILTER (WHERE c.c_unif_m IS NOT NULL))[1] AS c_unif_m,
        (array_agg(c.c_cv_h ORDER BY c.c_ts DESC, c.c_id DESC) FILTER (WHERE c.c_cv_h IS NOT NULL))[1]     AS c_cv_h,
        (array_agg(c.c_cv_m ORDER BY c.c_ts DESC, c.c_id DESC) FILTER (WHERE c.c_cv_m IS NOT NULL))[1]     AS c_cv_m,
        COALESCE(AVG(c.c_kcal_h) FILTER (WHERE c.c_kcal_h > 0), AVG(c.c_kcal_h)) AS c_kcal_h,
        COALESCE(AVG(c.c_prot_h) FILTER (WHERE c.c_prot_h > 0), AVG(c.c_prot_h)) AS c_prot_h
      FROM crudos c
     GROUP BY c.reg_date
),
seg_dias AS (
    -- COALESCE(...,false): si el lote no resuelve en `lotes` (borrado, id inexistente), el
    -- flag es fail-closed y NO debe dejar la fn entera en 0 filas — sigue leyendo crudo.
    SELECT * FROM seg_dias_dedup    WHERE NOT COALESCE((SELECT bool_or(ctx.permite_multiples) FROM ctx), false)
    UNION ALL
    SELECT * FROM seg_dias_agrupado WHERE     COALESCE((SELECT bool_or(ctx.permite_multiples) FROM ctx), false)
)
SELECT
    s.reg_date, s.c_ts, s.c_id,
    s.c_mort_h, s.c_mort_m, s.c_sel_h, s.c_sel_m, s.c_err_h, s.c_err_m,
    s.c_cons_h, s.c_cons_m,
    s.c_tras_sal_h, s.c_tras_sal_m, s.c_tras_ing_h, s.c_tras_ing_m,
    s.c_venta_h, s.c_venta_m,
    s.c_peso_h, s.c_peso_m, s.c_unif_h, s.c_unif_m, s.c_cv_h, s.c_cv_m,
    s.c_kcal_h, s.c_prot_h
  FROM seg_dias s
 ORDER BY s.reg_date;
$$;
""";

        private const string FnSeguimientoDiarioLevanteV1 = """
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- fn_seguimiento_diario_levante — grilla diaria CANÓNICA de levante (tipo_seguimiento='levante')
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- v1 (2026-09-05) — creación (plan
--   fase_de_desarrollo/seguimiento_produccion_multiples_registros_dia_plan.md, §5/S6-S7)
--
--   Antes de esta fn, NADA dedupeaba/agrupaba por día: `sp_recalcular_seguimiento_levante`,
--   `fn_indicadores_levante_postura`, `fn_reporte_semanal_levante_extras` y
--   `fn_resumen_semanal_ra_pesadas_levante` leían `seguimiento_diario_levante` cruda —lo
--   opuesto al bug de producción (que descartaba en silencio): acá TODO se sumaba/contaba
--   fila por fila. Con el índice único de siempre eso nunca se manifestó (nunca hubo 2 filas
--   el mismo lote+día); con el flag `companies.permite_multiples_seguimientos_diarios` ON
--   para LEVANTE, sin esta fn los 4 consumidores sobre-contarían "días con registro" y
--   `sp_recalcular_seguimiento_levante` calcularía `gr_ave_dia_h/m` (delta de peso día a día,
--   vía LAG) comparando dos registros del MISMO día como si fueran de días consecutivos.
--
--   Diseño (deliberadamente más simple que fn_seguimiento_diario_produccion — sin rama
--   LPP/legacy, sin saldo de aves acá: eso lo sigue calculando cada consumidor con SUS
--   propios acumuladores, ahora sobre una fila por día en vez de una fila por registro):
--   • SUMA de siempre (Postgres: SUM es asociativa) para lo aditivo — mortalidad, selección,
--     error de sexaje, consumo, traslados, venta de aves. El total semanal/acumulado NO
--     cambia si se agrupa por día antes: cambia lo que SÍ estaba roto, `COUNT(*)`/`dias`
--     (pasa de contar FILAS a contar DÍAS) y los deltas día-a-día (LAG) del SP.
--   • PROMEDIO simple para peso/uniformidad/CV/kcal/prot (mismo criterio que producción:
--     equivale a ponderar por aves vivas, que es un valor de DÍA constante ese día).
--   • Con UN solo registro el día (el caso de siempre, flag OFF en TODAS las demás empresas)
--     cada fórmula de abajo da exactamente el valor de esa fila — byte a byte igual a leer
--     la tabla cruda directamente. `seg_dias_dedup` (branch flag OFF) es DISTINCT ON por día:
--     con el índice único vigente nunca hay más de 1 fila, así que es un no-op verificable.
--
--   Consumidores: sp_recalcular_seguimiento_levante (grilla+saldo diario de
--   produccion_resultado_levante), fn_indicadores_levante_postura, fn_reporte_semanal_levante_extras,
--   fn_resumen_semanal_ra_pesadas_levante (los 3 semanales, vía TEMP TABLE reconstruida sobre esta fn).
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION fn_seguimiento_diario_levante(p_lote_id TEXT)
RETURNS TABLE (
    reg_date                    DATE,
    -- Timestamp representativo del día: el único registro si no hay agrupación (byte a byte
    -- igual al `fecha` crudo de siempre — necesario para aritmética que hoy resta timestamptz,
    -- p.ej. sp_recalcular_seguimiento_levante); MIN(fecha) del día si hay 2+ registros.
    fecha_ts                    TIMESTAMPTZ,
    reg_id                      BIGINT,
    mortalidad_hembras          INT,
    mortalidad_machos           INT,
    sel_h                       INT,
    sel_m                       INT,
    error_sexaje_hembras        INT,
    error_sexaje_machos         INT,
    consumo_kg_hembras          NUMERIC,
    consumo_kg_machos           NUMERIC,
    traslado_salida_hembras     INT,
    traslado_salida_machos      INT,
    traslado_ingreso_hembras    INT,
    traslado_ingreso_machos     INT,
    venta_aves_hembras          INT,
    venta_aves_machos           INT,
    peso_prom_hembras           DOUBLE PRECISION,
    peso_prom_machos            DOUBLE PRECISION,
    uniformidad_hembras         DOUBLE PRECISION,
    uniformidad_machos          DOUBLE PRECISION,
    cv_hembras                  DOUBLE PRECISION,
    cv_machos                   DOUBLE PRECISION,
    kcal_al_h                   DOUBLE PRECISION,
    prot_al_h                   DOUBLE PRECISION
)
LANGUAGE sql STABLE
AS $$
WITH ctx AS (
    SELECT COALESCE(comp.permite_multiples_seguimientos_diarios, false) AS permite_multiples
      FROM lotes l
      LEFT JOIN companies comp ON comp.id = l.company_id
     WHERE l.lote_id = NULLIF(p_lote_id, '')::int
       AND l.deleted_at IS NULL
),
crudos AS (
    SELECT sl.id::bigint                            AS c_id,
           sl.fecha                                  AS c_ts,
           COALESCE(sl.mortalidad_hembras, 0)        AS c_mort_h,
           COALESCE(sl.mortalidad_machos, 0)         AS c_mort_m,
           COALESCE(sl.sel_h, 0)                     AS c_sel_h,
           COALESCE(sl.sel_m, 0)                     AS c_sel_m,
           COALESCE(sl.error_sexaje_hembras, 0)      AS c_err_h,
           COALESCE(sl.error_sexaje_machos, 0)       AS c_err_m,
           COALESCE(sl.consumo_kg_hembras, 0)        AS c_cons_h,
           COALESCE(sl.consumo_kg_machos, 0)         AS c_cons_m,
           COALESCE(sl.traslado_salida_hembras, 0)   AS c_tras_sal_h,
           COALESCE(sl.traslado_salida_machos, 0)    AS c_tras_sal_m,
           COALESCE(sl.traslado_ingreso_hembras, 0)  AS c_tras_ing_h,
           COALESCE(sl.traslado_ingreso_machos, 0)   AS c_tras_ing_m,
           COALESCE(sl.venta_aves_hembras, 0)        AS c_venta_h,
           COALESCE(sl.venta_aves_machos, 0)         AS c_venta_m,
           sl.peso_prom_hembras                      AS c_peso_h,
           sl.peso_prom_machos                       AS c_peso_m,
           sl.uniformidad_hembras                    AS c_unif_h,
           sl.uniformidad_machos                     AS c_unif_m,
           sl.cv_hembras                             AS c_cv_h,
           sl.cv_machos                              AS c_cv_m,
           sl.kcal_al_h                               AS c_kcal_h,
           sl.prot_al_h                               AS c_prot_h,
           (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante'
       AND sl.lote_id = p_lote_id
),
seg_dias_dedup AS (
    SELECT DISTINCT ON (c.reg_date)
           c.reg_date, c.c_ts, c.c_id, c.c_mort_h, c.c_mort_m, c.c_sel_h, c.c_sel_m,
           c.c_err_h, c.c_err_m, c.c_cons_h, c.c_cons_m,
           c.c_tras_sal_h, c.c_tras_sal_m, c.c_tras_ing_h, c.c_tras_ing_m,
           c.c_venta_h, c.c_venta_m, c.c_peso_h, c.c_peso_m,
           c.c_unif_h, c.c_unif_m, c.c_cv_h, c.c_cv_m, c.c_kcal_h, c.c_prot_h
      FROM crudos c
     ORDER BY c.reg_date, c.c_ts
),
seg_dias_agrupado AS (
    SELECT
        c.reg_date,
        MIN(c.c_ts)                                             AS c_ts,
        MIN(c.c_id)                                             AS c_id,
        SUM(c.c_mort_h)::int                                    AS c_mort_h,
        SUM(c.c_mort_m)::int                                    AS c_mort_m,
        SUM(c.c_sel_h)::int                                     AS c_sel_h,
        SUM(c.c_sel_m)::int                                     AS c_sel_m,
        SUM(c.c_err_h)::int                                     AS c_err_h,
        SUM(c.c_err_m)::int                                     AS c_err_m,
        SUM(c.c_cons_h)                                         AS c_cons_h,
        SUM(c.c_cons_m)                                         AS c_cons_m,
        SUM(c.c_tras_sal_h)::int                                AS c_tras_sal_h,
        SUM(c.c_tras_sal_m)::int                                AS c_tras_sal_m,
        SUM(c.c_tras_ing_h)::int                                AS c_tras_ing_h,
        SUM(c.c_tras_ing_m)::int                                AS c_tras_ing_m,
        SUM(c.c_venta_h)::int                                   AS c_venta_h,
        SUM(c.c_venta_m)::int                                   AS c_venta_m,
        AVG(c.c_peso_h)                                         AS c_peso_h,
        AVG(c.c_peso_m)                                         AS c_peso_m,
        -- Uniformidad/CV: mismo criterio que producción — gana el ÚLTIMO registro del día,
        -- NO se promedia (es una medición puntual, no un consumo acumulable).
        (array_agg(c.c_unif_h ORDER BY c.c_ts DESC))[1]         AS c_unif_h,
        (array_agg(c.c_unif_m ORDER BY c.c_ts DESC))[1]         AS c_unif_m,
        (array_agg(c.c_cv_h ORDER BY c.c_ts DESC))[1]           AS c_cv_h,
        (array_agg(c.c_cv_m ORDER BY c.c_ts DESC))[1]           AS c_cv_m,
        AVG(c.c_kcal_h)                                         AS c_kcal_h,
        AVG(c.c_prot_h)                                         AS c_prot_h
      FROM crudos c
     GROUP BY c.reg_date
),
seg_dias AS (
    -- COALESCE(...,false): si el lote no resuelve en `lotes` (borrado, id inexistente), el
    -- flag es fail-closed y NO debe dejar la fn entera en 0 filas — sigue leyendo crudo.
    SELECT * FROM seg_dias_dedup    WHERE NOT COALESCE((SELECT bool_or(ctx.permite_multiples) FROM ctx), false)
    UNION ALL
    SELECT * FROM seg_dias_agrupado WHERE     COALESCE((SELECT bool_or(ctx.permite_multiples) FROM ctx), false)
)
SELECT
    s.reg_date, s.c_ts, s.c_id,
    s.c_mort_h, s.c_mort_m, s.c_sel_h, s.c_sel_m, s.c_err_h, s.c_err_m,
    s.c_cons_h, s.c_cons_m,
    s.c_tras_sal_h, s.c_tras_sal_m, s.c_tras_ing_h, s.c_tras_ing_m,
    s.c_venta_h, s.c_venta_m,
    s.c_peso_h, s.c_peso_m, s.c_unif_h, s.c_unif_m, s.c_cv_h, s.c_cv_m,
    s.c_kcal_h, s.c_prot_h
  FROM seg_dias s
 ORDER BY s.reg_date;
$$;
""";

        private const string FnReporteSemanalExtrasNueva = """
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- fn_reporte_semanal_levante_extras — complemento por sexo del Reporte Técnico Semanal LEVANTE
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- Fix 2026-09-05 (plan seguimiento_produccion_multiples_registros_dia_plan.md, §5/S6):
--   dias_con_registro contaba FILAS (COUNT(*)), no DIAS calendario. Con el flag
--   companies.permite_multiples_seguimientos_diarios ON para LEVANTE, 2 registros el mismo
--   dia inflarian 'dias' y, con el, el denominador de consumo diario g/ave/dia. Las SUMAS
--   (mortalidad, consumo, traslados) NO cambian: SUM es asociativa, sumar 2 filas del mismo
--   dia o sumar el dia ya agrupado da el MISMO total semanal. Fix quirurgico: COUNT(DISTINCT
--   reg_date) en vez de restructurar la fuente (funcion multi-lote/otros edge-cases finos,
--   no vale la pena el riesgo de tocar mas que el conteo).
-- Fix 2026-09-13 (plan levante_varios_registros_dia_pesaje_uniformidad_plan.md, L3):
--   el PESAJE de la semana tomaba UN registro ("el ultimo con peso>0"). Con 2+ registros el mismo
--   dia no promediaba, perdia el sexo que el ultimo registro no peso (y se arrastraba el de la
--   semana anterior) y la uniformidad/CV salia de ese registro aunque no la trajera. Ahora se arma
--   por DIA, igual que fn_indicadores_levante_postura (20260913120000): ultimo dia con pesaje; peso
--   por sexo = promedio de los registros de ese dia que pesaron ese sexo; uniformidad/CV por sexo =
--   la del ultimo registro (id) de ese dia que la trae. Con un registro con pesaje por dia da lo
--   mismo que antes. Especificacion ejecutable: Application/Calculos/PesajeSemanalLevanteCalculos.cs.
-- Espejo exacto de pg_get_functiondef (ground truth) + este fix, no reformateado.
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION public.fn_reporte_semanal_levante_extras(p_lote_id integer)
 RETURNS TABLE(semana integer, fecha_fin_semana date, dias_con_registro integer, base_hembras double precision, base_machos double precision, aves_hembras_inicio double precision, aves_hembras_fin double precision, aves_machos_inicio double precision, aves_machos_fin double precision, mortalidad_hembras_sem integer, mortalidad_machos_sem integer, seleccion_hembras_sem integer, seleccion_machos_sem integer, error_hembras_sem integer, error_machos_sem integer, traslado_ingreso_hembras_sem integer, traslado_ingreso_machos_sem integer, traslado_salida_hembras_sem integer, traslado_salida_machos_sem integer, consumo_kg_hembras_sem double precision, consumo_kg_machos_sem double precision, kcal_alimento_hembras double precision, prot_alimento_hembras double precision, uniformidad_hembras double precision, uniformidad_machos double precision, cv_hembras double precision, cv_machos double precision, peso_hembras_sem double precision, peso_machos_sem double precision)
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_hembras_l   integer;
    v_machos_l    integer;
    v_enc_date    date;
    v_min_reg     date;
    v_first_ing_h double precision;
    v_first_ing_m double precision;
    v_base_h      double precision;
    v_base_m      double precision;

    v_aves_acum_h double precision;
    v_aves_acum_m double precision;

    v_max_sem     integer;
    s             integer;

    r_mort_h      integer;
    r_mort_m      integer;
    r_sel_h       integer;
    r_sel_m       integer;
    r_err_h       integer;
    r_err_m       integer;
    r_tras_ing_h  integer;
    r_tras_ing_m  integer;
    r_tras_sal_h  integer;
    r_tras_sal_m  integer;
    r_venta_h     integer;
    r_venta_m     integer;
    r_cons_kg_h   double precision;
    r_cons_kg_m   double precision;
    r_dias        integer;
    r_kcal_h      double precision;
    r_prot_h      double precision;
    r_uh          double precision;
    r_um          double precision;
    r_cvh         double precision;
    r_cvm         double precision;
    r_ph          double precision;
    r_pm          double precision;
    v_peso_ant_h  double precision := NULL;
    v_peso_ant_m  double precision := NULL;
    r_peso_h      double precision;
    r_peso_m      double precision;
    r_fin_h       double precision;
    r_fin_m       double precision;
BEGIN
    SELECT l.hembras_l, l.machos_l,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date
      INTO v_hembras_l, v_machos_l, v_enc_date
      FROM lotes l
     WHERE l.lote_id = p_lote_id AND l.deleted_at IS NULL;

    IF NOT FOUND THEN RETURN; END IF;

    -- Aves entradas por traslado en filas que el armado de _seg_sem_rx DESCARTA (puro traslado
    -- > sem 25). Nadie las suma: la ventana las tira. Se rescatan como base cuando el lote no
    -- trae encaset.
    --
    -- ⚠️ El predicado debe ser el MISMO que el WHERE NOT (...) de _seg_sem_rx más abajo. Si acá
    --    entrara una fila que sí se procesa, sus aves contarían DOS veces (base + ingreso).
    -- SUM por sexo, no una sola fila: los sexos pueden llegar en traslados de días distintos,
    -- y con LIMIT 1 el sexo ausente de la fila más antigua quedaba con base 0 ⇒ saldo negativo.
    SELECT COALESCE(SUM(COALESCE(sl.traslado_ingreso_hembras,0)),0)::double precision,
           COALESCE(SUM(COALESCE(sl.traslado_ingreso_machos,0)),0)::double precision
      INTO v_first_ing_h, v_first_ing_m
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
       AND (floor(((( sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date) / 7.0))::int) + 1 > 25
       AND COALESCE(sl.mortalidad_hembras,0) = 0 AND COALESCE(sl.mortalidad_machos,0) = 0
       AND COALESCE(sl.sel_h,0) = 0 AND COALESCE(sl.sel_m,0) = 0
       AND COALESCE(sl.error_sexaje_hembras,0) = 0 AND COALESCE(sl.error_sexaje_machos,0) = 0
       AND COALESCE(sl.consumo_kg_hembras,0) = 0 AND COALESCE(sl.consumo_kg_machos,0) = 0
       AND COALESCE(sl.peso_prom_hembras,0) = 0 AND COALESCE(sl.peso_prom_machos,0) = 0
       AND COALESCE(sl.venta_aves_hembras,0) = 0 AND COALESCE(sl.venta_aves_machos,0) = 0
       AND (COALESCE(sl.traslado_salida_hembras,0) + COALESCE(sl.traslado_salida_machos,0)
          + COALESCE(sl.traslado_ingreso_hembras,0) + COALESCE(sl.traslado_ingreso_machos,0)) > 0;
    v_first_ing_h := COALESCE(v_first_ing_h, 0);
    v_first_ing_m := COALESCE(v_first_ing_m, 0);

    SELECT MIN((sl.fecha AT TIME ZONE 'America/Bogota')::date)
      INTO v_min_reg
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text;

    IF v_min_reg IS NULL THEN RETURN; END IF;
    IF v_enc_date IS NULL OR v_enc_date > v_min_reg THEN RETURN; END IF;

    v_base_h := COALESCE(NULLIF(v_hembras_l, 0)::double precision, NULLIF(v_first_ing_h, 0), 0);
    v_base_m := COALESCE(NULLIF(v_machos_l, 0)::double precision, NULLIF(v_first_ing_m, 0), 0);

    v_aves_acum_h := v_base_h;
    v_aves_acum_m := v_base_m;

    DROP TABLE IF EXISTS _seg_sem_rx;
    CREATE TEMP TABLE _seg_sem_rx ON COMMIT DROP AS
    WITH base AS (
        SELECT
            (floor((( (sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date ) / 7.0))::int) + 1 AS real_sem,
            (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date,
            COALESCE(sl.mortalidad_hembras,0) AS mort_h,
            COALESCE(sl.mortalidad_machos,0)  AS mort_m,
            COALESCE(sl.sel_h,0)              AS sel_h,
            COALESCE(sl.sel_m,0)              AS sel_m,
            COALESCE(sl.error_sexaje_hembras,0) AS err_h,
            COALESCE(sl.error_sexaje_machos,0)  AS err_m,
            COALESCE(sl.consumo_kg_hembras,0) AS cons_kg_h_num,
            COALESCE(sl.consumo_kg_machos,0)  AS cons_kg_m_num,
            COALESCE(sl.traslado_salida_hembras,0) AS tras_sal_h,
            COALESCE(sl.traslado_salida_machos,0)  AS tras_sal_m,
            COALESCE(sl.traslado_ingreso_hembras,0) AS tras_ing_h,
            COALESCE(sl.traslado_ingreso_machos,0)  AS tras_ing_m,
            -- Venta de aves: el saldo tiene que descontarla o el reporte sobrestima el lote.
            -- El total (venta_aves_cantidad) no sirve porque el saldo va POR SEXO; se usan los
            -- splits dedicados, espejo de movimiento_aves (que sigue siendo el dueño del número).
            COALESCE(sl.venta_aves_hembras,0)       AS venta_h,
            COALESCE(sl.venta_aves_machos,0)        AS venta_m,
            COALESCE(sl.peso_prom_hembras,0)  AS ph,
            COALESCE(sl.peso_prom_machos,0)   AS pm,
            sl.uniformidad_hembras            AS uh,
            sl.uniformidad_machos             AS um,
            sl.cv_hembras                     AS cvh,
            sl.cv_machos                      AS cvm,
            sl.kcal_al_h                      AS kcal_h,
            sl.prot_al_h                      AS prot_h,
            sl.id
          FROM seguimiento_diario_levante sl
         WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
    )
    SELECT
        LEAST(25, real_sem) AS sem,
        reg_date, mort_h, mort_m, sel_h, sel_m, err_h, err_m,
        cons_kg_h_num::double precision AS cons_kg_h,
        cons_kg_m_num::double precision AS cons_kg_m,
        tras_sal_h, tras_sal_m, tras_ing_h, tras_ing_m, venta_h, venta_m,
        ph, pm, uh, um, cvh, cvm, kcal_h, prot_h, id
      FROM base
     WHERE NOT (
            real_sem > 25
        AND mort_h = 0 AND mort_m = 0 AND sel_h = 0 AND sel_m = 0
        AND err_h = 0 AND err_m = 0
        AND cons_kg_h_num = 0 AND cons_kg_m_num = 0
        AND ph = 0 AND pm = 0
        AND venta_h = 0 AND venta_m = 0
        AND (tras_sal_h + tras_sal_m + tras_ing_h + tras_ing_m) > 0
     );

    SELECT MAX(x.sem) INTO v_max_sem FROM _seg_sem_rx x;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    FOR s IN 1..v_max_sem LOOP
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem_rx x WHERE x.sem = s);

        SELECT COALESCE(SUM(x.mort_h),0)::int, COALESCE(SUM(x.mort_m),0)::int,
               COALESCE(SUM(x.sel_h),0)::int,  COALESCE(SUM(x.sel_m),0)::int,
               COALESCE(SUM(x.err_h),0)::int,  COALESCE(SUM(x.err_m),0)::int,
               COALESCE(SUM(x.tras_ing_h),0)::int, COALESCE(SUM(x.tras_ing_m),0)::int,
               COALESCE(SUM(x.tras_sal_h),0)::int, COALESCE(SUM(x.tras_sal_m),0)::int,
               COALESCE(SUM(x.venta_h),0)::int, COALESCE(SUM(x.venta_m),0)::int,
               COALESCE(SUM(x.cons_kg_h),0), COALESCE(SUM(x.cons_kg_m),0),
               COUNT(DISTINCT x.reg_date)::int,
               AVG(x.kcal_h) FILTER (WHERE x.kcal_h IS NOT NULL AND x.kcal_h > 0),
               AVG(x.prot_h) FILTER (WHERE x.prot_h IS NOT NULL AND x.prot_h > 0)
          INTO r_mort_h, r_mort_m, r_sel_h, r_sel_m, r_err_h, r_err_m,
               r_tras_ing_h, r_tras_ing_m, r_tras_sal_h, r_tras_sal_m,
               r_venta_h, r_venta_m,
               r_cons_kg_h, r_cons_kg_m, r_dias, r_kcal_h, r_prot_h
          FROM _seg_sem_rx x WHERE x.sem = s;

        -- Pesaje de la semana = el ÚLTIMO DÍA con pesaje (ph>0 o pm>0), agregado POR DÍA — misma
        -- regla que la fn base (13-sep-2026, varios registros por día):
        --   • peso por sexo    = PROMEDIO de los registros de ese día que pesaron ESE sexo (>0);
        --   • uniformidad / CV = la del último registro (id) de ese día que la trae (>0).
        -- Con UN registro con pesaje por día cada agregado devuelve el valor de esa fila ⇒ idéntico
        -- a la selección de UNA fila de antes.
        IF EXISTS (SELECT 1 FROM _seg_sem_rx x WHERE x.sem = s AND (x.ph > 0 OR x.pm > 0)) THEN
            SELECT COALESCE(AVG(x.ph) FILTER (WHERE x.ph > 0), 0),
                   COALESCE(AVG(x.pm) FILTER (WHERE x.pm > 0), 0),
                   (array_agg(x.uh  ORDER BY x.id DESC) FILTER (WHERE x.uh  > 0))[1],
                   (array_agg(x.um  ORDER BY x.id DESC) FILTER (WHERE x.um  > 0))[1],
                   (array_agg(x.cvh ORDER BY x.id DESC) FILTER (WHERE x.cvh > 0))[1],
                   (array_agg(x.cvm ORDER BY x.id DESC) FILTER (WHERE x.cvm > 0))[1]
              INTO r_ph, r_pm, r_uh, r_um, r_cvh, r_cvm
              FROM _seg_sem_rx x
             WHERE x.sem = s AND (x.ph > 0 OR x.pm > 0)
               AND x.reg_date = (SELECT MAX(y.reg_date) FROM _seg_sem_rx y
                                  WHERE y.sem = s AND (y.ph > 0 OR y.pm > 0));
        ELSE
            -- Semana sin pesaje: como siempre, el último registro (la uniformidad puede venir sola).
            SELECT x.uh, x.um, x.cvh, x.cvm, x.ph, x.pm INTO r_uh, r_um, r_cvh, r_cvm, r_ph, r_pm
              FROM _seg_sem_rx x WHERE x.sem = s ORDER BY x.reg_date DESC, x.id DESC LIMIT 1;
        END IF;
        r_ph := COALESCE(r_ph, 0);
        r_pm := COALESCE(r_pm, 0);
        -- Arrastre por sexo (regla de la fn base): valor del pesaje si hay,
        -- si no el último conocido; NULL si nunca hubo pesaje del sexo.
        r_peso_h := CASE WHEN r_ph > 0 THEN r_ph ELSE v_peso_ant_h END;
        r_peso_m := CASE WHEN r_pm > 0 THEN r_pm ELSE v_peso_ant_m END;

        r_fin_h := v_aves_acum_h - r_mort_h - r_sel_h - r_err_h - r_tras_sal_h - r_venta_h + r_tras_ing_h;
        r_fin_m := v_aves_acum_m - r_mort_m - r_sel_m - r_err_m - r_tras_sal_m - r_venta_m + r_tras_ing_m;

        semana                       := s;
        fecha_fin_semana             := v_enc_date + ((s - 1) * 7) + 6;
        dias_con_registro            := r_dias;
        base_hembras                 := v_base_h;
        base_machos                  := v_base_m;
        aves_hembras_inicio          := v_aves_acum_h;
        aves_hembras_fin             := r_fin_h;
        aves_machos_inicio           := v_aves_acum_m;
        aves_machos_fin              := r_fin_m;
        mortalidad_hembras_sem       := r_mort_h;
        mortalidad_machos_sem        := r_mort_m;
        seleccion_hembras_sem        := r_sel_h;
        seleccion_machos_sem         := r_sel_m;
        error_hembras_sem            := r_err_h;
        error_machos_sem             := r_err_m;
        traslado_ingreso_hembras_sem := r_tras_ing_h;
        traslado_ingreso_machos_sem  := r_tras_ing_m;
        traslado_salida_hembras_sem  := r_tras_sal_h;
        traslado_salida_machos_sem   := r_tras_sal_m;
        consumo_kg_hembras_sem       := r_cons_kg_h;
        consumo_kg_machos_sem        := r_cons_kg_m;
        kcal_alimento_hembras        := r_kcal_h;
        prot_alimento_hembras        := r_prot_h;
        uniformidad_hembras          := NULLIF(COALESCE(r_uh, 0), 0);
        uniformidad_machos           := NULLIF(COALESCE(r_um, 0), 0);
        cv_hembras                   := NULLIF(COALESCE(r_cvh, 0), 0);
        cv_machos                    := NULLIF(COALESCE(r_cvm, 0), 0);
        peso_hembras_sem             := r_peso_h;
        peso_machos_sem              := r_peso_m;

        RETURN NEXT;

        v_aves_acum_h := r_fin_h;
        v_aves_acum_m := r_fin_m;
        v_peso_ant_h  := r_peso_h;
        v_peso_ant_m  := r_peso_m;
    END LOOP;

    RETURN;
END;
$function$
""";

        private const string FnReporteSemanalExtrasPrev = """
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- fn_reporte_semanal_levante_extras — complemento por sexo del Reporte Técnico Semanal LEVANTE
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- Fix 2026-09-05 (plan seguimiento_produccion_multiples_registros_dia_plan.md, §5/S6):
--   dias_con_registro contaba FILAS (COUNT(*)), no DIAS calendario. Con el flag
--   companies.permite_multiples_seguimientos_diarios ON para LEVANTE, 2 registros el mismo
--   dia inflarian 'dias' y, con el, el denominador de consumo diario g/ave/dia. Las SUMAS
--   (mortalidad, consumo, traslados) NO cambian: SUM es asociativa, sumar 2 filas del mismo
--   dia o sumar el dia ya agrupado da el MISMO total semanal. Fix quirurgico: COUNT(DISTINCT
--   reg_date) en vez de restructurar la fuente (funcion multi-lote/otros edge-cases finos,
--   no vale la pena el riesgo de tocar mas que el conteo).
-- Espejo exacto de pg_get_functiondef (ground truth) + este fix, no reformateado.
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION public.fn_reporte_semanal_levante_extras(p_lote_id integer)
 RETURNS TABLE(semana integer, fecha_fin_semana date, dias_con_registro integer, base_hembras double precision, base_machos double precision, aves_hembras_inicio double precision, aves_hembras_fin double precision, aves_machos_inicio double precision, aves_machos_fin double precision, mortalidad_hembras_sem integer, mortalidad_machos_sem integer, seleccion_hembras_sem integer, seleccion_machos_sem integer, error_hembras_sem integer, error_machos_sem integer, traslado_ingreso_hembras_sem integer, traslado_ingreso_machos_sem integer, traslado_salida_hembras_sem integer, traslado_salida_machos_sem integer, consumo_kg_hembras_sem double precision, consumo_kg_machos_sem double precision, kcal_alimento_hembras double precision, prot_alimento_hembras double precision, uniformidad_hembras double precision, uniformidad_machos double precision, cv_hembras double precision, cv_machos double precision, peso_hembras_sem double precision, peso_machos_sem double precision)
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_hembras_l   integer;
    v_machos_l    integer;
    v_enc_date    date;
    v_min_reg     date;
    v_first_ing_h double precision;
    v_first_ing_m double precision;
    v_base_h      double precision;
    v_base_m      double precision;

    v_aves_acum_h double precision;
    v_aves_acum_m double precision;

    v_max_sem     integer;
    s             integer;

    r_mort_h      integer;
    r_mort_m      integer;
    r_sel_h       integer;
    r_sel_m       integer;
    r_err_h       integer;
    r_err_m       integer;
    r_tras_ing_h  integer;
    r_tras_ing_m  integer;
    r_tras_sal_h  integer;
    r_tras_sal_m  integer;
    r_venta_h     integer;
    r_venta_m     integer;
    r_cons_kg_h   double precision;
    r_cons_kg_m   double precision;
    r_dias        integer;
    r_kcal_h      double precision;
    r_prot_h      double precision;
    r_uh          double precision;
    r_um          double precision;
    r_cvh         double precision;
    r_cvm         double precision;
    r_ph          double precision;
    r_pm          double precision;
    v_peso_ant_h  double precision := NULL;
    v_peso_ant_m  double precision := NULL;
    r_peso_h      double precision;
    r_peso_m      double precision;
    r_fin_h       double precision;
    r_fin_m       double precision;
BEGIN
    SELECT l.hembras_l, l.machos_l,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date
      INTO v_hembras_l, v_machos_l, v_enc_date
      FROM lotes l
     WHERE l.lote_id = p_lote_id AND l.deleted_at IS NULL;

    IF NOT FOUND THEN RETURN; END IF;

    -- Aves entradas por traslado en filas que el armado de _seg_sem_rx DESCARTA (puro traslado
    -- > sem 25). Nadie las suma: la ventana las tira. Se rescatan como base cuando el lote no
    -- trae encaset.
    --
    -- ⚠️ El predicado debe ser el MISMO que el WHERE NOT (...) de _seg_sem_rx más abajo. Si acá
    --    entrara una fila que sí se procesa, sus aves contarían DOS veces (base + ingreso).
    -- SUM por sexo, no una sola fila: los sexos pueden llegar en traslados de días distintos,
    -- y con LIMIT 1 el sexo ausente de la fila más antigua quedaba con base 0 ⇒ saldo negativo.
    SELECT COALESCE(SUM(COALESCE(sl.traslado_ingreso_hembras,0)),0)::double precision,
           COALESCE(SUM(COALESCE(sl.traslado_ingreso_machos,0)),0)::double precision
      INTO v_first_ing_h, v_first_ing_m
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
       AND (floor(((( sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date) / 7.0))::int) + 1 > 25
       AND COALESCE(sl.mortalidad_hembras,0) = 0 AND COALESCE(sl.mortalidad_machos,0) = 0
       AND COALESCE(sl.sel_h,0) = 0 AND COALESCE(sl.sel_m,0) = 0
       AND COALESCE(sl.error_sexaje_hembras,0) = 0 AND COALESCE(sl.error_sexaje_machos,0) = 0
       AND COALESCE(sl.consumo_kg_hembras,0) = 0 AND COALESCE(sl.consumo_kg_machos,0) = 0
       AND COALESCE(sl.peso_prom_hembras,0) = 0 AND COALESCE(sl.peso_prom_machos,0) = 0
       AND COALESCE(sl.venta_aves_hembras,0) = 0 AND COALESCE(sl.venta_aves_machos,0) = 0
       AND (COALESCE(sl.traslado_salida_hembras,0) + COALESCE(sl.traslado_salida_machos,0)
          + COALESCE(sl.traslado_ingreso_hembras,0) + COALESCE(sl.traslado_ingreso_machos,0)) > 0;
    v_first_ing_h := COALESCE(v_first_ing_h, 0);
    v_first_ing_m := COALESCE(v_first_ing_m, 0);

    SELECT MIN((sl.fecha AT TIME ZONE 'America/Bogota')::date)
      INTO v_min_reg
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text;

    IF v_min_reg IS NULL THEN RETURN; END IF;
    IF v_enc_date IS NULL OR v_enc_date > v_min_reg THEN RETURN; END IF;

    v_base_h := COALESCE(NULLIF(v_hembras_l, 0)::double precision, NULLIF(v_first_ing_h, 0), 0);
    v_base_m := COALESCE(NULLIF(v_machos_l, 0)::double precision, NULLIF(v_first_ing_m, 0), 0);

    v_aves_acum_h := v_base_h;
    v_aves_acum_m := v_base_m;

    DROP TABLE IF EXISTS _seg_sem_rx;
    CREATE TEMP TABLE _seg_sem_rx ON COMMIT DROP AS
    WITH base AS (
        SELECT
            (floor((( (sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date ) / 7.0))::int) + 1 AS real_sem,
            (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date,
            COALESCE(sl.mortalidad_hembras,0) AS mort_h,
            COALESCE(sl.mortalidad_machos,0)  AS mort_m,
            COALESCE(sl.sel_h,0)              AS sel_h,
            COALESCE(sl.sel_m,0)              AS sel_m,
            COALESCE(sl.error_sexaje_hembras,0) AS err_h,
            COALESCE(sl.error_sexaje_machos,0)  AS err_m,
            COALESCE(sl.consumo_kg_hembras,0) AS cons_kg_h_num,
            COALESCE(sl.consumo_kg_machos,0)  AS cons_kg_m_num,
            COALESCE(sl.traslado_salida_hembras,0) AS tras_sal_h,
            COALESCE(sl.traslado_salida_machos,0)  AS tras_sal_m,
            COALESCE(sl.traslado_ingreso_hembras,0) AS tras_ing_h,
            COALESCE(sl.traslado_ingreso_machos,0)  AS tras_ing_m,
            -- Venta de aves: el saldo tiene que descontarla o el reporte sobrestima el lote.
            -- El total (venta_aves_cantidad) no sirve porque el saldo va POR SEXO; se usan los
            -- splits dedicados, espejo de movimiento_aves (que sigue siendo el dueño del número).
            COALESCE(sl.venta_aves_hembras,0)       AS venta_h,
            COALESCE(sl.venta_aves_machos,0)        AS venta_m,
            COALESCE(sl.peso_prom_hembras,0)  AS ph,
            COALESCE(sl.peso_prom_machos,0)   AS pm,
            sl.uniformidad_hembras            AS uh,
            sl.uniformidad_machos             AS um,
            sl.cv_hembras                     AS cvh,
            sl.cv_machos                      AS cvm,
            sl.kcal_al_h                      AS kcal_h,
            sl.prot_al_h                      AS prot_h,
            sl.id
          FROM seguimiento_diario_levante sl
         WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
    )
    SELECT
        LEAST(25, real_sem) AS sem,
        reg_date, mort_h, mort_m, sel_h, sel_m, err_h, err_m,
        cons_kg_h_num::double precision AS cons_kg_h,
        cons_kg_m_num::double precision AS cons_kg_m,
        tras_sal_h, tras_sal_m, tras_ing_h, tras_ing_m, venta_h, venta_m,
        ph, pm, uh, um, cvh, cvm, kcal_h, prot_h, id
      FROM base
     WHERE NOT (
            real_sem > 25
        AND mort_h = 0 AND mort_m = 0 AND sel_h = 0 AND sel_m = 0
        AND err_h = 0 AND err_m = 0
        AND cons_kg_h_num = 0 AND cons_kg_m_num = 0
        AND ph = 0 AND pm = 0
        AND venta_h = 0 AND venta_m = 0
        AND (tras_sal_h + tras_sal_m + tras_ing_h + tras_ing_m) > 0
     );

    SELECT MAX(x.sem) INTO v_max_sem FROM _seg_sem_rx x;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    FOR s IN 1..v_max_sem LOOP
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem_rx x WHERE x.sem = s);

        SELECT COALESCE(SUM(x.mort_h),0)::int, COALESCE(SUM(x.mort_m),0)::int,
               COALESCE(SUM(x.sel_h),0)::int,  COALESCE(SUM(x.sel_m),0)::int,
               COALESCE(SUM(x.err_h),0)::int,  COALESCE(SUM(x.err_m),0)::int,
               COALESCE(SUM(x.tras_ing_h),0)::int, COALESCE(SUM(x.tras_ing_m),0)::int,
               COALESCE(SUM(x.tras_sal_h),0)::int, COALESCE(SUM(x.tras_sal_m),0)::int,
               COALESCE(SUM(x.venta_h),0)::int, COALESCE(SUM(x.venta_m),0)::int,
               COALESCE(SUM(x.cons_kg_h),0), COALESCE(SUM(x.cons_kg_m),0),
               COUNT(DISTINCT x.reg_date)::int,
               AVG(x.kcal_h) FILTER (WHERE x.kcal_h IS NOT NULL AND x.kcal_h > 0),
               AVG(x.prot_h) FILTER (WHERE x.prot_h IS NOT NULL AND x.prot_h > 0)
          INTO r_mort_h, r_mort_m, r_sel_h, r_sel_m, r_err_h, r_err_m,
               r_tras_ing_h, r_tras_ing_m, r_tras_sal_h, r_tras_sal_m,
               r_venta_h, r_venta_m,
               r_cons_kg_h, r_cons_kg_m, r_dias, r_kcal_h, r_prot_h
          FROM _seg_sem_rx x WHERE x.sem = s;

        -- Pesaje de la semana: misma selección de fila que la fn base.
        SELECT x.uh, x.um, x.cvh, x.cvm, x.ph, x.pm INTO r_uh, r_um, r_cvh, r_cvm, r_ph, r_pm
          FROM _seg_sem_rx x
         WHERE x.sem = s AND (x.ph > 0 OR x.pm > 0)
         ORDER BY x.reg_date DESC, x.id DESC LIMIT 1;
        IF NOT FOUND THEN
            SELECT x.uh, x.um, x.cvh, x.cvm, x.ph, x.pm INTO r_uh, r_um, r_cvh, r_cvm, r_ph, r_pm
              FROM _seg_sem_rx x WHERE x.sem = s ORDER BY x.reg_date DESC, x.id DESC LIMIT 1;
        END IF;
        r_ph := COALESCE(r_ph, 0);
        r_pm := COALESCE(r_pm, 0);
        -- Arrastre por sexo (regla de la fn base): valor del pesaje si hay,
        -- si no el último conocido; NULL si nunca hubo pesaje del sexo.
        r_peso_h := CASE WHEN r_ph > 0 THEN r_ph ELSE v_peso_ant_h END;
        r_peso_m := CASE WHEN r_pm > 0 THEN r_pm ELSE v_peso_ant_m END;

        r_fin_h := v_aves_acum_h - r_mort_h - r_sel_h - r_err_h - r_tras_sal_h - r_venta_h + r_tras_ing_h;
        r_fin_m := v_aves_acum_m - r_mort_m - r_sel_m - r_err_m - r_tras_sal_m - r_venta_m + r_tras_ing_m;

        semana                       := s;
        fecha_fin_semana             := v_enc_date + ((s - 1) * 7) + 6;
        dias_con_registro            := r_dias;
        base_hembras                 := v_base_h;
        base_machos                  := v_base_m;
        aves_hembras_inicio          := v_aves_acum_h;
        aves_hembras_fin             := r_fin_h;
        aves_machos_inicio           := v_aves_acum_m;
        aves_machos_fin              := r_fin_m;
        mortalidad_hembras_sem       := r_mort_h;
        mortalidad_machos_sem        := r_mort_m;
        seleccion_hembras_sem        := r_sel_h;
        seleccion_machos_sem         := r_sel_m;
        error_hembras_sem            := r_err_h;
        error_machos_sem             := r_err_m;
        traslado_ingreso_hembras_sem := r_tras_ing_h;
        traslado_ingreso_machos_sem  := r_tras_ing_m;
        traslado_salida_hembras_sem  := r_tras_sal_h;
        traslado_salida_machos_sem   := r_tras_sal_m;
        consumo_kg_hembras_sem       := r_cons_kg_h;
        consumo_kg_machos_sem        := r_cons_kg_m;
        kcal_alimento_hembras        := r_kcal_h;
        prot_alimento_hembras        := r_prot_h;
        uniformidad_hembras          := NULLIF(COALESCE(r_uh, 0), 0);
        uniformidad_machos           := NULLIF(COALESCE(r_um, 0), 0);
        cv_hembras                   := NULLIF(COALESCE(r_cvh, 0), 0);
        cv_machos                    := NULLIF(COALESCE(r_cvm, 0), 0);
        peso_hembras_sem             := r_peso_h;
        peso_machos_sem              := r_peso_m;

        RETURN NEXT;

        v_aves_acum_h := r_fin_h;
        v_aves_acum_m := r_fin_m;
        v_peso_ant_h  := r_peso_h;
        v_peso_ant_m  := r_peso_m;
    END LOOP;

    RETURN;
END;
$function$
""";

        private const string FnResumenRaPesadasNueva = """
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- fn_resumen_semanal_ra_pesadas_levante — Informe RA Pesadas (multi-lote/multi-granja)
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- Fix 2026-09-05 (plan seguimiento_produccion_multiples_registros_dia_plan.md, §5/S6):
--   'dias' contaba FILAS (COUNT(*)), no DIAS calendario, en el agregado semanal por lote.
--   Mismo razonamiento que fn_reporte_semanal_levante_extras: las SUMAS son asociativas y
--   no cambian, solo el conteo de dias necesitaba COUNT(DISTINCT reg_date).
-- Fix 2026-09-13 (plan levante_varios_registros_dia_pesaje_uniformidad_plan.md, L4):
--   el PESAJE de la semana (CTE 6) tomaba UNA fila. Con 2+ registros el mismo dia no promediaba,
--   perdia el sexo que el ultimo registro no peso y la uniformidad/CV salia de esa fila aunque no la
--   trajera. Ahora se arma por DIA, igual que fn_indicadores_levante_postura y
--   fn_reporte_semanal_levante_extras. Con un registro con pesaje por dia da lo mismo que antes.
--   Especificacion ejecutable: Application/Calculos/PesajeSemanalLevanteCalculos.cs.
-- Espejo exacto de pg_get_functiondef (ground truth) + este fix, no reformateado.
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION public.fn_resumen_semanal_ra_pesadas_levante(p_company_id integer, p_anio integer, p_sem_anio integer, p_granja_ids integer[] DEFAULT NULL::integer[], p_regional text DEFAULT NULL::text, p_excluir_trasladados boolean DEFAULT false)
 RETURNS TABLE(lote_id integer, lote_nombre text, granja_id integer, granja_nombre text, nucleo_nombre text, regional text, raza text, anio_guia integer, edad_semana integer, fecha_fin_semana date, dias_con_registro integer, tuvo_traslado boolean, part double precision, saldo_hembras double precision, saldo_machos double precision, mort_hembras_pct double precision, retiro_acum_hembras_pct double precision, retiro_acum_hembras_guia double precision, dif_consumo_hembras_pct double precision, dif_peso_hembras_pct double precision, uniformidad_hembras double precision, cv_hembras double precision, mort_machos_pct double precision, retiro_acum_machos_pct double precision, retiro_acum_machos_guia double precision, dif_consumo_machos_pct double precision, dif_peso_machos_pct double precision, uniformidad_machos double precision, cv_machos double precision)
 LANGUAGE sql
 STABLE
AS $function$
WITH
-- ── 1) Lotes candidatos de la empresa (+ ubicación y datos de guía) ──────────
lote_base AS (
    SELECT l.lote_id,
           l.lote_nombre::text                                        AS lote_nombre,
           l.granja_id,
           f.name::text                                               AS granja_nombre,
           n.nucleo_nombre::text                                      AS nucleo_nombre,
           COALESCE(NULLIF(mo.value, ''), NULLIF(l.regional, ''))::text AS regional,
           l.raza::text                                               AS raza,
           l.ano_tabla_genetica                                       AS anio_guia,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date       AS enc_date
      FROM lotes l
      JOIN farms f
        ON f.id = l.granja_id
      LEFT JOIN nucleos n
        ON n.granja_id = l.granja_id
       AND n.nucleo_id = l.nucleo_id
       AND n.deleted_at IS NULL
      LEFT JOIN master_list_options mo
        ON mo.id = f.regional_id
     WHERE l.company_id = p_company_id
       AND l.deleted_at IS NULL
       AND (p_granja_ids IS NULL OR l.granja_id = ANY (p_granja_ids))
),
-- ── 2) Registros diarios de levante, con la semana de edad ya resuelta ───────
--    Mismo WHERE, mismo COALESCE y misma exclusión de «puro traslado > 25»
--    que fn_reporte_semanal_levante_extras.
reg AS (
    SELECT lb.lote_id,
           (floor(((sl.fecha AT TIME ZONE 'America/Bogota')::date - lb.enc_date) / 7.0)::int) + 1 AS real_sem,
           (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date,
           COALESCE(sl.mortalidad_hembras, 0)      AS mort_h,
           COALESCE(sl.mortalidad_machos, 0)       AS mort_m,
           COALESCE(sl.sel_h, 0)                   AS sel_h,
           COALESCE(sl.sel_m, 0)                   AS sel_m,
           COALESCE(sl.error_sexaje_hembras, 0)    AS err_h,
           COALESCE(sl.error_sexaje_machos, 0)     AS err_m,
           COALESCE(sl.consumo_kg_hembras, 0)::double precision AS cons_kg_h,
           COALESCE(sl.consumo_kg_machos, 0)::double precision  AS cons_kg_m,
           COALESCE(sl.traslado_salida_hembras, 0) AS tras_sal_h,
           COALESCE(sl.traslado_salida_machos, 0)  AS tras_sal_m,
           COALESCE(sl.traslado_ingreso_hembras, 0) AS tras_ing_h,
           COALESCE(sl.traslado_ingreso_machos, 0)  AS tras_ing_m,
           -- Venta de aves: el saldo tiene que descontarla o el reporte sobrestima el lote. El total
           -- (venta_aves_cantidad) no sirve acá porque el saldo va POR SEXO; se usan los splits
           -- dedicados venta_aves_hembras/machos, espejo de movimiento_aves (que sigue siendo el
           -- dueño del número). Sin esto S-369B reportaba 1.281 machos con el maestro en 991.
           COALESCE(sl.venta_aves_hembras, 0)       AS venta_h,
           COALESCE(sl.venta_aves_machos, 0)        AS venta_m,
           COALESCE(sl.peso_prom_hembras, 0)::double precision AS ph,
           COALESCE(sl.peso_prom_machos, 0)::double precision  AS pm,
           sl.uniformidad_hembras::double precision AS uh,
           sl.uniformidad_machos::double precision  AS um,
           sl.cv_hembras::double precision          AS cvh,
           sl.cv_machos::double precision           AS cvm,
           sl.id
      FROM lote_base lb
      JOIN seguimiento_diario_levante sl
        ON sl.lote_id = lb.lote_id::text
       AND sl.tipo_seguimiento = 'levante'
),
-- ── 3) Guards por lote: primer registro y validez del encaset ────────────────
lote_ok AS (
    SELECT lb.*,
           g.min_reg,
           -- base por sexo con el mismo fallback del Detalle. Sigue siendo COALESCE, no suma:
           -- un lote CON encaset conserva exactamente su número de siempre. El fallback solo
           -- entra cuando el encaset es 0/NULL, que es el lote poblado únicamente por traslado.
           COALESCE(
               NULLIF(l.hembras_l, 0)::double precision,
               NULLIF(fi.ing_desc_h, 0),
               0)                                    AS base_h,
           COALESCE(
               NULLIF(l.machos_l, 0)::double precision,
               NULLIF(fi.ing_desc_m, 0),
               0)                                    AS base_m,
           COALESCE(tr.tuvo_traslado, false)         AS tuvo_traslado
      FROM lote_base lb
      JOIN lotes l
        ON l.lote_id = lb.lote_id
      JOIN LATERAL (
            SELECT MIN(r.reg_date) AS min_reg
              FROM reg r
             WHERE r.lote_id = lb.lote_id
      ) g ON true
      -- Aves que entraron por traslado en filas que reg_ok DESCARTA (puro traslado > sem 25).
      -- Esas aves no las suma nadie: la ventana las tira, así que si el lote no trae encaset
      -- quedan fuera del saldo. Se rescatan acá como base.
      --
      -- ⚠️ El predicado tiene que ser el MISMO que el de reg_ok (más abajo). Si cambia uno,
      --    cambia el otro: si acá entrara una fila que reg_ok SÍ cuenta, sus aves se sumarían
      --    dos veces (una como base y otra como ingreso) y el saldo saldría inflado.
      -- SUM por sexo, no una sola fila: los sexos pueden llegar en traslados de DÍAS DISTINTOS.
      -- Con `LIMIT 1` se leían los dos sexos de la fila más antigua, así que el sexo que no
      -- venía en esa fila quedaba con base 0 y el reporte lo mostraba NEGATIVO tras restarle
      -- la mortalidad (caso real: machos el 08-jun y hembras el 11-jun ⇒ hembras en -212).
      LEFT JOIN LATERAL (
            SELECT COALESCE(SUM(r.tras_ing_h), 0)::double precision AS ing_desc_h,
                   COALESCE(SUM(r.tras_ing_m), 0)::double precision AS ing_desc_m
              FROM reg r
             WHERE r.lote_id = lb.lote_id
               AND r.real_sem > 25
               AND r.mort_h = 0 AND r.mort_m = 0
               AND r.sel_h = 0  AND r.sel_m = 0
               AND r.err_h = 0  AND r.err_m = 0
               AND r.cons_kg_h = 0 AND r.cons_kg_m = 0
               AND r.ph = 0 AND r.pm = 0
               AND r.venta_h = 0 AND r.venta_m = 0
               AND (r.tras_sal_h + r.tras_sal_m + r.tras_ing_h + r.tras_ing_m) > 0
      ) fi ON true
      LEFT JOIN LATERAL (
            SELECT true AS tuvo_traslado
              FROM reg r
             WHERE r.lote_id = lb.lote_id
               AND (r.tras_ing_h + r.tras_ing_m + r.tras_sal_h + r.tras_sal_m) > 0
             LIMIT 1
      ) tr ON true
     WHERE g.min_reg IS NOT NULL
       AND lb.enc_date IS NOT NULL
       AND lb.enc_date <= g.min_reg
),
-- ── 4) Registros válidos (topados a 25, sin filas de puro traslado > 25) ─────
reg_ok AS (
    SELECT r.*,
           LEAST(25, r.real_sem) AS sem
      FROM reg r
      JOIN lote_ok lo ON lo.lote_id = r.lote_id
     WHERE NOT (
               r.real_sem > 25
           AND r.mort_h = 0 AND r.mort_m = 0
           AND r.sel_h = 0  AND r.sel_m = 0
           AND r.err_h = 0  AND r.err_m = 0
           AND r.cons_kg_h = 0 AND r.cons_kg_m = 0
           AND r.ph = 0 AND r.pm = 0
           AND r.venta_h = 0 AND r.venta_m = 0
           AND (r.tras_sal_h + r.tras_sal_m + r.tras_ing_h + r.tras_ing_m) > 0
       )
),
-- ── 5) Agregado semanal por lote ────────────────────────────────────────────
sem AS (
    SELECT lote_id,
           sem,
           COUNT(DISTINCT reg_date)::int        AS dias,
           SUM(mort_h)::double precision       AS mort_h,
           SUM(mort_m)::double precision       AS mort_m,
           SUM(sel_h)::double precision        AS sel_h,
           SUM(sel_m)::double precision        AS sel_m,
           SUM(err_h)::double precision        AS err_h,
           SUM(err_m)::double precision        AS err_m,
           SUM(tras_sal_h)::double precision   AS tras_sal_h,
           SUM(tras_sal_m)::double precision   AS tras_sal_m,
           SUM(tras_ing_h)::double precision   AS tras_ing_h,
           SUM(tras_ing_m)::double precision   AS tras_ing_m,
           SUM(venta_h)::double precision      AS venta_h,
           SUM(venta_m)::double precision      AS venta_m,
           SUM(cons_kg_h)                      AS cons_kg_h,
           SUM(cons_kg_m)                      AS cons_kg_m
      FROM reg_ok
     GROUP BY lote_id, sem
),
-- ── 6) Pesaje de la semana (misma regla que el Detalle y los Indicadores) ───
--    = el ÚLTIMO DÍA de la semana con pesaje (ph>0 o pm>0), agregado POR DÍA
--    (13-sep-2026, varios registros por día):
--      • peso por sexo    = PROMEDIO de los registros de ese día que pesaron ESE sexo;
--      • uniformidad / CV = la del último registro (id) de ese día que la trae (>0).
--    Con UN registro con pesaje por día cada agregado da el valor de esa fila ⇒ idéntico a la
--    selección de UNA fila de antes. Semana SIN pesaje: el último registro, como siempre (LATERAL).
dia_pesaje AS (
    SELECT r.lote_id, r.sem, MAX(r.reg_date) AS reg_date
      FROM reg_ok r
     WHERE r.ph > 0 OR r.pm > 0
     GROUP BY r.lote_id, r.sem
),
pesaje_del_dia AS (
    SELECT r.lote_id,
           r.sem,
           COALESCE(AVG(r.ph) FILTER (WHERE r.ph > 0), 0)                    AS ph,
           COALESCE(AVG(r.pm) FILTER (WHERE r.pm > 0), 0)                    AS pm,
           (array_agg(r.uh  ORDER BY r.id DESC) FILTER (WHERE r.uh  > 0))[1] AS uh,
           (array_agg(r.um  ORDER BY r.id DESC) FILTER (WHERE r.um  > 0))[1] AS um,
           (array_agg(r.cvh ORDER BY r.id DESC) FILTER (WHERE r.cvh > 0))[1] AS cvh,
           (array_agg(r.cvm ORDER BY r.id DESC) FILTER (WHERE r.cvm > 0))[1] AS cvm
      FROM reg_ok r
      JOIN dia_pesaje dp
        ON dp.lote_id = r.lote_id
       AND dp.sem = r.sem
       AND dp.reg_date = r.reg_date
     WHERE r.ph > 0 OR r.pm > 0
     GROUP BY r.lote_id, r.sem
),
pesaje AS (
    SELECT s.lote_id,
           s.sem,
           CASE WHEN pd.lote_id IS NOT NULL THEN pd.ph  ELSE p.ph  END AS ph,
           CASE WHEN pd.lote_id IS NOT NULL THEN pd.pm  ELSE p.pm  END AS pm,
           CASE WHEN pd.lote_id IS NOT NULL THEN pd.uh  ELSE p.uh  END AS uh,
           CASE WHEN pd.lote_id IS NOT NULL THEN pd.um  ELSE p.um  END AS um,
           CASE WHEN pd.lote_id IS NOT NULL THEN pd.cvh ELSE p.cvh END AS cvh,
           CASE WHEN pd.lote_id IS NOT NULL THEN pd.cvm ELSE p.cvm END AS cvm
      FROM sem s
      LEFT JOIN pesaje_del_dia pd
        ON pd.lote_id = s.lote_id
       AND pd.sem = s.sem
      LEFT JOIN LATERAL (
            SELECT r.ph, r.pm, r.uh, r.um, r.cvh, r.cvm
              FROM reg_ok r
             WHERE r.lote_id = s.lote_id
               AND r.sem = s.sem
             ORDER BY (CASE WHEN r.ph > 0 OR r.pm > 0 THEN 0 ELSE 1 END),
                      r.reg_date DESC, r.id DESC
             LIMIT 1
      ) p ON true
),
-- ── 7) Acumulados por ventana + arrastre (LOCF) del peso por sexo ───────────
--    El "grupo" de LOCF es el conteo de pesajes no nulos hasta la semana:
--    dentro de cada grupo, el primer valor es el último peso conocido.
acum AS (
    SELECT s.lote_id,
           s.sem,
           s.dias,
           s.mort_h, s.mort_m, s.sel_h, s.sel_m, s.err_h, s.err_m,
           s.tras_sal_h, s.tras_sal_m, s.tras_ing_h, s.tras_ing_m,
           s.venta_h, s.venta_m,
           s.cons_kg_h, s.cons_kg_m,
           NULLIF(p.ph, 0) AS peso_h_raw,
           NULLIF(p.pm, 0) AS peso_m_raw,
           NULLIF(COALESCE(p.uh, 0), 0)  AS unif_h,
           NULLIF(COALESCE(p.um, 0), 0)  AS unif_m,
           NULLIF(COALESCE(p.cvh, 0), 0) AS cv_h,
           NULLIF(COALESCE(p.cvm, 0), 0) AS cv_m,
           -- salidas netas acumuladas hasta ESTA semana (inclusive)
           SUM(s.mort_h + s.sel_h + s.err_h + s.tras_sal_h + s.venta_h - s.tras_ing_h)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS neto_out_h,
           SUM(s.mort_m + s.sel_m + s.err_m + s.tras_sal_m + s.venta_m - s.tras_ing_m)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS neto_out_m,
           -- retiro acumulado (mort + sel + err), SIN traslados: es lo que el
           -- Excel llama RetAcH/RetAcM y va sobre base FIJA
           SUM(s.mort_h + s.sel_h + s.err_h)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS retiro_ac_h,
           SUM(s.mort_m + s.sel_m + s.err_m)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS retiro_ac_m,
           COUNT(NULLIF(p.ph, 0))
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS grp_h,
           COUNT(NULLIF(p.pm, 0))
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS grp_m
      FROM sem s
      JOIN pesaje p
        ON p.lote_id = s.lote_id AND p.sem = s.sem
),
locf AS (
    SELECT a.*,
           FIRST_VALUE(a.peso_h_raw) OVER (
               PARTITION BY a.lote_id, a.grp_h ORDER BY a.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS peso_h,
           FIRST_VALUE(a.peso_m_raw) OVER (
               PARTITION BY a.lote_id, a.grp_m ORDER BY a.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS peso_m
      FROM acum a
),
-- ── 8) Solo la semana calendario pedida (WEEKNUM estilo Excel) ──────────────
sem_objetivo AS (
    SELECT lo.lote_id, lo.lote_nombre, lo.granja_id, lo.granja_nombre,
           lo.nucleo_nombre, lo.regional, lo.raza, lo.anio_guia,
           lo.base_h, lo.base_m, lo.tuvo_traslado,
           x.sem, x.dias,
           x.mort_h, x.mort_m, x.sel_h, x.sel_m, x.err_h, x.err_m,
           x.tras_sal_h, x.tras_sal_m, x.tras_ing_h, x.tras_ing_m,
           x.cons_kg_h, x.cons_kg_m,
           x.unif_h, x.unif_m, x.cv_h, x.cv_m,
           x.neto_out_h, x.neto_out_m, x.retiro_ac_h, x.retiro_ac_m,
           x.peso_h, x.peso_m,
           (lo.enc_date + ((x.sem - 1) * 7) + 6) AS fin_sem,
           -- Semana CALENDARIO (WEEKNUM estilo Excel) del cierre de la semana de edad.
           -- Se materializa acá porque la usan DOS cosas: el filtro de abajo y la
           -- partición de `part`. OJO: NO es lo mismo que fin_sem — fin_sem depende del
           -- encaset de CADA lote, así que dos sublotes del mismo lote padre con fechas
           -- de llegada distintas caen en la misma semana calendario con fin_sem DISTINTO.
           floor(
             ( (lo.enc_date + ((x.sem - 1) * 7) + 6)
               - date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp)::date
               + EXTRACT(DOW FROM date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp))::int
             ) / 7.0
           )::int + 1                            AS sem_cal
      FROM locf x
      JOIN lote_ok lo ON lo.lote_id = x.lote_id
     WHERE EXTRACT(YEAR FROM (lo.enc_date + ((x.sem - 1) * 7) + 6))::int = p_anio
       -- p_sem_anio NULL = TODAS las semanas del año (curva del año completo);
       -- con valor, una sola semana calendario.
       AND (p_sem_anio IS NULL OR (
             floor(
               ( (lo.enc_date + ((x.sem - 1) * 7) + 6)
                 - date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp)::date
                 + EXTRACT(DOW FROM date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp))::int
               ) / 7.0
             )::int + 1
           ) = p_sem_anio)
       AND (p_regional IS NULL OR lo.regional = p_regional)
       AND (NOT p_excluir_trasladados OR NOT lo.tuvo_traslado)
),
-- ── 9) Guía del lote para esa edad ──────────────────────────────────────────
con_guia AS (
    SELECT so.*,
           f_safe_numeric(g.retiro_ac_h)  AS g_retiro_ac_h,
           f_safe_numeric(g.retiro_ac_m)  AS g_retiro_ac_m,
           f_safe_numeric(g.gr_ave_dia_h) AS g_gr_ave_dia_h,
           f_safe_numeric(g.gr_ave_dia_m) AS g_gr_ave_dia_m,
           f_safe_numeric(g.peso_h)       AS g_peso_h,
           f_safe_numeric(g.peso_m)       AS g_peso_m
      FROM sem_objetivo so
      LEFT JOIN LATERAL (
            SELECT gg.*
              -- Fuente unificada: la compartida + la reducida proyectada al mismo shape.
              -- Aca NO hace falta leer `origen` como en fn_indicadores_*: estas columnas
              -- pasan por f_safe_numeric(), que ya devuelve NULL ante NULL o texto no
              -- numerico ⇒ no fabrica el 0 falso que alla habia que condicionar.
              FROM vw_guia_genetica_postura gg
             WHERE gg.company_id = p_company_id
               AND gg.deleted_at IS NULL
               AND lower(trim(gg.raza)) = lower(trim(COALESCE(so.raza, '')))
               AND trim(gg.anio_guia) = so.anio_guia::text
               -- ⚠️ Comparación de edad como TEXTO EXACTO, igual que
               --    fn_indicadores_levante_postura (`btrim(g.edad) = s::text`).
               --    NO parsear a número: la guía tiene DOS filas para la semana 25
               --    ('25' de levante y '25P' de producción) y el parseo numérico
               --    haría match con las dos, devolviendo la fila equivocada.
               AND btrim(gg.edad) = so.sem::text
             ORDER BY gg.id
             LIMIT 1
      ) g ON true
),
-- ── 10) Saldos y derivadas ──────────────────────────────────────────────────
calc AS (
    SELECT cg.*,
           (cg.base_h - cg.neto_out_h)                                   AS saldo_h,
           (cg.base_m - cg.neto_out_m)                                   AS saldo_m,
           -- aves al INICIO de la semana = saldo final + salidas netas de la semana
           (cg.base_h - cg.neto_out_h
              + (cg.mort_h + cg.sel_h + cg.err_h + cg.tras_sal_h - cg.tras_ing_h)) AS ini_h,
           (cg.base_m - cg.neto_out_m
              + (cg.mort_m + cg.sel_m + cg.err_m + cg.tras_sal_m - cg.tras_ing_m)) AS ini_m
      FROM con_guia cg
),
final AS (
    SELECT c.*,
           -- g/ave/día real por sexo: kg*1000 / promedio(inicio, fin) / días
           CASE WHEN c.dias > 0 AND ((c.ini_h + (c.base_h - c.neto_out_h)) / 2.0) > 0
                THEN (c.cons_kg_h * 1000.0)
                     / ((c.ini_h + (c.base_h - c.neto_out_h)) / 2.0) / c.dias
           END AS gr_ave_dia_h,
           CASE WHEN c.dias > 0 AND ((c.ini_m + (c.base_m - c.neto_out_m)) / 2.0) > 0
                THEN (c.cons_kg_m * 1000.0)
                     / ((c.ini_m + (c.base_m - c.neto_out_m)) / 2.0) / c.dias
           END AS gr_ave_dia_m
      FROM calc c
)
SELECT
    f.lote_id,
    f.lote_nombre,
    f.granja_id,
    f.granja_nombre,
    f.nucleo_nombre,
    f.regional,
    f.raza,
    f.anio_guia,
    f.sem                                                        AS edad_semana,
    f.fin_sem                                                    AS fecha_fin_semana,
    f.dias                                                       AS dias_con_registro,
    f.tuvo_traslado,
    -- Participación SIEMPRE dentro de su propia semana CALENDARIO: con p_sem_anio NULL
    -- la ventana global mezclaría las 52 semanas del año. Particiona por sem_cal, NO por
    -- fin_sem: fin_sem sale del encaset de cada lote, así que un lote padre con sublotes
    -- de fechas de llegada distintas dejaba a cada sublote SOLO en su partición y todos
    -- daban part = 1 (deberían repartirse ~0,50 y ~0,50). Con p_sem_anio concreto todas
    -- las filas comparten sem_cal, así que esto equivale al OVER () original.
    CASE WHEN SUM(f.saldo_h) OVER (PARTITION BY f.sem_cal) > 0
         THEN f.saldo_h / SUM(f.saldo_h) OVER (PARTITION BY f.sem_cal)
    END                                                          AS part,
    f.saldo_h                                                    AS saldo_hembras,
    f.saldo_m                                                    AS saldo_machos,
    -- ── hembras ──
    CASE WHEN f.ini_h > 0 THEN f.mort_h / f.ini_h * 100.0 END    AS mort_hembras_pct,
    CASE WHEN f.base_h > 0 THEN f.retiro_ac_h / f.base_h * 100.0 END AS retiro_acum_hembras_pct,
    f.g_retiro_ac_h::double precision                            AS retiro_acum_hembras_guia,
    CASE WHEN COALESCE(f.g_gr_ave_dia_h, 0) <> 0 AND f.gr_ave_dia_h IS NOT NULL
         THEN (f.gr_ave_dia_h / f.g_gr_ave_dia_h::double precision - 1) * 100.0 END
                                                                 AS dif_consumo_hembras_pct,
    CASE WHEN COALESCE(f.g_peso_h, 0) <> 0 AND f.peso_h IS NOT NULL
         THEN (f.peso_h / f.g_peso_h::double precision - 1) * 100.0 END
                                                                 AS dif_peso_hembras_pct,
    f.unif_h                                                     AS uniformidad_hembras,
    f.cv_h                                                       AS cv_hembras,
    -- ── machos ──
    CASE WHEN f.ini_m > 0 THEN f.mort_m / f.ini_m * 100.0 END    AS mort_machos_pct,
    CASE WHEN f.base_m > 0 THEN f.retiro_ac_m / f.base_m * 100.0 END AS retiro_acum_machos_pct,
    f.g_retiro_ac_m::double precision                            AS retiro_acum_machos_guia,
    CASE WHEN COALESCE(f.g_gr_ave_dia_m, 0) <> 0 AND f.gr_ave_dia_m IS NOT NULL
         THEN (f.gr_ave_dia_m / f.g_gr_ave_dia_m::double precision - 1) * 100.0 END
                                                                 AS dif_consumo_machos_pct,
    CASE WHEN COALESCE(f.g_peso_m, 0) <> 0 AND f.peso_m IS NOT NULL
         THEN (f.peso_m / f.g_peso_m::double precision - 1) * 100.0 END
                                                                 AS dif_peso_machos_pct,
    f.unif_m                                                     AS uniformidad_machos,
    f.cv_m                                                       AS cv_machos
  FROM final f
 ORDER BY f.sem DESC, f.lote_nombre;
$function$
""";

        private const string FnResumenRaPesadasPrev = """
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- fn_resumen_semanal_ra_pesadas_levante — Informe RA Pesadas (multi-lote/multi-granja)
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- Fix 2026-09-05 (plan seguimiento_produccion_multiples_registros_dia_plan.md, §5/S6):
--   'dias' contaba FILAS (COUNT(*)), no DIAS calendario, en el agregado semanal por lote.
--   Mismo razonamiento que fn_reporte_semanal_levante_extras: las SUMAS son asociativas y
--   no cambian, solo el conteo de dias necesitaba COUNT(DISTINCT reg_date).
-- Espejo exacto de pg_get_functiondef (ground truth) + este fix, no reformateado.
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION public.fn_resumen_semanal_ra_pesadas_levante(p_company_id integer, p_anio integer, p_sem_anio integer, p_granja_ids integer[] DEFAULT NULL::integer[], p_regional text DEFAULT NULL::text, p_excluir_trasladados boolean DEFAULT false)
 RETURNS TABLE(lote_id integer, lote_nombre text, granja_id integer, granja_nombre text, nucleo_nombre text, regional text, raza text, anio_guia integer, edad_semana integer, fecha_fin_semana date, dias_con_registro integer, tuvo_traslado boolean, part double precision, saldo_hembras double precision, saldo_machos double precision, mort_hembras_pct double precision, retiro_acum_hembras_pct double precision, retiro_acum_hembras_guia double precision, dif_consumo_hembras_pct double precision, dif_peso_hembras_pct double precision, uniformidad_hembras double precision, cv_hembras double precision, mort_machos_pct double precision, retiro_acum_machos_pct double precision, retiro_acum_machos_guia double precision, dif_consumo_machos_pct double precision, dif_peso_machos_pct double precision, uniformidad_machos double precision, cv_machos double precision)
 LANGUAGE sql
 STABLE
AS $function$
WITH
-- ── 1) Lotes candidatos de la empresa (+ ubicación y datos de guía) ──────────
lote_base AS (
    SELECT l.lote_id,
           l.lote_nombre::text                                        AS lote_nombre,
           l.granja_id,
           f.name::text                                               AS granja_nombre,
           n.nucleo_nombre::text                                      AS nucleo_nombre,
           COALESCE(NULLIF(mo.value, ''), NULLIF(l.regional, ''))::text AS regional,
           l.raza::text                                               AS raza,
           l.ano_tabla_genetica                                       AS anio_guia,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date       AS enc_date
      FROM lotes l
      JOIN farms f
        ON f.id = l.granja_id
      LEFT JOIN nucleos n
        ON n.granja_id = l.granja_id
       AND n.nucleo_id = l.nucleo_id
       AND n.deleted_at IS NULL
      LEFT JOIN master_list_options mo
        ON mo.id = f.regional_id
     WHERE l.company_id = p_company_id
       AND l.deleted_at IS NULL
       AND (p_granja_ids IS NULL OR l.granja_id = ANY (p_granja_ids))
),
-- ── 2) Registros diarios de levante, con la semana de edad ya resuelta ───────
--    Mismo WHERE, mismo COALESCE y misma exclusión de «puro traslado > 25»
--    que fn_reporte_semanal_levante_extras.
reg AS (
    SELECT lb.lote_id,
           (floor(((sl.fecha AT TIME ZONE 'America/Bogota')::date - lb.enc_date) / 7.0)::int) + 1 AS real_sem,
           (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date,
           COALESCE(sl.mortalidad_hembras, 0)      AS mort_h,
           COALESCE(sl.mortalidad_machos, 0)       AS mort_m,
           COALESCE(sl.sel_h, 0)                   AS sel_h,
           COALESCE(sl.sel_m, 0)                   AS sel_m,
           COALESCE(sl.error_sexaje_hembras, 0)    AS err_h,
           COALESCE(sl.error_sexaje_machos, 0)     AS err_m,
           COALESCE(sl.consumo_kg_hembras, 0)::double precision AS cons_kg_h,
           COALESCE(sl.consumo_kg_machos, 0)::double precision  AS cons_kg_m,
           COALESCE(sl.traslado_salida_hembras, 0) AS tras_sal_h,
           COALESCE(sl.traslado_salida_machos, 0)  AS tras_sal_m,
           COALESCE(sl.traslado_ingreso_hembras, 0) AS tras_ing_h,
           COALESCE(sl.traslado_ingreso_machos, 0)  AS tras_ing_m,
           -- Venta de aves: el saldo tiene que descontarla o el reporte sobrestima el lote. El total
           -- (venta_aves_cantidad) no sirve acá porque el saldo va POR SEXO; se usan los splits
           -- dedicados venta_aves_hembras/machos, espejo de movimiento_aves (que sigue siendo el
           -- dueño del número). Sin esto S-369B reportaba 1.281 machos con el maestro en 991.
           COALESCE(sl.venta_aves_hembras, 0)       AS venta_h,
           COALESCE(sl.venta_aves_machos, 0)        AS venta_m,
           COALESCE(sl.peso_prom_hembras, 0)::double precision AS ph,
           COALESCE(sl.peso_prom_machos, 0)::double precision  AS pm,
           sl.uniformidad_hembras::double precision AS uh,
           sl.uniformidad_machos::double precision  AS um,
           sl.cv_hembras::double precision          AS cvh,
           sl.cv_machos::double precision           AS cvm,
           sl.id
      FROM lote_base lb
      JOIN seguimiento_diario_levante sl
        ON sl.lote_id = lb.lote_id::text
       AND sl.tipo_seguimiento = 'levante'
),
-- ── 3) Guards por lote: primer registro y validez del encaset ────────────────
lote_ok AS (
    SELECT lb.*,
           g.min_reg,
           -- base por sexo con el mismo fallback del Detalle. Sigue siendo COALESCE, no suma:
           -- un lote CON encaset conserva exactamente su número de siempre. El fallback solo
           -- entra cuando el encaset es 0/NULL, que es el lote poblado únicamente por traslado.
           COALESCE(
               NULLIF(l.hembras_l, 0)::double precision,
               NULLIF(fi.ing_desc_h, 0),
               0)                                    AS base_h,
           COALESCE(
               NULLIF(l.machos_l, 0)::double precision,
               NULLIF(fi.ing_desc_m, 0),
               0)                                    AS base_m,
           COALESCE(tr.tuvo_traslado, false)         AS tuvo_traslado
      FROM lote_base lb
      JOIN lotes l
        ON l.lote_id = lb.lote_id
      JOIN LATERAL (
            SELECT MIN(r.reg_date) AS min_reg
              FROM reg r
             WHERE r.lote_id = lb.lote_id
      ) g ON true
      -- Aves que entraron por traslado en filas que reg_ok DESCARTA (puro traslado > sem 25).
      -- Esas aves no las suma nadie: la ventana las tira, así que si el lote no trae encaset
      -- quedan fuera del saldo. Se rescatan acá como base.
      --
      -- ⚠️ El predicado tiene que ser el MISMO que el de reg_ok (más abajo). Si cambia uno,
      --    cambia el otro: si acá entrara una fila que reg_ok SÍ cuenta, sus aves se sumarían
      --    dos veces (una como base y otra como ingreso) y el saldo saldría inflado.
      -- SUM por sexo, no una sola fila: los sexos pueden llegar en traslados de DÍAS DISTINTOS.
      -- Con `LIMIT 1` se leían los dos sexos de la fila más antigua, así que el sexo que no
      -- venía en esa fila quedaba con base 0 y el reporte lo mostraba NEGATIVO tras restarle
      -- la mortalidad (caso real: machos el 08-jun y hembras el 11-jun ⇒ hembras en -212).
      LEFT JOIN LATERAL (
            SELECT COALESCE(SUM(r.tras_ing_h), 0)::double precision AS ing_desc_h,
                   COALESCE(SUM(r.tras_ing_m), 0)::double precision AS ing_desc_m
              FROM reg r
             WHERE r.lote_id = lb.lote_id
               AND r.real_sem > 25
               AND r.mort_h = 0 AND r.mort_m = 0
               AND r.sel_h = 0  AND r.sel_m = 0
               AND r.err_h = 0  AND r.err_m = 0
               AND r.cons_kg_h = 0 AND r.cons_kg_m = 0
               AND r.ph = 0 AND r.pm = 0
               AND r.venta_h = 0 AND r.venta_m = 0
               AND (r.tras_sal_h + r.tras_sal_m + r.tras_ing_h + r.tras_ing_m) > 0
      ) fi ON true
      LEFT JOIN LATERAL (
            SELECT true AS tuvo_traslado
              FROM reg r
             WHERE r.lote_id = lb.lote_id
               AND (r.tras_ing_h + r.tras_ing_m + r.tras_sal_h + r.tras_sal_m) > 0
             LIMIT 1
      ) tr ON true
     WHERE g.min_reg IS NOT NULL
       AND lb.enc_date IS NOT NULL
       AND lb.enc_date <= g.min_reg
),
-- ── 4) Registros válidos (topados a 25, sin filas de puro traslado > 25) ─────
reg_ok AS (
    SELECT r.*,
           LEAST(25, r.real_sem) AS sem
      FROM reg r
      JOIN lote_ok lo ON lo.lote_id = r.lote_id
     WHERE NOT (
               r.real_sem > 25
           AND r.mort_h = 0 AND r.mort_m = 0
           AND r.sel_h = 0  AND r.sel_m = 0
           AND r.err_h = 0  AND r.err_m = 0
           AND r.cons_kg_h = 0 AND r.cons_kg_m = 0
           AND r.ph = 0 AND r.pm = 0
           AND r.venta_h = 0 AND r.venta_m = 0
           AND (r.tras_sal_h + r.tras_sal_m + r.tras_ing_h + r.tras_ing_m) > 0
       )
),
-- ── 5) Agregado semanal por lote ────────────────────────────────────────────
sem AS (
    SELECT lote_id,
           sem,
           COUNT(DISTINCT reg_date)::int        AS dias,
           SUM(mort_h)::double precision       AS mort_h,
           SUM(mort_m)::double precision       AS mort_m,
           SUM(sel_h)::double precision        AS sel_h,
           SUM(sel_m)::double precision        AS sel_m,
           SUM(err_h)::double precision        AS err_h,
           SUM(err_m)::double precision        AS err_m,
           SUM(tras_sal_h)::double precision   AS tras_sal_h,
           SUM(tras_sal_m)::double precision   AS tras_sal_m,
           SUM(tras_ing_h)::double precision   AS tras_ing_h,
           SUM(tras_ing_m)::double precision   AS tras_ing_m,
           SUM(venta_h)::double precision      AS venta_h,
           SUM(venta_m)::double precision      AS venta_m,
           SUM(cons_kg_h)                      AS cons_kg_h,
           SUM(cons_kg_m)                      AS cons_kg_m
      FROM reg_ok
     GROUP BY lote_id, sem
),
-- ── 6) Fila de pesaje de la semana (misma regla de selección del Detalle) ────
pesaje AS (
    SELECT s.lote_id,
           s.sem,
           p.ph, p.pm, p.uh, p.um, p.cvh, p.cvm
      FROM sem s
      LEFT JOIN LATERAL (
            SELECT r.ph, r.pm, r.uh, r.um, r.cvh, r.cvm
              FROM reg_ok r
             WHERE r.lote_id = s.lote_id
               AND r.sem = s.sem
             ORDER BY (CASE WHEN r.ph > 0 OR r.pm > 0 THEN 0 ELSE 1 END),
                      r.reg_date DESC, r.id DESC
             LIMIT 1
      ) p ON true
),
-- ── 7) Acumulados por ventana + arrastre (LOCF) del peso por sexo ───────────
--    El "grupo" de LOCF es el conteo de pesajes no nulos hasta la semana:
--    dentro de cada grupo, el primer valor es el último peso conocido.
acum AS (
    SELECT s.lote_id,
           s.sem,
           s.dias,
           s.mort_h, s.mort_m, s.sel_h, s.sel_m, s.err_h, s.err_m,
           s.tras_sal_h, s.tras_sal_m, s.tras_ing_h, s.tras_ing_m,
           s.venta_h, s.venta_m,
           s.cons_kg_h, s.cons_kg_m,
           NULLIF(p.ph, 0) AS peso_h_raw,
           NULLIF(p.pm, 0) AS peso_m_raw,
           NULLIF(COALESCE(p.uh, 0), 0)  AS unif_h,
           NULLIF(COALESCE(p.um, 0), 0)  AS unif_m,
           NULLIF(COALESCE(p.cvh, 0), 0) AS cv_h,
           NULLIF(COALESCE(p.cvm, 0), 0) AS cv_m,
           -- salidas netas acumuladas hasta ESTA semana (inclusive)
           SUM(s.mort_h + s.sel_h + s.err_h + s.tras_sal_h + s.venta_h - s.tras_ing_h)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS neto_out_h,
           SUM(s.mort_m + s.sel_m + s.err_m + s.tras_sal_m + s.venta_m - s.tras_ing_m)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS neto_out_m,
           -- retiro acumulado (mort + sel + err), SIN traslados: es lo que el
           -- Excel llama RetAcH/RetAcM y va sobre base FIJA
           SUM(s.mort_h + s.sel_h + s.err_h)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS retiro_ac_h,
           SUM(s.mort_m + s.sel_m + s.err_m)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS retiro_ac_m,
           COUNT(NULLIF(p.ph, 0))
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS grp_h,
           COUNT(NULLIF(p.pm, 0))
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS grp_m
      FROM sem s
      JOIN pesaje p
        ON p.lote_id = s.lote_id AND p.sem = s.sem
),
locf AS (
    SELECT a.*,
           FIRST_VALUE(a.peso_h_raw) OVER (
               PARTITION BY a.lote_id, a.grp_h ORDER BY a.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS peso_h,
           FIRST_VALUE(a.peso_m_raw) OVER (
               PARTITION BY a.lote_id, a.grp_m ORDER BY a.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS peso_m
      FROM acum a
),
-- ── 8) Solo la semana calendario pedida (WEEKNUM estilo Excel) ──────────────
sem_objetivo AS (
    SELECT lo.lote_id, lo.lote_nombre, lo.granja_id, lo.granja_nombre,
           lo.nucleo_nombre, lo.regional, lo.raza, lo.anio_guia,
           lo.base_h, lo.base_m, lo.tuvo_traslado,
           x.sem, x.dias,
           x.mort_h, x.mort_m, x.sel_h, x.sel_m, x.err_h, x.err_m,
           x.tras_sal_h, x.tras_sal_m, x.tras_ing_h, x.tras_ing_m,
           x.cons_kg_h, x.cons_kg_m,
           x.unif_h, x.unif_m, x.cv_h, x.cv_m,
           x.neto_out_h, x.neto_out_m, x.retiro_ac_h, x.retiro_ac_m,
           x.peso_h, x.peso_m,
           (lo.enc_date + ((x.sem - 1) * 7) + 6) AS fin_sem,
           -- Semana CALENDARIO (WEEKNUM estilo Excel) del cierre de la semana de edad.
           -- Se materializa acá porque la usan DOS cosas: el filtro de abajo y la
           -- partición de `part`. OJO: NO es lo mismo que fin_sem — fin_sem depende del
           -- encaset de CADA lote, así que dos sublotes del mismo lote padre con fechas
           -- de llegada distintas caen en la misma semana calendario con fin_sem DISTINTO.
           floor(
             ( (lo.enc_date + ((x.sem - 1) * 7) + 6)
               - date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp)::date
               + EXTRACT(DOW FROM date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp))::int
             ) / 7.0
           )::int + 1                            AS sem_cal
      FROM locf x
      JOIN lote_ok lo ON lo.lote_id = x.lote_id
     WHERE EXTRACT(YEAR FROM (lo.enc_date + ((x.sem - 1) * 7) + 6))::int = p_anio
       -- p_sem_anio NULL = TODAS las semanas del año (curva del año completo);
       -- con valor, una sola semana calendario.
       AND (p_sem_anio IS NULL OR (
             floor(
               ( (lo.enc_date + ((x.sem - 1) * 7) + 6)
                 - date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp)::date
                 + EXTRACT(DOW FROM date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp))::int
               ) / 7.0
             )::int + 1
           ) = p_sem_anio)
       AND (p_regional IS NULL OR lo.regional = p_regional)
       AND (NOT p_excluir_trasladados OR NOT lo.tuvo_traslado)
),
-- ── 9) Guía del lote para esa edad ──────────────────────────────────────────
con_guia AS (
    SELECT so.*,
           f_safe_numeric(g.retiro_ac_h)  AS g_retiro_ac_h,
           f_safe_numeric(g.retiro_ac_m)  AS g_retiro_ac_m,
           f_safe_numeric(g.gr_ave_dia_h) AS g_gr_ave_dia_h,
           f_safe_numeric(g.gr_ave_dia_m) AS g_gr_ave_dia_m,
           f_safe_numeric(g.peso_h)       AS g_peso_h,
           f_safe_numeric(g.peso_m)       AS g_peso_m
      FROM sem_objetivo so
      LEFT JOIN LATERAL (
            SELECT gg.*
              -- Fuente unificada: la compartida + la reducida proyectada al mismo shape.
              -- Aca NO hace falta leer `origen` como en fn_indicadores_*: estas columnas
              -- pasan por f_safe_numeric(), que ya devuelve NULL ante NULL o texto no
              -- numerico ⇒ no fabrica el 0 falso que alla habia que condicionar.
              FROM vw_guia_genetica_postura gg
             WHERE gg.company_id = p_company_id
               AND gg.deleted_at IS NULL
               AND lower(trim(gg.raza)) = lower(trim(COALESCE(so.raza, '')))
               AND trim(gg.anio_guia) = so.anio_guia::text
               -- ⚠️ Comparación de edad como TEXTO EXACTO, igual que
               --    fn_indicadores_levante_postura (`btrim(g.edad) = s::text`).
               --    NO parsear a número: la guía tiene DOS filas para la semana 25
               --    ('25' de levante y '25P' de producción) y el parseo numérico
               --    haría match con las dos, devolviendo la fila equivocada.
               AND btrim(gg.edad) = so.sem::text
             ORDER BY gg.id
             LIMIT 1
      ) g ON true
),
-- ── 10) Saldos y derivadas ──────────────────────────────────────────────────
calc AS (
    SELECT cg.*,
           (cg.base_h - cg.neto_out_h)                                   AS saldo_h,
           (cg.base_m - cg.neto_out_m)                                   AS saldo_m,
           -- aves al INICIO de la semana = saldo final + salidas netas de la semana
           (cg.base_h - cg.neto_out_h
              + (cg.mort_h + cg.sel_h + cg.err_h + cg.tras_sal_h - cg.tras_ing_h)) AS ini_h,
           (cg.base_m - cg.neto_out_m
              + (cg.mort_m + cg.sel_m + cg.err_m + cg.tras_sal_m - cg.tras_ing_m)) AS ini_m
      FROM con_guia cg
),
final AS (
    SELECT c.*,
           -- g/ave/día real por sexo: kg*1000 / promedio(inicio, fin) / días
           CASE WHEN c.dias > 0 AND ((c.ini_h + (c.base_h - c.neto_out_h)) / 2.0) > 0
                THEN (c.cons_kg_h * 1000.0)
                     / ((c.ini_h + (c.base_h - c.neto_out_h)) / 2.0) / c.dias
           END AS gr_ave_dia_h,
           CASE WHEN c.dias > 0 AND ((c.ini_m + (c.base_m - c.neto_out_m)) / 2.0) > 0
                THEN (c.cons_kg_m * 1000.0)
                     / ((c.ini_m + (c.base_m - c.neto_out_m)) / 2.0) / c.dias
           END AS gr_ave_dia_m
      FROM calc c
)
SELECT
    f.lote_id,
    f.lote_nombre,
    f.granja_id,
    f.granja_nombre,
    f.nucleo_nombre,
    f.regional,
    f.raza,
    f.anio_guia,
    f.sem                                                        AS edad_semana,
    f.fin_sem                                                    AS fecha_fin_semana,
    f.dias                                                       AS dias_con_registro,
    f.tuvo_traslado,
    -- Participación SIEMPRE dentro de su propia semana CALENDARIO: con p_sem_anio NULL
    -- la ventana global mezclaría las 52 semanas del año. Particiona por sem_cal, NO por
    -- fin_sem: fin_sem sale del encaset de cada lote, así que un lote padre con sublotes
    -- de fechas de llegada distintas dejaba a cada sublote SOLO en su partición y todos
    -- daban part = 1 (deberían repartirse ~0,50 y ~0,50). Con p_sem_anio concreto todas
    -- las filas comparten sem_cal, así que esto equivale al OVER () original.
    CASE WHEN SUM(f.saldo_h) OVER (PARTITION BY f.sem_cal) > 0
         THEN f.saldo_h / SUM(f.saldo_h) OVER (PARTITION BY f.sem_cal)
    END                                                          AS part,
    f.saldo_h                                                    AS saldo_hembras,
    f.saldo_m                                                    AS saldo_machos,
    -- ── hembras ──
    CASE WHEN f.ini_h > 0 THEN f.mort_h / f.ini_h * 100.0 END    AS mort_hembras_pct,
    CASE WHEN f.base_h > 0 THEN f.retiro_ac_h / f.base_h * 100.0 END AS retiro_acum_hembras_pct,
    f.g_retiro_ac_h::double precision                            AS retiro_acum_hembras_guia,
    CASE WHEN COALESCE(f.g_gr_ave_dia_h, 0) <> 0 AND f.gr_ave_dia_h IS NOT NULL
         THEN (f.gr_ave_dia_h / f.g_gr_ave_dia_h::double precision - 1) * 100.0 END
                                                                 AS dif_consumo_hembras_pct,
    CASE WHEN COALESCE(f.g_peso_h, 0) <> 0 AND f.peso_h IS NOT NULL
         THEN (f.peso_h / f.g_peso_h::double precision - 1) * 100.0 END
                                                                 AS dif_peso_hembras_pct,
    f.unif_h                                                     AS uniformidad_hembras,
    f.cv_h                                                       AS cv_hembras,
    -- ── machos ──
    CASE WHEN f.ini_m > 0 THEN f.mort_m / f.ini_m * 100.0 END    AS mort_machos_pct,
    CASE WHEN f.base_m > 0 THEN f.retiro_ac_m / f.base_m * 100.0 END AS retiro_acum_machos_pct,
    f.g_retiro_ac_m::double precision                            AS retiro_acum_machos_guia,
    CASE WHEN COALESCE(f.g_gr_ave_dia_m, 0) <> 0 AND f.gr_ave_dia_m IS NOT NULL
         THEN (f.gr_ave_dia_m / f.g_gr_ave_dia_m::double precision - 1) * 100.0 END
                                                                 AS dif_consumo_machos_pct,
    CASE WHEN COALESCE(f.g_peso_m, 0) <> 0 AND f.peso_m IS NOT NULL
         THEN (f.peso_m / f.g_peso_m::double precision - 1) * 100.0 END
                                                                 AS dif_peso_machos_pct,
    f.unif_m                                                     AS uniformidad_machos,
    f.cv_m                                                       AS cv_machos
  FROM final f
 ORDER BY f.sem DESC, f.lote_nombre;
$function$
""";
    }
}
