// Partial de 20260905035704_FnSeguimientoDiarioLevanteYFixesConteoDias: SQL verbatim.
// Nueva = espejo en backend/sql/ (este commit); Prev = version anterior para el Down.

namespace ZooSanMarino.Infrastructure.Migrations
{
    public partial class FnSeguimientoDiarioLevanteYFixesConteoDias
    {
        private const string FnSeguimientoDiarioLevante = """
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

        private const string FnIndicadoresLevanteNueva = """
-- ============================================================================
-- fn_indicadores_levante_postura(lote_id)
-- Indicadores semanales de LEVANTE (postura Colombia) calculados en la BD.
-- Reemplaza el cómputo del front (lote-levante/tabla-lista-indicadores +
-- graficas-principal): el front solo debe pintar.
--
-- Replica EXACTO el algoritmo del front (double precision, mismo orden) e
-- incorpora las correcciones ya acordadas:
--   * Peso/uniformidad del PESAJE semanal: último registro de la semana con
--     peso>0 (no el último día, que suele venir en 0) + arrastre del último
--     peso conocido cuando la semana no tiene pesaje (evita ganancia negativa
--     y dif -100%).  [bug histórico corregido]
--   * Guía genética REAL desde guia_genetica_sanmarino_colombia por
--     raza + año + company + semana (no valores hardcodeados / no Ecuador).
--
-- Correcciones matriz Verenice rev 6-jul-26:
--   * REQ-002e — Consumo por sexo: además del consumo mixto (compatibilidad),
--     se exponen consumo_diario_hembras / consumo_diario_machos (g/ave/día reales
--     por sexo = consumo_kg_sexo*1000 / saldo_prom_sexo / días) y
--     consumo_tabla_hembras / consumo_tabla_machos (gr_ave_dia_h/_m de la guía, SIN
--     promediar). Requiere llevar el saldo de aves POR GÉNERO dentro de la fn.
--     (Columnas renombradas de _h/_m a _hembras/_machos por el mapeo EF, ver nota abajo.)
--   * REQ-002f — Acumulados reales: mortalidad/selección acumuladas =
--     bajas_acumuladas / aves_encasetadas * 100 (acumulado real sobre aves
--     iniciales), no la suma de % semanales sobre base decreciente.
--   * REQ-002f/B36 — Semana fantasma: se EXCLUYEN las filas de PURO traslado
--     (sin mortalidad/selección/error/consumo/pesaje) posteriores a la
--     semana 25; ya no se clampean con LEAST(25) generando una "semana 25"
--     falsa con el salto de saldo del traslado post-levante.
--   * REQ-002B36 — Defensas:
--       - Base de aves con fallback: COALESCE(aves_encasetadas,
--         hembras_l+machos_l, primer traslado_ingreso, 0).
--       - Encaset futuro/ausente: si fecha_encaset es NULL o es POSTERIOR al
--         primer registro (encaset tecleado a futuro, p. ej. lote 116), se
--         devuelven CERO filas en lugar de colapsar 140+ días en una
--         "semana 1" absurda con base 0 y %pérdidas 100%. Se eligió devolver
--         cero filas (y no "usar el primer registro como referencia") porque
--         con un encaset inconsistente NINGÚN indicador es confiable: es más
--         seguro que el front muestre su empty-state a mostrar cifras
--         engañosas. Al devolver cero filas ya no hace falta GREATEST(1,…)
--         (no quedan semanas negativas que clampear).
--       - Idempotencia intra-transacción: DROP TABLE IF EXISTS _seg_sem antes
--         del CREATE TEMP TABLE (permite llamar la fn 2+ veces en la misma
--         transacción sin 'relation _seg_sem already exists').
--
-- Fuente de verdad del algoritmo: tabla-lista-indicadores.component.ts
-- Zona horaria: America/Bogota para el corte de semanas (calendario local).
--
-- Fase 3 (convergencia levante a Feature-13): lee la tabla CANÓNICA
-- seguimiento_diario_levante (tipo_seguimiento='levante') y las
-- salidas de la semana incluyen error de sexaje y traslados dedicados:
--   out = mort + sel + err + traslado_salida - traslado_ingreso;  aves_fin = aves - out.
-- ============================================================================
--   * REQ-010b — Series POR SEXO para el selector Hembras/Machos/Ambos de la
--     pestaña Gráfica: además del consumo por sexo, se exponen peso (real +
--     guía), mortalidad % (real + guía) y retiro % (real; la guía por sexo no
--     existe ⇒ NULL) por sexo, para que el control cambie las series Real/Guía.
--     Aritmética por sexo consistente con la mixta (mismo denominador = aves al
--     inicio de la semana del sexo; NULL cuando el sexo no tiene saldo/pesaje).
--
--   * TK-2026-000022 — TODOS los parametros por sexo en la TABLA de indicadores.
--     El usuario reporto que «los parametros aparecen solo para un grupo de aves y
--     no identifica si se refieren a hembras o machos». Peor: varias columnas
--     mixtas son un PROMEDIO ARITMETICO simple de los dos sexos (peso_cierre y
--     unif_real: (H+M)/2, sin ponderar por cantidad de aves), o sea un valor que
--     no le corresponde a ninguna ave del galpon —en reproductoras la hembra y el
--     macho tienen pesos muy distintos—. Se exponen aves inicio/fin, consumo total,
--     uniformidad, ganancia, dif % de peso vs guia, seleccion % y error de sexaje %
--     por sexo. NO se agrega aritmetica nueva: son las mismas variables internas
--     con las que ya se arman las columnas mixtas, publicadas sin promediar.
--
-- IMPORTANTE (mapeo EF): los nombres de las columnas por sexo son el snake_case
-- EXACTO de las props del DTO (…Hembras→…_hembras, …Machos→…_machos). EF Core
-- (SqlQueryRaw<IndicadorSemanalLevanteDto> con convención snake_case) mapea
-- ConsumoDiarioHembras↔consumo_diario_hembras, PesoHembras↔peso_hembras, etc.
-- Un nombre abreviado (_h/_m) NO mapearía a props …Hembras/…Machos (mismo patrón
-- probado en fn_indicadores_produccion_postura: porcentaje_mortalidad_hembras…).
-- Por eso las columnas de consumo por sexo se renombran de _h/_m a _hembras/_machos.
--
-- DROP previo: la firma cambió (se renombraron/agregaron columnas OUT por sexo),
-- y CREATE OR REPLACE no puede alterar el tipo de retorno.
DROP FUNCTION IF EXISTS fn_indicadores_levante_postura(integer);
CREATE OR REPLACE FUNCTION fn_indicadores_levante_postura(p_lote_id integer)
RETURNS TABLE(
    semana                          integer,
    aves_inicio_semana              double precision,
    aves_fin_semana                 double precision,
    consumo_diario                  double precision,   -- g/ave/día real (mixto H+M)
    consumo_tabla                   double precision,   -- g/ave/día guía (promedio H,M)
    consumo_total_semana            double precision,   -- gramos
    conversion_alimenticia          double precision,
    peso_tabla                      double precision,
    unif_real                       double precision,
    unif_tabla                      double precision,
    mort_tabla                      double precision,
    dif_peso_pct                    double precision,
    ganancia_semana                 double precision,
    ganancia_diaria_acumulada       double precision,
    ganancia_tabla                  double precision,
    mortalidad_sem                  double precision,
    seleccion_sem                   double precision,
    error_sexaje_sem                double precision,
    mortalidad_mas_seleccion        double precision,
    eficiencia                      double precision,
    ip                              double precision,
    vpi                             double precision,
    saldo_aves_semanal              double precision,
    mortalidad_acum                 double precision,
    seleccion_acum                  double precision,
    mortalidad_mas_seleccion_acum   double precision,
    piso_termico_visible            boolean,
    peso_inicial                    double precision,
    peso_cierre                     double precision,
    dias_con_registro               integer,
    -- REQ-002e / REQ-010b: series POR SEXO (reales y guía SIN promediar). numeric → decimal? en el DTO.
    -- Nombres = snake_case EXACTO de las props del DTO para que EF las mapee (ver nota de cabecera).
    consumo_diario_hembras          numeric,            -- g/ave/día real hembras
    consumo_diario_machos           numeric,            -- g/ave/día real machos
    consumo_tabla_hembras           numeric,            -- gr_ave_dia_h de la guía
    consumo_tabla_machos            numeric,            -- gr_ave_dia_m de la guía
    peso_hembras                    numeric,            -- peso prom hembras (arrastre si semana sin pesaje)
    peso_machos                     numeric,            -- peso prom machos  (arrastre si semana sin pesaje)
    peso_tabla_hembras              numeric,            -- guía peso_h
    peso_tabla_machos               numeric,            -- guía peso_m
    mort_pct_hembras                numeric,            -- % mort semana hembras = mort_h / aves_inicio_h * 100
    mort_pct_machos                 numeric,            -- % mort semana machos  = mort_m / aves_inicio_m * 100
    mort_tabla_hembras              numeric,            -- guía mort_sem_h
    mort_tabla_machos               numeric,            -- guía mort_sem_m
    retiro_pct_hembras              numeric,            -- % retiro hembras = (mort+sel+err)_h / aves_inicio_h * 100
    retiro_pct_machos               numeric,            -- % retiro machos  = (mort+sel+err)_m / aves_inicio_m * 100
    -- TK-2026-000022: el resto de los parametros POR SEXO. La tabla de indicadores mostraba una
    -- sola serie sin decir de que sexo era —y varias de esas columnas mixtas son un PROMEDIO
    -- ARITMETICO de hembras y machos (peso, uniformidad), o sea un numero que no le corresponde a
    -- ninguna ave real. Todo esto ya se calculaba dentro de la funcion; solo faltaba exponerlo.
    -- Convencion identica a las de arriba: NULL cuando el sexo no existe en el lote o no hay dato,
    -- nunca 0 sintetico.
    aves_inicio_hembras             numeric,            -- saldo hembras al inicio de la semana
    aves_fin_hembras                numeric,            -- saldo hembras al cierre de la semana
    aves_inicio_machos              numeric,            -- saldo machos al inicio de la semana
    aves_fin_machos                 numeric,            -- saldo machos al cierre de la semana
    consumo_total_semana_hembras    numeric,            -- gramos consumidos por las hembras en la semana
    consumo_total_semana_machos     numeric,            -- gramos consumidos por los machos en la semana
    unif_hembras                    numeric,            -- % uniformidad hembras del pesaje de la semana
    unif_machos                     numeric,            -- % uniformidad machos  del pesaje de la semana
    ganancia_hembras                numeric,            -- g ganados por las hembras respecto de la semana previa
    ganancia_machos                 numeric,            -- g ganados por los machos  respecto de la semana previa
    dif_peso_pct_hembras            numeric,            -- (peso_h - guia peso_h) / guia peso_h * 100
    dif_peso_pct_machos             numeric,            -- (peso_m - guia peso_m) / guia peso_m * 100
    seleccion_pct_hembras           numeric,            -- % seleccion semana hembras = sel_h / aves_inicio_h * 100
    seleccion_pct_machos            numeric,            -- % seleccion semana machos  = sel_m / aves_inicio_m * 100
    error_sexaje_pct_hembras        numeric,            -- % error sexaje hembras = err_h / aves_inicio_h * 100
    error_sexaje_pct_machos         numeric             -- % error sexaje machos  = err_m / aves_inicio_m * 100
)
LANGUAGE plpgsql VOLATILE AS $$
DECLARE
    v_raza        text;
    v_anio        text;
    v_company     integer;
    v_aves_enc_col integer;   -- lotes.aves_encasetadas (crudo)
    v_hembras_l   integer;    -- lotes.hembras_l (crudo)
    v_machos_l    integer;    -- lotes.machos_l (crudo)
    v_aves_enc    double precision;   -- base total resuelta (con fallback)
    v_aves_enc_h  double precision;   -- base hembras resuelta
    v_aves_enc_m  double precision;   -- base machos resuelta
    v_peso_ini    double precision;
    v_enc_date    date;
    v_min_reg     date;
    v_first_ing_h double precision;   -- primer traslado_ingreso (fallback base)
    v_first_ing_m double precision;

    -- acumuladores (mismos nombres que el front)
    v_aves_acum       double precision;
    v_aves_acum_h     double precision;
    v_aves_acum_m     double precision;
    v_mort_bajas_acum double precision := 0;   -- bajas acumuladas (unidades) REQ-002f
    v_sel_bajas_acum  double precision := 0;   -- selección acumulada (unidades) REQ-002f
    v_peso_anterior   double precision;
    v_peso_tabla_ant  double precision := 0;

    v_max_sem     integer;
    s             integer;

    -- por semana
    r_mort_tot    double precision;
    r_sel_tot     double precision;
    r_cons_kg     double precision;
    r_err_tot     double precision;
    r_tras_sal    double precision;
    r_tras_ing    double precision;
    r_venta_tot   double precision;   -- venta de aves: sale del lote y no llega a ningún otro
    r_dias        integer;
    r_aves_fin    double precision;
    -- por semana / por género
    r_mort_h      double precision;
    r_mort_m      double precision;
    r_sel_h       double precision;
    r_sel_m       double precision;
    r_err_h       double precision;
    r_err_m       double precision;
    r_cons_kg_h   double precision;
    r_cons_kg_m   double precision;
    r_tras_sal_h  double precision;
    r_tras_sal_m  double precision;
    r_tras_ing_h  double precision;
    r_tras_ing_m  double precision;
    r_venta_h     double precision;
    r_venta_m     double precision;
    r_aves_fin_h  double precision;
    r_aves_fin_m  double precision;
    r_aves_prom_h double precision;
    r_aves_prom_m double precision;
    r_cons_dia_h  double precision;
    r_cons_dia_m  double precision;
    r_cons_tabla_h double precision;
    r_cons_tabla_m double precision;
    -- REQ-010b: peso / mortalidad / retiro POR SEXO + guía por sexo.
    v_peso_ant_h   double precision;   -- arrastre peso hembras
    v_peso_ant_m   double precision;   -- arrastre peso machos
    r_peso_h       double precision;
    r_peso_m       double precision;
    r_peso_tabla_h double precision;
    r_peso_tabla_m double precision;
    r_mort_tabla_h double precision;
    r_mort_tabla_m double precision;
    -- De que tabla salio la fila de guia: 'compartida' (guia_genetica_sanmarino_colombia,
    -- >40 columnas) o 'propia' (guia_genetica_santa_reyes, 3 metricas y solo hembras).
    -- Ver el bloque de la guia mas abajo: gobierna si se coalescea a 0 o se deja NULL.
    v_origen_guia  text;
    -- ¿La EMPRESA del lote tiene guia propia (tabla reducida)? Distinto de v_origen_guia, que
    -- dice de donde salio LA FILA de esta semana y queda NULL cuando no hubo ninguna. Se necesita
    -- separado para el caso «empresa con guia propia + semana sin fila»: ahi un 0 seria un
    -- objetivo inventado (su guia arranca en la semana 18 y no cubre todo el levante).
    v_guia_propia_empresa boolean := false;
    r_mort_pct_h   double precision;
    r_mort_pct_m   double precision;
    r_retiro_pct_h double precision;
    r_retiro_pct_m double precision;

