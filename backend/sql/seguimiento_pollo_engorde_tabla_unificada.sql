-- =============================================================================
-- Seguimiento diario pollo de engorde — consulta unificada (tabla + histórico)
-- =============================================================================
-- Replica en SQL lo que la app arma con:
--   1) public.seguimiento_diario_aves_engorde  (registro diario; columnas + jsonb metadata)
--   2) public.lote_registro_historico_unificado (ingresos, traslados, consumo bodega, ventas)
--
-- Uso: en el CTE `params` asigne lote_ave_engorde_id (PK de public.lote_ave_engorde).
--
-- saldo_alimento_kg_bd = valor persistido en BD (tras RecalcularSaldoAlimentoPorLote en API).
-- saldo_alimento_kg_calculado = misma regla que backend/front (sin INV_CONSUMO en cadena de saldo).
--
-- LIMITACIONES (respecto a la UI Angular):
-- • “Ingreso / Traslado / Documento” en pantalla priorizan metadata del seguimiento y si hay
--   datos del histórico para ese día, los combina. Aquí se muestran columnas separadas:
--   texto desde histórico agregado + campos extraídos de metadata jsonb.
-- =============================================================================

WITH RECURSIVE
params AS (
  SELECT 12345::int AS lote_ave_engorde_id -- <<<<<<<<<< CAMBIAR: id del lote (lote_ave_engorde_id)
),

hist_base AS (
  SELECT
    h.lote_ave_engorde_id,
    COALESCE(
      CASE
        WHEN lower(trim(COALESCE(h.referencia, '') || ' ' || COALESCE(h.numero_documento, '')))
             ~ 'seguimiento\s+aves\s+engorde\s+#\d+\s+(\d{4}-\d{2}-\d{2})'
          THEN (substring(
            lower(trim(COALESCE(h.referencia, '') || ' ' || COALESCE(h.numero_documento, '')))
            FROM 'seguimiento\s+aves\s+engorde\s+#\d+\s+(\d{4}-\d{2}-\d{2})'
          ))::date
        ELSE NULL
      END,
      CASE
        WHEN h.tipo_evento = 'INV_CONSUMO'
         AND trim(COALESCE(h.referencia, '') || ' ' || COALESCE(h.numero_documento, '')) ~ '(\d{4}-\d{2}-\d{2})'
          THEN (substring(
            trim(COALESCE(h.referencia, '') || ' ' || COALESCE(h.numero_documento, ''))
            FROM '(\d{4}-\d{2}-\d{2})'
          ))::date
        ELSE NULL
      END,
      h.fecha_operacion::date
    ) AS ymd_efe,
    CASE
      WHEN h.anulado THEN NULL::numeric
      WHEN h.tipo_evento = 'INV_INGRESO' AND COALESCE(h.cantidad_kg, 0) <> 0 THEN h.cantidad_kg::numeric
      WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA' AND COALESCE(h.cantidad_kg, 0) <> 0 THEN h.cantidad_kg::numeric
      WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA' AND COALESCE(h.cantidad_kg, 0) <> 0 THEN -abs(h.cantidad_kg::numeric)
      WHEN h.tipo_evento = 'INV_OTRO' AND lower(trim(COALESCE(h.movement_type_original, ''))) = 'ajustestock'
        THEN h.cantidad_kg::numeric
      WHEN h.tipo_evento = 'INV_OTRO' AND lower(trim(COALESCE(h.movement_type_original, ''))) = 'eliminacionstock'
           AND COALESCE(h.cantidad_kg, 0) <> 0
        THEN -abs(h.cantidad_kg::numeric)
      ELSE NULL
    END AS delta_kg,
    CASE h.tipo_evento
      WHEN 'INV_INGRESO' THEN 0
      WHEN 'INV_TRASLADO_ENTRADA' THEN 1
      WHEN 'INV_TRASLADO_SALIDA' THEN 2
      WHEN 'INV_OTRO' THEN 2
      ELSE 99
    END AS ord_hist,
    (EXTRACT(EPOCH FROM h.created_at) * 1000)::bigint AS tie_h_ms
  FROM public.lote_registro_historico_unificado h
  INNER JOIN params p ON h.lote_ave_engorde_id = p.lote_ave_engorde_id
  WHERE NOT h.anulado
),

first_seg_f AS (
  SELECT p.lote_ave_engorde_id, MIN(s.fecha::date) AS d0
  FROM public.seguimiento_diario_aves_engorde s
  INNER JOIN params p ON s.lote_ave_engorde_id = p.lote_ave_engorde_id
  GROUP BY p.lote_ave_engorde_id
),

