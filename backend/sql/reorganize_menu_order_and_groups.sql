-- =============================================================================
-- Reorganizar orden y agrupación del menú para mejorar la visual del front
-- Ejecutar en la base de datos donde está la tabla menus.
-- No elimina ítems; solo actualiza "order", parent_id y opcionalmente sort_order.
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 1) ORDEN DE GRUPOS/ÍTEMS RAÍZ (parent_id IS NULL)
--    Orden sugerido: Dashboard → Registros Diarios → Config → Inventario → Reportes → Movimientos → Lotes sueltos → Otros
-- -----------------------------------------------------------------------------

UPDATE menus SET "order" = 1 WHERE parent_id IS NULL AND (route = '/dashboard' OR key = 'dashboard') AND is_active = true;
UPDATE menus SET "order" = 2 WHERE parent_id IS NULL AND (route = '/daily-log' OR label ILIKE '%Registros Diarios%' OR key = 'daily_log') AND is_active = true;
UPDATE menus SET "order" = 3 WHERE parent_id IS NULL AND (route = '/config' OR label ILIKE '%Configuraci%' AND NOT label ILIKE '%Inventario%') AND is_active = true;
UPDATE menus SET "order" = 4 WHERE parent_id IS NULL AND (route = '/inventario' OR route = '/config/inventario-management' OR key = 'inventory_management' OR (label ILIKE '%Inventario%' AND parent_id IS NULL)) AND is_active = true;
UPDATE menus SET "order" = 5 WHERE parent_id IS NULL AND (label ILIKE '%Reportes%' AND parent_id IS NULL) AND is_active = true;
UPDATE menus SET "order" = 6 WHERE parent_id IS NULL AND (label ILIKE '%Movimientos%' OR label ILIKE '%Traslados%') AND parent_id IS NULL AND is_active = true;
UPDATE menus SET "order" = 7 WHERE parent_id IS NULL AND (route = '/lote-reproductora' OR key = 'lote_reproductora') AND is_active = true;
UPDATE menus SET "order" = 8 WHERE parent_id IS NULL AND (route = '/config/lote-management' OR label ILIKE '%Gestion de Lotes%' AND parent_id IS NULL) AND is_active = true;
UPDATE menus SET "order" = 9 WHERE parent_id IS NULL AND (route = '/indicador-ecuador' OR key = 'indicador_ecuador') AND is_active = true;
-- Cualquier otro raíz que no hayamos tocado: orden alto para que queden al final
UPDATE menus SET "order" = 20 WHERE parent_id IS NULL AND "order" NOT IN (1,2,3,4,5,6,7,8,9) AND is_active = true;

-- -----------------------------------------------------------------------------
-- 2) HIJOS DE "REGISTROS DIARIOS" (parent_id = id del grupo Registros Diarios)
--    Orden: Levante → Producción → Lote Reproductora (levante) → Aves Engorde → Reproductora Aves Engorde
-- -----------------------------------------------------------------------------

WITH parent_daily AS (
  SELECT id FROM menus
  WHERE (label ILIKE '%Registros Diarios%' OR route = '/daily-log')
    AND (parent_id IS NULL OR parent_id = 0)
  LIMIT 1
)
UPDATE menus m
SET "order" = 1, parent_id = (SELECT id FROM parent_daily)
FROM parent_daily
WHERE m.route = '/daily-log/seguimiento' AND m.parent_id = parent_daily.id;

WITH parent_daily AS (
  SELECT id FROM menus
  WHERE (label ILIKE '%Registros Diarios%' OR route = '/daily-log')
    AND (parent_id IS NULL OR parent_id = 0)
  LIMIT 1
)
UPDATE menus m
SET "order" = 2, parent_id = (SELECT id FROM parent_daily)
FROM parent_daily
WHERE m.route = '/daily-log/produccion' AND m.parent_id = parent_daily.id;

WITH parent_daily AS (
  SELECT id FROM menus
  WHERE (label ILIKE '%Registros Diarios%' OR route = '/daily-log')
    AND (parent_id IS NULL OR parent_id = 0)
  LIMIT 1
)
UPDATE menus m
SET "order" = 3, parent_id = (SELECT id FROM parent_daily)
FROM parent_daily
WHERE m.route = '/daily-log/seguimiento-diario-lote-reproductora' AND m.parent_id = parent_daily.id;

