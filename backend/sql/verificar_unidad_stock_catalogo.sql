-- backend/sql/verificar_unidad_stock_catalogo.sql
-- Gate de TK-2026-000019 — «la unidad del stock la manda el catálogo».
--
-- Se corre ANTES y DESPUÉS de la migración AlinearUnidadInventarioConCatalogo. Lo que tiene que
-- pasar:
--   1) `divergentes` baja a 0 en TODAS las empresas.
--   2) `filas_alimento_divergentes` es 0 en las dos corridas: la unidad es una ETIQUETA y ningún
--      saldo de alimento (que siempre es kg) puede moverse por este cambio.
--   3) `suma_cantidad` por empresa es IDÉNTICA antes y después: no se convierte ninguna cantidad.
--
-- Uso:
--   psql "$CONN" -f backend/sql/verificar_unidad_stock_catalogo.sql

\echo '== 1. Stock: filas divergentes del catálogo, por empresa =========================='
SELECT c.name                                   AS empresa,
       COUNT(*)                                 AS filas,
       COUNT(*) FILTER (
           WHERE LOWER(TRIM(s.unit)) IS DISTINCT FROM LOWER(TRIM(i.unidad))
       )                                        AS divergentes,
       SUM(s.quantity)                          AS suma_cantidad
FROM   inventario_gestion_stock s
JOIN   item_inventario_ecuador i ON i.id = s.item_inventario_ecuador_id
JOIN   companies c               ON c.id = s.company_id
GROUP  BY c.name
ORDER  BY c.name;

\echo '== 2. ALIMENTO: tiene que dar 0 SIEMPRE (antes y después) ========================'
SELECT c.name                                   AS empresa,
       COUNT(*)                                 AS filas_alimento,
       COUNT(*) FILTER (
           WHERE LOWER(TRIM(s.unit)) IS DISTINCT FROM LOWER(TRIM(i.unidad))
       )                                        AS filas_alimento_divergentes
FROM   inventario_gestion_stock s
JOIN   item_inventario_ecuador i ON i.id = s.item_inventario_ecuador_id
JOIN   companies c               ON c.id = s.company_id
WHERE  LOWER(COALESCE(i.concepto, i.tipo_item, '')) = 'alimento'
GROUP  BY c.name
ORDER  BY c.name;

\echo '== 3. Movimientos e histórico unificado: divergentes ============================='
SELECT 'inventario_gestion_movimiento' AS tabla,
       COUNT(*)                        AS filas,
       COUNT(*) FILTER (
           WHERE LOWER(TRIM(m.unit)) IS DISTINCT FROM LOWER(TRIM(i.unidad))
       )                               AS divergentes
FROM   inventario_gestion_movimiento m
JOIN   item_inventario_ecuador i ON i.id = m.item_inventario_ecuador_id
UNION ALL
SELECT 'lote_registro_historico_unificado',
       COUNT(*),
       COUNT(*) FILTER (
           WHERE LOWER(TRIM(h.unidad)) IS DISTINCT FROM LOWER(TRIM(i.unidad))
       )
FROM   lote_registro_historico_unificado h
JOIN   item_inventario_ecuador i ON i.id = h.item_inventario_ecuador_id
WHERE  h.unidad IS NOT NULL;

\echo '== 4. Detalle de lo que todavía diverge (debe quedar vacío) ======================'
SELECT c.name AS empresa, i.codigo, i.nombre, i.unidad AS catalogo, s.unit AS stock, COUNT(*) AS filas
FROM   inventario_gestion_stock s
JOIN   item_inventario_ecuador i ON i.id = s.item_inventario_ecuador_id
JOIN   companies c               ON c.id = s.company_id
WHERE  LOWER(TRIM(s.unit)) IS DISTINCT FROM LOWER(TRIM(i.unidad))
GROUP  BY 1, 2, 3, 4, 5
ORDER  BY 1, 2;

\echo '== 5. Vocabulario en uso (tiene que caer dentro del selector del catálogo) ======='
SELECT 'catalogo' AS origen, unidad AS valor, COUNT(*) FROM item_inventario_ecuador GROUP BY 1, 2
UNION ALL
SELECT 'stock', unit, COUNT(*) FROM inventario_gestion_stock GROUP BY 1, 2
ORDER  BY 1, 3 DESC;