    r_pH          double precision;
    r_pM          double precision;
    r_peso_prom   double precision;
    r_uH          double precision;
    r_uM          double precision;
    r_unif_real   double precision;
    r_cons_g      double precision;
    r_aves_prom   double precision;
    r_cons_dia    double precision;
    r_cons_tabla  double precision;
    r_peso_tabla  double precision;
    r_unif_tabla  double precision;
    r_mort_tabla  double precision;
    r_gan_sem     double precision;
    r_cons_ave    double precision;
    r_conv        double precision;
    r_gan_dia_ac  double precision;
    r_gan_tabla   double precision;
    r_mort_sem    double precision;
    r_sel_sem     double precision;
    r_err_sem     double precision;
    r_mort_mas_sel double precision;
    r_efic        double precision;
    r_superv      double precision;
    r_ip          double precision;
BEGIN
    SELECT l.raza, l.ano_tabla_genetica::text, l.company_id,
           l.aves_encasetadas, l.hembras_l, l.machos_l,
           COALESCE(l.peso_inicial_h,0)::double precision,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date
      INTO v_raza, v_anio, v_company, v_aves_enc_col, v_hembras_l, v_machos_l, v_peso_ini, v_enc_date
      FROM lotes l
     WHERE l.lote_id = p_lote_id AND l.deleted_at IS NULL;

    IF NOT FOUND THEN RETURN; END IF;

    -- Una sola vez por lote: ¿esta empresa tiene guia propia? Gobierna el COALESCE a 0 de mas
    -- abajo. Para las cuatro empresas que leen la guia compartida da FALSE, y la expresion que
    -- se ejecuta queda identica a la de siempre.
    SELECT EXISTS (SELECT 1 FROM guia_genetica_santa_reyes gp
                    WHERE gp.company_id = v_company AND gp.deleted_at IS NULL)
      INTO v_guia_propia_empresa;

    -- Aves entradas por traslado en filas que el armado de la serie DESCARTA (puro traslado
    -- > sem 25): fallback de base cuando el lote se pobló por traslado y no trae
    -- aves_encasetadas / hembras_l / machos_l. Nadie más suma esas aves — la ventana las tira.
    --
    -- ⚠️ El predicado debe ser el MISMO que el WHERE NOT (...) del armado de la serie. Si acá
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

    -- Primer registro (calendario Bogotá) para validar el encaset.
    SELECT MIN((sl.fecha AT TIME ZONE 'America/Bogota')::date)
      INTO v_min_reg
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text;

    IF v_min_reg IS NULL THEN RETURN; END IF;   -- sin registros

    -- REQ-002B36: encaset ausente o POSTERIOR al primer registro (futuro) ⇒
    -- datos inconsistentes ⇒ cero filas (el front muestra su empty-state).
    IF v_enc_date IS NULL OR v_enc_date > v_min_reg THEN RETURN; END IF;

    -- Base de aves con fallback (REQ-002B36).
    v_aves_enc := COALESCE(
        NULLIF(v_aves_enc_col, 0)::double precision,
        NULLIF(COALESCE(v_hembras_l,0) + COALESCE(v_machos_l,0), 0)::double precision,
        NULLIF(v_first_ing_h + v_first_ing_m, 0),
        0);
    v_aves_enc_h := COALESCE(
        NULLIF(v_hembras_l, 0)::double precision,
        NULLIF(v_first_ing_h, 0),
        0);
    v_aves_enc_m := COALESCE(
        NULLIF(v_machos_l, 0)::double precision,
        NULLIF(v_first_ing_m, 0),
        0);

    v_aves_acum     := v_aves_enc;
    v_aves_acum_h   := v_aves_enc_h;
    v_aves_acum_m   := v_aves_enc_m;
    v_peso_anterior := v_peso_ini;
    v_peso_ant_h    := NULLIF(v_peso_ini, 0);   -- peso_inicial_h como base hembras (NULL si 0)
    v_peso_ant_m    := NULL;                     -- no hay peso_inicial_m ⇒ arranca NULL

    -- Semana de cada registro (calendario local Bogotá). real_sem = semana real
    -- (sin clamp inferior: el guard de encaset ya garantiza real_sem >= 1).
    -- LEAST(25,…) sólo topa por arriba filas de DATOS legítimos > 25 (no existen
    -- en levante); las filas de PURO traslado > 25 se EXCLUYEN (REQ-002f).
    DROP TABLE IF EXISTS _seg_sem;
    CREATE TEMP TABLE _seg_sem ON COMMIT DROP AS
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
            COALESCE(sl.consumo_kg_hembras,0) AS cons_kg_h_num,   -- numeric
            COALESCE(sl.consumo_kg_machos,0)  AS cons_kg_m_num,   -- numeric
            COALESCE(sl.traslado_salida_hembras,0) AS tras_sal_h,
            COALESCE(sl.traslado_salida_machos,0)  AS tras_sal_m,
            COALESCE(sl.traslado_ingreso_hembras,0) AS tras_ing_h,
            COALESCE(sl.traslado_ingreso_machos,0)  AS tras_ing_m,
            -- Venta de aves (2026-08-17): salen del lote igual que un traslado de salida, pero no
            -- llegan a ningún otro lote. Se usan los splits por sexo —no `venta_aves_cantidad`—
            -- porque el saldo también se lleva por sexo; es el mismo criterio de
            -- `fn_resumen_semanal_ra_pesadas_levante`, y el mixto se arma como h+m igual que
            -- mort/sel/err/traslados.
            COALESCE(sl.venta_aves_hembras,0)       AS venta_h,
            COALESCE(sl.venta_aves_machos,0)        AS venta_m,
            COALESCE(sl.peso_prom_hembras,0)  AS ph,
            COALESCE(sl.peso_prom_machos,0)   AS pm,
            COALESCE(sl.uniformidad_hembras,0) AS uh,
            COALESCE(sl.uniformidad_machos,0)  AS um,
            sl.id
          FROM seguimiento_diario_levante sl
         WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
    )
    SELECT
        LEAST(25, real_sem)                       AS sem,
        reg_date,
        (mort_h + mort_m)                         AS mort,
        (sel_h + sel_m)                           AS sel,
        (cons_kg_h_num + cons_kg_m_num)           AS cons_kg,   -- numeric (idéntico al original)
        (err_h + err_m)                           AS err,
        (tras_sal_h + tras_sal_m)                 AS tras_sal,
        (tras_ing_h + tras_ing_m)                 AS tras_ing,
        (venta_h + venta_m)                       AS venta,
        mort_h, mort_m, sel_h, sel_m, err_h, err_m,
        cons_kg_h_num::double precision           AS cons_kg_h,
        cons_kg_m_num::double precision           AS cons_kg_m,
        tras_sal_h, tras_sal_m, tras_ing_h, tras_ing_m,
        venta_h, venta_m,
        ph, pm, uh, um, id
      FROM base
     WHERE NOT (
            real_sem > 25
        AND mort_h = 0 AND mort_m = 0 AND sel_h = 0 AND sel_m = 0
        AND err_h = 0 AND err_m = 0
        AND cons_kg_h_num = 0 AND cons_kg_m_num = 0
        AND ph = 0 AND pm = 0
        -- Una fila que trae VENTA no es «puro traslado»: descartarla perdería esas aves, que es el
        -- defecto que este cambio viene a cerrar. El mismo término se agrega al predicado gemelo de
        -- `v_first_ing_*` — los dos tienen que seguir siendo idénticos o las aves cuentan dos veces.
        AND venta_h = 0 AND venta_m = 0
        AND (tras_sal_h + tras_sal_m + tras_ing_h + tras_ing_m) > 0
     );

