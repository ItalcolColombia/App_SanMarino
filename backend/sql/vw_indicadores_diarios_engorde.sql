-- =============================================================================
-- Vista: vw_indicadores_diarios_engorde  (REBUILD 2026-06-24)
-- =============================================================================
-- Alinea la vista con el indicador diario corregido del front
-- (IndicadoresDiariosEngordeComputeService) y el nuevo flujo de resolución de
-- guía (GuiaGeneticaEcuadorService.GetDatosAsync: por company_id + pais_id).
--
-- Cambios vs versión previa:
--  1. Guía emparejada por company_id + PAIS_ID (del lote) + raza + año + deleted_at IS NULL;
--     SIN exigir estado='active' (GetDatosAsync no lo exige). LATERAL LIMIT 1 → un solo header.
--  2. Consumo mixto = consumo_kg_hembras (campo mixto); solo si es 0 cae a hembras+machos.
--     (ítems generales de metadata = 0 en datos; no se replican aquí.)
--  3. Ganancia diaria real contra el ÚLTIMO peso medido > 0 (no el LAG del día anterior,
--     que se disparaba tras un día sin pesaje). Primer pesaje vs peso inicial del lote.
--  4. Aves vivas (inicio/fin) y mort_acum_pct restan también despachos de metadata
--     (sistema antiguo: despachoHembras/despachoH/despacho_hembra + variantes machos),
--     además de mortalidad + selección + error de sexaje.
-- Se conservan nombres de columnas; se agrega pais_id al final.
-- Backfill de pais_id de headers (de 0 al país de sus lotes) va en migración/script aparte.
-- =============================================================================

DROP VIEW IF EXISTS public.vw_indicadores_diarios_engorde;

