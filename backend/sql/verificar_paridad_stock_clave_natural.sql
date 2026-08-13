-- ============================================================================================
-- GATE de la Fase B (silos) — paridad de la CLAVE NATURAL de inventario_gestion_stock
-- Plan: fase_de_desarrollo/santa_reyes_silos_bodegas_inventario_plan.md  (secciones 4.6 y 10.3)
--
-- POR QUE EXISTE
-- --------------
-- La Fase B cambia el indice unico `ux_inventario_gestion_stock_clave_natural` para sumarle
-- `COALESCE(silo_id, 0)`. Ese indice esta cableado POR EXPRESION en el `ON CONFLICT` de
-- `SumarStockAtomicoAsync` (InventarioGestion/Funciones/InventarioGestionService.StockAtomico.cs):
-- Postgres exige que el inferidor coincida exactamente con el indice. Si el indice y la sentencia
-- quedan desalineados, TODO ingreso de TODAS las empresas falla con
-- «no unique or exclusion constraint matching the ON CONFLICT specification».
--
-- Para las empresas con el flag OFF, `silo_id` es siempre NULL => `COALESCE(silo_id,0)` es la
-- constante 0 => la clave nueva es EQUIVALENTE a la vieja. Esto tiene que verse en el dato: el
-- conteo de claves naturales distintas y la suma de saldos por empresa NO pueden moverse.
--
-- COMO SE USA
-- -----------
--   psql ... -f backend/sql/verificar_paridad_stock_clave_natural.sql   > antes.txt   # ANTES del swap
--   psql ... -f backend/sql/verificar_paridad_stock_clave_natural.sql   > despues.txt # DESPUES
--   diff antes.txt despues.txt
--
-- El bloque 1 (definicion del indice) es el UNICO que puede cambiar entre las dos corridas.
-- Cualquier diferencia en los bloques 2 a 6 es una regresion y bloquea el merge.
--
-- Es de solo lectura: no escribe nada.
-- ============================================================================================

\pset pager off
\pset footer off

-- Cuenta los `silo_id` no nulos SIN romper cuando la columna todavia no existe (corrida «antes»).
-- Devuelve -1 mientras la migracion de la Fase B no se haya aplicado.
CREATE OR REPLACE FUNCTION pg_temp.silo_no_nulos(p_tabla text) RETURNS bigint
LANGUAGE plpgsql AS $$
DECLARE n bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = p_tabla AND column_name = 'silo_id') THEN
    EXECUTE format('SELECT count(*) FROM public.%I WHERE silo_id IS NOT NULL', p_tabla) INTO n;
    RETURN n;
  END IF;
  RETURN -1;   -- la columna aun no existe
END $$;

\echo ''
\echo '=== 1. Indice vigente de la clave natural (lo unico que puede cambiar entre corridas) ==='
SELECT indexdef
FROM pg_indexes
WHERE schemaname = 'public'
  AND tablename  = 'inventario_gestion_stock'
  AND indexname  = 'ux_inventario_gestion_stock_clave_natural';

\echo ''
\echo '=== 2. Stock por empresa: filas, claves naturales distintas y saldo total ==='
SELECT s.company_id,
       c.name                                        AS empresa,
       c.maneja_inventario_por_silo                  AS flag_silo,
       count(*)                                      AS filas,
       count(DISTINCT (s.farm_id, s.item_inventario_ecuador_id,
                       COALESCE(s.nucleo_id, ''), COALESCE(s.galpon_id, '')))
                                                     AS claves_naturales,
       sum(s.quantity)                               AS saldo_total
FROM inventario_gestion_stock s
JOIN companies c ON c.id = s.company_id
GROUP BY s.company_id, c.name, c.maneja_inventario_por_silo
ORDER BY s.company_id;

\echo ''
\echo '=== 3. Claves naturales DUPLICADAS (tiene que dar 0 filas: lo garantiza el indice unico) ==='
SELECT farm_id, item_inventario_ecuador_id,
       COALESCE(nucleo_id, '') AS nucleo, COALESCE(galpon_id, '') AS galpon,
       count(*) AS filas
FROM inventario_gestion_stock
GROUP BY farm_id, item_inventario_ecuador_id, COALESCE(nucleo_id, ''), COALESCE(galpon_id, '')
HAVING count(*) > 1
ORDER BY 1, 2, 3, 4;

\echo ''
\echo '=== 4. silo_id no nulos (-1 = la columna todavia no existe; con flag OFF debe ser 0) ==='
SELECT pg_temp.silo_no_nulos('inventario_gestion_stock')            AS stock_con_silo,
       pg_temp.silo_no_nulos('inventario_gestion_movimiento')       AS movimiento_con_silo,
       pg_temp.silo_no_nulos('lote_registro_historico_unificado')   AS historico_con_silo;

\echo ''
\echo '=== 5. Movimientos por empresa y tipo (el smoke no puede mover nada que no sea suyo) ==='
SELECT m.company_id, m.movement_type, count(*) AS movimientos, sum(m.quantity) AS cantidad
FROM inventario_gestion_movimiento m
GROUP BY m.company_id, m.movement_type
ORDER BY m.company_id, m.movement_type;

\echo ''
\echo '=== 6. Espejo historico unificado: activas vs anuladas (el historico se anula, no se abandona) ==='
SELECT anulado, count(*) AS filas, sum(cantidad_kg) AS cantidad_kg
FROM lote_registro_historico_unificado
GROUP BY anulado
ORDER BY anulado;
