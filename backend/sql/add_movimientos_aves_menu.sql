-- =====================================================
-- Script para agregar el módulo "Movimientos de Aves" al menú
-- =====================================================
-- Este script agrega el módulo de movimientos de aves al menú de la aplicación
-- Ejecutar este script en la base de datos después de implementar el módulo

-- Insertar menú principal
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Movimientos de Aves',
    'truck',
    '/movimientos-aves/lista',
    NULL,
    8,
    true,
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM menus WHERE label = 'Movimientos de Aves' AND parent_id IS NULL
);

-- Insertar submenú
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Nuevo Movimiento',
    'plus-circle',
    '/movimientos-aves/nuevo',
    m.id,
    1,
    true,
    NOW(),
    NOW()
FROM menus m 
WHERE m.label = 'Movimientos de Aves' AND m.parent_id IS NULL
AND NOT EXISTS (
    SELECT 1 FROM menus WHERE label = 'Nuevo Movimiento' AND parent_id = m.id
);

-- Verificar que se insertaron correctamente
SELECT 
    m.id,
    m.label,
    m.icon,
    m.route,
    m.parent_id,
    m."order",
    m.is_active
FROM menus m
WHERE m.label = 'Movimientos de Aves' OR (m.label = 'Nuevo Movimiento' AND m.parent_id IN (SELECT id FROM menus WHERE label = 'Movimientos de Aves'))
ORDER BY m.parent_id NULLS FIRST, m."order";