CREATE VIEW public.vw_indicadores_diarios_engorde AS
WITH lote_filtrado AS (
    SELECT l.lote_ave_engorde_id,
        l.company_id,
        COALESCE(l.pais_id, 0) AS pais_id,
        COALESCE(co.name, l.empresa_nombre) AS empresa_nombre,
        l.lote_nombre,
        l.granja_id,
        fa.name AS granja_nombre,
        l.galpon_id,
        gp.galpon_nombre,
        l.nucleo_id,
        nu.nucleo_nombre,
        l.fecha_encaset,
        TRIM(BOTH FROM l.raza) AS raza,
        l.ano_tabla_genetica,
        l.peso_mixto,
        l.peso_inicial_h,
        l.peso_inicial_m,
        CASE
            WHEN COALESCE(l.aves_encasetadas, 0) > 0 THEN l.aves_encasetadas::bigint
            WHEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0)) > 0
                THEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0))::bigint
            ELSE 0::bigint
        END AS aves_iniciales
    FROM lote_ave_engorde l
        LEFT JOIN companies co ON co.id = l.company_id
        LEFT JOIN farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
        LEFT JOIN nucleos nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
        LEFT JOIN galpones gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
    WHERE l.deleted_at IS NULL
),
seg_agregado AS (
    SELECT s.lote_ave_engorde_id,
        s.fecha::date AS fecha_registro,
        sum(COALESCE(s.mortalidad_hembras, 0)) AS sum_mort_h,
        sum(COALESCE(s.mortalidad_machos, 0)) AS sum_mort_m,
        sum(COALESCE(s.sel_h, 0)) AS sum_sel_h,
        sum(COALESCE(s.sel_m, 0)) AS sum_sel_m,
        sum(COALESCE(s.error_sexaje_hembras, 0)) AS sum_err_h,
        sum(COALESCE(s.error_sexaje_machos, 0)) AS sum_err_m,
        -- Consumo mixto (corregido): hembras si >0, si no hembras+machos
        sum(CASE WHEN COALESCE(s.consumo_kg_hembras, 0) > 0
                 THEN s.consumo_kg_hembras
                 ELSE COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0) END) AS consumo_kg_dia,
        -- Despachos desde metadata (sistema antiguo): hembras + machos
        sum(
            COALESCE(NULLIF(regexp_replace(COALESCE(s.metadata->>'despachoHembras', s.metadata->>'despachoH', s.metadata->>'despacho_hembra', ''), '[^0-9.\-]', '', 'g'), '')::numeric, 0)
          + COALESCE(NULLIF(regexp_replace(COALESCE(s.metadata->>'despachoMachos', s.metadata->>'despachoM', s.metadata->>'despacho_macho', ''), '[^0-9.\-]', '', 'g'), '')::numeric, 0)
        ) AS despachos_dia
    FROM seguimiento_diario_aves_engorde s
        JOIN lote_filtrado l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
    GROUP BY s.lote_ave_engorde_id, (s.fecha::date)
),
seg_peso_ultimo AS (
    SELECT DISTINCT ON (s.lote_ave_engorde_id, (s.fecha::date)) s.lote_ave_engorde_id,
        s.fecha::date AS fecha_registro,
        s.peso_prom_hembras AS peso_h,
        s.peso_prom_machos AS peso_m
    FROM seguimiento_diario_aves_engorde s
        JOIN lote_filtrado l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
    ORDER BY s.lote_ave_engorde_id, (s.fecha::date), s.id DESC
),
dia_base AS (
    SELECT l.company_id,
        l.pais_id,
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
        GREATEST(0, a.fecha_registro - l.fecha_encaset::date) AS dia_vida,
        l.aves_iniciales,
        CASE
            WHEN l.peso_mixto IS NOT NULL AND l.peso_mixto > 0::double precision THEN l.peso_mixto::numeric
            WHEN COALESCE(l.peso_inicial_h, 0::double precision) > 0::double precision AND COALESCE(l.peso_inicial_m, 0::double precision) > 0::double precision
                THEN ((l.peso_inicial_h + l.peso_inicial_m) / 2.0::double precision)::numeric
            ELSE COALESCE(l.peso_inicial_h, l.peso_inicial_m, 0::double precision)::numeric
        END AS peso_inicial_mixto_g,
        -- Pérdidas que reducen aves vivas (corregido): mort + sel + errSexaje + despachos
        a.sum_mort_h + a.sum_mort_m + a.sum_sel_h + a.sum_sel_m + a.sum_err_h + a.sum_err_m + a.despachos_dia AS perdidas_aves_dia,
        -- Mort+sel del día (para % mort/sel; NO incluye errSexaje ni despachos)
        a.sum_mort_h + a.sum_mort_m + a.sum_sel_h + a.sum_sel_m AS mort_sel_dia,
        a.consumo_kg_dia,
        CASE
            WHEN COALESCE(p.peso_h, 0::double precision) > 0::double precision AND COALESCE(p.peso_m, 0::double precision) > 0::double precision
                THEN ((p.peso_h + p.peso_m) / 2.0::double precision)::numeric
            ELSE COALESCE(NULLIF(p.peso_h, 0::double precision), NULLIF(p.peso_m, 0::double precision), 0::double precision)::numeric
        END AS peso_mixto_dia_g
    FROM seg_agregado a
        JOIN lote_filtrado l ON l.lote_ave_engorde_id = a.lote_ave_engorde_id
        JOIN seg_peso_ultimo p ON p.lote_ave_engorde_id = a.lote_ave_engorde_id AND p.fecha_registro = a.fecha_registro
    WHERE l.fecha_encaset IS NOT NULL
),
con_aves AS (
    SELECT d.*,
        COALESCE(sum(d.perdidas_aves_dia) OVER (PARTITION BY d.lote_ave_engorde_id ORDER BY d.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0::numeric) AS perdidas_acum_prev,
        sum(d.perdidas_aves_dia) OVER (PARTITION BY d.lote_ave_engorde_id ORDER BY d.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS perdidas_acum_total,
        -- Carry-forward del último peso medido > 0 (grupo entre pesajes positivos)
        CASE WHEN d.peso_mixto_dia_g > 0 THEN d.peso_mixto_dia_g END AS peso_pos,
        count(CASE WHEN d.peso_mixto_dia_g > 0 THEN 1 END) OVER (PARTITION BY d.lote_ave_engorde_id ORDER BY d.fecha_registro ROWS UNBOUNDED PRECEDING) AS peso_grp
    FROM dia_base d
),
con_aves2 AS (
    SELECT c.*,
        GREATEST(0::bigint, c.aves_iniciales - c.perdidas_acum_prev::bigint) AS aves_inicio_dia,
        GREATEST(0::bigint, c.aves_iniciales - c.perdidas_acum_total::bigint) AS aves_fin_dia,
        max(c.peso_pos) OVER (PARTITION BY c.lote_ave_engorde_id, c.peso_grp) AS lpos_incl
    FROM con_aves c
),
con_guia AS (
    SELECT c.*,
        LAG(c.lpos_incl) OVER (PARTITION BY c.lote_ave_engorde_id ORDER BY c.fecha_registro) AS peso_medido_prev,
        gh.id AS guia_genetica_ecuador_header_id,
        gd.peso_corporal_g::numeric AS peso_tabla_g,
        gd.ganancia_diaria_g::numeric AS ganancia_diaria_tabla_g,
        gd.cantidad_alimento_diario_g::numeric AS consumo_diario_tabla_g,
        gd.alimento_acumulado_g::numeric AS alimento_acum_tabla_g,
        gd.ca::numeric AS ca_tabla,
        gd.mortalidad_seleccion_diaria::numeric AS mort_sel_tabla_pct
    FROM con_aves2 c
        LEFT JOIN LATERAL (
            SELECT h.id
            FROM guia_genetica_ecuador_header h
            WHERE h.company_id = c.company_id
              AND h.pais_id = c.pais_id
              AND TRIM(BOTH FROM lower(h.raza::text)) = TRIM(BOTH FROM lower(COALESCE(c.raza, ''::text)))
              AND h.anio_guia = c.ano_tabla_genetica
              AND c.ano_tabla_genetica IS NOT NULL
              AND TRIM(BOTH FROM COALESCE(c.raza, ''::text)) <> ''::text
              AND h.deleted_at IS NULL
            ORDER BY h.id
            LIMIT 1
        ) gh ON TRUE
        LEFT JOIN LATERAL (
            SELECT d.peso_corporal_g, d.ganancia_diaria_g, d.cantidad_alimento_diario_g, d.alimento_acumulado_g, d.ca, d.mortalidad_seleccion_diaria
            FROM guia_genetica_ecuador_detalle d
            WHERE d.guia_genetica_ecuador_header_id = gh.id
              AND lower(TRIM(BOTH FROM d.sexo)) = 'mixto'::text
              AND d.deleted_at IS NULL
              AND d.dia <= c.dia_vida
            ORDER BY d.dia DESC
            LIMIT 1
        ) gd ON TRUE
),
con_calc AS (
    SELECT g.*,
        CASE WHEN g.aves_inicio_dia > 0 THEN g.consumo_kg_dia * 1000.0 / g.aves_inicio_dia::numeric ELSE 0::numeric END AS consumo_diario_real_g,
        sum(CASE WHEN g.aves_inicio_dia > 0 THEN g.consumo_kg_dia * 1000.0 / g.aves_inicio_dia::numeric ELSE 0::numeric END)
            OVER (PARTITION BY g.lote_ave_engorde_id ORDER BY g.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS alimento_acum_real_g
    FROM con_guia g
),
final AS (
    SELECT c.company_id,
        c.pais_id,
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
        -- Ganancia diaria real (corregido): contra el último peso medido > 0
        CASE
            WHEN c.peso_mixto_dia_g > 0::numeric
                THEN c.peso_mixto_dia_g - COALESCE(c.peso_medido_prev, c.peso_inicial_mixto_g)
            ELSE NULL::numeric
        END AS ganancia_diaria_real_g,
        c.ganancia_diaria_tabla_g,
        c.consumo_diario_real_g,
        c.consumo_diario_tabla_g,
        c.alimento_acum_real_g,
        c.alimento_acum_tabla_g,
        CASE
            WHEN c.peso_mixto_dia_g > 0::numeric AND c.alimento_acum_real_g > 0::numeric
                THEN c.alimento_acum_real_g / NULLIF(c.peso_mixto_dia_g, 0::numeric)
            ELSE NULL::numeric
        END AS ca_real,
        c.ca_tabla,
        CASE WHEN c.aves_inicio_dia > 0 THEN c.mort_sel_dia::numeric * 100.0 / c.aves_inicio_dia::numeric ELSE 0::numeric END AS mort_sel_real_pct,
        c.mort_sel_tabla_pct,
        CASE WHEN c.peso_tabla_g > 0::numeric AND c.peso_mixto_dia_g > 0::numeric
             THEN (c.peso_mixto_dia_g - c.peso_tabla_g) / NULLIF(c.peso_tabla_g, 0::numeric) * 100.0
             ELSE 0::numeric END AS dif_peso_vs_tabla_pct,
        -- % acum pérdidas (corregido): (inicial − aves_fin)/inicial, topado a 100 por construcción
        CASE WHEN c.aves_iniciales > 0
             THEN (c.aves_iniciales - c.aves_fin_dia)::numeric * 100.0 / c.aves_iniciales::numeric
             ELSE 0::numeric END AS mort_acum_pct
    FROM con_calc c
)
SELECT company_id,
    empresa_nombre,
    lote_ave_engorde_id,
    lote_nombre,
    granja_id,
    granja_nombre,
    galpon_id,
    galpon_nombre,
    nucleo_id,
    nucleo_nombre,
    raza,
    ano_tabla_genetica,
    guia_genetica_ecuador_header_id,
    to_char(fecha_registro::timestamp with time zone, 'YYYY-MM-DD'::text) AS fecha_ymd,
    fecha_registro,
    dia_vida,
    aves_iniciales,
    aves_inicio_dia,
    aves_fin_dia,
    trim_scale(peso_inicial_mixto_g) AS peso_inicial_mixto_g,
    trim_scale(peso_real_g) AS peso_real_g,
    trim_scale(peso_tabla_g) AS peso_tabla_g,
    trim_scale(ganancia_diaria_real_g) AS ganancia_diaria_real_g,
    trim_scale(ganancia_diaria_tabla_g) AS ganancia_diaria_tabla_g,
    trim_scale(consumo_diario_real_g) AS consumo_diario_real_g,
    trim_scale(consumo_diario_tabla_g) AS consumo_diario_tabla_g,
    trim_scale(alimento_acum_real_g) AS alimento_acum_real_g,
    trim_scale(alimento_acum_tabla_g) AS alimento_acum_tabla_g,
    trim_scale(ca_real) AS ca_real,
    trim_scale(ca_tabla) AS ca_tabla,
    trim_scale(mort_sel_real_pct) AS mort_sel_real_pct,
    trim_scale(mort_sel_tabla_pct) AS mort_sel_tabla_pct,
    trim_scale(dif_peso_vs_tabla_pct) AS dif_peso_vs_tabla_pct,
    trim_scale(mort_acum_pct) AS mort_acum_pct,
    -- NUEVO: país (para filtros Power BI)
    pais_id
FROM final f;

ALTER VIEW public.vw_indicadores_diarios_engorde OWNER TO repropesa01;
GRANT SELECT ON TABLE public.vw_indicadores_diarios_engorde TO "usrDWH";

COMMENT ON VIEW public.vw_indicadores_diarios_engorde IS
  'Indicadores diarios engorde vs guía Ecuador (mixto), alineado al cómputo del front. Guía por company_id + pais_id (sin estado=active). Consumo mixto = consumo_kg_hembras. Ganancia vs último peso>0. Aves/mort_acum restan despachos de metadata.';
