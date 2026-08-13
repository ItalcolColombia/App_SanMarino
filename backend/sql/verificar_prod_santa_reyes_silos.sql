-- backend/sql/verificar_prod_santa_reyes_silos.sql
--
-- Chequeo de go-live de los silos de Santa Reyes contra datos REALES de producción.
-- Se corre sobre la BD local YA refrescada con el dump de prod, ANTES de desplegar.
--
-- SOLO LECTURA: no inserta, no actualiza, no borra. Se puede correr las veces que haga falta.
--
-- Contesta, en orden, las preguntas que deciden si el deploy va a funcionar o va a fallar en
-- silencio (el seed `SeedSilosSantaReyes` es idempotente y localiza TODO por nombre: si un nombre
-- no matchea no inserta nada y NO tira error — el flag queda en false y SR sigue en modo clásico).
--
-- Uso:
--   psql -h 127.0.0.1 -p 5433 -U postgres -d sanmarinoapplocal -f backend/sql/verificar_prod_santa_reyes_silos.sql
--
-- ── ORDEN DE USO (el dump de prod convierte esto en un ENSAYO del deploy) ───────────────────────
-- Las migraciones de silos todavía NO están en prod, así que la BD recién restaurada está en el
-- estado real de producción. Y como `Database:RunMigrations=true` en appsettings.Development.json,
-- levantar el backend local aplica las migraciones pendientes: es exactamente lo que va a pasar en
-- ECS al desplegar, pero sobre una copia. Por eso conviene correrlo DOS veces:
--
--   1) Restaurar el dump y correr este script SIN levantar el backend
--      → foto del estado REAL de prod: el nombre de la empresa, si SR ya cargó inventario
--        (precondición del plan), los nombres de los 38 silos, y qué migraciones tiene prod hoy.
--      → si el paso 1 dice «NO MATCHEA» o el paso 2 trae filas, PARAR: hay que ajustar antes.
--
--   2) Levantar el backend (`dotnet run`) para que aplique las migraciones, y correr el script otra vez
--      → verifica que el seed hizo su trabajo sobre datos de producción: flag en true, los 38 silos
--        enlazados al catálogo, la bodega convertida y el menú Silos visible para los roles de SR.
--
-- La diferencia entre las dos corridas ES el efecto del deploy, medido antes de desplegar.

\echo ''
\echo '================================================================'
\echo ' 1. EL NOMBRE DE LA EMPRESA  (el seed busca exactamente 'Santa Reyes')'
\echo '================================================================'
-- Si `match_exacto` no dice OK, el seed entero no hace NADA en prod.
-- Se muestran los delimitadores para cazar espacios al principio/final, que a simple vista no se ven.
SELECT id,
       '['|| name ||']'                        AS nombre_con_delimitadores,
       length(name)                            AS largo,
       CASE WHEN name = 'Santa Reyes' THEN 'OK — el seed matchea'
            ELSE '*** NO MATCHEA: el seed no hará nada ***' END AS match_exacto,
       maneja_inventario_por_silo              AS flag_ya_puesto
FROM companies
WHERE name ILIKE '%santa%reyes%' OR name ILIKE '%reyes%'
ORDER BY id;

\echo ''
\echo '================================================================'
\echo ' 2. PRECONDICIÓN DEL PLAN: ¿SR ya tiene inventario cargado?'
\echo '================================================================'
-- El plan arranca de «0 movimientos, 0 stock, 0 lotes ⇒ sin backfill».
-- Cualquier número > 0 acá obliga a replanificar: ese stock quedaría con silo_id NULL y en modo
-- silo ninguna pantalla lo encontraría.
WITH sr AS (SELECT id FROM companies WHERE name ILIKE '%santa%reyes%')
SELECT 'inventario_gestion_stock'      AS tabla,
       count(*)                        AS filas,
       count(*) FILTER (WHERE s.silo_id IS NOT NULL) AS ya_con_silo,
       CASE WHEN count(*) = 0 THEN 'OK — sin backfill'
            ELSE '*** REPLANIFICAR: hay stock que migrar ***' END AS veredicto
  FROM inventario_gestion_stock s
 WHERE s.company_id IN (SELECT id FROM sr)
UNION ALL
SELECT 'inventario_gestion_movimiento',
       count(*),
       count(*) FILTER (WHERE m.silo_id IS NOT NULL),
       CASE WHEN count(*) = 0 THEN 'OK — sin backfill'
            ELSE '*** REPLANIFICAR: hay movimientos previos ***' END
  FROM inventario_gestion_movimiento m
 WHERE m.company_id IN (SELECT id FROM sr)
UNION ALL
SELECT 'lotes',
       count(*), 0,
       CASE WHEN count(*) = 0 THEN 'OK — sin lotes'
            ELSE 'HAY LOTES: revisar si ya consumen alimento' END
  FROM lotes l JOIN farms f ON f.id = l.granja_id
 WHERE f.company_id IN (SELECT id FROM sr);

\echo ''
\echo '================================================================'
\echo ' 3. LOS SILOS FÍSICOS: ¿existen y con qué nombres?'
\echo '================================================================'
-- El seed enlaza catálogo ↔ granja con `silo_catalogo.nombre = farm_silos.nombre`, esperando
-- 'Silo 1'..'Silo 38' + una bodega. Nombres distintos ⇒ quedan sin enlazar, en silencio.
WITH sr AS (SELECT id FROM companies WHERE name ILIKE '%santa%reyes%')
SELECT f.id                                   AS granja_id,
       f.name                                 AS granja,
       fs.tipo,
       count(*)                               AS cuantos,
       count(*) FILTER (WHERE fs.nombre ~ '^Silo [0-9]+$') AS con_nombre_esperado,
       min(fs.nombre)                         AS ejemplo_min,
       max(fs.nombre)                         AS ejemplo_max
  FROM farm_silos fs
  JOIN farms f ON f.id = fs.granja_id
 WHERE f.company_id IN (SELECT id FROM sr)
   AND fs.deleted_at IS NULL
 GROUP BY f.id, f.name, fs.tipo
 ORDER BY f.id, fs.tipo;

