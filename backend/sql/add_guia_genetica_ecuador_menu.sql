-- Ítem de menú: Guía genética Ecuador (Configuración)
-- Ruta frontend: /config/guia-genetica-ecuador
-- Asignar permisos/role_menus según política del proyecto.

WITH parent_config AS (
  SELECT id
  FROM menus
  WHERE (route = '/config' AND parent_id IS NULL)
     OR (label ILIKE '%config%' AND parent_id IS NULL)
  ORDER BY id
  LIMIT 1
)
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT
  'Guía genética Ecuador',
  'dna',
  '/config/guia-genetica-ecuador',
  (SELECT id FROM parent_config),
  100,
  true,
  NOW(),
  NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM menus WHERE route = '/config/guia-genetica-ecuador'
);

SELECT m.id, m.label, m.icon, m.route, m.parent_id, m."order", m.is_active
FROM menus m
WHERE m.route = '/config/guia-genetica-ecuador';
