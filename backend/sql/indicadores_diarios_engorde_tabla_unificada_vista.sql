-- =============================================================================
-- Vista: indicadores diarios pollo engorde vs guía genética Ecuador (mixto)
-- =============================================================================
-- Misma lógica que indicadores_diarios_engorde_tabla_unificada.sql sin CTE params:
-- incluye todas las compañías / lotes no eliminados. Filtrar en consulta:
--   WHERE company_id = 2
--   AND lote_ave_engorde_id = 12345   -- opcional
--
-- Columnas de contexto: company_id, empresa_nombre (companies.name), granja_id/nombre,
--   galpon_id/nombre, nucleo_id/nombre.
--
-- Requisitos en lote: fecha_encaset; raza + ano_tabla_genetica para enlazar guía.
-- Ver comentarios en el SQL parametrizado sobre consumo (hembras+machos) y ganancia.
-- =============================================================================

CREATE OR REPLACE VIEW public.vw_indicadores_diarios_engorde_unificado AS
WITH
lote_filtrado AS (
  SELECT
    l.lote_ave_engorde_id,
    l.company_id,
    COALESCE(co.name, l.empresa_nombre) AS empresa_nombre,
    l.lote_nombre,
    l.granja_id,
    fa.name AS granja_nombre,
    l.galpon_id,
    gp.galpon_nombre,
    l.nucleo_id,
    nu.nucleo_nombre,
    l.fecha_encaset,
    TRIM(l.raza) AS raza,
    l.ano_tabla_genetica,
    l.peso_mixto::double precision AS peso_mixto,
    l.peso_inicial_h::double precision AS peso_inicial_h,
    l.peso_inicial_m::double precision AS peso_inicial_m,
    CASE
      WHEN COALESCE(l.aves_encasetadas, 0) > 0 THEN l.aves_encasetadas::bigint
      WHEN COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0) > 0
        THEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0))::bigint
      ELSE 0::bigint
    END AS aves_iniciales
  FROM public.lote_ave_engorde l
  LEFT JOIN public.companies co ON co.id = l.company_id
  LEFT JOIN public.farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
  LEFT JOIN public.nucleos nu ON nu.nucleo_id = l.nucleo_id AND nu.granja_id = l.granja_id
  LEFT JOIN public.galpones gp ON gp.galpon_id = l.galpon_id AND gp.granja_id = l.granja_id
  WHERE l.deleted_at IS NULL
),

