-- Script para agregar el módulo "Traslados Aves" al menú
-- Ejecutar después de que el sistema esté funcionando

-- Insertar el módulo principal "Traslados Aves"
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
VALUES ('Traslados Aves', 'boxes-alt', '/traslados-aves', NULL, 6, true, NOW(), NOW());

-- Obtener el ID del módulo recién insertado
-- (En PostgreSQL, podemos usar RETURNING para obtener el ID)

-- Insertar submenús del módulo Traslados Aves
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Dashboard Inventario',
    'tachometer-alt',
    '/traslados-aves/dashboard',
    m.id,
    1,
    true,
    NOW(),
    NOW()
FROM menus m 
WHERE m.label = 'Traslados Aves' AND m.parent_id IS NULL;

INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Nuevo Traslado',
    'truck',
    '/traslados-aves/traslados',
    m.id,
    2,
    true,
    NOW(),
    NOW()
FROM menus m 
WHERE m.label = 'Traslados Aves' AND m.parent_id IS NULL;

INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Movimientos',
    'list',
    '/traslados-aves/movimientos',
    m.id,
    3,
    true,
    NOW(),
    NOW()
FROM menus m 
WHERE m.label = 'Traslados Aves' AND m.parent_id IS NULL;

INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Historial y Trazabilidad',
    'history',
    '/traslados-aves/historial',
    m.id,
    4,
    true,
    NOW(),
    NOW()
FROM menus m 
WHERE m.label = 'Traslados Aves' AND m.parent_id IS NULL;

-- Verificar que se insertaron correctamente
SELECT 
    m.id,
    m.label,
    m.icon,
    m.route,
    m.parent_id,
    m."order",
    m.is_active,
    pm.label as parent_label
FROM menus m
LEFT JOIN menus pm ON m.parent_id = pm.id
WHERE m.label = 'Traslados Aves' OR m.parent_id IN (
    SELECT id FROM menus WHERE label = 'Traslados Aves' AND parent_id IS NULL
)
ORDER BY m.parent_id, m."order";







