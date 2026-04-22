-- =============================================================================
-- Fix: ingresos de alimento (INV_INGRESO / INV_TRASLADO*) → lote_registro_historico_unificado
-- Lote: lote_ave_engorde_id = 26  (granja 37, núcleo 198400, galpón G0034)
--
-- Problema: el trigger trg_lote_hist_desde_inventario_gestion inserta los registros
-- de inventario_gestion_movimiento en lote_registro_historico_unificado resolviendo
-- el lote vía fn_lote_ave_engorde_id_desde_ubicacion() en el momento del INSERT.
-- Para este galpón los registros quedaron con lote_ave_engorde_id = NULL o con un
-- lote_id distinto (lote inexistente / diferente al momento del INSERT), por lo que
-- el API (que filtra por lote_ave_engorde_id = 26) no los devuelve.
--
-- Este script:
--   PASO 1 — Actualiza filas existentes con lote_ave_engorde_id incorrecto/NULL.
--   PASO 2 — Inserta las filas faltantes (trigger no había sido ejecutado).
--   PASO 3 — Recalcula acumulado_entradas_alimento_kg para el lote 26.
--
-- Si ves: "current transaction is aborted, commands ignored until end of transaction block"
--   → ROLLBACK; y vuelve a ejecutar.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- PASO 1: Corregir filas existentes con lote_ave_engorde_id NULL o distinto de 26
-- ---------------------------------------------------------------------------
UPDATE public.lote_registro_historico_unificado h
SET lote_ave_engorde_id = 26
WHERE h.origen_tabla = 'inventario_gestion_movimiento'
  AND (h.lote_ave_engorde_id IS NULL OR h.lote_ave_engorde_id <> 26)
  AND h.farm_id  = 37
  AND COALESCE(TRIM(h.nucleo_id), '') = '198400'
  AND COALESCE(TRIM(h.galpon_id), '') = 'G0034';

-- Verificar cuántas filas actualizó PASO 1:
-- SELECT COUNT(*) AS filas_corregidas
-- FROM public.lote_registro_historico_unificado
-- WHERE origen_tabla = 'inventario_gestion_movimiento'
--   AND lote_ave_engorde_id = 26
--   AND farm_id = 37 AND nucleo_id = '198400' AND galpon_id = 'G0034';

-- ---------------------------------------------------------------------------
-- PASO 2: Insertar filas faltantes (sin entrada en lote_registro_historico_unificado)
-- ---------------------------------------------------------------------------
INSERT INTO public.lote_registro_historico_unificado (
    company_id,
    lote_ave_engorde_id,
    farm_id,
    nucleo_id,
    galpon_id,
    fecha_operacion,
    tipo_evento,
    origen_tabla,
    origen_id,
    movement_type_original,
    item_inventario_ecuador_id,
    item_resumen,
    cantidad_kg,
    unidad,
    referencia,
    numero_documento,
    acumulado_entradas_alimento_kg,
    anulado,
    created_at
)
SELECT
    m.company_id,
    26                                                  AS lote_ave_engorde_id,
    m.farm_id,
    m.nucleo_id,
    m.galpon_id,
    (m.created_at AT TIME ZONE 'UTC')::DATE             AS fecha_operacion,
    public.fn_tipo_evento_inventario(m.movement_type)  AS tipo_evento,
    'inventario_gestion_movimiento'                     AS origen_tabla,
    m.id                                                AS origen_id,
    m.movement_type                                     AS movement_type_original,
    m.item_inventario_ecuador_id,
    CONCAT(i.codigo, ' — ', i.nombre)                  AS item_resumen,
    m.quantity                                          AS cantidad_kg,
    m.unit                                              AS unidad,
    m.reference                                         AS referencia,
    NULL                                                AS numero_documento,
    NULL                                                AS acumulado_entradas_alimento_kg,
    FALSE                                               AS anulado,
    m.created_at
FROM public.inventario_gestion_movimiento m
JOIN public.item_inventario_ecuador i ON i.id = m.item_inventario_ecuador_id
WHERE m.farm_id  = 37
  AND COALESCE(TRIM(m.nucleo_id), '') = '198400'
  AND COALESCE(TRIM(m.galpon_id), '') = 'G0034'
  -- Solo tipos que van al histórico de alimento (excluye Consumo que va en seguimiento)
  AND m.movement_type IN ('Ingreso', 'TrasladoEntrada', 'TrasladoInterGranjaEntrada',
                           'TrasladoSalida', 'TrasladoInterGranjaSalida',
                           'TrasladoInterGranjaPendiente')
  -- Solo los que todavía no tienen fila en el histórico
  AND NOT EXISTS (
      SELECT 1
      FROM public.lote_registro_historico_unificado h
      WHERE h.origen_tabla = 'inventario_gestion_movimiento'
        AND h.origen_id    = m.id
  )
ON CONFLICT (origen_tabla, origen_id) DO NOTHING;

-- Verificar cuántas filas insertó PASO 2:
-- SELECT COUNT(*) AS filas_insertadas
-- FROM public.lote_registro_historico_unificado
-- WHERE origen_tabla = 'inventario_gestion_movimiento'
--   AND lote_ave_engorde_id = 26
--   AND farm_id = 37 AND nucleo_id = '198400' AND galpon_id = 'G0034';

-- ---------------------------------------------------------------------------
-- PASO 3: Recalcular acumulado_entradas_alimento_kg para el lote 26
-- (solo filas de tipo entrada, no anuladas)
-- ---------------------------------------------------------------------------
WITH sums AS (
    SELECT
        h.id,
        SUM(COALESCE(h.cantidad_kg, 0)) OVER (
            PARTITION BY h.lote_ave_engorde_id
            ORDER BY h.fecha_operacion, h.id
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS acum
    FROM public.lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = 26
      AND NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA')
)
UPDATE public.lote_registro_historico_unificado t
SET acumulado_entradas_alimento_kg = s.acum
FROM sums s
WHERE t.id = s.id;

-- ---------------------------------------------------------------------------
-- Diagnóstico final: todos los registros del lote 26 en el histórico
-- ---------------------------------------------------------------------------
-- SELECT id, tipo_evento, fecha_operacion, cantidad_kg, referencia, anulado,
--        acumulado_entradas_alimento_kg
-- FROM public.lote_registro_historico_unificado
-- WHERE lote_ave_engorde_id = 26
-- ORDER BY fecha_operacion, id;
