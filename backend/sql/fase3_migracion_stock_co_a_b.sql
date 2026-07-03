-- ============================================================================
-- Fase 3 (paso 1) — Migración de CATÁLOGO + STOCK de Colombia: modelo A → modelo B
-- ----------------------------------------------------------------------------
-- Mueve el inventario de Colombia del sistema "viejo" (modelo A: catalogo_items /
-- farm_product_inventory, ruta /inventario) al sistema "nuevo" unificado (modelo B:
-- item_inventario_ecuador / inventario_gestion_stock, ruta /gestion-inventario, con tránsito),
-- scope company_id=1, pais_id=1.
--
-- Validado en local (dry-run BEGIN/ROLLBACK): 61 ítems de catálogo + 17 filas de stock;
-- suma de stock CONSERVADA exacto (667.421 A == 667.421 B). Idempotente (WHERE NOT EXISTS).
--
-- ⚠️ ALCANCE: este script SOLO migra los datos (catálogo + stock). NO cambia el consumo:
--    hasta el paso de "switch de consumo Colombia A→B" (Fase 3 paso 2), Colombia sigue
--    descontando de A. Ejecutar este backfill de forma COORDINADA con ese switch para no
--    dejar stock duplicado en A y B. NO ejecutar en PROD sin OK explícito + backup.
--
-- No hay overlap de códigos A↔B (verificado): se CREA el catálogo Colombia en B bajo
-- company 1/pais 1 → sin colisión con Ecuador (company 3/pais 2). El id-mapeo es por código.
-- Granularidad: A guarda stock a nivel GRANJA (location string); se migra a B a nivel granja
--    (nucleo_id/galpon_id = NULL), agregando por (granja, ítem) para no perder cantidades.
--    El ajuste a granularidad núcleo/galpón (si se requiere para el consumo B de alimento)
--    es parte del paso de switch de consumo.
--
-- Para DRY-RUN: cambiá el COMMIT final por ROLLBACK.
-- ============================================================================
BEGIN;

-- Paso 1 — Catálogo Colombia A → B (idempotente por (company, pais, codigo)).
INSERT INTO item_inventario_ecuador
    (codigo, nombre, tipo_item, concepto, unidad, activo, company_id, pais_id, created_at, updated_at)
SELECT ci.codigo, ci.nombre, ci.item_type, lower(btrim(ci.item_type)), 'kg',
       COALESCE(ci.activo, true), 1, 1, now(), now()
  FROM catalogo_items ci
 WHERE ci.company_id = 1
   AND NOT EXISTS (
       SELECT 1 FROM item_inventario_ecuador e
        WHERE e.company_id = 1 AND e.pais_id = 1
          AND btrim(lower(e.codigo)) = btrim(lower(ci.codigo)));

-- Paso 2 — Stock Colombia A → B, nivel granja (nucleo/galpon NULL), AGREGADO por (granja, ítem)
-- para conservar cantidades cuando A tiene varias filas (distinta location) del mismo ítem/granja.
INSERT INTO inventario_gestion_stock
    (company_id, pais_id, farm_id, item_inventario_ecuador_id, nucleo_id, galpon_id, quantity, unit, created_at, updated_at)
SELECT 1, 1, agg.farm_id, agg.item_b_id, NULL, NULL, agg.qty, 'kg', now(), now()
  FROM (
      SELECT fpi.farm_id, e.id AS item_b_id, SUM(fpi.quantity) AS qty
        FROM farm_product_inventory fpi
        JOIN catalogo_items ci ON ci.id = fpi.catalog_item_id
        JOIN item_inventario_ecuador e
             ON e.company_id = 1 AND e.pais_id = 1
            AND btrim(lower(e.codigo)) = btrim(lower(ci.codigo))
       WHERE fpi.company_id = 1
       GROUP BY fpi.farm_id, e.id
  ) agg
 WHERE NOT EXISTS (
       SELECT 1 FROM inventario_gestion_stock s
        WHERE s.company_id = 1 AND s.pais_id = 1
          AND s.farm_id = agg.farm_id
          AND s.item_inventario_ecuador_id = agg.item_b_id
          AND s.nucleo_id IS NULL AND s.galpon_id IS NULL);

-- Verificación (la suma B debe igualar la suma A de origen).
SELECT 'B catalogo Colombia'                AS k, count(*)::bigint AS v FROM item_inventario_ecuador   WHERE company_id=1 AND pais_id=1
UNION ALL SELECT 'B stock Colombia (filas)',   count(*)::bigint       FROM inventario_gestion_stock WHERE company_id=1 AND pais_id=1
UNION ALL SELECT 'B stock Colombia (suma qty)', sum(quantity)::bigint FROM inventario_gestion_stock WHERE company_id=1 AND pais_id=1
UNION ALL SELECT 'A stock Colombia (suma qty origen)', sum(quantity)::bigint FROM farm_product_inventory WHERE company_id=1;

COMMIT;
