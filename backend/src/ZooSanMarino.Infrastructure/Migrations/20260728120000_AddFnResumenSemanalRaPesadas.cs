using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Despliega las dos funciones de la hoja RESUMEN SEMANAL del Informe RA Pesadas:
    /// fn_resumen_semanal_ra_pesadas_levante y fn_resumen_semanal_ra_pesadas_produccion.
    /// Una fila por lote para UNA semana calendario (WEEKNUM de Excel), set-based.
    /// Espejo .sql: backend/sql/fn_resumen_semanal_ra_pesadas_*.sql.
    /// Data-only (Designer clonado, ModelSnapshot intacto). Idempotente
    /// (DROP FUNCTION IF EXISTS + CREATE OR REPLACE).
    /// </summary>
    public partial class AddFnResumenSemanalRaPesadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"-- ============================================================================
-- fn_resumen_semanal_ra_pesadas_levante(...)
-- Hoja «RESUMEN SEMANAL» del Informe RA Pesadas — BLOQUE LEVANTE.
-- ----------------------------------------------------------------------------
-- UNA FILA POR LOTE para UNA semana CALENDARIO (año + semana del año).
-- Es la contracara del Detalle: donde fn_reporte_semanal_levante_extras devuelve
-- N semanas de 1 lote, ésta devuelve N lotes de 1 semana.
--
-- ⚠️ Set-based A PROPÓSITO (LANGUAGE sql, ventanas). Está PROHIBIDO resolver el
--    resumen iterando lotes desde C# y llamando la fn por-lote: son ~70 lotes ×
--    25 semanas y el endpoint se cuelga (mismo patrón que ya rompió los
--    endpoints multipaís). Regla del repo: la BD filtra, el backend orquesta.
--
-- EQUIVALENCIA CON EL DETALLE (requisito duro):
--   Replica EXACTO lo de fn_reporte_semanal_levante_extras / fn_indicadores_levante_postura:
--     * semana de edad  = floor((fecha_bogota − encaset)/7) + 1, topada en 25
--     * guards          = sin registros ⇒ lote fuera; encaset NULL o POSTERIOR
--                         al primer registro ⇒ lote fuera (datos inconsistentes)
--     * exclusión       = filas de PURO traslado posteriores a la semana 25
--     * base por sexo   = hembras_l / machos_l, con fallback al primer
--                         traslado_ingreso del lote, si no 0
--     * saldo por sexo  = base − Σ(mort + sel + err + tras_salida − tras_ingreso)
--     * pesaje          = último registro de la semana con peso>0; si no hay,
--                         el último registro de la semana; con ARRASTRE (LOCF)
--                         del último peso conocido del sexo
--   ⇒ para un mismo (lote, semana) el Resumen y el Detalle deben dar los MISMOS
--     números. Cualquier diferencia es un bug (ver plan §7.5b).
--
-- SEMANA DEL AÑO: se usa la convención WEEKNUM de Excel (return_type = 1:
--   semanas que arrancan en DOMINGO, la semana 1 es la que contiene el 1-ene),
--   NO la semana ISO. Verificado contra el archivo fuente: 1825/1825 filas
--   coinciden con WEEKNUM y solo 1736/1825 con ISO. La fecha que se clasifica
--   es `fecha_fin_semana` = encaset + (semana−1)*7 + 6, la misma columna FECHA
--   que ya emite el Detalle.
--
-- FÓRMULAS (verificadas 1:1 contra la hoja «Datos semanal LEV» del archivo):
--   %MortH      = mort_h_semana / aves_hembras_INICIO_semana * 100
--   %RetiroH    = Σ(mort+sel+err)_h hasta la semana / base_hembras * 100   (base FIJA)
--   %DifConsH   = (gr_ave_dia_h_real / gr_ave_dia_h_guia − 1) * 100
--   %DifPesoH   = (peso_h_real / peso_h_guia − 1) * 100
--   PART        = saldo_hembras del lote / Σ saldo_hembras de la selección
--   (idem machos; en el Excel la columna macho se llama «DifConsM» sin %, pero
--    es la misma fórmula)
--
-- Guía: guia_genetica_sanmarino_colombia por (company, raza, año del lote, edad).
-- Todo TEXT en esa tabla ⇒ se parsea con f_safe_numeric (tolera coma decimal).
--
-- Zona horaria: America/Bogota (idéntica a las fns hermanas).
-- ============================================================================
DROP FUNCTION IF EXISTS fn_resumen_semanal_ra_pesadas_levante(integer, integer, integer, integer[], text, boolean);