    SELECT MAX(sem) INTO v_max_sem FROM _seg_sem;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    FOR s IN 1..v_max_sem LOOP
        -- ¿la semana tiene registros? (el front solo itera semanas presentes)
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem WHERE sem = s);

        SELECT COALESCE(SUM(mort),0), COALESCE(SUM(sel),0), COALESCE(SUM(cons_kg),0),
               COALESCE(SUM(err),0), COALESCE(SUM(tras_sal),0), COALESCE(SUM(tras_ing),0), COUNT(DISTINCT reg_date)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0),
               COALESCE(SUM(sel_h),0),  COALESCE(SUM(sel_m),0),
               COALESCE(SUM(err_h),0),  COALESCE(SUM(err_m),0),
               COALESCE(SUM(cons_kg_h),0), COALESCE(SUM(cons_kg_m),0),
               COALESCE(SUM(tras_sal_h),0), COALESCE(SUM(tras_sal_m),0),
               COALESCE(SUM(tras_ing_h),0), COALESCE(SUM(tras_ing_m),0),
               COALESCE(SUM(venta),0), COALESCE(SUM(venta_h),0), COALESCE(SUM(venta_m),0)
          INTO r_mort_tot, r_sel_tot, r_cons_kg, r_err_tot, r_tras_sal, r_tras_ing, r_dias,
               r_mort_h, r_mort_m, r_sel_h, r_sel_m, r_err_h, r_err_m,
               r_cons_kg_h, r_cons_kg_m, r_tras_sal_h, r_tras_sal_m, r_tras_ing_h, r_tras_ing_m,
               r_venta_tot, r_venta_h, r_venta_m
          FROM _seg_sem WHERE sem = s;

        -- Saldo físico Feature-13: salidas = mort + sel + err + traslado_salida + VENTA - traslado_ingreso.
        --
        -- ⭐ 2026-08-17: la VENTA entró acá. Antes esta fn era el único lector del saldo de levante
        -- que no la descontaba, así que el mismo lote y la misma semana mostraban dos conteos según
        -- la pantalla (lote 143 sem 24: 10.619 acá contra 10.329 en `fn_reporte_semanal_levante_extras`,
        -- diferencia = la venta acumulada). Una ave vendida sale del lote: no contarla infla el saldo
        -- y, en cascada, subestima el consumo por ave — el mismo mecanismo por el que en su momento
        -- hubo que sumar el error de sexaje. La especificación ejecutable es
        -- `SaldoAvesLevanteCalculos.BajasNetas`, que ya la incluía.
        r_aves_fin := v_aves_acum - r_mort_tot - r_sel_tot - r_err_tot - r_tras_sal - r_venta_tot + r_tras_ing;
        -- Saldo por género (REQ-002e). Por sexo se usan los splits dedicados, no `venta_aves_cantidad`.
        r_aves_fin_h := v_aves_acum_h - r_mort_h - r_sel_h - r_err_h - r_tras_sal_h - r_venta_h + r_tras_ing_h;
        r_aves_fin_m := v_aves_acum_m - r_mort_m - r_sel_m - r_err_m - r_tras_sal_m - r_venta_m + r_tras_ing_m;

        -- Pesaje: último registro (por fecha, luego id) de la semana con peso>0.
        SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
          FROM _seg_sem
         WHERE sem = s AND (ph > 0 OR pm > 0)
         ORDER BY reg_date DESC, id DESC LIMIT 1;
        IF NOT FOUND THEN
            SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
              FROM _seg_sem WHERE sem = s ORDER BY reg_date DESC, id DESC LIMIT 1;
        END IF;
        r_pH := COALESCE(r_pH,0); r_pM := COALESCE(r_pM,0);
        r_uH := COALESCE(r_uH,0); r_uM := COALESCE(r_uM,0);

        r_peso_prom := CASE WHEN r_pH > 0 AND r_pM > 0 THEN (r_pH + r_pM)/2
                            WHEN r_pH > 0 THEN r_pH ELSE r_pM END;
        IF r_peso_prom <= 0 THEN r_peso_prom := COALESCE(v_peso_anterior,0); END IF;
        r_unif_real := CASE WHEN r_uH > 0 AND r_uM > 0 THEN (r_uH + r_uM)/2
                            WHEN r_uH > 0 THEN r_uH ELSE r_uM END;

        -- Peso por sexo (REQ-010b): valor del pesaje del sexo; arrastre del último conocido
        -- cuando la semana no tiene pesaje del sexo (mismo criterio que el peso mixto, que
        -- también arrastra). NULL si nunca hubo pesaje del sexo (p.ej. machos sin pesaje ⇒
        -- serie vacía en el chart, degrada con spanGaps).
        r_peso_h := CASE WHEN r_pH > 0 THEN r_pH ELSE v_peso_ant_h END;
        r_peso_m := CASE WHEN r_pM > 0 THEN r_pM ELSE v_peso_ant_m END;

        r_cons_g    := r_cons_kg * 1000;
        r_aves_prom := (v_aves_acum + r_aves_fin)/2;
        r_cons_dia  := CASE WHEN r_aves_prom > 0 AND r_dias > 0 THEN r_cons_g/(r_aves_prom*r_dias) ELSE 0 END;

        -- Consumo real por sexo (g/ave/día): consumo_kg_sexo*1000 / saldo_prom_sexo / días.
        r_aves_prom_h := (v_aves_acum_h + r_aves_fin_h)/2;
        r_aves_prom_m := (v_aves_acum_m + r_aves_fin_m)/2;
        r_cons_dia_h  := CASE WHEN r_aves_prom_h > 0 AND r_dias > 0
                              THEN (r_cons_kg_h*1000)/(r_aves_prom_h*r_dias) ELSE NULL END;
        r_cons_dia_m  := CASE WHEN r_aves_prom_m > 0 AND r_dias > 0
                              THEN (r_cons_kg_m*1000)/(r_aves_prom_m*r_dias) ELSE NULL END;

        -- Guía real para la semana. Mixto (compat) + por sexo SIN promediar (REQ-002e).
        --
        -- 🔴 EL PROMEDIO MIXTO NO SE PUEDE APLICAR A UNA GUÍA DE SOLO HEMBRAS.
        -- Las tres expresiones mixtas hacen COALESCE de cada término y dividen por 2 FIJO.
        -- Con la guía reducida —que trae hembras y NO machos— eso da (95.00 + 0)/2 = 47,5
        -- donde el cliente dice 95,00: no es NULL, no es 0, no revienta. Es un número
        -- plausible y equivocado por un factor de 2, que nadie detecta mirando la pantalla.
        -- Por eso el promedio se aplica SOLO cuando la fila viene de la guía compartida;
        -- para la propia se usa el valor de hembras tal cual, que es el único que existe.
        -- La rama 'compartida' es LITERALMENTE la expresión de siempre ⇒ delta cero por
        -- construcción para Sanmarino, Demo, Ecuador y Panamá, no «verificado después».
        SELECT CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.gr_ave_dia_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,0))/2 END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.peso_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.peso_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.peso_m),'')::double precision,0))/2 END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.uniformidad),'')::double precision
                    ELSE COALESCE(NULLIF(btrim(g.uniformidad),'')::double precision,0) END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.mort_sem_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.mort_sem_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.mort_sem_m),'')::double precision,0))/2 END,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision,
               g.origen
          INTO r_cons_tabla, r_peso_tabla, r_unif_tabla, r_mort_tabla, r_cons_tabla_h, r_cons_tabla_m,
               r_peso_tabla_h, r_peso_tabla_m, r_mort_tabla_h, r_mort_tabla_m, v_origen_guia
          FROM vw_guia_genetica_postura g
         WHERE g.company_id = v_company
           -- ⚠️ La comparacion de raza de la rama COMPARTIDA queda EXACTA y case-sensitive, como
           -- siempre: aflojarla haria matchear filas que hoy no matchean para Sanmarino, Demo,
           -- Ecuador y Panama, o sea el refactor cambiaria resultados por si solo. La rama PROPIA
           -- —inalcanzable para esas cuatro— si compara normalizado, porque produccion ya lo hace
           -- y tenerlo de un solo lado era la causa medida de que `CRIOLLA` cruzara en produccion
           -- y no en levante (30-ago-2026). La grafia del ERP la resuelve la vista, con su alias.
           AND (CASE WHEN g.origen = 'propia'
                     THEN btrim(lower(g.raza)) = btrim(lower(v_raza))
                     ELSE g.raza = v_raza END)
           AND g.anio_guia = v_anio
           AND btrim(g.edad) = s::text
         LIMIT 1;
        -- El COALESCE a 0 también es exclusivo de la guía compartida: ahí la columna existe en
        -- toda la curva y el 0 se lee como «la guía dice 0». En la propia la métrica NO EXISTE
        -- (no trae peso, ni uniformidad, ni mortalidad semanal — su retiro_ac_h es ACUMULADO),
        -- y un 0 ahí se leería como un objetivo real. NULL es la única lectura honesta, y el
        -- front ya lo sabe pintar: las series por sexo llegan NULL desde siempre.
        -- `AND NOT v_guia_propia_empresa`: sin eso, una semana SIN fila de guia (v_origen_guia
        -- NULL) caia igual en el COALESCE. Para una empresa con guia propia eso pinta 0,00 en las
        -- cuatro columnas de guia —un objetivo inventado— justo donde su guia no llega: la de
        -- Santa Reyes arranca en la semana 18 y el levante empieza en la 1. Medido el 30-ago-2026.
        -- Para las cuatro empresas sin guia propia la condicion nueva es siempre TRUE ⇒ la misma
        -- expresion de hoy, incluido el 0 legitimo cuando la guia compartida trae la columna vacia.
        IF v_origen_guia IS DISTINCT FROM 'propia' AND NOT v_guia_propia_empresa THEN
            r_cons_tabla := COALESCE(r_cons_tabla,0);
            r_peso_tabla := COALESCE(r_peso_tabla,0);
            r_unif_tabla := COALESCE(r_unif_tabla,0);
            r_mort_tabla := COALESCE(r_mort_tabla,0);
        END IF;
        -- r_cons_tabla_h/_m, r_peso_tabla_h/_m, r_mort_tabla_h/_m se dejan NULL si la guía
        -- no trae el dato del sexo (series de guía degradan a NULL, sin promediar).

        r_gan_sem   := r_peso_prom - v_peso_anterior;
        r_cons_ave  := CASE WHEN r_aves_prom > 0 THEN r_cons_g/r_aves_prom ELSE 0 END;
        r_conv      := CASE WHEN r_gan_sem > 0 THEN r_cons_ave/r_gan_sem ELSE 0 END;
        r_gan_dia_ac := r_gan_sem/7;
        r_gan_tabla := CASE WHEN r_peso_tabla > 0 AND v_peso_tabla_ant > 0 THEN r_peso_tabla - v_peso_tabla_ant ELSE 0 END;

        r_mort_sem  := CASE WHEN v_aves_acum > 0 THEN (r_mort_tot/v_aves_acum)*100 ELSE 0 END;
        r_sel_sem   := CASE WHEN v_aves_acum > 0 THEN (r_sel_tot/v_aves_acum)*100 ELSE 0 END;
        r_err_sem   := CASE WHEN v_aves_acum > 0 THEN (r_err_tot/v_aves_acum)*100 ELSE 0 END;
        r_mort_mas_sel := r_mort_sem + r_sel_sem;

        -- REQ-010b: mortalidad y retiro POR SEXO. Mismo denominador que el mixto (aves al inicio
        -- de la semana del sexo). El retiro replica el mixto retiroSem = mort+sel+errSex del sexo.
        -- NULL (no 0 sintético) cuando el sexo no tiene saldo ⇒ la serie degrada con spanGaps.
        r_mort_pct_h   := CASE WHEN v_aves_acum_h > 0 THEN (r_mort_h / v_aves_acum_h) * 100 ELSE NULL END;
        r_mort_pct_m   := CASE WHEN v_aves_acum_m > 0 THEN (r_mort_m / v_aves_acum_m) * 100 ELSE NULL END;
        r_retiro_pct_h := CASE WHEN v_aves_acum_h > 0 THEN ((r_mort_h + r_sel_h + r_err_h) / v_aves_acum_h) * 100 ELSE NULL END;
        r_retiro_pct_m := CASE WHEN v_aves_acum_m > 0 THEN ((r_mort_m + r_sel_m + r_err_m) / v_aves_acum_m) * 100 ELSE NULL END;

        r_efic   := CASE WHEN r_cons_ave > 0 THEN r_gan_sem/r_cons_ave ELSE 0 END;
        r_superv := CASE WHEN v_aves_acum > 0 THEN r_aves_fin/v_aves_acum ELSE 0 END;
        r_ip     := r_efic * r_superv;

        -- REQ-002f: acumulados reales = bajas_acumuladas / aves_encasetadas * 100.
        v_mort_bajas_acum := v_mort_bajas_acum + r_mort_tot;
        v_sel_bajas_acum  := v_sel_bajas_acum + r_sel_tot;

        semana                        := s;
        aves_inicio_semana            := v_aves_acum;
        aves_fin_semana               := r_aves_fin;
        consumo_diario                := r_cons_dia;
        consumo_tabla                 := r_cons_tabla;
        consumo_total_semana          := r_cons_g;
        conversion_alimenticia        := r_conv;
        peso_tabla                    := r_peso_tabla;
        unif_real                     := r_unif_real;
        unif_tabla                    := r_unif_tabla;
        mort_tabla                    := r_mort_tabla;
        dif_peso_pct                  := CASE WHEN r_peso_tabla > 0 THEN ((r_peso_prom - r_peso_tabla)/r_peso_tabla)*100 ELSE 0 END;
        ganancia_semana               := r_gan_sem;
        ganancia_diaria_acumulada     := r_gan_dia_ac;
        ganancia_tabla                := r_gan_tabla;
        mortalidad_sem                := r_mort_sem;
        seleccion_sem                 := r_sel_sem;
        error_sexaje_sem              := r_err_sem;
        mortalidad_mas_seleccion      := r_mort_mas_sel;
        eficiencia                    := r_efic;
        ip                            := r_ip;
        vpi                           := r_ip;   -- front: vpi = supervivencia*eficiencia = ip
        saldo_aves_semanal            := r_aves_fin;
        mortalidad_acum               := CASE WHEN v_aves_enc > 0 THEN (v_mort_bajas_acum/v_aves_enc)*100 ELSE 0 END;
        seleccion_acum                := CASE WHEN v_aves_enc > 0 THEN (v_sel_bajas_acum/v_aves_enc)*100 ELSE 0 END;
        mortalidad_mas_seleccion_acum := CASE WHEN v_aves_enc > 0 THEN ((v_mort_bajas_acum + v_sel_bajas_acum)/v_aves_enc)*100 ELSE 0 END;
        piso_termico_visible          := false;  -- la guía no expone el flag; front daba false
        peso_inicial                  := v_peso_anterior;
        peso_cierre                   := r_peso_prom;
        dias_con_registro             := r_dias;
        consumo_diario_hembras        := r_cons_dia_h;
        consumo_diario_machos         := r_cons_dia_m;
        consumo_tabla_hembras         := r_cons_tabla_h;
        consumo_tabla_machos          := r_cons_tabla_m;
        peso_hembras                  := r_peso_h;
        peso_machos                   := r_peso_m;
        peso_tabla_hembras            := r_peso_tabla_h;
        peso_tabla_machos             := r_peso_tabla_m;
        mort_pct_hembras              := r_mort_pct_h;
        mort_pct_machos               := r_mort_pct_m;
        mort_tabla_hembras            := r_mort_tabla_h;
        mort_tabla_machos             := r_mort_tabla_m;
        retiro_pct_hembras            := r_retiro_pct_h;
        retiro_pct_machos             := r_retiro_pct_m;

        -- TK-2026-000022 — el resto de los parametros por sexo. Ninguno introduce aritmetica
        -- nueva: son las MISMAS variables con las que ya se arman las columnas mixtas, expuestas
        -- sin promediar. El criterio de NULL es el de las series por sexo de arriba: si el sexo no
        -- tiene saldo (o la semana no tuvo pesaje / la guia no trae el dato) va NULL, para que la
        -- pantalla muestre un guion en vez de un cero que se leeria como dato real.
        aves_inicio_hembras           := CASE WHEN v_aves_enc_h > 0 THEN v_aves_acum_h ELSE NULL END;
        aves_fin_hembras              := CASE WHEN v_aves_enc_h > 0 THEN r_aves_fin_h  ELSE NULL END;
        aves_inicio_machos            := CASE WHEN v_aves_enc_m > 0 THEN v_aves_acum_m ELSE NULL END;
        aves_fin_machos               := CASE WHEN v_aves_enc_m > 0 THEN r_aves_fin_m  ELSE NULL END;
        consumo_total_semana_hembras  := CASE WHEN v_aves_enc_h > 0 THEN r_cons_kg_h * 1000 ELSE NULL END;
        consumo_total_semana_machos   := CASE WHEN v_aves_enc_m > 0 THEN r_cons_kg_m * 1000 ELSE NULL END;
        -- Uniformidad: 0 significa "no hubo pesaje esta semana", no "0 % de uniformidad".
        unif_hembras                  := CASE WHEN r_uH > 0 THEN r_uH ELSE NULL END;
        unif_machos                   := CASE WHEN r_uM > 0 THEN r_uM ELSE NULL END;
        ganancia_hembras              := CASE WHEN r_peso_h IS NOT NULL AND v_peso_ant_h IS NOT NULL
                                              THEN r_peso_h - v_peso_ant_h ELSE NULL END;
        ganancia_machos               := CASE WHEN r_peso_m IS NOT NULL AND v_peso_ant_m IS NOT NULL
                                              THEN r_peso_m - v_peso_ant_m ELSE NULL END;
        dif_peso_pct_hembras          := CASE WHEN r_peso_tabla_h > 0 AND r_peso_h IS NOT NULL
                                              THEN ((r_peso_h - r_peso_tabla_h)/r_peso_tabla_h)*100 ELSE NULL END;
        dif_peso_pct_machos           := CASE WHEN r_peso_tabla_m > 0 AND r_peso_m IS NOT NULL
                                              THEN ((r_peso_m - r_peso_tabla_m)/r_peso_tabla_m)*100 ELSE NULL END;
        seleccion_pct_hembras         := CASE WHEN v_aves_acum_h > 0 THEN (r_sel_h / v_aves_acum_h) * 100 ELSE NULL END;
        seleccion_pct_machos          := CASE WHEN v_aves_acum_m > 0 THEN (r_sel_m / v_aves_acum_m) * 100 ELSE NULL END;
        error_sexaje_pct_hembras      := CASE WHEN v_aves_acum_h > 0 THEN (r_err_h / v_aves_acum_h) * 100 ELSE NULL END;
        error_sexaje_pct_machos       := CASE WHEN v_aves_acum_m > 0 THEN (r_err_m / v_aves_acum_m) * 100 ELSE NULL END;

        RETURN NEXT;

        -- avanzar acumuladores (idéntico al front) + saldo por género.
        v_aves_acum      := r_aves_fin;
        v_aves_acum_h    := r_aves_fin_h;
        v_aves_acum_m    := r_aves_fin_m;
        v_peso_anterior  := r_peso_prom;
        v_peso_tabla_ant := r_peso_tabla;
        v_peso_ant_h     := r_peso_h;   -- arrastre peso por sexo (REQ-010b)
        v_peso_ant_m     := r_peso_m;
    END LOOP;

    RETURN;
