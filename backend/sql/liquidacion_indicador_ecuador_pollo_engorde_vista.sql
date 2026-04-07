-- =============================================================================
-- Vista: liquidación técnica Indicador Ecuador — Pollo Engorde (lote padre)
-- =============================================================================
-- Alineada con IndicadorEcuadorService.CalcularIndicadorLoteAveEngordeAsync y
-- IndicadorEcuadorDto (misma matriz que indicador-ecuador-list liquidación Pollo).
--
-- Incluye lotes abiertos y cerrados en tiempo real (no solo liquidados).
-- Filtros típicos en consulta (la vista no parametriza):
--   WHERE company_id = 2
--     AND granja_id = 10
--     AND (nucleo_id IS NOT DISTINCT FROM 'X' OR :nucleo IS NULL)
--     AND lote_ave_engorde_id = 123   -- opcional
--     AND fecha_encaset::date BETWEEN :d1 AND :d2
--     AND lote_cerrado_logico = true    -- solo cerrados, si aplica
--
-- Constantes conversión ajustada (mismas que backend): peso_ajuste 2.7, divisor 4.5
-- =============================================================================

CREATE OR REPLACE VIEW public.vw_liquidacion_indicador_ecuador_pollo_engorde AS
WITH
-- Parse seguro ancho/largo galpón (texto → numeric; inválido → NULL)
params AS (
  SELECT
    2.7::numeric AS peso_ajuste,
    4.5::numeric AS divisor_ajuste
),
seg_padre AS (
  SELECT
    s.lote_ave_engorde_id,
    SUM(COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)) AS sum_mort,
    SUM(COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)) AS sum_sel,
    SUM(COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::numeric AS consumo_kg
  FROM public.seguimiento_diario_aves_engorde s
  GROUP BY s.lote_ave_engorde_id
),
mov_salida AS (
  SELECT
    m.lote_ave_engorde_origen_id AS lote_ave_engorde_id,
    SUM(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0))::bigint AS aves_sacrificadas,
    SUM(
      CASE
        WHEN m.peso_bruto IS NOT NULL AND m.peso_tara IS NOT NULL
        THEN (m.peso_bruto::numeric - m.peso_tara::numeric)
        ELSE 0::numeric
      END
    ) AS kg_carne,
    AVG(m.edad_aves::numeric) FILTER (WHERE m.edad_aves IS NOT NULL) AS edad_promedio,
    MAX(m.fecha_movimiento) AS fecha_ultimo_despacho
  FROM public.movimiento_pollo_engorde m
  WHERE m.estado IS DISTINCT FROM 'Cancelado'
    AND m.deleted_at IS NULL
    AND m.lote_ave_engorde_origen_id IS NOT NULL
    AND m.tipo_movimiento IN ('Venta', 'Despacho', 'Retiro')
  GROUP BY m.lote_ave_engorde_origen_id
),
mov_traslado_rep AS (
  SELECT
    m.lote_ave_engorde_origen_id AS lote_ave_engorde_id,
    SUM(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0))::bigint AS aves_trasladadas_rep
  FROM public.movimiento_pollo_engorde m
  WHERE m.estado IS DISTINCT FROM 'Cancelado'
    AND m.deleted_at IS NULL
    AND m.tipo_movimiento = 'Traslado'
    AND m.lote_ave_engorde_origen_id IS NOT NULL
    AND m.lote_reproductora_ave_engorde_destino_id IS NOT NULL
  GROUP BY m.lote_ave_engorde_origen_id
),
rep_base AS (
  SELECT
    r.id AS lote_reproductora_id,
    r.lote_ave_engorde_id,
    CASE
      WHEN COALESCE(r.aves_inicio_hembras, 0) + COALESCE(r.aves_inicio_machos, 0) + COALESCE(r.mixtas, 0) > 0
        THEN COALESCE(r.aves_inicio_hembras, 0) + COALESCE(r.aves_inicio_machos, 0) + COALESCE(r.mixtas, 0)
      ELSE COALESCE(r.h, 0) + COALESCE(r.m, 0) + COALESCE(r.mixtas, 0)
    END::bigint AS encaset_rep
  FROM public.lote_reproductora_ave_engorde r
),
rep_seg AS (
  SELECT
    s.lote_reproductora_ave_engorde_id,
    SUM(
      COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
      + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)
    )::bigint AS mort_sel_rep
  FROM public.seguimiento_diario_lote_reproductora_aves_engorde s
  GROUP BY s.lote_reproductora_ave_engorde_id
),
rep_mov AS (
  SELECT
    m.lote_reproductora_ave_engorde_origen_id AS lote_reproductora_id,
    SUM(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0))::bigint AS ventas_rep
  FROM public.movimiento_pollo_engorde m
  WHERE m.estado IS DISTINCT FROM 'Cancelado'
    AND m.deleted_at IS NULL
    AND m.lote_reproductora_ave_engorde_origen_id IS NOT NULL
    AND m.tipo_movimiento IN ('Venta', 'Despacho', 'Retiro')
  GROUP BY m.lote_reproductora_ave_engorde_origen_id
),
rep_tiene_aves AS (
  SELECT
    rb.lote_ave_engorde_id,
    BOOL_OR(
      GREATEST(
        0::bigint,
        rb.encaset_rep - COALESCE(rs.mort_sel_rep, 0) - COALESCE(rm.ventas_rep, 0)
      ) > 0
    ) AS alguna_rep_con_aves_positivas
  FROM rep_base rb
  LEFT JOIN rep_seg rs ON rs.lote_reproductora_ave_engorde_id = rb.lote_reproductora_id
  LEFT JOIN rep_mov rm ON rm.lote_reproductora_id = rb.lote_reproductora_id
  GROUP BY rb.lote_ave_engorde_id
),
rep_counts AS (
  SELECT r.lote_ave_engorde_id, COUNT(*)::int AS cnt_rep
  FROM public.lote_reproductora_ave_engorde r
  GROUP BY r.lote_ave_engorde_id
),
ult_seg_padre AS (
  SELECT DISTINCT ON (s.lote_ave_engorde_id)
    s.lote_ave_engorde_id,
    s.fecha::date AS ultima_fecha_seg
  FROM public.seguimiento_diario_aves_engorde s
  ORDER BY s.lote_ave_engorde_id, s.fecha DESC, s.id DESC
),
ult_mov_cualquier AS (
  SELECT DISTINCT ON (m.lote_ave_engorde_origen_id)
    m.lote_ave_engorde_origen_id AS lote_ave_engorde_id,
    m.fecha_movimiento AS ultima_fecha_mov
  FROM public.movimiento_pollo_engorde m
  WHERE m.estado IS DISTINCT FROM 'Cancelado'
    AND m.deleted_at IS NULL
    AND m.lote_ave_engorde_origen_id IS NOT NULL
  ORDER BY m.lote_ave_engorde_origen_id, m.fecha_movimiento DESC, m.id DESC
),
base AS (
  SELECT
    l.lote_ave_engorde_id,
    l.company_id,
    COALESCE(c.name, l.empresa_nombre) AS empresa_nombre,
    l.granja_id,
    fa.name AS granja_nombre,
    l.nucleo_id,
    nu.nucleo_nombre,
    l.galpon_id,
    gp.galpon_nombre,
    l.lote_nombre,
    l.fecha_encaset::date AS fecha_encaset,
    l.estado_operativo_lote,
    l.liquidado_at,
    COALESCE(l.aves_encasetadas, 0)::bigint AS aves_encasetadas_raw,
    CASE
      WHEN COALESCE(l.aves_encasetadas, 0) > 0 THEN l.aves_encasetadas::bigint
      ELSE (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0))::bigint
    END AS aves_encasetadas,
    COALESCE(sp.sum_mort, 0) + COALESCE(sp.sum_sel, 0) AS mort_sel_padre,
    COALESCE(sp.consumo_kg, 0::numeric) AS consumo_total_kg,
    COALESCE(ms.aves_sacrificadas, 0::bigint) AS aves_sacrificadas,
    COALESCE(ms.kg_carne, 0::numeric) AS kg_carne_pollos,
    COALESCE(ms.edad_promedio, 0::numeric) AS edad_promedio_mov,
    ms.fecha_ultimo_despacho,
    COALESCE(mt.aves_trasladadas_rep, 0::bigint) AS aves_trasladadas_rep,
    COALESCE(rc.cnt_rep, 0) AS cantidad_lotes_reproductores,
    CASE
      WHEN COALESCE(rc.cnt_rep, 0) = 0 THEN FALSE
      ELSE NOT COALESCE(rt.alguna_rep_con_aves_positivas, FALSE)
    END AS todos_reproductores_sin_aves,
    us.ultima_fecha_seg,
    umc.ultima_fecha_mov,
    -- m²: galpón del lote o suma de galpones de la granja si no hay galpón
    CASE
      WHEN l.galpon_id IS NOT NULL AND TRIM(l.galpon_id) <> '' THEN
        CASE
          WHEN gp.ancho IS NOT NULL AND gp.largo IS NOT NULL
            AND TRIM(gp.ancho::text) <> ''
            AND TRIM(gp.largo::text) <> ''
            AND TRIM(gp.ancho::text) ~ '^[0-9]+([.,][0-9]+)?$'
            AND TRIM(gp.largo::text) ~ '^[0-9]+([.,][0-9]+)?$'
          THEN
            (REPLACE(REPLACE(TRIM(gp.ancho::text), ',', '.'), ' ', '')::numeric)
            * (REPLACE(REPLACE(TRIM(gp.largo::text), ',', '.'), ' ', '')::numeric)
          ELSE NULL::numeric
        END
      ELSE
        (
          SELECT COALESCE(SUM(
            CASE
              WHEN g.ancho IS NOT NULL AND g.largo IS NOT NULL
                AND TRIM(g.ancho::text) <> ''
                AND TRIM(g.largo::text) <> ''
                AND TRIM(g.ancho::text) ~ '^[0-9]+([.,][0-9]+)?$'
                AND TRIM(g.largo::text) ~ '^[0-9]+([.,][0-9]+)?$'
              THEN
                (REPLACE(REPLACE(TRIM(g.ancho::text), ',', '.'), ' ', '')::numeric)
                * (REPLACE(REPLACE(TRIM(g.largo::text), ',', '.'), ' ', '')::numeric)
              ELSE 0::numeric
            END
          ), 0::numeric)
          FROM public.galpones g
          WHERE g.granja_id = l.granja_id
            AND g.deleted_at IS NULL
        )
    END AS metros_cuadrados
  FROM public.lote_ave_engorde l
  LEFT JOIN public.companies c ON c.id = l.company_id
  LEFT JOIN public.farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
  LEFT JOIN public.nucleos nu ON nu.nucleo_id = l.nucleo_id AND nu.granja_id = l.granja_id
  LEFT JOIN public.galpones gp ON gp.galpon_id = l.galpon_id AND gp.granja_id = l.granja_id
  LEFT JOIN seg_padre sp ON sp.lote_ave_engorde_id = l.lote_ave_engorde_id
  LEFT JOIN mov_salida ms ON ms.lote_ave_engorde_id = l.lote_ave_engorde_id
  LEFT JOIN mov_traslado_rep mt ON mt.lote_ave_engorde_id = l.lote_ave_engorde_id
  LEFT JOIN rep_tiene_aves rt ON rt.lote_ave_engorde_id = l.lote_ave_engorde_id
  LEFT JOIN rep_counts rc ON rc.lote_ave_engorde_id = l.lote_ave_engorde_id
  LEFT JOIN ult_seg_padre us ON us.lote_ave_engorde_id = l.lote_ave_engorde_id
  LEFT JOIN ult_mov_cualquier umc ON umc.lote_ave_engorde_id = l.lote_ave_engorde_id
  WHERE l.deleted_at IS NULL
),
calc AS (
  SELECT
    b.*,
    (b.mort_sel_padre)::bigint AS mortalidad_unidades,
    GREATEST(
      0::bigint,
      b.aves_encasetadas - b.mort_sel_padre::bigint - b.aves_sacrificadas - b.aves_trasladadas_rep
    ) AS aves_actuales,
    CASE
      WHEN b.aves_encasetadas > 0 THEN
        (b.mort_sel_padre::numeric / b.aves_encasetadas::numeric) * 100::numeric
      ELSE 0::numeric
    END AS mortalidad_porcentaje,
    CASE
      WHEN b.aves_encasetadas > 0 THEN
        ((b.aves_encasetadas - b.mort_sel_padre::bigint)::numeric / b.aves_encasetadas::numeric) * 100::numeric
      ELSE 0::numeric
    END AS supervivencia_porcentaje,
    CASE
      WHEN b.aves_sacrificadas > 0 THEN b.consumo_total_kg / b.aves_sacrificadas::numeric * 1000::numeric
      ELSE 0::numeric
    END AS consumo_ave_gramos,
    CASE
      WHEN b.aves_sacrificadas > 0 THEN b.kg_carne_pollos / b.aves_sacrificadas::numeric
      ELSE 0::numeric
    END AS peso_promedio_kilos,
    CASE
      WHEN b.kg_carne_pollos > 0 THEN b.consumo_total_kg / b.kg_carne_pollos
      ELSE 0::numeric
    END AS conversion,
    (SELECT p.peso_ajuste FROM params p) AS peso_ajuste_variable,
    (SELECT p.divisor_ajuste FROM params p) AS divisor_ajuste_variable
  FROM base b
),
calc2 AS (
  SELECT
    c.*,
    CASE
      WHEN GREATEST(0::bigint, c.aves_actuales) = 0 THEN TRUE
      ELSE FALSE
    END AS cerrado_por_aves_cero,
    CASE
      WHEN GREATEST(0::bigint, c.aves_actuales) > 0
        AND c.aves_sacrificadas = 0
        AND COALESCE(c.mort_sel_padre, 0) = 0
        AND c.todos_reproductores_sin_aves
        AND c.cantidad_lotes_reproductores > 0
      THEN TRUE
      ELSE FALSE
    END AS cerrado_por_reproductores_vendidos,
    -- Misma fórmula que IndicadorEcuadorService.CalcularConversionAjustada
    CASE
      WHEN c.conversion > 0 THEN
        c.conversion + ((c.peso_ajuste_variable - c.peso_promedio_kilos) / c.divisor_ajuste_variable)
      ELSE 0::numeric
    END AS conversion_ajustada2700
  FROM calc c
)
SELECT
  c2.company_id,
  c2.empresa_nombre,
  c2.granja_id,
  c2.granja_nombre,
  c2.nucleo_id,
  c2.nucleo_nombre,
  c2.galpon_id,
  c2.galpon_nombre,
  c2.lote_ave_engorde_id,
  c2.lote_nombre,
  c2.fecha_encaset,
  c2.estado_operativo_lote,
  c2.liquidado_at,
  c2.cantidad_lotes_reproductores,
  c2.aves_encasetadas,
  c2.aves_sacrificadas,
  c2.mortalidad_unidades AS mortalidad,
  c2.mortalidad_porcentaje,
  c2.supervivencia_porcentaje,
  c2.consumo_total_kg AS consumo_total_alimento_kg,
  c2.consumo_ave_gramos,
  c2.kg_carne_pollos,
  c2.peso_promedio_kilos,
  c2.conversion,
  c2.conversion_ajustada2700,
  c2.peso_ajuste_variable,
  c2.divisor_ajuste_variable,
  c2.edad_promedio_mov AS edad_promedio,
  COALESCE(c2.metros_cuadrados, 0::numeric) AS metros_cuadrados,
  CASE
    WHEN COALESCE(c2.metros_cuadrados, 0) > 0 THEN
      c2.aves_sacrificadas::numeric / c2.metros_cuadrados
    ELSE 0::numeric
  END AS aves_por_metro_cuadrado,
  CASE
    WHEN COALESCE(c2.metros_cuadrados, 0) > 0 THEN
      c2.kg_carne_pollos / c2.metros_cuadrados
    ELSE 0::numeric
  END AS kg_por_metro_cuadrado,
  CASE
    WHEN c2.conversion > 0 THEN (c2.peso_promedio_kilos / c2.conversion) * 100::numeric
    ELSE 0::numeric
  END AS eficiencia_americana,
  CASE
    WHEN c2.conversion > 0 AND c2.edad_promedio_mov > 0 THEN
      ((c2.peso_promedio_kilos * c2.supervivencia_porcentaje) / (c2.edad_promedio_mov * c2.conversion)) * 100::numeric
    ELSE 0::numeric
  END AS eficiencia_europea,
  CASE
    WHEN c2.conversion > 0 THEN
      ((c2.peso_promedio_kilos / c2.conversion) / c2.conversion) * 100::numeric
    ELSE 0::numeric
  END AS indice_productividad,
  CASE
    WHEN c2.edad_promedio_mov > 0 THEN (c2.peso_promedio_kilos / c2.edad_promedio_mov) * 1000::numeric
    ELSE 0::numeric
  END AS ganancia_dia,
  -- Tiempo real: aves en galpón (lógica padre) y cierre
  c2.aves_trasladadas_rep,
  c2.aves_actuales,
  (c2.aves_actuales > 0) AS tiene_aves,
  (c2.cerrado_por_aves_cero OR c2.cerrado_por_reproductores_vendidos) AS lote_cerrado_logico,
  c2.cerrado_por_aves_cero,
  c2.cerrado_por_reproductores_vendidos,
  c2.fecha_ultimo_despacho AS fecha_cierre_ultimo_despacho,
  CASE
    WHEN (c2.cerrado_por_aves_cero OR c2.cerrado_por_reproductores_vendidos)
      AND c2.fecha_ultimo_despacho IS NULL
    THEN COALESCE(c2.ultima_fecha_seg::timestamptz, c2.ultima_fecha_mov, c2.fecha_encaset::timestamptz)
    ELSE c2.fecha_ultimo_despacho
  END AS fecha_cierre_efectiva
FROM calc2 c2;

COMMENT ON VIEW public.vw_liquidacion_indicador_ecuador_pollo_engorde IS
  'Liquidación técnica Pollo Engorde (lote padre): mismos campos que Indicador Ecuador / IndicadorEcuadorDto. Incluye aves_actuales y lote_cerrado_logico en tiempo real. Filtrar por company_id, granja_id, nucleo_id, galpon_id, fechas.';
