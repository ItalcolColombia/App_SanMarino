-- ============================================================================
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
