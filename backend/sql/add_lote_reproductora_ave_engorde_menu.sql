-- Menú: Lote Reproductora Aves de Engorde (dentro de Configuración)
-- Ruta frontend: /config/lote-reproductora-ave-engorde

WITH parent_config AS (
  SELECT id FROM menus
  WHERE (route = '/config' AND parent_id IS NULL) OR (label ILIKE '%config%' AND parent_id IS NULL)
  ORDER BY id LIMIT 1
),
next_order AS (
  SELECT COALESCE(MAX(m."order"), 0) + 1 AS num
  FROM menus m WHERE m.parent_id = (SELECT id FROM parent_config)
)
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT
  'Lote Reproductora Aves de Engorde',
  'layer-group',
  '/config/lote-reproductora-ave-engorde',
  (SELECT id FROM parent_config),
  (SELECT num FROM next_order),
  true,
  NOW(),
  NOW()
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE route = '/config/lote-reproductora-ave-engorde');

-- Asignar a roles que tienen Config
INSERT INTO role_menus (role_id, menu_id)
SELECT r.id, m.id
FROM roles r
CROSS JOIN menus m
WHERE m.route = '/config/lote-reproductora-ave-engorde'
  AND NOT EXISTS (SELECT 1 FROM role_menus rm WHERE rm.role_id = r.id AND rm.menu_id = m.id)
  AND EXISTS (
    SELECT 1 FROM menus parent
    WHERE parent.parent_id IS NULL AND (parent.route = '/config' OR parent.label ILIKE '%config%')
      AND EXISTS (SELECT 1 FROM role_menus rm2 WHERE rm2.role_id = r.id AND rm2.menu_id = parent.id)
  );
