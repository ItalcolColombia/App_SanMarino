-- =============================================================================
-- Fix general: sincronizar lote_registro_historico_unificado con inventario_gestion_movimiento
-- Aplica a TODOS los galpones / lotes donde el trigger dejó lote_ave_engorde_id = NULL
-- o donde el registro nunca llegó al histórico (trigger no encontró el lote activo).
--
-- CUÁNDO OCURRE EL PROBLEMA:
--   El trigger trg_lote_hist_desde_inventario_gestion llama a
--   fn_lote_ave_engorde_id_desde_ubicacion(farm_id, nucleo_id, galpon_id) en el
--   momento del INSERT. Si el lote todavía no existía (o aún no tenía deleted_at=NULL),
--   la función devuelve NULL y el registro queda sin lote, invisible para el API.
--
-- PASOS:
--   PASO 1 — Actualiza filas con lote_ave_engorde_id = NULL resolviendo el lote actual.
--   PASO 2 — Inserta filas que nunca llegaron al histórico.
--   PASO 3 — Recalcula acumulado_entradas_alimento_kg en todos los lotes tocados.
--
-- NOTA: Si aparece "current transaction is aborted, commands ignored..."
--   → ROLLBACK; y vuelve a ejecutar.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- PASO 1: Corregir filas con lote_ave_engorde_id = NULL
--         usando fn_lote_ave_engorde_id_desde_ubicacion (lote activo actual)
-- ---------------------------------------------------------------------------
UPDATE public.lote_registro_historico_unificado h
SET lote_ave_engorde_id = public.fn_lote_ave_engorde_id_desde_ubicacion(
    h.farm_id, h.nucleo_id, h.galpon_id
)
WHERE h.origen_tabla = 'inventario_gestion_movimiento'
  AND h.lote_ave_engorde_id IS NULL
  AND public.fn_lote_ave_engorde_id_desde_ubicacion(h.farm_id, h.nucleo_id, h.galpon_id) IS NOT NULL;

-- Cuántas filas corrigió PASO 1:
-- SELECT COUNT(*) AS paso1_corregidas
-- FROM public.lote_registro_historico_unificado
-- WHERE origen_tabla = 'inventario_gestion_movimiento'
--   AND lote_ave_engorde_id IS NOT NULL;

-- ---------------------------------------------------------------------------
-- PASO 2: Insertar filas de inventario_gestion_movimiento que no tienen entrada
--         en lote_registro_historico_unificado (trigger nunca las procesó o falló)
--         Solo para galpones donde hoy existe un lote activo.
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
    public.fn_lote_ave_engorde_id_desde_ubicacion(m.farm_id, m.nucleo_id, m.galpon_id) AS lote_ave_engorde_id,
    m.farm_id,
    m.nucleo_id,
    m.galpon_id,
    (m.created_at AT TIME ZONE 'UTC')::DATE                    AS fecha_operacion,
    public.fn_tipo_evento_inventario(m.movement_type)          AS tipo_evento,
    'inventario_gestion_movimiento'                             AS origen_tabla,
    m.id                                                        AS origen_id,
    m.movement_type                                             AS movement_type_original,
    m.item_inventario_ecuador_id,
    CONCAT(i.codigo, ' — ', i.nombre)                          AS item_resumen,
    m.quantity                                                  AS cantidad_kg,
    m.unit                                                      AS unidad,
    m.reference                                                 AS referencia,
    NULL                                                        AS numero_documento,
    NULL                                                        AS acumulado_entradas_alimento_kg,
    FALSE                                                       AS anulado,
    m.created_at
FROM public.inventario_gestion_movimiento m
JOIN public.item_inventario_ecuador i ON i.id = m.item_inventario_ecuador_id
WHERE
    -- Solo tipos de movimiento que van al histórico
    m.movement_type IN (
        'Ingreso',
        'TrasladoEntrada',
        'TrasladoInterGranjaEntrada',
        'TrasladoSalida',
        'TrasladoInterGranjaSalida',
        'TrasladoInterGranjaPendiente'
    )
    -- Solo los que todavía no tienen fila en el histórico
    AND NOT EXISTS (
        SELECT 1
        FROM public.lote_registro_historico_unificado h
        WHERE h.origen_tabla = 'inventario_gestion_movimiento'
          AND h.origen_id    = m.id
    )
    -- Solo si hoy existe un lote activo en ese galpón (evita insertar con NULL)
    AND public.fn_lote_ave_engorde_id_desde_ubicacion(m.farm_id, m.nucleo_id, m.galpon_id) IS NOT NULL
ON CONFLICT (origen_tabla, origen_id) DO NOTHING;

-- Cuántas filas insertó PASO 2:
-- SELECT COUNT(*) AS paso2_insertadas
-- FROM public.lote_registro_historico_unificado
-- WHERE origen_tabla = 'inventario_gestion_movimiento'
--   AND created_at > NOW() - INTERVAL '5 minutes';

-- ---------------------------------------------------------------------------
-- PASO 3: Recalcular acumulado_entradas_alimento_kg en todos los lotes
--         que tienen al menos un INV_INGRESO / INV_TRASLADO_ENTRADA no anulado.
--         (Ventana acumulativa por lote, ordenada por fecha + id)
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
    WHERE h.lote_ave_engorde_id IS NOT NULL
      AND NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA')
)
UPDATE public.lote_registro_historico_unificado t
SET acumulado_entradas_alimento_kg = s.acum
FROM sums s
WHERE t.id = s.id;

-- ---------------------------------------------------------------------------
-- Diagnóstico: galpones que aún tienen filas sin lote (deberían ser 0 tras el fix)
-- ---------------------------------------------------------------------------
-- SELECT farm_id, nucleo_id, galpon_id, COUNT(*) AS filas_sin_lote
-- FROM public.lote_registro_historico_unificado
-- WHERE origen_tabla = 'inventario_gestion_movimiento'
--   AND lote_ave_engorde_id IS NULL
-- GROUP BY farm_id, nucleo_id, galpon_id
-- ORDER BY farm_id, galpon_id;

-- ---------------------------------------------------------------------------
-- Diagnóstico: resumen de cuántos registros tiene cada lote en el histórico
-- ---------------------------------------------------------------------------
-- SELECT
--     l.lote_nombre,
--     h.lote_ave_engorde_id,
--     h.farm_id,
--     h.galpon_id,
--     COUNT(*) FILTER (WHERE h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA')) AS entradas,
--     COUNT(*) FILTER (WHERE h.tipo_evento IN ('INV_TRASLADO_SALIDA'))                AS salidas,
--     COUNT(*) FILTER (WHERE h.tipo_evento = 'INV_CONSUMO')                           AS consumos,
--     COUNT(*) FILTER (WHERE h.tipo_evento = 'VENTA_AVES')                            AS ventas
-- FROM public.lote_registro_historico_unificado h
-- JOIN public.lote_ave_engorde l ON l.lote_ave_engorde_id = h.lote_ave_engorde_id
-- GROUP BY l.lote_nombre, h.lote_ave_engorde_id, h.farm_id, h.galpon_id
-- ORDER BY h.farm_id, h.galpon_id, h.lote_ave_engorde_id;
