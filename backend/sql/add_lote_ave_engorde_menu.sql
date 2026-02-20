-- Script para agregar el módulo "Lote Aves de Engorde" al menú (dentro de Configuración)
-- Ruta en el frontend: /config/lote-engorde  (NO /lote-engorde)
-- Si usas /lote-engorde en el menú, la app redirige a login porque esa ruta no existe.

-- 0) Corregir si ya creaste el ítem con ruta incorrecta /lote-engorde
UPDATE menus
SET route = '/config/lote-engorde',
    parent_id = (SELECT id FROM menus WHERE (route = '/config' AND parent_id IS NULL) OR (label ILIKE '%config%' AND parent_id IS NULL) LIMIT 1)
WHERE route = '/lote-engorde';

-- 1) Menú padre = Configuración
WITH parent_config AS (
  SELECT id
  FROM menus
  WHERE (route = '/config' AND parent_id IS NULL)
     OR (label ILIKE '%config%' AND parent_id IS NULL)
  ORDER BY id
  LIMIT 1
),
-- 2) Siguiente "order" dentro de Config
next_order AS (
  SELECT COALESCE(MAX(m."order"), 0) + 1 AS num
  FROM menus m
  WHERE m.parent_id = (SELECT id FROM parent_config)
)
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT
  'Lote Aves de Engorde',
  'drumstick-bite',
  '/config/lote-engorde',
  (SELECT id FROM parent_config),
  (SELECT num FROM next_order),
  true,
  NOW(),
  NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM menus WHERE route = '/config/lote-engorde'
);

-- 3) Asignar el nuevo menú a todos los roles que ya tienen el menú Config (para que no redirija a login)
INSERT INTO role_menus (role_id, menu_id)
SELECT r.id, m.id
FROM roles r
CROSS JOIN menus m
WHERE m.route = '/config/lote-engorde'
  AND NOT EXISTS (
    SELECT 1 FROM role_menus rm
    WHERE rm.role_id = r.id AND rm.menu_id = m.id
  )
  AND EXISTS (
    SELECT 1 FROM menus parent
    WHERE parent.parent_id IS NULL
      AND (parent.route = '/config' OR parent.label ILIKE '%config%')
      AND EXISTS (
        SELECT 1 FROM role_menus rm2
        WHERE rm2.role_id = r.id AND rm2.menu_id = parent.id
      )
  );

-- Verificación
SELECT m.id, m.label, m.icon, m.route, m.parent_id, m."order", m.is_active
FROM menus m
WHERE m.route = '/config/lote-engorde';
