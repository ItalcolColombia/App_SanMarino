-- ============================================================================
-- Vista para Power BI (REQ-005) — Comparativo Real vs Guía Genética por lote+semana
-- ----------------------------------------------------------------------------
-- Expone, para cada lote de postura (levante y producción) de San Marino Colombia,
-- la fila de guía genética (guia_genetica_sanmarino_colombia) que le corresponde por
-- Raza + Año genético + Edad(semana). Power BI cruza esto contra los datos reales
-- (seguimiento diario) por lote + semana para el comparativo por línea de producción.
--
-- Idempotente. No cambia schema. Semana = edad (semanas de vida).
-- La guía se almacena como texto (puede traer '', '-', o coma decimal) → se castea seguro.
--
-- DEPLOY (REQ-005): este .sql es la SPEC. NO se aplica corriéndolo a mano en prod.
-- Se aplica envolviendo su contenido tal cual en `migrationBuilder.Sql(@"...")`
-- dentro de una migración EF nueva (mismo patrón que
-- 20260703120000_AddFnIndicadoresProduccionPostura.cs: Up() = CREATE OR REPLACE
-- FUNCTION + CREATE OR REPLACE VIEW; Down() = DROP VIEW/FUNCTION IF EXISTS). La
-- migración la crea el coordinador; este archivo queda como fuente de verdad para
-- copiar/pegar dentro de Up().
-- ============================================================================

-- Cast seguro a numeric: tolera vacío, '-', y coma decimal.
CREATE OR REPLACE FUNCTION public.f_safe_numeric(v text) RETURNS numeric AS $$
  SELECT CASE
           WHEN replace(trim(coalesce(v, '')), ',', '.') ~ '^-?[0-9]+(\.[0-9]+)?$'
           THEN replace(trim(v), ',', '.')::numeric
           ELSE NULL
         END;
$$ LANGUAGE sql IMMUTABLE;

CREATE OR REPLACE VIEW public.vw_guia_genetica_por_lote_postura AS
WITH guia AS (
    SELECT
        g.company_id,
        g.raza,
        g.anio_guia,
        NULLIF(regexp_replace(g.edad, '[^0-9]', '', 'g'), '')::int AS semana,
        public.f_safe_numeric(g.gr_ave_dia_h)   AS gr_ave_dia_h,
        public.f_safe_numeric(g.gr_ave_dia_m)   AS gr_ave_dia_m,
        public.f_safe_numeric(g.cons_ac_h)      AS cons_ac_h,
        public.f_safe_numeric(g.cons_ac_m)      AS cons_ac_m,
        public.f_safe_numeric(g.peso_h)         AS peso_h,
        public.f_safe_numeric(g.peso_m)         AS peso_m,
        public.f_safe_numeric(g.uniformidad)    AS uniformidad,
        public.f_safe_numeric(g.mort_sem_h)     AS mort_sem_h,
        public.f_safe_numeric(g.mort_sem_m)     AS mort_sem_m,
        public.f_safe_numeric(g.retiro_ac_h)    AS retiro_ac_h,
        public.f_safe_numeric(g.retiro_ac_m)    AS retiro_ac_m,
        public.f_safe_numeric(g.h_total_aa)     AS h_total_aa,
        public.f_safe_numeric(g.h_inc_aa)       AS h_inc_aa,
        public.f_safe_numeric(g.prod_porcentaje) AS prod_porcentaje,
        public.f_safe_numeric(g.aprov_sem)      AS aprov_sem,
        public.f_safe_numeric(g.peso_huevo)     AS peso_huevo,
        public.f_safe_numeric(g.apareo)         AS apareo
    FROM public.guia_genetica_sanmarino_colombia g
    WHERE g.deleted_at IS NULL
      AND g.edad ~ '^[0-9]'
)
SELECT
    'Levante'::text                    AS etapa,
    l.lote_postura_levante_id          AS lote_postura_id,
    l.lote_id                          AS lote_id,
    l.lote_nombre                      AS lote_nombre,
    l.company_id                       AS company_id,
    l.regional                         AS regional,
    l.raza                             AS raza,
    l.ano_tabla_genetica               AS ano_tabla_genetica,
    gu.semana                          AS semana,
    gu.gr_ave_dia_h, gu.gr_ave_dia_m, gu.cons_ac_h, gu.cons_ac_m,
    gu.peso_h, gu.peso_m, gu.uniformidad, gu.mort_sem_h, gu.mort_sem_m,
    gu.retiro_ac_h, gu.retiro_ac_m,
    NULL::numeric AS h_total_aa, NULL::numeric AS h_inc_aa, NULL::numeric AS prod_porcentaje,
    NULL::numeric AS aprov_sem, NULL::numeric AS peso_huevo, NULL::numeric AS apareo
FROM public.lote_postura_levante l
JOIN guia gu
  ON gu.company_id = l.company_id
 AND gu.raza = l.raza
 AND gu.anio_guia = l.ano_tabla_genetica::text
 AND gu.semana BETWEEN 1 AND 25
WHERE l.deleted_at IS NULL

UNION ALL

SELECT
    'Produccion'::text                 AS etapa,
    p.lote_postura_produccion_id       AS lote_postura_id,
    p.lote_id                          AS lote_id,
    p.lote_nombre                      AS lote_nombre,
    p.company_id                       AS company_id,
    p.regional                         AS regional,
    p.raza                             AS raza,
    p.ano_tabla_genetica               AS ano_tabla_genetica,
    gu.semana                          AS semana,
    gu.gr_ave_dia_h, gu.gr_ave_dia_m, gu.cons_ac_h, gu.cons_ac_m,
    gu.peso_h, gu.peso_m, gu.uniformidad, gu.mort_sem_h, gu.mort_sem_m,
    gu.retiro_ac_h, gu.retiro_ac_m,
    gu.h_total_aa, gu.h_inc_aa, gu.prod_porcentaje, gu.aprov_sem, gu.peso_huevo, gu.apareo
FROM public.lote_postura_produccion p
JOIN guia gu
  ON gu.company_id = p.company_id
 AND gu.raza = p.raza
 AND gu.anio_guia = p.ano_tabla_genetica::text
 AND gu.semana >= 26
WHERE p.deleted_at IS NULL;
