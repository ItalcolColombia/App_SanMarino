-- =============================================================================
-- DIAGNÓSTICO: historico unificado para lote 2 (todos los eventos de alimento)
-- Muestra INV_INGRESO e INV_CONSUMO del galpón asociado al lote 2,
-- incluido el estado anulado y la referencia completa.
-- =============================================================================

-- Paso 1: datos del lote 2
SELECT lote_ave_engorde_id, lote_nombre, granja_id, nucleo_id, galpon_id, fecha_encaset
FROM public.lote_ave_engorde
WHERE lote_ave_engorde_id = 2;

-- Paso 2: todos los movimientos de alimento del galpón del lote 2
SELECT
  h.id,
  h.tipo_evento,
  h.movement_type_original,
  h.cantidad_kg,
  h.fecha_operacion,
  h.anulado,
  h.referencia,
  h.lote_ave_engorde_id
FROM public.lote_registro_historico_unificado h
WHERE h.tipo_evento IN ('INV_INGRESO', 'INV_CONSUMO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
  AND h.farm_id = (SELECT granja_id FROM public.lote_ave_engorde WHERE lote_ave_engorde_id = 2 LIMIT 1)
  AND COALESCE(TRIM(h.nucleo_id), '') = COALESCE(TRIM((SELECT nucleo_id FROM public.lote_ave_engorde WHERE lote_ave_engorde_id = 2 LIMIT 1)), '')
  AND COALESCE(TRIM(h.galpon_id), '') = COALESCE(TRIM((SELECT galpon_id FROM public.lote_ave_engorde WHERE lote_ave_engorde_id = 2 LIMIT 1)), '')
ORDER BY h.fecha_operacion, h.id;

-- Paso 3: INV_INGRESO activos que pasarían los filtros del backend
-- (lo que realmente entraría al cálculo de saldo)
SELECT
  h.id,
  h.tipo_evento,
  h.cantidad_kg,
  h.fecha_operacion,
  h.referencia
FROM public.lote_registro_historico_unificado h
WHERE h.tipo_evento = 'INV_INGRESO'
  AND h.anulado = FALSE
  AND h.farm_id = (SELECT granja_id FROM public.lote_ave_engorde WHERE lote_ave_engorde_id = 2 LIMIT 1)
  AND COALESCE(TRIM(h.nucleo_id), '') = COALESCE(TRIM((SELECT nucleo_id FROM public.lote_ave_engorde WHERE lote_ave_engorde_id = 2 LIMIT 1)), '')
  AND COALESCE(TRIM(h.galpon_id), '') = COALESCE(TRIM((SELECT galpon_id FROM public.lote_ave_engorde WHERE lote_ave_engorde_id = 2 LIMIT 1)), '')
  -- Excluir devoluciones generadas por el sistema de seguimiento
  AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
ORDER BY h.fecha_operacion, h.id;

-- Paso 4: seguimientos activos del lote 2 con su saldo almacenado
SELECT id, fecha, consumo_kg_hembras, consumo_kg_machos, saldo_alimento_kg
FROM public.seguimiento_diario_aves_engorde
WHERE lote_ave_engorde_id = 2
ORDER BY fecha, id;

-- Paso 5: INV_INGRESO que serían excluidos por el filtro (devoluciones)
SELECT
  h.id,
  h.tipo_evento,
  h.cantidad_kg,
  h.fecha_operacion,
  h.anulado,
  h.referencia
FROM public.lote_registro_historico_unificado h
WHERE h.tipo_evento = 'INV_INGRESO'
  AND h.referencia LIKE 'Seguimiento aves engorde #%'
  AND h.farm_id = (SELECT granja_id FROM public.lote_ave_engorde WHERE lote_ave_engorde_id = 2 LIMIT 1)
  AND COALESCE(TRIM(h.nucleo_id), '') = COALESCE(TRIM((SELECT nucleo_id FROM public.lote_ave_engorde WHERE lote_ave_engorde_id = 2 LIMIT 1)), '')
  AND COALESCE(TRIM(h.galpon_id), '') = COALESCE(TRIM((SELECT galpon_id FROM public.lote_ave_engorde WHERE lote_ave_engorde_id = 2 LIMIT 1)), '')
ORDER BY h.fecha_operacion, h.id;
