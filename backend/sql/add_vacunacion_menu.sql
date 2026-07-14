-- Agregar módulo "Vacunación" al menú: grupo padre + 3 hijos (cronograma / registro / reportes).
-- Esquema real de `menus` (verificado contra sanmarinoapplocal): key TEXT UNIQUE NOT NULL,
-- is_group BOOLEAN, sort_order INT, además de label/icon/route/parent_id/order/is_active — el
-- patrón viejo de otros *.sql en este folder (sin key/is_group) está desactualizado, no copiarlo.
--
-- A diferencia de otros módulos, NO se asigna automáticamente a ningún rol por herencia de un menú
-- "hermano" (no existe uno natural): el usuario pidió que la asignación a roles quede en manos del
-- módulo de Roles/UI, igual que los permisos vacunacion.* (ver AddPermisosVacunacion).

INSERT INTO menus (label, icon, route, parent_id, "order", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT
    'Vacunación',
    'syringe',
    NULL,
    NULL,
    (SELECT COALESCE(MAX(m."order"), 0) + 1 FROM menus m WHERE m.parent_id IS NULL),
    0,
    true,
    true,
    'vacunacion',
    NOW(),
    NOW()
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE key = 'vacunacion');

INSERT INTO menus (label, icon, route, parent_id, "order", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT 'Cronograma', 'calendar-check', '/vacunacion/cronograma', p.id, 1, 1, false, true, 'vacunacion.cronograma', NOW(), NOW()
FROM menus p
WHERE p.key = 'vacunacion'
  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'vacunacion.cronograma');

INSERT INTO menus (label, icon, route, parent_id, "order", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT 'Registro de Aplicación', 'clipboard-check', '/vacunacion/registro', p.id, 2, 2, false, true, 'vacunacion.registro', NOW(), NOW()
FROM menus p
WHERE p.key = 'vacunacion'
  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'vacunacion.registro');

INSERT INTO menus (label, icon, route, parent_id, "order", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT 'Reportes de Cumplimiento', 'chart-line', '/vacunacion/reportes', p.id, 3, 3, false, true, 'vacunacion.reportes', NOW(), NOW()
FROM menus p
WHERE p.key = 'vacunacion'
  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'vacunacion.reportes');
