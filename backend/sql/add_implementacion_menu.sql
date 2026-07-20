-- Menú del módulo Implementación (grupo + planes + mis tareas).
-- Espejo de la migración EF 20260720135304_AddImplementacionMenu (la migración es la que se aplica
-- en deploy; este archivo queda como referencia/ejecución manual). Idempotente por key.
-- role_menus NO se siembra: asignar el menú a los roles por la UI de Roles.

INSERT INTO menus (label, icon, route, parent_id, "order", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT
    'Implementación',
    'clipboard-list',
    NULL,
    NULL,
    (SELECT COALESCE(MAX(m."order"), 0) + 1 FROM menus m WHERE m.parent_id IS NULL),
    0,
    true,
    true,
    'implementacion',
    NOW(),
    NOW()
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE key = 'implementacion');

INSERT INTO menus (label, icon, route, parent_id, "order", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT 'Planes de implementación', 'calendar-day', '/implementacion/planes', p.id, 1, 1, false, true, 'implementacion.planes', NOW(), NOW()
FROM menus p
WHERE p.key = 'implementacion'
  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'implementacion.planes');

INSERT INTO menus (label, icon, route, parent_id, "order", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT 'Mis tareas', 'list', '/implementacion/mis-tareas', p.id, 2, 2, false, true, 'implementacion.mis-tareas', NOW(), NOW()
FROM menus p
WHERE p.key = 'implementacion'
  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'implementacion.mis-tareas');
