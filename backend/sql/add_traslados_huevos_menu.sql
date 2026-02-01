-- Script para agregar el módulo "Traslados Huevos" al menú
-- Ejecutar después de que el sistema esté funcionando
-- 
-- Este script inserta el menú principal "Traslados Huevos"
-- que permite acceder al módulo de gestión de traslados de huevos

-- Verificar si ya existe el menú para evitar duplicados
DO $$
DECLARE
    menu_exists BOOLEAN;
BEGIN
    -- Verificar si el menú ya existe
    SELECT EXISTS(SELECT 1 FROM menus WHERE label = 'Traslados Huevos' AND parent_id IS NULL) INTO menu_exists;
    
    -- Insertar solo si no existe
    IF NOT menu_exists THEN
        -- Insertar el módulo principal "Traslados Huevos"
        -- Orden 7 (después de "Traslados Aves" que tiene order 6, antes de "Reportes Técnicos" que tiene order 8)
        INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
        VALUES ('Traslados Huevos', 'egg', '/traslados-huevos', NULL, 7, true, NOW(), NOW());
        
        RAISE NOTICE 'Menú "Traslados Huevos" insertado correctamente';
    ELSE
        RAISE NOTICE 'El menú "Traslados Huevos" ya existe, no se insertó';
    END IF;
END $$;

-- Insertar submenú "Lista de Traslados" del módulo Traslados Huevos
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Lista de Traslados',
    'list',
    '/traslados-huevos/lista',
    m.id,
    1,
    true,
    NOW(),
    NOW()
FROM menus m 
WHERE m.label = 'Traslados Huevos' AND m.parent_id IS NULL
AND NOT EXISTS (
    SELECT 1 FROM menus WHERE label = 'Lista de Traslados' AND parent_id = m.id
);

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
WHERE m.label = 'Traslados Huevos' OR m.parent_id IN (
    SELECT id FROM menus WHERE label = 'Traslados Huevos' AND parent_id IS NULL
)
ORDER BY m.parent_id, m."order";