hist_opening AS (
  SELECT
    hb.lote_ave_engorde_id,
    0 AS phase,
    hb.ymd_efe,
    0 AS ord_sort,
    hb.tie_h_ms AS tie,
    NULL::bigint AS seg_id,
    hb.delta_kg
  FROM hist_base hb
  INNER JOIN first_seg_f f ON f.lote_ave_engorde_id = hb.lote_ave_engorde_id
  WHERE hb.delta_kg IS NOT NULL
    AND hb.ymd_efe < f.d0
),

hist_main AS (
  SELECT
    hb.lote_ave_engorde_id,
    1 AS phase,
    hb.ymd_efe,
    hb.ord_hist AS ord_sort,
    hb.tie_h_ms AS tie,
    NULL::bigint AS seg_id,
    hb.delta_kg
  FROM hist_base hb
  INNER JOIN first_seg_f f ON f.lote_ave_engorde_id = hb.lote_ave_engorde_id
  INNER JOIN public.lote_ave_engorde la ON la.lote_ave_engorde_id = hb.lote_ave_engorde_id
  WHERE hb.delta_kg IS NOT NULL
    AND hb.ymd_efe >= f.d0
    AND (la.fecha_encaset IS NULL OR hb.ymd_efe >= la.fecha_encaset::date)
),

seg_events AS (
  SELECT
    s.lote_ave_engorde_id,
    1 AS phase,
    s.fecha::date AS ymd_efe,
    3 AS ord_sort,
    (EXTRACT(EPOCH FROM ((s.fecha::date + interval '12 hours')::timestamp AT TIME ZONE 'UTC')) * 1000)::bigint AS tie,
    s.id AS seg_id,
    -(COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::numeric AS delta_kg
  FROM public.seguimiento_diario_aves_engorde s
  INNER JOIN params p ON s.lote_ave_engorde_id = p.lote_ave_engorde_id
),

events_union AS (
  SELECT * FROM hist_opening
  UNION ALL
  SELECT * FROM hist_main
  UNION ALL
  SELECT * FROM seg_events
),

events_ordered AS (
  SELECT
    eu.*,
    row_number() OVER (
      PARTITION BY eu.lote_ave_engorde_id
      ORDER BY eu.phase, eu.ymd_efe, eu.ord_sort, eu.tie, COALESCE(eu.seg_id, 0::bigint)
    ) AS seq
  FROM events_union eu
),

rec AS (
  SELECT
    eo.lote_ave_engorde_id,
    eo.seq,
    eo.seg_id,
    eo.delta_kg,
    GREATEST(0::numeric, eo.delta_kg) AS bal
  FROM events_ordered eo
  WHERE eo.seq = 1
  UNION ALL
  SELECT
    eo.lote_ave_engorde_id,
    eo.seq,
    eo.seg_id,
    eo.delta_kg,
    GREATEST(0::numeric, r.bal + eo.delta_kg)
  FROM rec r
  INNER JOIN events_ordered eo
    ON eo.lote_ave_engorde_id = r.lote_ave_engorde_id
   AND eo.seq = r.seq + 1
),

saldo_ui AS (
  SELECT r.seg_id, r.bal AS saldo_alimento_kg_calculado
  FROM rec r
  WHERE r.seg_id IS NOT NULL
),

lote AS (
  SELECT
    l.company_id,
    COALESCE(co.name, l.empresa_nombre) AS empresa_nombre,
    l.lote_ave_engorde_id,
    l.lote_nombre,
    l.fecha_encaset,
    l.granja_id,
    fa.name AS granja_nombre,
    l.galpon_id,
    gp.galpon_nombre,
    l.nucleo_id,
    nu.nucleo_nombre,
    -- Misma base que tabs-principal engorde (avesInicialesLote): hembras+machos, si no aves_encasetadas
    GREATEST(
      0,
      CASE
        WHEN COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) > 0
          THEN COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0)
        ELSE COALESCE(l.aves_encasetadas, 0)
      END
    )::bigint AS aves_iniciales
  FROM public.lote_ave_engorde l
  INNER JOIN params p ON l.lote_ave_engorde_id = p.lote_ave_engorde_id
  LEFT JOIN public.companies co ON co.id = l.company_id
  LEFT JOIN public.farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
  LEFT JOIN public.nucleos nu ON nu.nucleo_id = l.nucleo_id AND nu.granja_id = l.granja_id
  LEFT JOIN public.galpones gp ON gp.galpon_id = l.galpon_id AND gp.granja_id = l.granja_id
),