\echo ''
\echo '--- SILOS con nombre fuera del patrón «Silo N» (estos NO se enlazarían al catálogo) ---'
\echo '--- (la BODEGA queda excluida a propósito: no se enlaza al catálogo, es correcto)   ---'
WITH sr AS (SELECT id FROM companies WHERE name ILIKE '%santa%reyes%')
SELECT fs.id, f.name AS granja, '['|| fs.nombre ||']' AS nombre, fs.tipo, fs.activo
  FROM farm_silos fs
  JOIN farms f ON f.id = fs.granja_id
 WHERE f.company_id IN (SELECT id FROM sr)
   AND fs.deleted_at IS NULL
   AND fs.tipo <> 'Bodega'
   AND fs.nombre !~ '^Silo [0-9]+$'
 ORDER BY fs.id;

\echo ''
\echo '--- la BODEGA de la granja (el seed pasa tipo «Insumos» → «Bodega»; debe quedar 1) ---'
WITH sr AS (SELECT id FROM companies WHERE name ILIKE '%santa%reyes%')
SELECT fs.id, f.name AS granja, '['|| fs.nombre ||']' AS nombre, fs.tipo, fs.activo,
       CASE WHEN fs.tipo = 'Bodega' THEN 'OK — ya convertida'
            ELSE 'la convierte el seed al desplegar' END AS estado
  FROM farm_silos fs
  JOIN farms f ON f.id = fs.granja_id
 WHERE f.company_id IN (SELECT id FROM sr)
   AND fs.deleted_at IS NULL
   AND (fs.tipo IN ('Bodega','Insumos') OR fs.nombre ILIKE '%insumo%')
 ORDER BY fs.id;

\echo ''
\echo '================================================================'
\echo ' 4. ¿LAS MIGRACIONES DE SILOS YA CORRIERON EN PROD?'
\echo '================================================================'
-- Si ya están, el deploy no las vuelve a aplicar (y este chequeo pasa a ser post-deploy).
SELECT migration_id,
       CASE WHEN migration_id LIKE '%Silo%' OR migration_id LIKE '%SantaReyes%'
            THEN 'silos' ELSE 'otra' END AS grupo
  FROM "__EFMigrationsHistory"
 WHERE migration_id LIKE '%Silo%'
    OR migration_id LIKE '%SantaReyes%'
    OR migration_id LIKE '%GastosExistencias%'
 ORDER BY migration_id;

\echo ''
\echo '================================================================'
\echo ' 5. LOS DOS REPORTES DE SR Y EL ALIMENTO EN CERO'
\echo '================================================================'
-- Los reportes Contable y Técnico leen el alimento de la tabla LEGACY farm_inventory_movements.
-- Si SR tiene 0 ahí y N en la tabla nueva, sus columnas de alimento van a dar cero.
WITH sr AS (SELECT id FROM companies WHERE name ILIKE '%santa%reyes%')
SELECT c.id, c.name,
       (SELECT count(*) FROM farm_inventory_movements x      WHERE x.company_id = c.id) AS legacy_la_que_leen_los_reportes,
       (SELECT count(*) FROM inventario_gestion_movimiento y WHERE y.company_id = c.id) AS nueva_la_que_escribe_la_app,
       CASE WHEN (SELECT count(*) FROM farm_inventory_movements x WHERE x.company_id = c.id) = 0
             AND (SELECT count(*) FROM inventario_gestion_movimiento y WHERE y.company_id = c.id) > 0
            THEN '*** los reportes mostrarán CERO alimento ***'
            ELSE 'revisar caso por caso' END AS diagnostico
  FROM companies c
 WHERE c.id IN (SELECT id FROM sr)
    OR EXISTS (SELECT 1 FROM company_menus cm JOIN menus m ON m.id = cm.menu_id
                WHERE cm.company_id = c.id AND m.route IN ('/reporte-contable','/reportes-tecnicos'))
 ORDER BY c.id;

\echo ''
\echo '================================================================'
\echo ' 6. MENÚS DE SR (qué módulos puede abrir realmente)'
\echo '================================================================'
-- El sidebar sale de role_menus, no de company_menus: un módulo habilitado por empresa pero sin
-- role_menus NO aparece. Interesa sobre todo /config/silos (lo crea la migración de menús).
WITH sr AS (SELECT id FROM companies WHERE name ILIKE '%santa%reyes%')
SELECT m.route,
       m.label,
       EXISTS (SELECT 1 FROM company_menus cm WHERE cm.menu_id = m.id
                AND cm.company_id IN (SELECT id FROM sr))          AS en_company_menus,
       (SELECT count(DISTINCT r.id) FROM roles r
          JOIN role_menus rm ON rm.role_id = r.id AND rm.menu_id = m.id
         WHERE EXISTS (SELECT 1 FROM user_roles ur
                        WHERE ur.role_id = r.id AND ur.company_id IN (SELECT id FROM sr)))
                                                                    AS roles_de_sr_que_lo_ven
  FROM menus m
 WHERE m.route IN ('/config/silos','/gestion-inventario','/reporte-contable','/reportes-tecnicos',
                   '/inventario-gastos','/daily-log/seguimiento','/daily-log/produccion')
 ORDER BY m.route;

\echo ''
\echo '=== fin del chequeo ==='
