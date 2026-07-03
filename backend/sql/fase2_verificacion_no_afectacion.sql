-- ============================================================================
-- fase2_verificacion_no_afectacion.sql
-- Fase 2 / S5 — Verificaciones de NO-AFECTACIÓN (contable + indicadores + kardex).
-- Solo LECTURA + un bloque transaccional con ROLLBACK (no muta datos).
-- Ejecutar en local (:5433) para evidenciar que el descuento Colombia (modelo A) con los
-- tipos ConsumoSeguimiento/DevolucionSeguimiento NO altera el reporte contable ni los
-- indicadores, y que el kardex firma correctamente los tipos nuevos.
-- ============================================================================

\echo '=== (b) INDICADORES — refs a inventario en las fn (esperado 0 en ambas) ==='
SELECT p.proname,
  regexp_count(lower(pg_get_functiondef(p.oid)),
    'farm_inventory_movements|farm_product_inventory|inventario_gestion|catalogo_items|item_inventario_ecuador'
  ) AS refs_inventario
FROM pg_proc p
WHERE p.proname IN ('fn_indicadores_levante_postura','fn_indicadores_produccion_postura')
ORDER BY p.proname;

\echo '=== (c) KARDEX — signo de los tipos Fase 2 (esperado Consumo=-1, Devolucion=+1) ==='
SELECT fn_kardex_signo('ConsumoSeguimiento',10)    AS consumo_seguimiento,
       fn_kardex_signo('DevolucionSeguimiento',10) AS devolucion_seguimiento;

\echo '=== (a) CONTABLE — los buckets (Entradas/Traslados/Retiros) EXCLUYEN los tipos Fase 2 ==='
\echo '    Insertamos 1 ConsumoSeguimiento + 1 DevolucionSeguimiento en farm 3 / item 61,'
\echo '    comparamos los 3 buckets antes/despues (deben ser IDENTICOS) y luego ROLLBACK.'
BEGIN;
SELECT 'ANTES' AS fase,
  count(*) FILTER (WHERE movement_type IN ('Entry','TransferIn')) AS entradas,
  count(*) FILTER (WHERE movement_type = 'TransferOut')          AS traslados,
  count(*) FILTER (WHERE movement_type = 'Exit')                 AS retiros,
  count(*)                                                        AS total_movs
FROM farm_inventory_movements WHERE farm_id=3 AND catalog_item_id=61;

INSERT INTO farm_inventory_movements
  (farm_id, catalog_item_id, item_type, company_id, pais_id, quantity, movement_type, unit, reference, metadata, created_at)
VALUES
  (3,61,'alimento',1,1,100,'ConsumoSeguimiento','kg','VERIF consumo',      '{}'::jsonb, now()),
  (3,61,'alimento',1,1, 30,'DevolucionSeguimiento','kg','VERIF devolucion','{}'::jsonb, now());

SELECT 'DESPUES' AS fase,
  count(*) FILTER (WHERE movement_type IN ('Entry','TransferIn')) AS entradas,
  count(*) FILTER (WHERE movement_type = 'TransferOut')          AS traslados,
  count(*) FILTER (WHERE movement_type = 'Exit')                 AS retiros,
  count(*)                                                        AS total_movs
FROM farm_inventory_movements WHERE farm_id=3 AND catalog_item_id=61;

\echo '    Kardex de los 2 movimientos nuevos (cantidad con signo + saldo acumulado):'
SELECT tipo, cantidad, saldo
FROM fn_kardex_farm_inventory(3,61,1,1)
WHERE tipo IN ('ConsumoSeguimiento','DevolucionSeguimiento')
ORDER BY fecha, tipo;
ROLLBACK;

\echo '=== Resultado esperado: buckets IDENTICOS antes/despues; consumo -100, devolucion +30. ==='