hist_por_dia AS (
  SELECT
    h.lote_ave_engorde_id,
    h.fecha_operacion::date AS dia,
    SUM(CASE WHEN h.tipo_evento = 'INV_INGRESO' AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END) AS ingreso_kg,
    SUM(CASE WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA' AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END) AS traslado_entrada_kg,
    SUM(CASE WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA' AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END) AS traslado_salida_kg,
    SUM(CASE WHEN h.tipo_evento = 'INV_CONSUMO' AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END) AS consumo_bodega_kg,
    SUM(CASE WHEN h.tipo_evento = 'VENTA_AVES' AND NOT h.anulado THEN COALESCE(h.cantidad_hembras, 0) ELSE 0 END) AS venta_hembras,
    SUM(CASE WHEN h.tipo_evento = 'VENTA_AVES' AND NOT h.anulado THEN COALESCE(h.cantidad_machos, 0) ELSE 0 END) AS venta_machos,
    SUM(CASE WHEN h.tipo_evento = 'VENTA_AVES' AND NOT h.anulado THEN COALESCE(h.cantidad_mixtas, 0) ELSE 0 END) AS venta_mixtas,
    string_agg(DISTINCT NULLIF(trim(COALESCE(h.numero_documento, h.referencia, '')), ''), ', ')
      FILTER (WHERE trim(COALESCE(h.numero_documento, h.referencia, '')) <> '') AS documentos_hist
  FROM public.lote_registro_historico_unificado h
  INNER JOIN params p ON h.lote_ave_engorde_id = p.lote_ave_engorde_id
  WHERE NOT h.anulado
  GROUP BY h.lote_ave_engorde_id, h.fecha_operacion::date
),

base AS (
  SELECT
    s.id AS seguimiento_id,
    l.company_id,
    l.empresa_nombre,
    s.lote_ave_engorde_id,
    s.fecha::date AS fecha_registro,
    l.lote_nombre,
    l.fecha_encaset,
    l.granja_id,
    l.granja_nombre,
    l.galpon_id,
    l.galpon_nombre,
    l.nucleo_id,
    l.nucleo_nombre,
    l.aves_iniciales,

    GREATEST(0, (s.fecha::date - l.fecha_encaset::date))::int AS edad_dias_vida,
    LEAST(8, GREATEST(1, CEIL((GREATEST(0, (s.fecha::date - l.fecha_encaset::date)) + 1) / 7.0)::int)) AS semana_ui,

    COALESCE(s.mortalidad_hembras, 0) AS mortalidad_hembras,
    COALESCE(s.mortalidad_machos, 0) AS mortalidad_machos,
    COALESCE(s.sel_h, 0) AS seleccion_hembras,
    COALESCE(s.sel_m, 0) AS seleccion_machos,
    COALESCE(s.error_sexaje_hembras, 0) AS error_sexaje_hembras,
    COALESCE(s.error_sexaje_machos, 0) AS error_sexaje_machos,

    COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
      + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) AS total_mort_sel_dia,

    COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
      + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)
      + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_todas_dia,

    s.tipo_alimento,
    CASE
      WHEN UPPER(COALESCE(s.tipo_alimento, '')) LIKE '%PRE%' THEN 'PRE'
      WHEN UPPER(COALESCE(s.tipo_alimento, '')) LIKE '%INI%' THEN 'INI'
      WHEN UPPER(COALESCE(s.tipo_alimento, '')) LIKE '%ENG%' THEN 'ENG'
      WHEN UPPER(COALESCE(s.tipo_alimento, '')) LIKE '%FIN%' THEN 'FIN-D'
      WHEN COALESCE(s.tipo_alimento, '') = '' THEN '—'
      ELSE LEFT(s.tipo_alimento, 8)
    END AS tipo_alimento_corto,

    COALESCE(s.consumo_kg_hembras, 0)::numeric + COALESCE(s.consumo_kg_machos, 0)::numeric AS consumo_real_dia_kg,
    s.consumo_kg_hembras,
    s.consumo_kg_machos,
    s.consumo_agua_diario,
    s.peso_prom_hembras,
    s.peso_prom_machos,
    s.observaciones,
    s.saldo_alimento_kg AS saldo_alimento_kg_bd,
    su.saldo_alimento_kg_calculado,

    s.metadata,
    s.items_adicionales,

    h.ingreso_kg,
    h.traslado_entrada_kg,
    h.traslado_salida_kg,
    h.consumo_bodega_kg,
    h.venta_hembras,
    h.venta_machos,
    h.venta_mixtas,
    h.documentos_hist
  FROM public.seguimiento_diario_aves_engorde s
  INNER JOIN params p ON s.lote_ave_engorde_id = p.lote_ave_engorde_id
  INNER JOIN lote l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
  LEFT JOIN hist_por_dia h
    ON h.lote_ave_engorde_id = s.lote_ave_engorde_id
   AND h.dia = s.fecha::date
  LEFT JOIN saldo_ui su ON su.seg_id = s.id
),

