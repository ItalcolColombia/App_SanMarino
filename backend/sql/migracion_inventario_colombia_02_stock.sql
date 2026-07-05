-- ============================================================================
-- Unificación de inventario — Colombia · PASO 2: stock (saldos)
--   Origen : farm_product_inventory   (company_id = 1)              [módulo VIEJO]
--   Destino: inventario_gestion_stock (company_id = 1, pais_id = 1) [módulo NUEVO]
--
-- Requiere PASO 1 (ítems) ejecutado: se mapea catalog_item_id -> codigo -> item_inventario_ecuador_id.
-- Colombia = nivel granja  => nucleo_id / galpon_id = NULL.
-- IDEMPOTENTE: WHERE NOT EXISTS por (company_id, farm_id, item_inventario_ecuador_id, nucleo NULL, galpon NULL).
-- Preserva quantity, unit y la FECHA ORIGINAL de ingreso (created_at/updated_at del inventario viejo).
-- ============================================================================
INSERT INTO public.inventario_gestion_stock
    (company_id, pais_id, farm_id, nucleo_id, galpon_id, item_inventario_ecuador_id, quantity, unit, created_at, updated_at)
SELECT
    1, 1, fpi.farm_id, NULL, NULL, i.id, fpi.quantity, fpi.unit, fpi.created_at, fpi.updated_at
FROM public.farm_product_inventory fpi
JOIN public.catalogo_items          c ON c.id = fpi.catalog_item_id
JOIN public.item_inventario_ecuador i ON i.company_id = 1 AND i.pais_id = 1 AND i.codigo = c.codigo
WHERE fpi.company_id = 1
  AND NOT EXISTS (
      SELECT 1 FROM public.inventario_gestion_stock s
      WHERE s.company_id = 1
        AND s.farm_id = fpi.farm_id
        AND s.item_inventario_ecuador_id = i.id
        AND s.nucleo_id IS NULL
        AND s.galpon_id IS NULL
  );
