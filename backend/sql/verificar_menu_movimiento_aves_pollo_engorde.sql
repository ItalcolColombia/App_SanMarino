-- Verificación: menú Movimiento de Aves vs Movimiento de Pollo Engorde
-- Ejecutar para ver qué roles tienen cada ítem y detectar si "Movimientos de Aves" sigue asignado.

-- 1) IDs de los ítems de menú
SELECT id, label, route, parent_id, "order"
FROM menus
WHERE route IN ('/movimientos-aves/lista', '/movimiento-pollo-engorde/lista')
   OR label ILIKE '%Movimientos de Aves%'
   OR label ILIKE '%Movimiento de Pollo Engorde%'
ORDER BY label;

-- 2) Roles que tienen "Movimientos de Aves"
SELECT r.id AS role_id, r.name AS role_name, m.id AS menu_id, m.label AS menu_label, m.route
FROM role_menus rm
JOIN roles r ON r.id = rm.role_id
JOIN menus m ON m.id = rm.menu_id
WHERE m.route = '/movimientos-aves/lista' OR m.label ILIKE '%Movimientos de Aves%'
ORDER BY r.name, m.label;

-- 3) Roles que tienen "Movimiento de Pollo Engorde"
SELECT r.id AS role_id, r.name AS role_name, m.id AS menu_id, m.label AS menu_label, m.route
FROM role_menus rm
JOIN roles r ON r.id = rm.role_id
JOIN menus m ON m.id = rm.menu_id
WHERE m.route = '/movimiento-pollo-engorde/lista' OR m.label ILIKE '%Movimiento de Pollo Engorde%'
ORDER BY r.name, m.label;

-- 4) Para quitar "Movimientos de Aves" de un rol concreto (descomentar y sustituir NNN por role_id):
-- DELETE FROM role_menus
-- WHERE role_id = NNN
--   AND menu_id = (SELECT id FROM menus WHERE route = '/movimientos-aves/lista' LIMIT 1);
