-- ============================================================================
-- fase2_limpieza_espurios.sql
-- Fase 2 / S1 — Limpieza de datos ESPURIOS previos a habilitar el descuento
-- de inventario en Colombia (modelo A).
--
-- Borra 5 filas residuo del bug pre-Fase-1 (descuento cross-país silencioso):
--   (A) Modelo B — 2 filas de Colombia (company 1 / pais 1) que nunca debieron existir:
--         * inventario_gestion_movimiento id=5705  (Ingreso 323 kg, item 89,
--           "Seguimiento lote levante #892 (devolución por eliminación)")
--         * inventario_gestion_stock      id=352   (323 kg, item 89, farm 20)
--       Colombia opera en modelo A; su único stock legítimo vive en
--       farm_product_inventory. Estas 2 filas son el residuo del consumo cross-país
--       que la Fase 1 cerró (colisión de ids: item_inventario_ecuador.id=89 = medicamento
--       Ecuador vs catalogo_items.id=89 = pollita Colombia).
--   (B) catalogo_items — 3 filas de Ecuador (pais 2, company 3) que contaminan el
--       catálogo del modelo A (Colombia): ids 303 (ALI3454), 304 (SM0009), 305 (SM0047).
--
-- IDEMPOTENTE: los DELETE usan WHERE exactos (id + company_id + pais_id + item);
-- re-ejecutar tras el primer borrado no afecta nada (0 filas). Los bloques de
-- verificación (SELECT antes/después) muestran el estado.
--
-- ⚠️ EJECUTAR SOLO EN LOCAL (sanmarinoapplocal :5433). PRODUCCIÓN REQUIERE OK APARTE:
--    las mismas filas espurias pueden (o no) existir en RDS prod; verificar con los
--    SELECT antes de tocar prod, y correr solo con confirmación explícita del usuario.
-- ============================================================================

\echo '=== ANTES — Modelo B Colombia (company 1 / pais 1): esperado mov=5705 y stock=352 ==='
SELECT 'inventario_gestion_movimiento' AS tabla, id, company_id, pais_id, farm_id,
       item_inventario_ecuador_id, quantity, movement_type, reference
FROM   inventario_gestion_movimiento
WHERE  company_id = 1 AND pais_id = 1;

SELECT 'inventario_gestion_stock' AS tabla, id, company_id, pais_id, farm_id,
       item_inventario_ecuador_id, quantity
FROM   inventario_gestion_stock
WHERE  company_id = 1 AND pais_id = 1;

\echo '=== ANTES — catalogo_items pais 2 (Ecuador espurias): esperado ids 303/304/305 ==='
SELECT id, company_id, pais_id, codigo, nombre
FROM   catalogo_items
WHERE  pais_id = 2
ORDER  BY id;

BEGIN;

-- (A) Modelo B — 2 filas espurias Colombia. WHERE exacto por id + scope + item.
DELETE FROM inventario_gestion_movimiento
WHERE  id = 5705 AND company_id = 1 AND pais_id = 1 AND item_inventario_ecuador_id = 89;

DELETE FROM inventario_gestion_stock
WHERE  id = 352 AND company_id = 1 AND pais_id = 1 AND item_inventario_ecuador_id = 89;

-- (B) catalogo_items — 3 filas Ecuador (pais 2) que contaminan el catálogo A.
DELETE FROM catalogo_items
WHERE  pais_id = 2 AND company_id = 3 AND id IN (303, 304, 305);

COMMIT;

\echo '=== DESPUES — Modelo B Colombia (company 1 / pais 1): esperado 0 filas en ambas ==='
SELECT 'inventario_gestion_movimiento' AS tabla, count(*) AS filas_co
FROM   inventario_gestion_movimiento
WHERE  company_id = 1 AND pais_id = 1;

SELECT 'inventario_gestion_stock' AS tabla, count(*) AS filas_co
FROM   inventario_gestion_stock
WHERE  company_id = 1 AND pais_id = 1;

\echo '=== DESPUES — catalogo_items pais 2: esperado 0 filas ==='
SELECT count(*) AS filas_pais2
FROM   catalogo_items
WHERE  pais_id = 2;
