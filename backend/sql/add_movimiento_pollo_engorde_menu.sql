-- Agregar módulo "Movimiento de Pollo Engorde" al menú (lista en /movimiento-pollo-engorde/lista)

INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT
    'Movimiento de Pollo Engorde',
    'drumstick-bite',
    '/movimiento-pollo-engorde/lista',
    NULL,
    (SELECT COALESCE(MAX(m."order"), 0) + 1 FROM menus m WHERE m.parent_id IS NULL),
    true,
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM menus WHERE route = '/movimiento-pollo-engorde/lista'
);

-- Asignar a roles que tienen "Movimientos de Aves" (mismo tipo de permiso)
INSERT INTO role_menus (role_id, menu_id)
SELECT r.id, m.id
FROM roles r
CROSS JOIN menus m
WHERE m.route = '/movimiento-pollo-engorde/lista'
  AND NOT EXISTS (SELECT 1 FROM role_menus rm WHERE rm.role_id = r.id AND rm.menu_id = m.id)
  AND EXISTS (
    SELECT 1 FROM role_menus rm2
    JOIN menus m2 ON m2.id = rm2.menu_id
    WHERE rm2.role_id = r.id AND (m2.route = '/movimientos-aves/lista' OR m2.label = 'Movimientos de Aves')
  );
