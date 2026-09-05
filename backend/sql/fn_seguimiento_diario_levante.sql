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