END;
$$;
""";

        private const string FnIndicadoresLevantePrev = """
-- ============================================================================
-- fn_indicadores_levante_postura(lote_id)
-- Indicadores semanales de LEVANTE (postura Colombia) calculados en la BD.
-- Reemplaza el cómputo del front (lote-levante/tabla-lista-indicadores +
-- graficas-principal): el front solo debe pintar.
--
-- Replica EXACTO el algoritmo del front (double precision, mismo orden) e
-- incorpora las correcciones ya acordadas:
--   * Peso/uniformidad del PESAJE semanal: último registro de la semana con
--     peso>0 (no el último día, que suele venir en 0) + arrastre del último
--     peso conocido cuando la semana no tiene pesaje (evita ganancia negativa
--     y dif -100%).  [bug histórico corregido]
--   * Guía genética REAL desde guia_genetica_sanmarino_colombia por
--     raza + año + company + semana (no valores hardcodeados / no Ecuador).
--
-- Correcciones matriz Verenice rev 6-jul-26:
--   * REQ-002e — Consumo por sexo: además del consumo mixto (compatibilidad),
--     se exponen consumo_diario_hembras / consumo_diario_machos (g/ave/día reales
--     por sexo = consumo_kg_sexo*1000 / saldo_prom_sexo / días) y
--     consumo_tabla_hembras / consumo_tabla_machos (gr_ave_dia_h/_m de la guía, SIN
--     promediar). Requiere llevar el saldo de aves POR GÉNERO dentro de la fn.
--     (Columnas renombradas de _h/_m a _hembras/_machos por el mapeo EF, ver nota abajo.)
--   * REQ-002f — Acumulados reales: mortalidad/selección acumuladas =
--     bajas_acumuladas / aves_encasetadas * 100 (acumulado real sobre aves
--     iniciales), no la suma de % semanales sobre base decreciente.
--   * REQ-002f/B36 — Semana fantasma: se EXCLUYEN las filas de PURO traslado
--     (sin mortalidad/selección/error/consumo/pesaje) posteriores a la
--     semana 25; ya no se clampean con LEAST(25) generando una "semana 25"
--     falsa con el salto de saldo del traslado post-levante.
--   * REQ-002B36 — Defensas:
--       - Base de aves con fallback: COALESCE(aves_encasetadas,
--         hembras_l+machos_l, primer traslado_ingreso, 0).
--       - Encaset futuro/ausente: si fecha_encaset es NULL o es POSTERIOR al
--         primer registro (encaset tecleado a futuro, p. ej. lote 116), se
--         devuelven CERO filas en lugar de colapsar 140+ días en una
--         "semana 1" absurda con base 0 y %pérdidas 100%. Se eligió devolver
--         cero filas (y no "usar el primer registro como referencia") porque
--         con un encaset inconsistente NINGÚN indicador es confiable: es más
--         seguro que el front muestre su empty-state a mostrar cifras
--         engañosas. Al devolver cero filas ya no hace falta GREATEST(1,…)
--         (no quedan semanas negativas que clampear).
--       - Idempotencia intra-transacción: DROP TABLE IF EXISTS _seg_sem antes
--         del CREATE TEMP TABLE (permite llamar la fn 2+ veces en la misma
--         transacción sin 'relation _seg_sem already exists').
--
-- Fuente de verdad del algoritmo: tabla-lista-indicadores.component.ts
-- Zona horaria: America/Bogota para el corte de semanas (calendario local).
--
-- Fase 3 (convergencia levante a Feature-13): lee la tabla CANÓNICA
-- seguimiento_diario_levante (tipo_seguimiento='levante') y las
-- salidas de la semana incluyen error de sexaje y traslados dedicados:
--   out = mort + sel + err + traslado_salida - traslado_ingreso;  aves_fin = aves - out.
-- ============================================================================
--   * REQ-010b — Series POR SEXO para el selector Hembras/Machos/Ambos de la
--     pestaña Gráfica: además del consumo por sexo, se exponen peso (real +
--     guía), mortalidad % (real + guía) y retiro % (real; la guía por sexo no
--     existe ⇒ NULL) por sexo, para que el control cambie las series Real/Guía.
--     Aritmética por sexo consistente con la mixta (mismo denominador = aves al
--     inicio de la semana del sexo; NULL cuando el sexo no tiene saldo/pesaje).
--
--   * TK-2026-000022 — TODOS los parametros por sexo en la TABLA de indicadores.
--     El usuario reporto que «los parametros aparecen solo para un grupo de aves y
--     no identifica si se refieren a hembras o machos». Peor: varias columnas
--     mixtas son un PROMEDIO ARITMETICO simple de los dos sexos (peso_cierre y
--     unif_real: (H+M)/2, sin ponderar por cantidad de aves), o sea un valor que
--     no le corresponde a ninguna ave del galpon —en reproductoras la hembra y el
--     macho tienen pesos muy distintos—. Se exponen aves inicio/fin, consumo total,
--     uniformidad, ganancia, dif % de peso vs guia, seleccion % y error de sexaje %
--     por sexo. NO se agrega aritmetica nueva: son las mismas variables internas
--     con las que ya se arman las columnas mixtas, publicadas sin promediar.
--
-- IMPORTANTE (mapeo EF): los nombres de las columnas por sexo son el snake_case
-- EXACTO de las props del DTO (…Hembras→…_hembras, …Machos→…_machos). EF Core
-- (SqlQueryRaw<IndicadorSemanalLevanteDto> con convención snake_case) mapea
-- ConsumoDiarioHembras↔consumo_diario_hembras, PesoHembras↔peso_hembras, etc.
-- Un nombre abreviado (_h/_m) NO mapearía a props …Hembras/…Machos (mismo patrón
-- probado en fn_indicadores_produccion_postura: porcentaje_mortalidad_hembras…).
-- Por eso las columnas de consumo por sexo se renombran de _h/_m a _hembras/_machos.
--
-- DROP previo: la firma cambió (se renombraron/agregaron columnas OUT por sexo),
-- y CREATE OR REPLACE no puede alterar el tipo de retorno.
DROP FUNCTION IF EXISTS fn_indicadores_levante_postura(integer);
CREATE OR REPLACE FUNCTION fn_indicadores_levante_postura(p_lote_id integer)
RETURNS TABLE(
    semana                          integer,
    aves_inicio_semana              double precision,
    aves_fin_semana                 double precision,
    consumo_diario                  double precision,   -- g/ave/día real (mixto H+M)
    consumo_tabla                   double precision,   -- g/ave/día guía (promedio H,M)
    consumo_total_semana            double precision,   -- gramos
    conversion_alimenticia          double precision,
    peso_tabla                      double precision,
    unif_real                       double precision,
    unif_tabla                      double precision,
    mort_tabla                      double precision,
    dif_peso_pct                    double precision,
    ganancia_semana                 double precision,
    ganancia_diaria_acumulada       double precision,
    ganancia_tabla                  double precision,
    mortalidad_sem                  double precision,
    seleccion_sem                   double precision,
    error_sexaje_sem                double precision,
    mortalidad_mas_seleccion        double precision,
    eficiencia                      double precision,
    ip                              double precision,
    vpi                             double precision,
    saldo_aves_semanal              double precision,
    mortalidad_acum                 double precision,
    seleccion_acum                  double precision,
    mortalidad_mas_seleccion_acum   double precision,
    piso_termico_visible            boolean,
    peso_inicial                    double precision,
    peso_cierre                     double precision,
    dias_con_registro               integer,
    -- REQ-002e / REQ-010b: series POR SEXO (reales y guía SIN promediar). numeric → decimal? en el DTO.
    -- Nombres = snake_case EXACTO de las props del DTO para que EF las mapee (ver nota de cabecera).
    consumo_diario_hembras          numeric,            -- g/ave/día real hembras
    consumo_diario_machos           numeric,            -- g/ave/día real machos
    consumo_tabla_hembras           numeric,            -- gr_ave_dia_h de la guía
    consumo_tabla_machos            numeric,            -- gr_ave_dia_m de la guía
    peso_hembras                    numeric,            -- peso prom hembras (arrastre si semana sin pesaje)
    peso_machos                     numeric,            -- peso prom machos  (arrastre si semana sin pesaje)
    peso_tabla_hembras              numeric,            -- guía peso_h
    peso_tabla_machos               numeric,            -- guía peso_m
    mort_pct_hembras                numeric,            -- % mort semana hembras = mort_h / aves_inicio_h * 100
    mort_pct_machos                 numeric,            -- % mort semana machos  = mort_m / aves_inicio_m * 100
    mort_tabla_hembras              numeric,            -- guía mort_sem_h
    mort_tabla_machos               numeric,            -- guía mort_sem_m
    retiro_pct_hembras              numeric,            -- % retiro hembras = (mort+sel+err)_h / aves_inicio_h * 100
    retiro_pct_machos               numeric,            -- % retiro machos  = (mort+sel+err)_m / aves_inicio_m * 100
    -- TK-2026-000022: el resto de los parametros POR SEXO. La tabla de indicadores mostraba una
    -- sola serie sin decir de que sexo era —y varias de esas columnas mixtas son un PROMEDIO
    -- ARITMETICO de hembras y machos (peso, uniformidad), o sea un numero que no le corresponde a
    -- ninguna ave real. Todo esto ya se calculaba dentro de la funcion; solo faltaba exponerlo.
    -- Convencion identica a las de arriba: NULL cuando el sexo no existe en el lote o no hay dato,
    -- nunca 0 sintetico.
    aves_inicio_hembras             numeric,            -- saldo hembras al inicio de la semana
    aves_fin_hembras                numeric,            -- saldo hembras al cierre de la semana
    aves_inicio_machos              numeric,            -- saldo machos al inicio de la semana
    aves_fin_machos                 numeric,            -- saldo machos al cierre de la semana
    consumo_total_semana_hembras    numeric,            -- gramos consumidos por las hembras en la semana
    consumo_total_semana_machos     numeric,            -- gramos consumidos por los machos en la semana
    unif_hembras                    numeric,            -- % uniformidad hembras del pesaje de la semana
    unif_machos                     numeric,            -- % uniformidad machos  del pesaje de la semana
    ganancia_hembras                numeric,            -- g ganados por las hembras respecto de la semana previa
    ganancia_machos                 numeric,            -- g ganados por los machos  respecto de la semana previa
    dif_peso_pct_hembras            numeric,            -- (peso_h - guia peso_h) / guia peso_h * 100
    dif_peso_pct_machos             numeric,            -- (peso_m - guia peso_m) / guia peso_m * 100
    seleccion_pct_hembras           numeric,            -- % seleccion semana hembras = sel_h / aves_inicio_h * 100
    seleccion_pct_machos            numeric,            -- % seleccion semana machos  = sel_m / aves_inicio_m * 100
    error_sexaje_pct_hembras        numeric,            -- % error sexaje hembras = err_h / aves_inicio_h * 100
    error_sexaje_pct_machos         numeric             -- % error sexaje machos  = err_m / aves_inicio_m * 100
)
LANGUAGE plpgsql VOLATILE AS $$
DECLARE
    v_raza        text;
    v_anio        text;
    v_company     integer;
    v_aves_enc_col integer;   -- lotes.aves_encasetadas (crudo)
    v_hembras_l   integer;    -- lotes.hembras_l (crudo)
    v_machos_l    integer;    -- lotes.machos_l (crudo)
    v_aves_enc    double precision;   -- base total resuelta (con fallback)
    v_aves_enc_h  double precision;   -- base hembras resuelta
    v_aves_enc_m  double precision;   -- base machos resuelta
    v_peso_ini    double precision;
    v_enc_date    date;
    v_min_reg     date;
    v_first_ing_h double precision;   -- primer traslado_ingreso (fallback base)
    v_first_ing_m double precision;

    -- acumuladores (mismos nombres que el front)
    v_aves_acum       double precision;
    v_aves_acum_h     double precision;
    v_aves_acum_m     double precision;
    v_mort_bajas_acum double precision := 0;   -- bajas acumuladas (unidades) REQ-002f
    v_sel_bajas_acum  double precision := 0;   -- selección acumulada (unidades) REQ-002f
    v_peso_anterior   double precision;
    v_peso_tabla_ant  double precision := 0;

    v_max_sem     integer;
    s             integer;

    -- por semana
    r_mort_tot    double precision;
    r_sel_tot     double precision;
    r_cons_kg     double precision;
    r_err_tot     double precision;
    r_tras_sal    double precision;
    r_tras_ing    double precision;
    r_venta_tot   double precision;   -- venta de aves: sale del lote y no llega a ningún otro
    r_dias        integer;
    r_aves_fin    double precision;
    -- por semana / por género
    r_mort_h      double precision;
    r_mort_m      double precision;
    r_sel_h       double precision;
    r_sel_m       double precision;
    r_err_h       double precision;
    r_err_m       double precision;
    r_cons_kg_h   double precision;
    r_cons_kg_m   double precision;
    r_tras_sal_h  double precision;
    r_tras_sal_m  double precision;
    r_tras_ing_h  double precision;
    r_tras_ing_m  double precision;
    r_venta_h     double precision;
    r_venta_m     double precision;
    r_aves_fin_h  double precision;
    r_aves_fin_m  double precision;
    r_aves_prom_h double precision;
    r_aves_prom_m double precision;
    r_cons_dia_h  double precision;
    r_cons_dia_m  double precision;
    r_cons_tabla_h double precision;
    r_cons_tabla_m double precision;
    -- REQ-010b: peso / mortalidad / retiro POR SEXO + guía por sexo.
    v_peso_ant_h   double precision;   -- arrastre peso hembras
    v_peso_ant_m   double precision;   -- arrastre peso machos
    r_peso_h       double precision;
    r_peso_m       double precision;
    r_peso_tabla_h double precision;
    r_peso_tabla_m double precision;
    r_mort_tabla_h double precision;
    r_mort_tabla_m double precision;
    -- De que tabla salio la fila de guia: 'compartida' (guia_genetica_sanmarino_colombia,
    -- >40 columnas) o 'propia' (guia_genetica_santa_reyes, 3 metricas y solo hembras).
    -- Ver el bloque de la guia mas abajo: gobierna si se coalescea a 0 o se deja NULL.
    v_origen_guia  text;
    -- ¿La EMPRESA del lote tiene guia propia (tabla reducida)? Distinto de v_origen_guia, que
    -- dice de donde salio LA FILA de esta semana y queda NULL cuando no hubo ninguna. Se necesita
    -- separado para el caso «empresa con guia propia + semana sin fila»: ahi un 0 seria un
    -- objetivo inventado (su guia arranca en la semana 18 y no cubre todo el levante).
    v_guia_propia_empresa boolean := false;
    r_mort_pct_h   double precision;
    r_mort_pct_m   double precision;
    r_retiro_pct_h double precision;
    r_retiro_pct_m double precision;

    r_pH          double precision;
    r_pM          double precision;
    r_peso_prom   double precision;
    r_uH          double precision;
    r_uM          double precision;
    r_unif_real   double precision;
    r_cons_g      double precision;
    r_aves_prom   double precision;
    r_cons_dia    double precision;
    r_cons_tabla  double precision;
    r_peso_tabla  double precision;
    r_unif_tabla  double precision;
    r_mort_tabla  double precision;
    r_gan_sem     double precision;
    r_cons_ave    double precision;
    r_conv        double precision;
    r_gan_dia_ac  double precision;
    r_gan_tabla   double precision;
    r_mort_sem    double precision;
    r_sel_sem     double precision;
    r_err_sem     double precision;
    r_mort_mas_sel double precision;
    r_efic        double precision;
    r_superv      double precision;
    r_ip          double precision;