WITH parent_daily AS (
  SELECT id FROM menus
  WHERE (label ILIKE '%Registros Diarios%' OR route = '/daily-log')
    AND (parent_id IS NULL OR parent_id = 0)
  LIMIT 1
)
UPDATE menus m
SET "order" = 4, parent_id = (SELECT id FROM parent_daily)
FROM parent_daily
WHERE m.route = '/daily-log/aves-engorde' AND m.parent_id = parent_daily.id;

WITH parent_daily AS (
  SELECT id FROM menus
  WHERE (label ILIKE '%Registros Diarios%' OR route = '/daily-log')
    AND (parent_id IS NULL OR parent_id = 0)
  LIMIT 1
)
UPDATE menus m
SET "order" = 5, parent_id = (SELECT id FROM parent_daily)
FROM parent_daily
WHERE m.route = '/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde' AND m.parent_id = parent_daily.id;

-- -----------------------------------------------------------------------------
-- 3) HIJOS DE "CONFIGURACIÓN" (parent_id = id del grupo Config)
--    Orden: Granjas → Núcleos → Galpones → Lotes → Lote Engorde → Lote Reproductora Aves Engorde → Guía genética → Listas maestras → etc.
-- -----------------------------------------------------------------------------

WITH parent_config AS (
  SELECT id FROM menus
  WHERE (route = '/config' OR label ILIKE '%Configuraci%') AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 1, parent_id = (SELECT id FROM parent_config)
WHERE m.route = '/config/farm-management' AND (m.parent_id = (SELECT id FROM parent_config) OR m.parent_id IS NULL);

WITH parent_config AS (
  SELECT id FROM menus
  WHERE (route = '/config' OR label ILIKE '%Configuraci%') AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 2, parent_id = (SELECT id FROM parent_config)
WHERE m.route = '/config/nucleo-management' AND (m.parent_id = (SELECT id FROM parent_config) OR m.parent_id IS NULL);

WITH parent_config AS (
  SELECT id FROM menus
  WHERE (route = '/config' OR label ILIKE '%Configuraci%') AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 3, parent_id = (SELECT id FROM parent_config)
WHERE m.route = '/config/galpon-management' AND (m.parent_id = (SELECT id FROM parent_config) OR m.parent_id IS NULL);

WITH parent_config AS (
  SELECT id FROM menus
  WHERE (route = '/config' OR label ILIKE '%Configuraci%') AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 4, parent_id = (SELECT id FROM parent_config)
WHERE m.route = '/config/lote-management' AND (m.parent_id = (SELECT id FROM parent_config) OR m.parent_id IS NULL);

WITH parent_config AS (
  SELECT id FROM menus
  WHERE (route = '/config' OR label ILIKE '%Configuraci%') AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 5, parent_id = (SELECT id FROM parent_config)
WHERE m.route = '/config/lote-engorde' AND (m.parent_id = (SELECT id FROM parent_config) OR m.parent_id IS NULL);

WITH parent_config AS (
  SELECT id FROM menus
  WHERE (route = '/config' OR label ILIKE '%Configuraci%') AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 6, parent_id = (SELECT id FROM parent_config)
WHERE m.route = '/config/lote-reproductora-ave-engorde' AND (m.parent_id = (SELECT id FROM parent_config) OR m.parent_id IS NULL);

WITH parent_config AS (
  SELECT id FROM menus
  WHERE (route = '/config' OR label ILIKE '%Configuraci%') AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 7, parent_id = (SELECT id FROM parent_config)
WHERE m.route = '/config/guia-genetica' AND (m.parent_id = (SELECT id FROM parent_config) OR m.parent_id IS NULL);

WITH parent_config AS (
  SELECT id FROM menus
  WHERE (route = '/config' OR label ILIKE '%Configuraci%') AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 8, parent_id = (SELECT id FROM parent_config)
WHERE m.route = '/config/master-lists' AND (m.parent_id = (SELECT id FROM parent_config) OR m.parent_id IS NULL);

WITH parent_config AS (
  SELECT id FROM menus
  WHERE (route = '/config' OR label ILIKE '%Configuraci%') AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 9, parent_id = (SELECT id FROM parent_config)
WHERE m.route = '/config/inventario-management' AND (m.parent_id = (SELECT id FROM parent_config) OR m.parent_id IS NULL);

-- Cualquier otro hijo de config que no tenga orden asignado
WITH parent_config AS (
  SELECT id FROM menus
  WHERE (route = '/config' OR label ILIKE '%Configuraci%') AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = COALESCE(m."order", 0) + 10
WHERE m.parent_id = (SELECT id FROM parent_config) AND m."order" IS NULL;

-- -----------------------------------------------------------------------------
-- 4) INVENTARIO: si hay dos ítems (ej. /inventario y /config/inventario-management),
--    dejar uno como raíz orden 4 y el que esté bajo config con order 9
-- -----------------------------------------------------------------------------
-- (Ya cubierto en 1 y 3)

-- -----------------------------------------------------------------------------
-- 5) REPORTES: orden de hijos (Reportes Técnicos, Reporte Contable, Reporte Técnico Producción, etc.)
-- -----------------------------------------------------------------------------

WITH parent_reportes AS (
  SELECT id FROM menus
  WHERE label ILIKE '%Reportes%' AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 1, parent_id = (SELECT id FROM parent_reportes)
WHERE (m.label ILIKE '%Reportes Técnicos%' OR m.route LIKE '%reportes-tecnicos%') AND m.parent_id = (SELECT id FROM parent_reportes);

WITH parent_reportes AS (
  SELECT id FROM menus
  WHERE label ILIKE '%Reportes%' AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 2, parent_id = (SELECT id FROM parent_reportes)
WHERE (m.label ILIKE '%Reporte Contable%') AND m.parent_id = (SELECT id FROM parent_reportes);

WITH parent_reportes AS (
  SELECT id FROM menus
  WHERE label ILIKE '%Reportes%' AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 3, parent_id = (SELECT id FROM parent_reportes)
WHERE (m.label ILIKE '%Reporte Técnico Producción%') AND m.parent_id = (SELECT id FROM parent_reportes);

-- -----------------------------------------------------------------------------
-- 6) MOVIMIENTOS: agrupar Traslados Aves, Traslados Huevos, Movimientos Aves bajo un mismo grupo si existe "Movimientos"
-- -----------------------------------------------------------------------------

WITH parent_mov AS (
  SELECT id FROM menus
  WHERE label ILIKE '%Movimientos%' AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 1, parent_id = (SELECT id FROM parent_mov)
WHERE (m.label ILIKE '%Movimientos de Aves%' OR m.route LIKE '%movimientos-aves%') AND m.parent_id = (SELECT id FROM parent_mov);

WITH parent_mov AS (
  SELECT id FROM menus
  WHERE label ILIKE '%Movimientos%' AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 2, parent_id = (SELECT id FROM parent_mov)
WHERE (m.label ILIKE '%Traslados Aves%') AND m.parent_id = (SELECT id FROM parent_mov);

WITH parent_mov AS (
  SELECT id FROM menus
  WHERE label ILIKE '%Movimientos%' AND parent_id IS NULL
  LIMIT 1
)
UPDATE menus m SET "order" = 3, parent_id = (SELECT id FROM parent_mov)
WHERE (m.label ILIKE '%Traslados Huevos%') AND m.parent_id = (SELECT id FROM parent_mov);

-- Si la tabla tiene sort_order, alinearlo con "order" para consistencia
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'menus' AND column_name = 'sort_order'
  ) THEN
    UPDATE menus SET sort_order = "order" WHERE sort_order IS DISTINCT FROM "order";
  END IF;
END $$;

COMMIT;

-- =============================================================================
-- CONSULTA DE VERIFICACIÓN (ejecutar después para ver resultado)
-- =============================================================================
/*
SELECT
  m.id,
  m.label,
  m.icon,
  m.route,
  m.parent_id,
  p.label AS parent_label,
  m."order",
  m.is_active
FROM menus m
LEFT JOIN menus p ON p.id = m.parent_id
ORDER BY COALESCE(m.parent_id, 0), m."order", m.id;
*/