seg_agregado AS (
  SELECT
    s.lote_ave_engorde_id,
    s.fecha::date AS fecha_registro,
    SUM(COALESCE(s.mortalidad_hembras, 0)) AS sum_mort_h,
    SUM(COALESCE(s.mortalidad_machos, 0)) AS sum_mort_m,
    SUM(COALESCE(s.sel_h, 0)) AS sum_sel_h,
    SUM(COALESCE(s.sel_m, 0)) AS sum_sel_m,
    SUM(COALESCE(s.error_sexaje_hembras, 0)) AS sum_err_h,
    SUM(COALESCE(s.error_sexaje_machos, 0)) AS sum_err_m,
    SUM(COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::numeric AS consumo_kg_dia
  FROM public.seguimiento_diario_aves_engorde s
  INNER JOIN lote_filtrado l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
  GROUP BY s.lote_ave_engorde_id, s.fecha::date
),

seg_peso_ultimo AS (
  SELECT DISTINCT ON (s.lote_ave_engorde_id, s.fecha::date)
    s.lote_ave_engorde_id,
    s.fecha::date AS fecha_registro,
    s.peso_prom_hembras::double precision AS peso_h,
    s.peso_prom_machos::double precision AS peso_m
  FROM public.seguimiento_diario_aves_engorde s
  INNER JOIN lote_filtrado l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
  ORDER BY s.lote_ave_engorde_id, s.fecha::date, s.id DESC
),

dia_base AS (
  SELECT
    l.company_id,
    l.empresa_nombre,
    l.lote_ave_engorde_id,
    l.lote_nombre,
    l.granja_id,
    l.granja_nombre,
    l.galpon_id,
    l.galpon_nombre,
    l.nucleo_id,
    l.nucleo_nombre,
    l.raza,
    l.ano_tabla_genetica,
    a.fecha_registro,
    GREATEST(0, (a.fecha_registro - l.fecha_encaset::date))::int AS dia_vida,
    l.aves_iniciales,
    CASE
      WHEN l.peso_mixto IS NOT NULL AND l.peso_mixto > 0 THEN l.peso_mixto::numeric
      WHEN COALESCE(l.peso_inicial_h, 0) > 0 AND COALESCE(l.peso_inicial_m, 0) > 0
        THEN ((l.peso_inicial_h + l.peso_inicial_m) / 2.0)::numeric
      ELSE COALESCE(l.peso_inicial_h, l.peso_inicial_m, 0)::numeric
    END AS peso_inicial_mixto_g,
    a.sum_mort_h + a.sum_mort_m + a.sum_sel_h + a.sum_sel_m + a.sum_err_h + a.sum_err_m AS perdidas_dia,
    a.sum_mort_h + a.sum_mort_m + a.sum_sel_h + a.sum_sel_m AS mort_sel_dia,
    a.consumo_kg_dia,
    CASE
      WHEN COALESCE(p.peso_h, 0) > 0 AND COALESCE(p.peso_m, 0) > 0 THEN ((p.peso_h + p.peso_m) / 2.0)::numeric
      ELSE COALESCE(NULLIF(p.peso_h, 0), NULLIF(p.peso_m, 0), 0::double precision)::numeric
    END AS peso_mixto_dia_g
  FROM seg_agregado a
  INNER JOIN lote_filtrado l ON l.lote_ave_engorde_id = a.lote_ave_engorde_id
  INNER JOIN seg_peso_ultimo p
    ON p.lote_ave_engorde_id = a.lote_ave_engorde_id
   AND p.fecha_registro = a.fecha_registro
  WHERE l.fecha_encaset IS NOT NULL
),

con_aves AS (
  SELECT
    d.*,
    COALESCE(
      SUM(d.perdidas_dia) OVER (
        PARTITION BY d.lote_ave_engorde_id
        ORDER BY d.fecha_registro
        ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
      ),
      0
    )::bigint AS perdidas_acum_prev,
    SUM(d.perdidas_dia) OVER (
      PARTITION BY d.lote_ave_engorde_id
      ORDER BY d.fecha_registro
      ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    )::bigint AS perdidas_acum_total
  FROM dia_base d
),

con_aves2 AS (
  SELECT
    c.*,
    GREATEST(0, c.aves_iniciales - c.perdidas_acum_prev)::bigint AS aves_inicio_dia,
    GREATEST(0, c.aves_iniciales - c.perdidas_acum_total)::bigint AS aves_fin_dia
  FROM con_aves c
),

con_guia AS (
  SELECT
    c.*,
    gh.id AS guia_genetica_ecuador_header_id,
    gd.peso_corporal_g::numeric AS peso_tabla_g,
    gd.ganancia_diaria_g::numeric AS ganancia_diaria_tabla_g,
    gd.cantidad_alimento_diario_g::numeric AS consumo_diario_tabla_g,
    gd.alimento_acumulado_g::numeric AS alimento_acum_tabla_g,
    gd.ca::numeric AS ca_tabla,
    gd.mortalidad_seleccion_diaria::numeric AS mort_sel_tabla_pct
  FROM con_aves2 c
  LEFT JOIN public.guia_genetica_ecuador_header gh
    ON gh.company_id = c.company_id
   AND TRIM(LOWER(gh.raza)) = TRIM(LOWER(COALESCE(c.raza, '')))
   AND gh.anio_guia = c.ano_tabla_genetica
   AND c.ano_tabla_genetica IS NOT NULL
   AND TRIM(COALESCE(c.raza, '')) <> ''
   AND gh.estado = 'active'
   AND gh.deleted_at IS NULL
  LEFT JOIN LATERAL (
    SELECT d.*
    FROM public.guia_genetica_ecuador_detalle d
    WHERE d.guia_genetica_ecuador_header_id = gh.id
      AND LOWER(TRIM(d.sexo)) = 'mixto'
      AND d.deleted_at IS NULL
      AND d.dia <= c.dia_vida
    ORDER BY d.dia DESC
    LIMIT 1
  ) gd ON TRUE
),

con_calc AS (
  SELECT
    g.*,
    CASE
      WHEN g.aves_inicio_dia > 0 THEN (g.consumo_kg_dia * 1000.0) / g.aves_inicio_dia::numeric
      ELSE 0::numeric
    END AS consumo_diario_real_g,
    LAG(g.peso_mixto_dia_g) OVER (PARTITION BY g.lote_ave_engorde_id ORDER BY g.fecha_registro) AS peso_mixto_dia_prev,
    SUM(
      CASE
        WHEN g.aves_inicio_dia > 0 THEN (g.consumo_kg_dia * 1000.0) / g.aves_inicio_dia::numeric
        ELSE 0::numeric
      END
    ) OVER (
      PARTITION BY g.lote_ave_engorde_id
      ORDER BY g.fecha_registro
      ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS alimento_acum_real_g
  FROM con_guia g
),

final AS (
  SELECT
    c.company_id,
    c.empresa_nombre,
    c.lote_ave_engorde_id,
    c.lote_nombre,
    c.granja_id,
    c.granja_nombre,
    c.galpon_id,
    c.galpon_nombre,
    c.nucleo_id,
    c.nucleo_nombre,
    c.raza,
    c.ano_tabla_genetica,
    c.guia_genetica_ecuador_header_id,
    c.fecha_registro,
    c.dia_vida,
    c.aves_iniciales,
    c.aves_inicio_dia,
    c.aves_fin_dia,
    c.peso_inicial_mixto_g,
    c.peso_mixto_dia_g AS peso_real_g,
    c.peso_tabla_g,
    CASE
      WHEN c.peso_mixto_dia_g > 0 AND c.peso_mixto_dia_prev IS NOT NULL
        THEN c.peso_mixto_dia_g - c.peso_mixto_dia_prev
      WHEN c.peso_mixto_dia_g > 0 AND c.peso_mixto_dia_prev IS NULL
        THEN c.peso_mixto_dia_g - c.peso_inicial_mixto_g
      ELSE NULL::numeric
    END AS ganancia_diaria_real_g,
    c.ganancia_diaria_tabla_g,
    c.consumo_diario_real_g,
    c.consumo_diario_tabla_g,
    c.alimento_acum_real_g,
    c.alimento_acum_tabla_g,
    CASE
      WHEN c.peso_mixto_dia_g > 0 AND c.alimento_acum_real_g > 0
        THEN c.alimento_acum_real_g / NULLIF(c.peso_mixto_dia_g, 0)
      ELSE NULL::numeric
    END AS ca_real,
    c.ca_tabla,
    CASE
      WHEN c.aves_inicio_dia > 0 THEN (c.mort_sel_dia::numeric * 100.0) / c.aves_inicio_dia::numeric
      ELSE 0::numeric
    END AS mort_sel_real_pct,
    c.mort_sel_tabla_pct,
    CASE
      WHEN c.peso_tabla_g > 0 AND c.peso_mixto_dia_g > 0
        THEN ((c.peso_mixto_dia_g - c.peso_tabla_g) / NULLIF(c.peso_tabla_g, 0)) * 100.0
      ELSE 0::numeric
    END AS dif_peso_vs_tabla_pct,
    CASE
      WHEN c.aves_iniciales > 0
        THEN (c.perdidas_acum_total::numeric * 100.0) / c.aves_iniciales::numeric
      ELSE 0::numeric
    END AS mort_acum_pct
  FROM con_calc c
)

SELECT
  f.company_id,
  f.empresa_nombre,
  f.lote_ave_engorde_id,
  f.lote_nombre,
  f.granja_id,
  f.granja_nombre,
  f.galpon_id,
  f.galpon_nombre,
  f.nucleo_id,
  f.nucleo_nombre,
  f.raza,
  f.ano_tabla_genetica,
  f.guia_genetica_ecuador_header_id,
  to_char(f.fecha_registro, 'YYYY-MM-DD') AS fecha_ymd,
  f.fecha_registro,
  f.dia_vida,
  f.aves_iniciales,
  f.aves_inicio_dia,
  f.aves_fin_dia,
  trim_scale(f.peso_inicial_mixto_g) AS peso_inicial_mixto_g,
  trim_scale(f.peso_real_g) AS peso_real_g,
  trim_scale(f.peso_tabla_g) AS peso_tabla_g,
  trim_scale(f.ganancia_diaria_real_g) AS ganancia_diaria_real_g,
  trim_scale(f.ganancia_diaria_tabla_g) AS ganancia_diaria_tabla_g,
  trim_scale(f.consumo_diario_real_g) AS consumo_diario_real_g,
  trim_scale(f.consumo_diario_tabla_g) AS consumo_diario_tabla_g,
  trim_scale(f.alimento_acum_real_g) AS alimento_acum_real_g,
  trim_scale(f.alimento_acum_tabla_g) AS alimento_acum_tabla_g,
  trim_scale(f.ca_real) AS ca_real,
  trim_scale(f.ca_tabla) AS ca_tabla,
  trim_scale(f.mort_sel_real_pct) AS mort_sel_real_pct,
  trim_scale(f.mort_sel_tabla_pct) AS mort_sel_tabla_pct,
  trim_scale(f.dif_peso_vs_tabla_pct) AS dif_peso_vs_tabla_pct,
  trim_scale(f.mort_acum_pct) AS mort_acum_pct
FROM final f;

COMMENT ON VIEW public.vw_indicadores_diarios_engorde_unificado IS
  'Indicadores diarios engorde vs guía Ecuador (mixto): una fila por lote y día. Filtrar por company_id y opcionalmente lote_ave_engorde_id.';