BEGIN
    SELECT l.raza, l.ano_tabla_genetica::text, l.company_id,
           l.aves_encasetadas, l.hembras_l, l.machos_l,
           COALESCE(l.peso_inicial_h,0)::double precision,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date
      INTO v_raza, v_anio, v_company, v_aves_enc_col, v_hembras_l, v_machos_l, v_peso_ini, v_enc_date
      FROM lotes l
     WHERE l.lote_id = p_lote_id AND l.deleted_at IS NULL;

    IF NOT FOUND THEN RETURN; END IF;

    -- Una sola vez por lote: ¿esta empresa tiene guia propia? Gobierna el COALESCE a 0 de mas
    -- abajo. Para las cuatro empresas que leen la guia compartida da FALSE, y la expresion que
    -- se ejecuta queda identica a la de siempre.
    SELECT EXISTS (SELECT 1 FROM guia_genetica_santa_reyes gp
                    WHERE gp.company_id = v_company AND gp.deleted_at IS NULL)
      INTO v_guia_propia_empresa;

    -- Aves entradas por traslado en filas que el armado de la serie DESCARTA (puro traslado
    -- > sem 25): fallback de base cuando el lote se pobló por traslado y no trae
    -- aves_encasetadas / hembras_l / machos_l. Nadie más suma esas aves — la ventana las tira.
    --
    -- ⚠️ El predicado debe ser el MISMO que el WHERE NOT (...) del armado de la serie. Si acá
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

    -- Primer registro (calendario Bogotá) para validar el encaset.
    SELECT MIN((sl.fecha AT TIME ZONE 'America/Bogota')::date)
      INTO v_min_reg
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text;

    IF v_min_reg IS NULL THEN RETURN; END IF;   -- sin registros

    -- REQ-002B36: encaset ausente o POSTERIOR al primer registro (futuro) ⇒
    -- datos inconsistentes ⇒ cero filas (el front muestra su empty-state).
    IF v_enc_date IS NULL OR v_enc_date > v_min_reg THEN RETURN; END IF;

    -- Base de aves con fallback (REQ-002B36).
    v_aves_enc := COALESCE(
        NULLIF(v_aves_enc_col, 0)::double precision,
        NULLIF(COALESCE(v_hembras_l,0) + COALESCE(v_machos_l,0), 0)::double precision,
        NULLIF(v_first_ing_h + v_first_ing_m, 0),
        0);
    v_aves_enc_h := COALESCE(
        NULLIF(v_hembras_l, 0)::double precision,
        NULLIF(v_first_ing_h, 0),
        0);
    v_aves_enc_m := COALESCE(
        NULLIF(v_machos_l, 0)::double precision,
        NULLIF(v_first_ing_m, 0),
        0);

    v_aves_acum     := v_aves_enc;
    v_aves_acum_h   := v_aves_enc_h;
    v_aves_acum_m   := v_aves_enc_m;
    v_peso_anterior := v_peso_ini;
    v_peso_ant_h    := NULLIF(v_peso_ini, 0);   -- peso_inicial_h como base hembras (NULL si 0)
    v_peso_ant_m    := NULL;                     -- no hay peso_inicial_m ⇒ arranca NULL

    -- Semana de cada registro (calendario local Bogotá). real_sem = semana real
    -- (sin clamp inferior: el guard de encaset ya garantiza real_sem >= 1).
    -- LEAST(25,…) sólo topa por arriba filas de DATOS legítimos > 25 (no existen
    -- en levante); las filas de PURO traslado > 25 se EXCLUYEN (REQ-002f).
    DROP TABLE IF EXISTS _seg_sem;
    CREATE TEMP TABLE _seg_sem ON COMMIT DROP AS
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
            COALESCE(sl.consumo_kg_hembras,0) AS cons_kg_h_num,   -- numeric
            COALESCE(sl.consumo_kg_machos,0)  AS cons_kg_m_num,   -- numeric
            COALESCE(sl.traslado_salida_hembras,0) AS tras_sal_h,
            COALESCE(sl.traslado_salida_machos,0)  AS tras_sal_m,
            COALESCE(sl.traslado_ingreso_hembras,0) AS tras_ing_h,
            COALESCE(sl.traslado_ingreso_machos,0)  AS tras_ing_m,
            -- Venta de aves (2026-08-17): salen del lote igual que un traslado de salida, pero no
            -- llegan a ningún otro lote. Se usan los splits por sexo —no `venta_aves_cantidad`—
            -- porque el saldo también se lleva por sexo; es el mismo criterio de
            -- `fn_resumen_semanal_ra_pesadas_levante`, y el mixto se arma como h+m igual que
            -- mort/sel/err/traslados.
            COALESCE(sl.venta_aves_hembras,0)       AS venta_h,
            COALESCE(sl.venta_aves_machos,0)        AS venta_m,
            COALESCE(sl.peso_prom_hembras,0)  AS ph,
            COALESCE(sl.peso_prom_machos,0)   AS pm,
            COALESCE(sl.uniformidad_hembras,0) AS uh,
            COALESCE(sl.uniformidad_machos,0)  AS um,
            sl.id
          FROM seguimiento_diario_levante sl
         WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
    )
    SELECT
        LEAST(25, real_sem)                       AS sem,
        reg_date,
        (mort_h + mort_m)                         AS mort,
        (sel_h + sel_m)                           AS sel,
        (cons_kg_h_num + cons_kg_m_num)           AS cons_kg,   -- numeric (idéntico al original)
        (err_h + err_m)                           AS err,
        (tras_sal_h + tras_sal_m)                 AS tras_sal,
        (tras_ing_h + tras_ing_m)                 AS tras_ing,
        (venta_h + venta_m)                       AS venta,
        mort_h, mort_m, sel_h, sel_m, err_h, err_m,
        cons_kg_h_num::double precision           AS cons_kg_h,
        cons_kg_m_num::double precision           AS cons_kg_m,
        tras_sal_h, tras_sal_m, tras_ing_h, tras_ing_m,
        venta_h, venta_m,
        ph, pm, uh, um, id
      FROM base
     WHERE NOT (
            real_sem > 25
        AND mort_h = 0 AND mort_m = 0 AND sel_h = 0 AND sel_m = 0
        AND err_h = 0 AND err_m = 0
        AND cons_kg_h_num = 0 AND cons_kg_m_num = 0
        AND ph = 0 AND pm = 0
        -- Una fila que trae VENTA no es «puro traslado»: descartarla perdería esas aves, que es el
        -- defecto que este cambio viene a cerrar. El mismo término se agrega al predicado gemelo de
        -- `v_first_ing_*` — los dos tienen que seguir siendo idénticos o las aves cuentan dos veces.
        AND venta_h = 0 AND venta_m = 0
        AND (tras_sal_h + tras_sal_m + tras_ing_h + tras_ing_m) > 0
     );

    SELECT MAX(sem) INTO v_max_sem FROM _seg_sem;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    FOR s IN 1..v_max_sem LOOP
        -- ¿la semana tiene registros? (el front solo itera semanas presentes)
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem WHERE sem = s);

        SELECT COALESCE(SUM(mort),0), COALESCE(SUM(sel),0), COALESCE(SUM(cons_kg),0),
               COALESCE(SUM(err),0), COALESCE(SUM(tras_sal),0), COALESCE(SUM(tras_ing),0), COUNT(*)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0),
               COALESCE(SUM(sel_h),0),  COALESCE(SUM(sel_m),0),
               COALESCE(SUM(err_h),0),  COALESCE(SUM(err_m),0),
               COALESCE(SUM(cons_kg_h),0), COALESCE(SUM(cons_kg_m),0),
               COALESCE(SUM(tras_sal_h),0), COALESCE(SUM(tras_sal_m),0),
               COALESCE(SUM(tras_ing_h),0), COALESCE(SUM(tras_ing_m),0),
               COALESCE(SUM(venta),0), COALESCE(SUM(venta_h),0), COALESCE(SUM(venta_m),0)
          INTO r_mort_tot, r_sel_tot, r_cons_kg, r_err_tot, r_tras_sal, r_tras_ing, r_dias,
               r_mort_h, r_mort_m, r_sel_h, r_sel_m, r_err_h, r_err_m,
               r_cons_kg_h, r_cons_kg_m, r_tras_sal_h, r_tras_sal_m, r_tras_ing_h, r_tras_ing_m,
               r_venta_tot, r_venta_h, r_venta_m
          FROM _seg_sem WHERE sem = s;

        -- Saldo físico Feature-13: salidas = mort + sel + err + traslado_salida + VENTA - traslado_ingreso.
        --
        -- ⭐ 2026-08-17: la VENTA entró acá. Antes esta fn era el único lector del saldo de levante
        -- que no la descontaba, así que el mismo lote y la misma semana mostraban dos conteos según
        -- la pantalla (lote 143 sem 24: 10.619 acá contra 10.329 en `fn_reporte_semanal_levante_extras`,
        -- diferencia = la venta acumulada). Una ave vendida sale del lote: no contarla infla el saldo
        -- y, en cascada, subestima el consumo por ave — el mismo mecanismo por el que en su momento
        -- hubo que sumar el error de sexaje. La especificación ejecutable es
        -- `SaldoAvesLevanteCalculos.BajasNetas`, que ya la incluía.
        r_aves_fin := v_aves_acum - r_mort_tot - r_sel_tot - r_err_tot - r_tras_sal - r_venta_tot + r_tras_ing;
        -- Saldo por género (REQ-002e). Por sexo se usan los splits dedicados, no `venta_aves_cantidad`.
        r_aves_fin_h := v_aves_acum_h - r_mort_h - r_sel_h - r_err_h - r_tras_sal_h - r_venta_h + r_tras_ing_h;
        r_aves_fin_m := v_aves_acum_m - r_mort_m - r_sel_m - r_err_m - r_tras_sal_m - r_venta_m + r_tras_ing_m;

        -- Pesaje: último registro (por fecha, luego id) de la semana con peso>0.
        SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
          FROM _seg_sem
         WHERE sem = s AND (ph > 0 OR pm > 0)
         ORDER BY reg_date DESC, id DESC LIMIT 1;
        IF NOT FOUND THEN
            SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
              FROM _seg_sem WHERE sem = s ORDER BY reg_date DESC, id DESC LIMIT 1;
        END IF;
        r_pH := COALESCE(r_pH,0); r_pM := COALESCE(r_pM,0);
        r_uH := COALESCE(r_uH,0); r_uM := COALESCE(r_uM,0);

        r_peso_prom := CASE WHEN r_pH > 0 AND r_pM > 0 THEN (r_pH + r_pM)/2
                            WHEN r_pH > 0 THEN r_pH ELSE r_pM END;
        IF r_peso_prom <= 0 THEN r_peso_prom := COALESCE(v_peso_anterior,0); END IF;
        r_unif_real := CASE WHEN r_uH > 0 AND r_uM > 0 THEN (r_uH + r_uM)/2
                            WHEN r_uH > 0 THEN r_uH ELSE r_uM END;

        -- Peso por sexo (REQ-010b): valor del pesaje del sexo; arrastre del último conocido
        -- cuando la semana no tiene pesaje del sexo (mismo criterio que el peso mixto, que
        -- también arrastra). NULL si nunca hubo pesaje del sexo (p.ej. machos sin pesaje ⇒
        -- serie vacía en el chart, degrada con spanGaps).
        r_peso_h := CASE WHEN r_pH > 0 THEN r_pH ELSE v_peso_ant_h END;
        r_peso_m := CASE WHEN r_pM > 0 THEN r_pM ELSE v_peso_ant_m END;

        r_cons_g    := r_cons_kg * 1000;
        r_aves_prom := (v_aves_acum + r_aves_fin)/2;
        r_cons_dia  := CASE WHEN r_aves_prom > 0 AND r_dias > 0 THEN r_cons_g/(r_aves_prom*r_dias) ELSE 0 END;

        -- Consumo real por sexo (g/ave/día): consumo_kg_sexo*1000 / saldo_prom_sexo / días.
        r_aves_prom_h := (v_aves_acum_h + r_aves_fin_h)/2;
        r_aves_prom_m := (v_aves_acum_m + r_aves_fin_m)/2;
        r_cons_dia_h  := CASE WHEN r_aves_prom_h > 0 AND r_dias > 0
                              THEN (r_cons_kg_h*1000)/(r_aves_prom_h*r_dias) ELSE NULL END;
        r_cons_dia_m  := CASE WHEN r_aves_prom_m > 0 AND r_dias > 0
                              THEN (r_cons_kg_m*1000)/(r_aves_prom_m*r_dias) ELSE NULL END;

        -- Guía real para la semana. Mixto (compat) + por sexo SIN promediar (REQ-002e).
        --
        -- 🔴 EL PROMEDIO MIXTO NO SE PUEDE APLICAR A UNA GUÍA DE SOLO HEMBRAS.
        -- Las tres expresiones mixtas hacen COALESCE de cada término y dividen por 2 FIJO.
        -- Con la guía reducida —que trae hembras y NO machos— eso da (95.00 + 0)/2 = 47,5
        -- donde el cliente dice 95,00: no es NULL, no es 0, no revienta. Es un número
        -- plausible y equivocado por un factor de 2, que nadie detecta mirando la pantalla.
        -- Por eso el promedio se aplica SOLO cuando la fila viene de la guía compartida;
        -- para la propia se usa el valor de hembras tal cual, que es el único que existe.
        -- La rama 'compartida' es LITERALMENTE la expresión de siempre ⇒ delta cero por
        -- construcción para Sanmarino, Demo, Ecuador y Panamá, no «verificado después».
        SELECT CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.gr_ave_dia_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,0))/2 END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.peso_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.peso_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.peso_m),'')::double precision,0))/2 END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.uniformidad),'')::double precision
                    ELSE COALESCE(NULLIF(btrim(g.uniformidad),'')::double precision,0) END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.mort_sem_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.mort_sem_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.mort_sem_m),'')::double precision,0))/2 END,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision,
               g.origen
          INTO r_cons_tabla, r_peso_tabla, r_unif_tabla, r_mort_tabla, r_cons_tabla_h, r_cons_tabla_m,
               r_peso_tabla_h, r_peso_tabla_m, r_mort_tabla_h, r_mort_tabla_m, v_origen_guia
          FROM vw_guia_genetica_postura g
         WHERE g.company_id = v_company
           -- ⚠️ La comparacion de raza de la rama COMPARTIDA queda EXACTA y case-sensitive, como
           -- siempre: aflojarla haria matchear filas que hoy no matchean para Sanmarino, Demo,
           -- Ecuador y Panama, o sea el refactor cambiaria resultados por si solo. La rama PROPIA
           -- —inalcanzable para esas cuatro— si compara normalizado, porque produccion ya lo hace
           -- y tenerlo de un solo lado era la causa medida de que `CRIOLLA` cruzara en produccion
           -- y no en levante (30-ago-2026). La grafia del ERP la resuelve la vista, con su alias.
           AND (CASE WHEN g.origen = 'propia'
                     THEN btrim(lower(g.raza)) = btrim(lower(v_raza))
                     ELSE g.raza = v_raza END)
           AND g.anio_guia = v_anio
           AND btrim(g.edad) = s::text
         LIMIT 1;
        -- El COALESCE a 0 también es exclusivo de la guía compartida: ahí la columna existe en
        -- toda la curva y el 0 se lee como «la guía dice 0». En la propia la métrica NO EXISTE
        -- (no trae peso, ni uniformidad, ni mortalidad semanal — su retiro_ac_h es ACUMULADO),
        -- y un 0 ahí se leería como un objetivo real. NULL es la única lectura honesta, y el
        -- front ya lo sabe pintar: las series por sexo llegan NULL desde siempre.
        -- `AND NOT v_guia_propia_empresa`: sin eso, una semana SIN fila de guia (v_origen_guia
        -- NULL) caia igual en el COALESCE. Para una empresa con guia propia eso pinta 0,00 en las
        -- cuatro columnas de guia —un objetivo inventado— justo donde su guia no llega: la de
        -- Santa Reyes arranca en la semana 18 y el levante empieza en la 1. Medido el 30-ago-2026.
        -- Para las cuatro empresas sin guia propia la condicion nueva es siempre TRUE ⇒ la misma
        -- expresion de hoy, incluido el 0 legitimo cuando la guia compartida trae la columna vacia.
        IF v_origen_guia IS DISTINCT FROM 'propia' AND NOT v_guia_propia_empresa THEN
            r_cons_tabla := COALESCE(r_cons_tabla,0);
            r_peso_tabla := COALESCE(r_peso_tabla,0);
            r_unif_tabla := COALESCE(r_unif_tabla,0);
            r_mort_tabla := COALESCE(r_mort_tabla,0);
        END IF;
        -- r_cons_tabla_h/_m, r_peso_tabla_h/_m, r_mort_tabla_h/_m se dejan NULL si la guía
        -- no trae el dato del sexo (series de guía degradan a NULL, sin promediar).

        r_gan_sem   := r_peso_prom - v_peso_anterior;
        r_cons_ave  := CASE WHEN r_aves_prom > 0 THEN r_cons_g/r_aves_prom ELSE 0 END;
        r_conv      := CASE WHEN r_gan_sem > 0 THEN r_cons_ave/r_gan_sem ELSE 0 END;
        r_gan_dia_ac := r_gan_sem/7;
        r_gan_tabla := CASE WHEN r_peso_tabla > 0 AND v_peso_tabla_ant > 0 THEN r_peso_tabla - v_peso_tabla_ant ELSE 0 END;

        r_mort_sem  := CASE WHEN v_aves_acum > 0 THEN (r_mort_tot/v_aves_acum)*100 ELSE 0 END;
        r_sel_sem   := CASE WHEN v_aves_acum > 0 THEN (r_sel_tot/v_aves_acum)*100 ELSE 0 END;
        r_err_sem   := CASE WHEN v_aves_acum > 0 THEN (r_err_tot/v_aves_acum)*100 ELSE 0 END;
        r_mort_mas_sel := r_mort_sem + r_sel_sem;

        -- REQ-010b: mortalidad y retiro POR SEXO. Mismo denominador que el mixto (aves al inicio
        -- de la semana del sexo). El retiro replica el mixto retiroSem = mort+sel+errSex del sexo.
        -- NULL (no 0 sintético) cuando el sexo no tiene saldo ⇒ la serie degrada con spanGaps.
        r_mort_pct_h   := CASE WHEN v_aves_acum_h > 0 THEN (r_mort_h / v_aves_acum_h) * 100 ELSE NULL END;
        r_mort_pct_m   := CASE WHEN v_aves_acum_m > 0 THEN (r_mort_m / v_aves_acum_m) * 100 ELSE NULL END;
        r_retiro_pct_h := CASE WHEN v_aves_acum_h > 0 THEN ((r_mort_h + r_sel_h + r_err_h) / v_aves_acum_h) * 100 ELSE NULL END;
        r_retiro_pct_m := CASE WHEN v_aves_acum_m > 0 THEN ((r_mort_m + r_sel_m + r_err_m) / v_aves_acum_m) * 100 ELSE NULL END;

        r_efic   := CASE WHEN r_cons_ave > 0 THEN r_gan_sem/r_cons_ave ELSE 0 END;
        r_superv := CASE WHEN v_aves_acum > 0 THEN r_aves_fin/v_aves_acum ELSE 0 END;
        r_ip     := r_efic * r_superv;

        -- REQ-002f: acumulados reales = bajas_acumuladas / aves_encasetadas * 100.
        v_mort_bajas_acum := v_mort_bajas_acum + r_mort_tot;
        v_sel_bajas_acum  := v_sel_bajas_acum + r_sel_tot;

        semana                        := s;
        aves_inicio_semana            := v_aves_acum;
        aves_fin_semana               := r_aves_fin;
        consumo_diario                := r_cons_dia;
        consumo_tabla                 := r_cons_tabla;
        consumo_total_semana          := r_cons_g;
        conversion_alimenticia        := r_conv;
        peso_tabla                    := r_peso_tabla;
        unif_real                     := r_unif_real;
        unif_tabla                    := r_unif_tabla;
        mort_tabla                    := r_mort_tabla;
        dif_peso_pct                  := CASE WHEN r_peso_tabla > 0 THEN ((r_peso_prom - r_peso_tabla)/r_peso_tabla)*100 ELSE 0 END;
        ganancia_semana               := r_gan_sem;
        ganancia_diaria_acumulada     := r_gan_dia_ac;
        ganancia_tabla                := r_gan_tabla;
        mortalidad_sem                := r_mort_sem;
        seleccion_sem                 := r_sel_sem;
        error_sexaje_sem              := r_err_sem;
        mortalidad_mas_seleccion      := r_mort_mas_sel;
        eficiencia                    := r_efic;
        ip                            := r_ip;
        vpi                           := r_ip;   -- front: vpi = supervivencia*eficiencia = ip
        saldo_aves_semanal            := r_aves_fin;
        mortalidad_acum               := CASE WHEN v_aves_enc > 0 THEN (v_mort_bajas_acum/v_aves_enc)*100 ELSE 0 END;
        seleccion_acum                := CASE WHEN v_aves_enc > 0 THEN (v_sel_bajas_acum/v_aves_enc)*100 ELSE 0 END;
        mortalidad_mas_seleccion_acum := CASE WHEN v_aves_enc > 0 THEN ((v_mort_bajas_acum + v_sel_bajas_acum)/v_aves_enc)*100 ELSE 0 END;
        piso_termico_visible          := false;  -- la guía no expone el flag; front daba false
        peso_inicial                  := v_peso_anterior;
        peso_cierre                   := r_peso_prom;
        dias_con_registro             := r_dias;
        consumo_diario_hembras        := r_cons_dia_h;
        consumo_diario_machos         := r_cons_dia_m;
        consumo_tabla_hembras         := r_cons_tabla_h;
        consumo_tabla_machos          := r_cons_tabla_m;
        peso_hembras                  := r_peso_h;
        peso_machos                   := r_peso_m;
        peso_tabla_hembras            := r_peso_tabla_h;
        peso_tabla_machos             := r_peso_tabla_m;
        mort_pct_hembras              := r_mort_pct_h;
        mort_pct_machos               := r_mort_pct_m;
        mort_tabla_hembras            := r_mort_tabla_h;
        mort_tabla_machos             := r_mort_tabla_m;
        retiro_pct_hembras            := r_retiro_pct_h;
        retiro_pct_machos             := r_retiro_pct_m;

        -- TK-2026-000022 — el resto de los parametros por sexo. Ninguno introduce aritmetica
        -- nueva: son las MISMAS variables con las que ya se arman las columnas mixtas, expuestas
        -- sin promediar. El criterio de NULL es el de las series por sexo de arriba: si el sexo no
        -- tiene saldo (o la semana no tuvo pesaje / la guia no trae el dato) va NULL, para que la
        -- pantalla muestre un guion en vez de un cero que se leeria como dato real.
        aves_inicio_hembras           := CASE WHEN v_aves_enc_h > 0 THEN v_aves_acum_h ELSE NULL END;
        aves_fin_hembras              := CASE WHEN v_aves_enc_h > 0 THEN r_aves_fin_h  ELSE NULL END;
        aves_inicio_machos            := CASE WHEN v_aves_enc_m > 0 THEN v_aves_acum_m ELSE NULL END;
        aves_fin_machos               := CASE WHEN v_aves_enc_m > 0 THEN r_aves_fin_m  ELSE NULL END;
        consumo_total_semana_hembras  := CASE WHEN v_aves_enc_h > 0 THEN r_cons_kg_h * 1000 ELSE NULL END;
        consumo_total_semana_machos   := CASE WHEN v_aves_enc_m > 0 THEN r_cons_kg_m * 1000 ELSE NULL END;
        -- Uniformidad: 0 significa "no hubo pesaje esta semana", no "0 % de uniformidad".
        unif_hembras                  := CASE WHEN r_uH > 0 THEN r_uH ELSE NULL END;
        unif_machos                   := CASE WHEN r_uM > 0 THEN r_uM ELSE NULL END;
        ganancia_hembras              := CASE WHEN r_peso_h IS NOT NULL AND v_peso_ant_h IS NOT NULL
                                              THEN r_peso_h - v_peso_ant_h ELSE NULL END;
        ganancia_machos               := CASE WHEN r_peso_m IS NOT NULL AND v_peso_ant_m IS NOT NULL
                                              THEN r_peso_m - v_peso_ant_m ELSE NULL END;
        dif_peso_pct_hembras          := CASE WHEN r_peso_tabla_h > 0 AND r_peso_h IS NOT NULL
                                              THEN ((r_peso_h - r_peso_tabla_h)/r_peso_tabla_h)*100 ELSE NULL END;
        dif_peso_pct_machos           := CASE WHEN r_peso_tabla_m > 0 AND r_peso_m IS NOT NULL
                                              THEN ((r_peso_m - r_peso_tabla_m)/r_peso_tabla_m)*100 ELSE NULL END;
        seleccion_pct_hembras         := CASE WHEN v_aves_acum_h > 0 THEN (r_sel_h / v_aves_acum_h) * 100 ELSE NULL END;
        seleccion_pct_machos          := CASE WHEN v_aves_acum_m > 0 THEN (r_sel_m / v_aves_acum_m) * 100 ELSE NULL END;
        error_sexaje_pct_hembras      := CASE WHEN v_aves_acum_h > 0 THEN (r_err_h / v_aves_acum_h) * 100 ELSE NULL END;
        error_sexaje_pct_machos       := CASE WHEN v_aves_acum_m > 0 THEN (r_err_m / v_aves_acum_m) * 100 ELSE NULL END;

        RETURN NEXT;

        -- avanzar acumuladores (idéntico al front) + saldo por género.
        v_aves_acum      := r_aves_fin;
        v_aves_acum_h    := r_aves_fin_h;
        v_aves_acum_m    := r_aves_fin_m;
        v_peso_anterior  := r_peso_prom;
        v_peso_tabla_ant := r_peso_tabla;
        v_peso_ant_h     := r_peso_h;   -- arrastre peso por sexo (REQ-010b)
        v_peso_ant_m     := r_peso_m;
    END LOOP;

    RETURN;