con_acum AS (
  SELECT
    b.*,
    SUM(b.perdidas_todas_dia) OVER (
      PARTITION BY b.lote_ave_engorde_id
      ORDER BY b.fecha_registro, b.seguimiento_id
      ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS acum_perdidas_todas,
    SUM(b.consumo_real_dia_kg) OVER (
      PARTITION BY b.lote_ave_engorde_id
      ORDER BY b.fecha_registro, b.seguimiento_id
      ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS consumo_acumulado_kg
  FROM base b
),

final AS (
  SELECT
    c.*,
    GREATEST(0, c.aves_iniciales - c.acum_perdidas_todas)::bigint AS saldo_aves_vivas_fin_dia,
    GREATEST(0, c.aves_iniciales - c.acum_perdidas_todas + c.perdidas_todas_dia)::numeric AS saldo_aves_inicio_dia
  FROM con_acum c
)

SELECT
  f.company_id,
  f.empresa_nombre,
  f.seguimiento_id,
  f.lote_ave_engorde_id,
  f.lote_nombre,
  f.granja_id,
  f.granja_nombre,
  f.galpon_id,
  f.galpon_nombre,
  f.nucleo_id,
  f.nucleo_nombre,
  to_char(f.fecha_registro, 'DD/MM/YYYY') AS fecha_dmy,
  f.fecha_registro,
  f.semana_ui AS semana,
  f.edad_dias_vida AS edad_dias_vida,
  to_char(f.fecha_registro, 'Dy, DD Mon') AS dia_calendario_corto,

  f.mortalidad_hembras,
  f.mortalidad_machos,
  f.seleccion_hembras,
  f.seleccion_machos,
  f.total_mort_sel_dia AS total_mort_mas_sel_dia,
  f.error_sexaje_hembras,
  f.error_sexaje_machos,

  f.venta_hembras AS despacho_hembras_hist,
  f.venta_machos AS despacho_machos_hist,
  f.venta_mixtas AS despacho_mixtas_hist,

  trim_scale(f.saldo_alimento_kg_bd) AS saldo_alimento_kg_bd,
  trim_scale(f.saldo_alimento_kg_calculado) AS saldo_alimento_kg_calculado,

  f.saldo_aves_vivas_fin_dia AS saldo_aves_vivas,

  f.tipo_alimento,
  f.tipo_alimento_corto,

  CASE WHEN COALESCE(f.ingreso_kg, 0) > 0 THEN to_char(f.ingreso_kg, 'FM9999999999990.999') || ' kg' ELSE NULL END AS ingreso_alimento_texto_hist,
  CASE
    WHEN COALESCE(f.traslado_entrada_kg, 0) = 0 AND COALESCE(f.traslado_salida_kg, 0) = 0 THEN NULL
    ELSE concat_ws(
      ' · ',
      CASE WHEN COALESCE(f.traslado_entrada_kg, 0) > 0 THEN 'Entrada ' || to_char(f.traslado_entrada_kg, 'FM9999999999990.999') || ' kg' END,
      CASE WHEN COALESCE(f.traslado_salida_kg, 0) > 0 THEN 'Salida ' || to_char(f.traslado_salida_kg, 'FM9999999999990.999') || ' kg' END
    )
  END AS traslado_texto_hist,
  COALESCE(f.documentos_hist, '') AS documento_hist,

  f.metadata->>'ingresoAlimento' AS metadata_ingreso_alimento,
  f.metadata->>'traslado' AS metadata_traslado,
  f.metadata->>'documento' AS metadata_documento,

  trim_scale(f.consumo_kg_hembras::numeric) AS consumo_kg_hembras,
  trim_scale(f.consumo_kg_machos::numeric) AS consumo_kg_machos,
  trim_scale(f.consumo_real_dia_kg) AS consumo_real_dia_kg,
  trim_scale(f.consumo_acumulado_kg) AS consumo_acumulado_kg,
  trim_scale(f.consumo_bodega_kg) AS consumo_bodega_kg,

  trim_scale(f.consumo_agua_diario::numeric) AS consumo_agua_diario,

  trim_scale(
    CASE
      WHEN f.saldo_aves_inicio_dia > 0 THEN
        round((100.0 * f.total_mort_sel_dia / f.saldo_aves_inicio_dia)::numeric, 2)
      WHEN f.total_mort_sel_dia > 0 THEN 100::numeric
      ELSE NULL
    END
  ) AS pct_perdidas_dia,

  trim_scale(f.peso_prom_hembras::numeric) AS peso_prom_hembras,
  trim_scale(f.peso_prom_machos::numeric) AS peso_prom_machos,
  f.observaciones,

  f.metadata,
  f.items_adicionales

FROM final f
ORDER BY f.fecha_registro, f.seguimiento_id;

-- -----------------------------------------------------------------------------
-- Vista global (todos los lotes): seguimiento_pollo_engorde_tabla_unificada_vista.sql
--   public.vw_seguimiento_pollo_engorde_unificado
--   Ejemplo: SELECT * FROM public.vw_seguimiento_pollo_engorde_unificado WHERE lote_ave_engorde_id = ?;
-- -----------------------------------------------------------------------------