CREATE OR REPLACE FUNCTION fn_resumen_semanal_ra_pesadas_levante(
    p_company_id           integer,
    p_anio                 integer,
    p_sem_anio             integer,
    p_granja_ids           integer[] DEFAULT NULL,
    p_regional             text      DEFAULT NULL,
    p_excluir_trasladados  boolean   DEFAULT false
)
RETURNS TABLE(
    lote_id                   integer,
    lote_nombre               text,
    granja_id                 integer,
    granja_nombre             text,
    nucleo_nombre             text,
    regional                  text,
    raza                      text,
    anio_guia                 integer,
    edad_semana               integer,
    fecha_fin_semana          date,
    dias_con_registro         integer,
    tuvo_traslado             boolean,
    part                      double precision,
    saldo_hembras             double precision,
    saldo_machos              double precision,
    mort_hembras_pct          double precision,
    retiro_acum_hembras_pct   double precision,
    retiro_acum_hembras_guia  double precision,
    dif_consumo_hembras_pct   double precision,
    dif_peso_hembras_pct      double precision,
    uniformidad_hembras       double precision,
    cv_hembras                double precision,
    mort_machos_pct           double precision,
    retiro_acum_machos_pct    double precision,
    retiro_acum_machos_guia   double precision,
    dif_consumo_machos_pct    double precision,
    dif_peso_machos_pct       double precision,
    uniformidad_machos        double precision,
    cv_machos                 double precision
)
LANGUAGE sql STABLE AS $$
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
           -- base por sexo con el mismo fallback del Detalle
           COALESCE(
               NULLIF(l.hembras_l, 0)::double precision,
               NULLIF(fi.first_ing_h, 0),
               0)                                    AS base_h,
           COALESCE(
               NULLIF(l.machos_l, 0)::double precision,
               NULLIF(fi.first_ing_m, 0),
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
      LEFT JOIN LATERAL (
            SELECT r.tras_ing_h::double precision AS first_ing_h,
                   r.tras_ing_m::double precision AS first_ing_m
              FROM reg r
             WHERE r.lote_id = lb.lote_id
               AND (r.tras_ing_h + r.tras_ing_m) > 0
             ORDER BY r.reg_date ASC, r.id ASC
             LIMIT 1
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
--    El ""grupo"" de LOCF es el conteo de pesajes no nulos hasta la semana:
--    dentro de cada grupo, el primer valor es el último peso conocido.
acum AS (
    SELECT s.lote_id,
           s.sem,
           s.dias,
           s.mort_h, s.mort_m, s.sel_h, s.sel_m, s.err_h, s.err_m,
           s.tras_sal_h, s.tras_sal_m, s.tras_ing_h, s.tras_ing_m,
           s.cons_kg_h, s.cons_kg_m,
           NULLIF(p.ph, 0) AS peso_h_raw,
           NULLIF(p.pm, 0) AS peso_m_raw,
           NULLIF(COALESCE(p.uh, 0), 0)  AS unif_h,
           NULLIF(COALESCE(p.um, 0), 0)  AS unif_m,
           NULLIF(COALESCE(p.cvh, 0), 0) AS cv_h,
           NULLIF(COALESCE(p.cvm, 0), 0) AS cv_m,
           -- salidas netas acumuladas hasta ESTA semana (inclusive)
           SUM(s.mort_h + s.sel_h + s.err_h + s.tras_sal_h - s.tras_ing_h)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS neto_out_h,
           SUM(s.mort_m + s.sel_m + s.err_m + s.tras_sal_m - s.tras_ing_m)
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
           (lo.enc_date + ((x.sem - 1) * 7) + 6) AS fin_sem
      FROM locf x
      JOIN lote_ok lo ON lo.lote_id = x.lote_id
     WHERE EXTRACT(YEAR FROM (lo.enc_date + ((x.sem - 1) * 7) + 6))::int = p_anio
       AND (
             floor(
               ( (lo.enc_date + ((x.sem - 1) * 7) + 6)
                 - date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp)::date
                 + EXTRACT(DOW FROM date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp))::int
               ) / 7.0
             )::int + 1
           ) = p_sem_anio
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
              FROM guia_genetica_sanmarino_colombia gg
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
    CASE WHEN SUM(f.saldo_h) OVER () > 0
         THEN f.saldo_h / SUM(f.saldo_h) OVER ()
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
$$;

COMMENT ON FUNCTION fn_resumen_semanal_ra_pesadas_levante(integer, integer, integer, integer[], text, boolean)
IS 'Informe RA Pesadas — hoja RESUMEN SEMANAL, bloque Levante: una fila por lote para una semana calendario (WEEKNUM Excel). Set-based; equivalente 1:1 al Detalle de fn_reporte_semanal_levante_extras.';
");

            migrationBuilder.Sql(@"-- ============================================================================
-- fn_resumen_semanal_ra_pesadas_produccion(...)
-- Hoja «RESUMEN SEMANAL» del Informe RA Pesadas — BLOQUE PRODUCCIÓN AP.
-- ----------------------------------------------------------------------------
-- UNA FILA POR LOTE para UNA semana CALENDARIO. Hermana de
-- fn_resumen_semanal_ra_pesadas_levante; misma convención de semana del año
-- (WEEKNUM de Excel, ver cabecera de la fn de levante).
--
-- ⚠️ Set-based A PROPÓSITO. Prohibido iterar lotes desde C# llamando la fn
--    por-lote: la BD filtra, el backend orquesta (regla del repo).
--
-- EQUIVALENCIA CON EL DETALLE (fn_indicadores_produccion_postura, **flujo LPP**):
--   ⚠️ El Detalle (ReporteTecnicoSemanalService.Produccion) llama la fn base con
--      `lpp.LotePosturaProduccionId`, NO con lote_id ⇒ hay que replicar el flujo
--      LPP, que resuelve distinto que el flujo legacy por lote. Usar
--      lotes.fecha_inicio_produccion aquí daría OTRA semana de vida y el Resumen
--      no cuadraría con el Detalle.
--   * unidad      = lote_postura_produccion (lpp), scope por company + deleted_at
--   * fecha ref   = COALESCE(lev.fecha_encaset, lpp.fecha_encaset,
--                            lpp.fecha_inicio_produccion)  ← encaset del levante
--                   ligado PRIMERO. Sin fecha ⇒ lote fuera.
--   * base aves   = COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0)
--                   COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0)
--   * fuentes     = seguimiento_diario_levante (tipo_seguimiento='produccion')
--                   UNION seguimiento_diario_produccion, ambas filtradas por
--                   lote_postura_produccion_id, DEDUPLICADAS POR DÍA
--                   (calendario Bogotá): gana el registro de timestamp MÁS
--                   TEMPRANO del día — idéntico al DISTINCT ON de la fn base.
--   * semana vida = ((reg_date − fecha_ref) / 7) + 1  (división ENTERA, como la
--                   fn base; equivale a floor() con fechas no negativas)
--   * arranque    = semana 25 (REQ-012b)
--   * saldos      = hembras: −(mort + sel);  machos: −mort  (los machos NO
--                   tienen selección en esta fn — desviación preservada)
--   * %           = todos los porcentajes semanales van sobre el saldo al
--                   INICIO de la semana (pre-decremento), igual que la fn base
--
-- FÓRMULAS (verificadas contra la fn base y ReporteTecnicoSemanalCalculos):
--   %Prod        = (huevo_tot / días) / saldo_hembras_inicio * 100
--   HTAA         = Σ huevo_tot hasta la semana / hembras_iniciales
--   HIAA         = Σ huevo_inc hasta la semana / hembras_iniciales
--   %AprovSem    = huevo_inc * 100 / huevo_tot
--   GrHuevoInc   = (cons_kg_h + cons_kg_m) * 1000 / huevo_inc
--   %MortH       = mort_h / saldo_hembras_inicio * 100
--   %RetiroH     = Σ(mort_h + sel_h) / hembras_iniciales * 100
--   %RetiroM     = Σ mort_m / machos_iniciales * 100
--   PesoM/H      = peso_m / peso_h * 100   (pesos normalizados a kg: >100 ⇒ /1000)
--   Dif*         = fn_dif_pct(real, guía) = (real − guía) / guía * 100
--
-- Guía: guia_genetica_sanmarino_colombia por (company, raza, año, edad = semana
-- de vida). Todo TEXT ⇒ f_safe_numeric.
-- ============================================================================
DROP FUNCTION IF EXISTS fn_resumen_semanal_ra_pesadas_produccion(integer, integer, integer, integer[], text, text);