END;
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

        private const string FnReporteSemanalExtrasPrev = """
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
               COUNT(*)::int,
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

        private const string FnResumenRaPesadasPrev = """
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
           COUNT(*)::int                       AS dias,
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

        private const string SpRecalcularNueva = """
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- sp_recalcular_seguimiento_levante — grilla diaria + saldo de LEVANTE (produccion_resultado_levante)
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- Primer espejo de esta fn en backend/sql/ (no tenia uno hasta ahora).
-- Fix 2026-09-05 (plan seguimiento_produccion_multiples_registros_dia_plan.md, §5/S6):
--   el CTE 'base' leia seguimiento_diario_levante CRUDA. Con el flag
--   companies.permite_multiples_seguimientos_diarios ON, 2+ registros el mismo dia harian
--   que lag(peso_prom_h) OVER (ORDER BY fecha_registro) -- unas lineas mas abajo, en ac_base --
--   comparara dos registros del MISMO dia como si fueran dias consecutivos, dando un
--   gr_ave_dia_h/m sin sentido. A diferencia de las 3 fns semanales (donde SUM asociativa
--   hacia bastar un fix de conteo), ESTA fn necesitaba agrupar por dia ANTES de la ventana.
-- Fuente ahora: fn_seguimiento_diario_levante(l_lote_id) en vez de la tabla cruda -- una
-- fila por dia siempre (agrupada si el flag esta ON, un espejo 1:1 si esta OFF).
-- Espejo exacto de pg_get_functiondef (ground truth) + este fix, no reformateado.
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION public.sp_recalcular_seguimiento_levante(l_lote_id text)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
declare
  v_fecha_encaset date;
  v_h_ini int;
  v_m_ini int;
  v_mort_caja_h int;
  v_mort_caja_m int;
  v_codigo_guia text;
  v_raza text;
  v_ano_gen int;
begin

  select fecha_encaset,
         coalesce(hembras_l, 0),
         coalesce(machos_l, 0),
         coalesce(mort_caja_h, 0),
         coalesce(mort_caja_m, 0),
         codigo_guia_genetica,
         raza,
         ano_tabla_genetica
    into v_fecha_encaset, v_h_ini, v_m_ini, v_mort_caja_h, v_mort_caja_m, v_codigo_guia, v_raza, v_ano_gen
  from lotes
  where lote_id = l_lote_id::integer;   -- lotes.lote_id es integer; l_lote_id es text

  if not found then
    raise exception 'Lote % no existe', l_lote_id;
  end if;

  delete from produccion_resultado_levante where lote_id = l_lote_id;

  insert into produccion_resultado_levante (
    lote_id, fecha, edad_semana,
    hembra_viva, mort_h, sel_h_out, err_h, cons_kg_h, peso_h, unif_h, cv_h,
    mort_h_pct, sel_h_pct, err_h_pct, ms_eh_h,
    ac_mort_h, ac_sel_h, ac_err_h, ac_cons_kg_h, cons_ac_gr_h, gr_ave_dia_h,
    dif_cons_h_pct, dif_peso_h_pct, retiro_h_pct, retiro_h_ac_pct,
    macho_vivo, mort_m, sel_m_out, err_m, cons_kg_m, peso_m, unif_m, cv_m,
    mort_m_pct, sel_m_pct, err_m_pct, ms_em_m,
    ac_mort_m, ac_sel_m, ac_err_m, ac_cons_kg_m, cons_ac_gr_m, gr_ave_dia_m,
    dif_cons_m_pct, dif_peso_m_pct, retiro_m_pct, retiro_m_ac_pct,
    rel_m_h_pct,
    peso_h_guia, unif_h_guia, cons_ac_gr_h_guia, gr_ave_dia_h_guia, mort_h_pct_guia,
    peso_m_guia, unif_m_guia, cons_ac_gr_m_guia, gr_ave_dia_m_guia, mort_m_pct_guia,
    alimento_h_guia, alimento_m_guia
  )
  with base as (
    -- fn_seguimiento_diario_levante: agrupa por dia cuando el flag de la empresa lo pide (una
    -- fila por dia SIEMPRE). Antes esto leia la tabla cruda directo -- con 2 registros el mismo
    -- dia, lag(peso_prom_h) unas lineas mas abajo compararia dos registros del MISMO dia como si
    -- fueran dias consecutivos. fecha_ts preserva el timestamptz original (byte a byte igual al
    -- `s.fecha` de antes cuando no hay agrupacion).
    select
           s.fecha_ts as fecha_registro,
           s.mortalidad_hembras, s.mortalidad_machos,
           s.sel_h, s.sel_m,
           s.error_sexaje_hembras, s.error_sexaje_machos,
           s.consumo_kg_hembras, s.consumo_kg_machos,
           s.peso_prom_hembras as peso_prom_h, s.peso_prom_machos as peso_prom_m,
           s.uniformidad_hembras as uniformidad_h, s.uniformidad_machos as uniformidad_m,
           s.cv_hembras as cv_h, s.cv_machos as cv_m,
           case when v_fecha_encaset is null then null
                else (1 + floor(extract(epoch from (s.fecha_ts - v_fecha_encaset)) / 86400.0 / 7.0)::int)
           end as edad_sem,
           (coalesce(s.mortalidad_hembras,0) + coalesce(s.sel_h,0) + coalesce(s.error_sexaje_hembras,0)
             + coalesce(s.traslado_salida_hembras,0) - coalesce(s.traslado_ingreso_hembras,0)) as out_h,
           (coalesce(s.mortalidad_machos,0)  + coalesce(s.sel_m,0) + coalesce(s.error_sexaje_machos,0)
             + coalesce(s.traslado_salida_machos,0) - coalesce(s.traslado_ingreso_machos,0)) as out_m
    from fn_seguimiento_diario_levante(l_lote_id) s
  ),
  ac_base as (
    select b.*,
           sum(b.out_h) over (order by b.fecha_registro
                              rows between unbounded preceding and 1 preceding) as ac_out_h_prev,
           sum(b.out_m) over (order by b.fecha_registro
                              rows between unbounded preceding and 1 preceding) as ac_out_m_prev,
           sum(coalesce(b.mortalidad_hembras,0)) over (order by b.fecha_registro) as ac_mort_h,
           sum(coalesce(b.sel_h,0))             over (order by b.fecha_registro) as ac_sel_h,
           sum(coalesce(b.error_sexaje_hembras,0)) over (order by b.fecha_registro) as ac_err_h,
           sum(coalesce(b.consumo_kg_hembras,0))   over (order by b.fecha_registro) as ac_cons_kg_h,
           sum(coalesce(b.mortalidad_machos,0)) over (order by b.fecha_registro) as ac_mort_m,
           sum(coalesce(b.sel_m,0))             over (order by b.fecha_registro) as ac_sel_m,
           sum(coalesce(b.error_sexaje_machos,0)) over (order by b.fecha_registro) as ac_err_m,
           sum(coalesce(b.consumo_kg_machos,0))   over (order by b.fecha_registro) as ac_cons_kg_m,
           lag(b.peso_prom_h) over (order by b.fecha_registro) as peso_h_prev,
           lag(b.peso_prom_m) over (order by b.fecha_registro) as peso_m_prev
    from base b
  ),
  pobl as (
    select a.*,
           greatest(0, (coalesce(v_h_ini,0) - coalesce(v_mort_caja_h,0) - coalesce(a.ac_out_h_prev,0)))::int as hembra_viva,
           greatest(0, (coalesce(v_m_ini,0) - coalesce(v_mort_caja_m,0) - coalesce(a.ac_out_m_prev,0)))::int as macho_vivo
    from ac_base a
  ),
  gh as (
    select semana, peso_obj, unif_obj, mort_pct_obj, cons_ac_gr_obj, gr_ave_dia_obj, incr_cons_obj,
           kcal_sem_obj, kcal_sem_ac_obj, prot_sem_obj, prot_sem_ac_obj, alimento_nom
    from guia_semana
    where sexo='H'
      and (codigo_guia_genetica is not distinct from v_codigo_guia)
      and (raza is not distinct from v_raza)
      and (ano_tabla_genetica is not distinct from v_ano_gen)
  ),
  gm as (
    select semana, peso_obj, unif_obj, mort_pct_obj, cons_ac_gr_obj, gr_ave_dia_obj, incr_cons_obj,
           kcal_sem_obj, kcal_sem_ac_obj, prot_sem_obj, prot_sem_ac_obj, alimento_nom
    from guia_semana
    where sexo='M'
      and (codigo_guia_genetica is not distinct from v_codigo_guia)
      and (raza is not distinct from v_raza)
      and (ano_tabla_genetica is not distinct from v_ano_gen)
  )
  select
    l_lote_id as lote_id,
    p.fecha_registro as fecha,
    p.edad_sem as edad_semana,
    p.hembra_viva,
    coalesce(p.mortalidad_hembras,0) as mort_h,
    coalesce(p.sel_h,0)              as sel_h_out,
    coalesce(p.error_sexaje_hembras,0) as err_h,
    p.consumo_kg_hembras             as cons_kg_h,
    p.peso_prom_h                    as peso_h,
    p.uniformidad_h                  as unif_h,
    p.cv_h                           as cv_h,
    case when p.hembra_viva>0 then dpr(p.mortalidad_hembras * 100.0 / p.hembra_viva, 3) end as mort_h_pct,
    case when p.hembra_viva>0 then dpr(p.sel_h * 100.0 / p.hembra_viva, 3) end              as sel_h_pct,
    case when p.hembra_viva>0 then dpr(p.error_sexaje_hembras * 100.0 / p.hembra_viva, 3) end as err_h_pct,
    (coalesce(p.mortalidad_hembras,0)+coalesce(p.sel_h,0)+coalesce(p.error_sexaje_hembras,0))   as ms_eh_h,
    p.ac_mort_h, p.ac_sel_h, p.ac_err_h,
    p.ac_cons_kg_h,
    case when p.hembra_viva>0 then dpr( (p.ac_cons_kg_h*1000.0)/p.hembra_viva, 3) end as cons_ac_gr_h,
    case when p.peso_prom_h is null or p.peso_h_prev is null then null
         else dpr(p.peso_prom_h - p.peso_h_prev, 2)
    end as gr_ave_dia_h,
    case when gh.cons_ac_gr_obj is null or p.hembra_viva<=0 then null
         else dpr( (((p.ac_cons_kg_h*1000.0)/p.hembra_viva) - gh.cons_ac_gr_obj) * 100.0 / gh.cons_ac_gr_obj, 3)
    end as dif_cons_h_pct,
    case when gh.peso_obj is null or p.peso_prom_h is null then null
         else dpr( (p.peso_prom_h - gh.peso_obj) * 100.0 / gh.peso_obj, 3)
    end as dif_peso_h_pct,
    case when p.hembra_viva>0 then dpr( (coalesce(p.sel_h,0)+coalesce(p.error_sexaje_hembras,0)) * 100.0 / p.hembra_viva, 3) end as retiro_h_pct,
    case when (p.hembra_viva + p.ac_mort_h + p.ac_sel_h + p.ac_err_h)>0
         then dpr( (p.ac_sel_h + p.ac_err_h) * 100.0 / (p.hembra_viva + p.ac_mort_h + p.ac_sel_h + p.ac_err_h), 3)
    end as retiro_h_ac_pct,
    p.macho_vivo,
    coalesce(p.mortalidad_machos,0) as mort_m,
    coalesce(p.sel_m,0)             as sel_m_out,
    coalesce(p.error_sexaje_machos,0) as err_m,
    p.consumo_kg_machos             as cons_kg_m,
    p.peso_prom_m                   as peso_m,
    p.uniformidad_m                 as unif_m,
    p.cv_m                          as cv_m,
    case when p.macho_vivo>0 then dpr(p.mortalidad_machos * 100.0 / p.macho_vivo, 3) end as mort_m_pct,
    case when p.macho_vivo>0 then dpr(p.sel_m * 100.0 / p.macho_vivo, 3) end             as sel_m_pct,
    case when p.macho_vivo>0 then dpr(p.error_sexaje_machos * 100.0 / p.macho_vivo, 3) end as err_m_pct,
    (coalesce(p.mortalidad_machos,0)+coalesce(p.sel_m,0)+coalesce(p.error_sexaje_machos,0))   as ms_em_m,
    p.ac_mort_m, p.ac_sel_m, p.ac_err_m,
    p.ac_cons_kg_m,
    case when p.macho_vivo>0 then dpr( (p.ac_cons_kg_m*1000.0)/p.macho_vivo, 3) end as cons_ac_gr_m,
    case when p.peso_prom_m is null or p.peso_m_prev is null then null
         else dpr(p.peso_prom_m - p.peso_m_prev, 2)
    end as gr_ave_dia_m,
    case when gm.cons_ac_gr_obj is null or p.macho_vivo<=0 then null
         else dpr( (((p.ac_cons_kg_m*1000.0)/p.macho_vivo) - gm.cons_ac_gr_obj) * 100.0 / gm.cons_ac_gr_obj, 3)
    end as dif_cons_m_pct,
    case when gm.peso_obj is null or p.peso_prom_m is null then null
         else dpr( (p.peso_prom_m - gm.peso_obj) * 100.0 / gm.peso_obj, 3)
    end as dif_peso_m_pct,
    case when p.macho_vivo>0 then dpr( (coalesce(p.sel_m,0)+coalesce(p.error_sexaje_machos,0)) * 100.0 / p.macho_vivo, 3) end as retiro_m_pct,
    case when (p.macho_vivo + p.ac_mort_m + p.ac_sel_m + p.ac_err_m)>0
         then dpr( (p.ac_sel_m + p.ac_err_m) * 100.0 / (p.macho_vivo + p.ac_mort_m + p.ac_sel_m + p.ac_err_m), 3)
    end as retiro_m_ac_pct,
    case when p.hembra_viva is null or p.hembra_viva=0 then null
         else dpr(p.macho_vivo * 100.0 / p.hembra_viva, 3)
    end as rel_m_h_pct,
    gh.peso_obj, gh.unif_obj, gh.cons_ac_gr_obj, gh.gr_ave_dia_obj, gh.mort_pct_obj,
    gm.peso_obj, gm.unif_obj, gm.cons_ac_gr_obj, gm.gr_ave_dia_obj, gm.mort_pct_obj,
    gh.alimento_nom, gm.alimento_nom
  from pobl p
  left join gh on gh.semana = p.edad_sem
  left join gm on gm.semana = p.edad_sem
  order by p.fecha_registro;

end;
$function$

""";

        private const string SpRecalcularPrev = """
CREATE OR REPLACE FUNCTION public.sp_recalcular_seguimiento_levante(l_lote_id text)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
declare
  v_fecha_encaset date;
  v_h_ini int;
  v_m_ini int;
  v_mort_caja_h int;
  v_mort_caja_m int;
  v_codigo_guia text;
  v_raza text;
  v_ano_gen int;
begin

  select fecha_encaset,
         coalesce(hembras_l, 0),
         coalesce(machos_l, 0),
         coalesce(mort_caja_h, 0),
         coalesce(mort_caja_m, 0),
         codigo_guia_genetica,
         raza,
         ano_tabla_genetica
    into v_fecha_encaset, v_h_ini, v_m_ini, v_mort_caja_h, v_mort_caja_m, v_codigo_guia, v_raza, v_ano_gen
  from lotes
  where lote_id = l_lote_id::integer;   -- lotes.lote_id es integer; l_lote_id es text

  if not found then
    raise exception 'Lote % no existe', l_lote_id;
  end if;

  delete from produccion_resultado_levante where lote_id = l_lote_id;

  insert into produccion_resultado_levante (
    lote_id, fecha, edad_semana,
    hembra_viva, mort_h, sel_h_out, err_h, cons_kg_h, peso_h, unif_h, cv_h,
    mort_h_pct, sel_h_pct, err_h_pct, ms_eh_h,
    ac_mort_h, ac_sel_h, ac_err_h, ac_cons_kg_h, cons_ac_gr_h, gr_ave_dia_h,
    dif_cons_h_pct, dif_peso_h_pct, retiro_h_pct, retiro_h_ac_pct,
    macho_vivo, mort_m, sel_m_out, err_m, cons_kg_m, peso_m, unif_m, cv_m,
    mort_m_pct, sel_m_pct, err_m_pct, ms_em_m,
    ac_mort_m, ac_sel_m, ac_err_m, ac_cons_kg_m, cons_ac_gr_m, gr_ave_dia_m,
    dif_cons_m_pct, dif_peso_m_pct, retiro_m_pct, retiro_m_ac_pct,
    rel_m_h_pct,
    peso_h_guia, unif_h_guia, cons_ac_gr_h_guia, gr_ave_dia_h_guia, mort_h_pct_guia,
    peso_m_guia, unif_m_guia, cons_ac_gr_m_guia, gr_ave_dia_m_guia, mort_m_pct_guia,
    alimento_h_guia, alimento_m_guia
  )
  with base as (
    select
           s.fecha as fecha_registro,
           s.mortalidad_hembras, s.mortalidad_machos,
           s.sel_h, s.sel_m,
           s.error_sexaje_hembras, s.error_sexaje_machos,
           s.consumo_kg_hembras, s.consumo_kg_machos,
           s.peso_prom_hembras as peso_prom_h, s.peso_prom_machos as peso_prom_m,
           s.uniformidad_hembras as uniformidad_h, s.uniformidad_machos as uniformidad_m,
           s.cv_hembras as cv_h, s.cv_machos as cv_m,
           case when v_fecha_encaset is null then null
                else (1 + floor(extract(epoch from (s.fecha - v_fecha_encaset)) / 86400.0 / 7.0)::int)
           end as edad_sem,
           (coalesce(s.mortalidad_hembras,0) + coalesce(s.sel_h,0) + coalesce(s.error_sexaje_hembras,0)
             + coalesce(s.traslado_salida_hembras,0) - coalesce(s.traslado_ingreso_hembras,0)) as out_h,
           (coalesce(s.mortalidad_machos,0)  + coalesce(s.sel_m,0) + coalesce(s.error_sexaje_machos,0)
             + coalesce(s.traslado_salida_machos,0) - coalesce(s.traslado_ingreso_machos,0)) as out_m
    from seguimiento_diario_levante s
    where s.tipo_seguimiento = 'levante' and s.lote_id = l_lote_id
  ),
  ac_base as (
    select b.*,
           sum(b.out_h) over (order by b.fecha_registro
                              rows between unbounded preceding and 1 preceding) as ac_out_h_prev,
           sum(b.out_m) over (order by b.fecha_registro
                              rows between unbounded preceding and 1 preceding) as ac_out_m_prev,
           sum(coalesce(b.mortalidad_hembras,0)) over (order by b.fecha_registro) as ac_mort_h,
           sum(coalesce(b.sel_h,0))             over (order by b.fecha_registro) as ac_sel_h,
           sum(coalesce(b.error_sexaje_hembras,0)) over (order by b.fecha_registro) as ac_err_h,
           sum(coalesce(b.consumo_kg_hembras,0))   over (order by b.fecha_registro) as ac_cons_kg_h,
           sum(coalesce(b.mortalidad_machos,0)) over (order by b.fecha_registro) as ac_mort_m,
           sum(coalesce(b.sel_m,0))             over (order by b.fecha_registro) as ac_sel_m,
           sum(coalesce(b.error_sexaje_machos,0)) over (order by b.fecha_registro) as ac_err_m,
           sum(coalesce(b.consumo_kg_machos,0))   over (order by b.fecha_registro) as ac_cons_kg_m,
           lag(b.peso_prom_h) over (order by b.fecha_registro) as peso_h_prev,
           lag(b.peso_prom_m) over (order by b.fecha_registro) as peso_m_prev
    from base b
  ),
  pobl as (
    select a.*,
           greatest(0, (coalesce(v_h_ini,0) - coalesce(v_mort_caja_h,0) - coalesce(a.ac_out_h_prev,0)))::int as hembra_viva,
           greatest(0, (coalesce(v_m_ini,0) - coalesce(v_mort_caja_m,0) - coalesce(a.ac_out_m_prev,0)))::int as macho_vivo
    from ac_base a
  ),
  gh as (
    select semana, peso_obj, unif_obj, mort_pct_obj, cons_ac_gr_obj, gr_ave_dia_obj, incr_cons_obj,
           kcal_sem_obj, kcal_sem_ac_obj, prot_sem_obj, prot_sem_ac_obj, alimento_nom
    from guia_semana
    where sexo='H'
      and (codigo_guia_genetica is not distinct from v_codigo_guia)
      and (raza is not distinct from v_raza)
      and (ano_tabla_genetica is not distinct from v_ano_gen)
  ),
  gm as (
    select semana, peso_obj, unif_obj, mort_pct_obj, cons_ac_gr_obj, gr_ave_dia_obj, incr_cons_obj,
           kcal_sem_obj, kcal_sem_ac_obj, prot_sem_obj, prot_sem_ac_obj, alimento_nom
    from guia_semana
    where sexo='M'
      and (codigo_guia_genetica is not distinct from v_codigo_guia)
      and (raza is not distinct from v_raza)
      and (ano_tabla_genetica is not distinct from v_ano_gen)
  )
  select
    l_lote_id as lote_id,
    p.fecha_registro as fecha,
    p.edad_sem as edad_semana,
    p.hembra_viva,
    coalesce(p.mortalidad_hembras,0) as mort_h,
    coalesce(p.sel_h,0)              as sel_h_out,
    coalesce(p.error_sexaje_hembras,0) as err_h,
    p.consumo_kg_hembras             as cons_kg_h,
    p.peso_prom_h                    as peso_h,
    p.uniformidad_h                  as unif_h,
    p.cv_h                           as cv_h,
    case when p.hembra_viva>0 then dpr(p.mortalidad_hembras * 100.0 / p.hembra_viva, 3) end as mort_h_pct,
    case when p.hembra_viva>0 then dpr(p.sel_h * 100.0 / p.hembra_viva, 3) end              as sel_h_pct,
    case when p.hembra_viva>0 then dpr(p.error_sexaje_hembras * 100.0 / p.hembra_viva, 3) end as err_h_pct,
    (coalesce(p.mortalidad_hembras,0)+coalesce(p.sel_h,0)+coalesce(p.error_sexaje_hembras,0))   as ms_eh_h,
    p.ac_mort_h, p.ac_sel_h, p.ac_err_h,
    p.ac_cons_kg_h,
    case when p.hembra_viva>0 then dpr( (p.ac_cons_kg_h*1000.0)/p.hembra_viva, 3) end as cons_ac_gr_h,
    case when p.peso_prom_h is null or p.peso_h_prev is null then null
         else dpr(p.peso_prom_h - p.peso_h_prev, 2)
    end as gr_ave_dia_h,
    case when gh.cons_ac_gr_obj is null or p.hembra_viva<=0 then null
         else dpr( (((p.ac_cons_kg_h*1000.0)/p.hembra_viva) - gh.cons_ac_gr_obj) * 100.0 / gh.cons_ac_gr_obj, 3)
    end as dif_cons_h_pct,
    case when gh.peso_obj is null or p.peso_prom_h is null then null
         else dpr( (p.peso_prom_h - gh.peso_obj) * 100.0 / gh.peso_obj, 3)
    end as dif_peso_h_pct,
    case when p.hembra_viva>0 then dpr( (coalesce(p.sel_h,0)+coalesce(p.error_sexaje_hembras,0)) * 100.0 / p.hembra_viva, 3) end as retiro_h_pct,
    case when (p.hembra_viva + p.ac_mort_h + p.ac_sel_h + p.ac_err_h)>0
         then dpr( (p.ac_sel_h + p.ac_err_h) * 100.0 / (p.hembra_viva + p.ac_mort_h + p.ac_sel_h + p.ac_err_h), 3)
    end as retiro_h_ac_pct,
    p.macho_vivo,
    coalesce(p.mortalidad_machos,0) as mort_m,
    coalesce(p.sel_m,0)             as sel_m_out,
    coalesce(p.error_sexaje_machos,0) as err_m,
    p.consumo_kg_machos             as cons_kg_m,
    p.peso_prom_m                   as peso_m,
    p.uniformidad_m                 as unif_m,
    p.cv_m                          as cv_m,
    case when p.macho_vivo>0 then dpr(p.mortalidad_machos * 100.0 / p.macho_vivo, 3) end as mort_m_pct,
    case when p.macho_vivo>0 then dpr(p.sel_m * 100.0 / p.macho_vivo, 3) end             as sel_m_pct,
    case when p.macho_vivo>0 then dpr(p.error_sexaje_machos * 100.0 / p.macho_vivo, 3) end as err_m_pct,
    (coalesce(p.mortalidad_machos,0)+coalesce(p.sel_m,0)+coalesce(p.error_sexaje_machos,0))   as ms_em_m,
    p.ac_mort_m, p.ac_sel_m, p.ac_err_m,
    p.ac_cons_kg_m,
    case when p.macho_vivo>0 then dpr( (p.ac_cons_kg_m*1000.0)/p.macho_vivo, 3) end as cons_ac_gr_m,
    case when p.peso_prom_m is null or p.peso_m_prev is null then null
         else dpr(p.peso_prom_m - p.peso_m_prev, 2)
    end as gr_ave_dia_m,
    case when gm.cons_ac_gr_obj is null or p.macho_vivo<=0 then null
         else dpr( (((p.ac_cons_kg_m*1000.0)/p.macho_vivo) - gm.cons_ac_gr_obj) * 100.0 / gm.cons_ac_gr_obj, 3)
    end as dif_cons_m_pct,
    case when gm.peso_obj is null or p.peso_prom_m is null then null
         else dpr( (p.peso_prom_m - gm.peso_obj) * 100.0 / gm.peso_obj, 3)
    end as dif_peso_m_pct,
    case when p.macho_vivo>0 then dpr( (coalesce(p.sel_m,0)+coalesce(p.error_sexaje_machos,0)) * 100.0 / p.macho_vivo, 3) end as retiro_m_pct,
    case when (p.macho_vivo + p.ac_mort_m + p.ac_sel_m + p.ac_err_m)>0
         then dpr( (p.ac_sel_m + p.ac_err_m) * 100.0 / (p.macho_vivo + p.ac_mort_m + p.ac_sel_m + p.ac_err_m), 3)
    end as retiro_m_ac_pct,
    case when p.hembra_viva is null or p.hembra_viva=0 then null
         else dpr(p.macho_vivo * 100.0 / p.hembra_viva, 3)
    end as rel_m_h_pct,
    gh.peso_obj, gh.unif_obj, gh.cons_ac_gr_obj, gh.gr_ave_dia_obj, gh.mort_pct_obj,
    gm.peso_obj, gm.unif_obj, gm.cons_ac_gr_obj, gm.gr_ave_dia_obj, gm.mort_pct_obj,
    gh.alimento_nom, gm.alimento_nom
  from pobl p
  left join gh on gh.semana = p.edad_sem
  left join gm on gm.semana = p.edad_sem
  order by p.fecha_registro;

end;
$function$

""";
    }
}
