-- ============================================================================
-- Unificación de inventario — Colombia · PASO 3: movimientos (histórico/kardex)
--   Origen : farm_inventory_movements    (company_id = 1, 323 filas)  [módulo VIEJO]
--   Destino: inventario_gestion_movimiento (company_id = 1, pais_id = 1) [módulo NUEVO]
--
-- Requiere PASO 1 (ítems). Colombia = nivel granja => nucleo/galpon NULL.
-- Mapeo de tipo (preserva el SIGNO del kardex → reconcilia con el stock migrado):
--   Entry       -> Ingreso          (+)
--   Exit        -> Consumo          (-)   (252 'Consumo diario' + 1 'Devolución')
--   TransferIn  -> TrasladoEntrada  (+)
--   TransferOut -> TrasladoSalida   (-)
-- Reconciliación verificada: SUM(mov con signo) == saldo stock en los 20 pares (farm,item).
-- Preserva created_at original (orden del kardex), reference, reason, transfer_group_id, usuario.
-- IDEMPOTENTE: NOT EXISTS por (company, farm, item, created_at, quantity, unit).
-- ============================================================================
INSERT INTO public.inventario_gestion_movimiento
    (company_id, pais_id, farm_id, nucleo_id, galpon_id, item_inventario_ecuador_id,
     quantity, unit, movement_type, reference, reason, transfer_group_id,
     created_at, created_by_user_id, estado)
SELECT
    1, 1, m.farm_id, NULL, NULL, i.id,
    m.quantity, m.unit,
    CASE m.movement_type
        WHEN 'Entry'       THEN 'Ingreso'
        WHEN 'Exit'        THEN 'Consumo'
        WHEN 'TransferIn'  THEN 'TrasladoEntrada'
        WHEN 'TransferOut' THEN 'TrasladoSalida'
        ELSE 'AjusteStock'
    END,
    m.reference,
    COALESCE(NULLIF(m.reason, ''), m.destination, m.origin),  -- preserva 'Consumo diario'/'Devolución'/origen
    m.transfer_group_id,
    m.created_at,
    m.responsible_user_id,
    NULL
FROM public.farm_inventory_movements m
JOIN public.catalogo_items          c ON c.id = m.catalog_item_id
JOIN public.item_inventario_ecuador i ON i.company_id = 1 AND i.pais_id = 1 AND i.codigo = c.codigo
WHERE m.company_id = 1
  AND NOT EXISTS (
      SELECT 1 FROM public.inventario_gestion_movimiento d
      WHERE d.company_id = 1
        AND d.farm_id = m.farm_id
        AND d.item_inventario_ecuador_id = i.id
        AND d.created_at = m.created_at
        AND d.quantity = m.quantity
        AND d.unit = m.unit
  );