CREATE OR REPLACE FUNCTION fn_resumen_semanal_ra_pesadas_produccion(
    p_company_id  integer,
    p_anio        integer,
    p_sem_anio    integer,
    p_granja_ids  integer[] DEFAULT NULL,
    p_regional    text      DEFAULT NULL,
    p_ciclo       text      DEFAULT NULL
)
RETURNS TABLE(
    lote_postura_produccion_id integer,
    lote_id                   integer,
    lote_nombre               text,
    granja_id                 integer,
    granja_nombre             text,
    nucleo_nombre             text,
    regional                  text,
    raza                      text,
    anio_guia                 integer,
    ciclo_produccion          text,
    tipo_nido                 text,
    edad_semana               integer,
    fecha_fin_semana          date,
    dias_con_registro         integer,
    part                      double precision,
    saldo_hembras             double precision,
    saldo_machos              double precision,
    produccion_pct            double precision,
    produccion_pct_guia       double precision,
    dif_produccion_pct        double precision,
    htaa                      double precision,
    htaa_guia                 double precision,
    dif_htaa                  double precision,
    hiaa                      double precision,
    hiaa_guia                 double precision,
    dif_hiaa                  double precision,
    aprov_sem_pct             double precision,
    aprov_sem_pct_guia        double precision,
    dif_aprov_sem_pct         double precision,
    gr_huevo_inc              double precision,
    mort_hembras_pct          double precision,
    retiro_acum_hembras_pct   double precision,
    retiro_acum_hembras_guia  double precision,
    mort_machos_pct           double precision,
    retiro_acum_machos_pct    double precision,
    retiro_acum_machos_guia   double precision,
    peso_macho_sobre_hembra   double precision
)
LANGUAGE sql STABLE AS $$
WITH
-- ── 1) Lotes de producción (LPP) de la empresa (+ ubicación, guía y fecha ref) ─
lote_base AS (
    SELECT lpp.lote_postura_produccion_id,
           lpp.lote_id,
           lpp.lote_nombre::text                                        AS lote_nombre,
           lpp.granja_id,
           f.name::text                                                 AS granja_nombre,
           n.nucleo_nombre::text                                        AS nucleo_nombre,
           COALESCE(NULLIF(mo.value, ''), NULLIF(lpp.regional, ''))::text AS regional,
           COALESCE(lpp.raza, '')::text                                 AS raza,
           lpp.ano_tabla_genetica                                       AS anio_guia,
           NULLIF(lpp.ciclo_produccion, '')::text                       AS ciclo_produccion,
           NULLIF(lpp.tipo_nido, '')::text                              AS tipo_nido,
           (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date                    AS ref_date,
           COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0)::double precision AS base_h,
           COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0)::double precision  AS base_m
      FROM lote_postura_produccion lpp
      JOIN farms f
        ON f.id = lpp.granja_id
      LEFT JOIN lote_postura_levante lev
        ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
       AND lev.deleted_at IS NULL
      LEFT JOIN nucleos n
        ON n.granja_id = lpp.granja_id
       AND n.nucleo_id = lpp.nucleo_id
       AND n.deleted_at IS NULL
      LEFT JOIN master_list_options mo
        ON mo.id = f.regional_id
     WHERE lpp.company_id = p_company_id
       AND lpp.deleted_at IS NULL
       AND (p_granja_ids IS NULL OR lpp.granja_id = ANY (p_granja_ids))
       AND (p_ciclo IS NULL OR COALESCE(lpp.ciclo_produccion, '') = p_ciclo)
),
lote_ok AS (
    SELECT * FROM lote_base WHERE ref_date IS NOT NULL
),
-- ── 2) Registros crudos de las DOS fuentes ──────────────────────────────────
crudos AS (
    SELECT lo.lote_postura_produccion_id                   AS lpp_id,
           sd.fecha                                        AS ts,
           COALESCE(sd.mortalidad_hembras, 0)              AS mort_h,
           COALESCE(sd.mortalidad_machos, 0)               AS mort_m,
           COALESCE(sd.sel_h, 0)                           AS sel_h,
           COALESCE(sd.consumo_kg_hembras, 0)::double precision AS cons_h,
           COALESCE(sd.consumo_kg_machos, 0)::double precision  AS cons_m,
           COALESCE(sd.huevo_tot, 0)                       AS huevo_tot,
           COALESCE(sd.huevo_inc, 0)                       AS huevo_inc,
           sd.peso_h::double precision                     AS peso_h,
           sd.peso_m::double precision                     AS peso_m
      FROM lote_ok lo
      JOIN seguimiento_diario_levante sd
        ON sd.lote_postura_produccion_id = lo.lote_postura_produccion_id
       AND sd.tipo_seguimiento = 'produccion'
    UNION ALL
    SELECT lo.lote_postura_produccion_id                   AS lpp_id,
           sp.fecha_registro                               AS ts,
           COALESCE(sp.mortalidad_hembras, 0),
           COALESCE(sp.mortalidad_machos, 0),
           COALESCE(sp.sel_h, 0),
           COALESCE(sp.cons_kg_h, 0)::double precision,
           COALESCE(sp.cons_kg_m, 0)::double precision,
           COALESCE(sp.huevo_tot, 0),
           COALESCE(sp.huevo_inc, 0),
           sp.peso_h::double precision,
           sp.peso_m::double precision
      FROM lote_ok lo
      JOIN seguimiento_diario_produccion sp
        ON sp.lote_postura_produccion_id = lo.lote_postura_produccion_id
),
-- ── 3) Un registro por lote y DÍA: gana el timestamp más temprano ───────────
dedup AS (
    SELECT DISTINCT ON (c.lpp_id, (c.ts AT TIME ZONE 'America/Bogota')::date)
           c.lpp_id,
           (c.ts AT TIME ZONE 'America/Bogota')::date AS reg_date,
           c.mort_h, c.mort_m, c.sel_h, c.cons_h, c.cons_m,
           c.huevo_tot, c.huevo_inc, c.peso_h, c.peso_m
      FROM crudos c
     ORDER BY c.lpp_id, (c.ts AT TIME ZONE 'America/Bogota')::date, c.ts
),
-- ── 4) Semana de vida (división entera, arranque en 25) ─────────────────────
reg AS (
    SELECT d.*,
           ((d.reg_date - lo.ref_date) / 7) + 1 AS sem
      FROM dedup d
      JOIN lote_ok lo ON lo.lote_postura_produccion_id = d.lpp_id
),
reg_ok AS (
    SELECT * FROM reg WHERE sem >= 25
),
-- ── 5) Agregado semanal por lote ────────────────────────────────────────────
sem AS (
    SELECT lpp_id,
           sem,
           COUNT(*)::int                     AS dias,
           SUM(mort_h)::double precision     AS mort_h,
           SUM(mort_m)::double precision     AS mort_m,
           SUM(sel_h)::double precision      AS sel_h,
           SUM(cons_h)                       AS cons_kg_h,
           SUM(cons_m)                       AS cons_kg_m,
           SUM(huevo_tot)::double precision  AS huevo_tot,
           SUM(huevo_inc)::double precision  AS huevo_inc,
           AVG(peso_h) FILTER (WHERE peso_h IS NOT NULL) AS peso_h,
           AVG(peso_m) FILTER (WHERE peso_m IS NOT NULL) AS peso_m
      FROM reg_ok
     GROUP BY lpp_id, sem
),
-- ── 6) Acumulados por ventana ──────────────────────────────────────────────
--    `*_prev` = acumulado EXCLUYENDO la semana actual ⇒ saldo al INICIO de la
--    semana, que es el denominador de todos los % (igual que la fn base).
acum AS (
    SELECT s.*,
           COALESCE(SUM(s.mort_h + s.sel_h) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS bajas_h_prev,
           COALESCE(SUM(s.mort_m) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS bajas_m_prev,
           SUM(s.mort_h + s.sel_h) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS bajas_h_ac,
           SUM(s.mort_m) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS bajas_m_ac,
           SUM(s.huevo_tot) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS huevo_tot_ac,
           SUM(s.huevo_inc) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS huevo_inc_ac
      FROM sem s
),
-- ── 7) Solo la semana calendario pedida ────────────────────────────────────
sem_objetivo AS (
    SELECT lo.lote_postura_produccion_id, lo.lote_id, lo.lote_nombre, lo.granja_id, lo.granja_nombre,
           lo.nucleo_nombre, lo.regional, lo.raza, lo.anio_guia,
           lo.ciclo_produccion, lo.tipo_nido, lo.base_h, lo.base_m,
           a.sem, a.dias, a.mort_h, a.mort_m, a.sel_h,
           a.cons_kg_h, a.cons_kg_m, a.huevo_tot, a.huevo_inc,
           a.peso_h, a.peso_m,
           a.bajas_h_prev, a.bajas_m_prev, a.bajas_h_ac, a.bajas_m_ac,
           a.huevo_tot_ac, a.huevo_inc_ac,
           (lo.ref_date + ((a.sem - 1) * 7) + 6) AS fin_sem
      FROM acum a
      JOIN lote_ok lo ON lo.lote_postura_produccion_id = a.lpp_id
     WHERE EXTRACT(YEAR FROM (lo.ref_date + ((a.sem - 1) * 7) + 6))::int = p_anio
       AND (
             floor(
               ( (lo.ref_date + ((a.sem - 1) * 7) + 6)
                 - date_trunc('year', (lo.ref_date + ((a.sem - 1) * 7) + 6)::timestamp)::date
                 + EXTRACT(DOW FROM date_trunc('year', (lo.ref_date + ((a.sem - 1) * 7) + 6)::timestamp))::int
               ) / 7.0
             )::int + 1
           ) = p_sem_anio
       AND (p_regional IS NULL OR lo.regional = p_regional)
),
-- ── 8) Guía ────────────────────────────────────────────────────────────────
con_guia AS (
    SELECT so.*,
           f_safe_numeric(g.prod_porcentaje) AS g_prod_pct,
           f_safe_numeric(g.h_total_aa)      AS g_htaa,
           f_safe_numeric(g.h_inc_aa)        AS g_hiaa,
           f_safe_numeric(g.aprov_sem)       AS g_aprov_sem,
           f_safe_numeric(g.retiro_ac_h)     AS g_retiro_ac_h,
           f_safe_numeric(g.retiro_ac_m)     AS g_retiro_ac_m
      FROM sem_objetivo so
      LEFT JOIN LATERAL (
            SELECT gg.*
              FROM guia_genetica_sanmarino_colombia gg
             WHERE gg.company_id = p_company_id
               AND gg.deleted_at IS NULL
               AND lower(trim(gg.raza)) = lower(trim(COALESCE(so.raza, '')))
               AND trim(gg.anio_guia) = so.anio_guia::text
               -- ⚠️ Edad PARSEADA a número, igual que fn_indicadores_produccion_postura
               --    (`fn_parse_edad_numerica(g.edad) = s`) — distinto de la fn de
               --    levante, que compara texto exacto.
               --    La semana 25 tiene DOS filas en la guía y ambas parsean a 25:
               --      '25'  → cierre de LEVANTE   (retiro_ac_h 4,03)
               --      '25P' → arranque de PRODUCCIÓN (retiro_ac_h 0,10)
               --    La fn base resuelve con LIMIT 1 sin ORDER BY y el plan (index
               --    scan por company_id) le devuelve la fila '25P'. Acá el desempate
               --    se hace EXPLÍCITO —prefiriendo la variante con sufijo, que es la
               --    de producción— para no depender del plan de ejecución.
               AND fn_parse_edad_numerica(gg.edad) = so.sem
             ORDER BY (CASE WHEN btrim(gg.edad) = so.sem::text THEN 1 ELSE 0 END), gg.id
             LIMIT 1
      ) g ON true
),
-- ── 9) Saldos, pesos normalizados y derivadas ──────────────────────────────
calc AS (
    SELECT cg.*,
           GREATEST(0, cg.base_h - cg.bajas_h_prev) AS ini_h,
           GREATEST(0, cg.base_m - cg.bajas_m_prev) AS ini_m,
           GREATEST(0, cg.base_h - cg.bajas_h_ac)   AS fin_h,
           GREATEST(0, cg.base_m - cg.bajas_m_ac)   AS fin_m,
           CASE WHEN cg.peso_h IS NULL THEN NULL
                WHEN cg.peso_h > 100 THEN cg.peso_h / 1000.0 ELSE cg.peso_h END AS peso_h_kg,
           CASE WHEN cg.peso_m IS NULL THEN NULL
                WHEN cg.peso_m > 100 THEN cg.peso_m / 1000.0 ELSE cg.peso_m END AS peso_m_kg
      FROM con_guia cg
),
final AS (
    SELECT c.*,
           CASE WHEN c.dias > 0 AND c.ini_h > 0
                THEN (c.huevo_tot / c.dias) / c.ini_h * 100.0 END        AS prod_pct,
           CASE WHEN c.base_h > 0 THEN c.huevo_tot_ac / c.base_h END     AS htaa_real,
           CASE WHEN c.base_h > 0 THEN c.huevo_inc_ac / c.base_h END     AS hiaa_real,
           CASE WHEN c.huevo_tot > 0 THEN c.huevo_inc * 100.0 / c.huevo_tot END AS aprov_pct,
           CASE WHEN c.huevo_inc > 0
                THEN (c.cons_kg_h + c.cons_kg_m) * 1000.0 / c.huevo_inc END AS gr_huevo_inc_real
      FROM calc c
)
SELECT
    f.lote_postura_produccion_id,
    f.lote_id,
    f.lote_nombre,
    f.granja_id,
    f.granja_nombre,
    f.nucleo_nombre,
    f.regional,
    f.raza,
    f.anio_guia,
    f.ciclo_produccion,
    f.tipo_nido,
    f.sem                                                          AS edad_semana,
    f.fin_sem                                                      AS fecha_fin_semana,
    f.dias                                                         AS dias_con_registro,
    CASE WHEN SUM(f.fin_h) OVER () > 0
         THEN f.fin_h / SUM(f.fin_h) OVER () END                   AS part,
    f.fin_h                                                        AS saldo_hembras,
    f.fin_m                                                        AS saldo_machos,
    f.prod_pct                                                     AS produccion_pct,
    f.g_prod_pct::double precision                                 AS produccion_pct_guia,
    fn_dif_pct(f.prod_pct, f.g_prod_pct::double precision)         AS dif_produccion_pct,
    f.htaa_real                                                    AS htaa,
    f.g_htaa::double precision                                     AS htaa_guia,
    fn_dif_pct(f.htaa_real, f.g_htaa::double precision)            AS dif_htaa,
    f.hiaa_real                                                    AS hiaa,
    f.g_hiaa::double precision                                     AS hiaa_guia,
    fn_dif_pct(f.hiaa_real, f.g_hiaa::double precision)            AS dif_hiaa,
    f.aprov_pct                                                    AS aprov_sem_pct,
    f.g_aprov_sem::double precision                                AS aprov_sem_pct_guia,
    fn_dif_pct(f.aprov_pct, f.g_aprov_sem::double precision)       AS dif_aprov_sem_pct,
    f.gr_huevo_inc_real                                            AS gr_huevo_inc,
    CASE WHEN f.ini_h > 0 THEN f.mort_h / f.ini_h * 100.0 END      AS mort_hembras_pct,
    CASE WHEN f.base_h > 0 THEN f.bajas_h_ac / f.base_h * 100.0 END AS retiro_acum_hembras_pct,
    f.g_retiro_ac_h::double precision                              AS retiro_acum_hembras_guia,
    CASE WHEN f.ini_m > 0 THEN f.mort_m / f.ini_m * 100.0 END      AS mort_machos_pct,
    CASE WHEN f.base_m > 0 THEN f.bajas_m_ac / f.base_m * 100.0 END AS retiro_acum_machos_pct,
    f.g_retiro_ac_m::double precision                              AS retiro_acum_machos_guia,
    CASE WHEN COALESCE(f.peso_h_kg, 0) > 0 AND f.peso_m_kg IS NOT NULL
         THEN f.peso_m_kg / f.peso_h_kg * 100.0 END                AS peso_macho_sobre_hembra
  FROM final f
 ORDER BY f.sem DESC, f.lote_nombre;
$$;

COMMENT ON FUNCTION fn_resumen_semanal_ra_pesadas_produccion(integer, integer, integer, integer[], text, text)
IS 'Informe RA Pesadas — hoja RESUMEN SEMANAL, bloque Producción: una fila por lote para una semana calendario (WEEKNUM Excel). Set-based; equivalente 1:1 a fn_indicadores_produccion_postura (flujo lote).';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP FUNCTION IF EXISTS fn_resumen_semanal_ra_pesadas_levante(integer, integer, integer, integer[], text, boolean);");
            migrationBuilder.Sql(
                @"DROP FUNCTION IF EXISTS fn_resumen_semanal_ra_pesadas_produccion(integer, integer, integer, integer[], text, text);");
        }
    }
}
