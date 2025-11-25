-- Script para agregar el módulo "Reportes Técnicos" al menú
-- Ejecutar después de que el sistema esté funcionando
-- 
-- Este script inserta el menú principal "Reportes Técnicos"
-- que permite acceder al módulo de generación de reportes técnicos diarios y semanales

-- Verificar si ya existe el menú para evitar duplicados
DO $$
DECLARE
    menu_exists BOOLEAN;
BEGIN
    -- Verificar si el menú ya existe
    SELECT EXISTS(SELECT 1 FROM menus WHERE label = 'Reportes Técnicos' AND parent_id IS NULL) INTO menu_exists;
    
    -- Insertar solo si no existe
    IF NOT menu_exists THEN
        -- Insertar el módulo principal "Reportes Técnicos"
        -- Orden 8 (después de "Traslados Aves" que tiene order 7)
        INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
        VALUES ('Reportes Técnicos', 'file-alt', '/reportes-tecnicos', NULL, 8, true, NOW(), NOW());
        
        RAISE NOTICE 'Menú "Reportes Técnicos" insertado correctamente';
    ELSE
        RAISE NOTICE 'El menú "Reportes Técnicos" ya existe, no se insertó';
    END IF;
END $$;

-- Verificar que se insertó correctamente
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
WHERE m.label = 'Reportes Técnicos'
ORDER BY m.parent_id, m."order";

-- Nota: Si necesitas asignar permisos específicos a este menú, puedes usar:
-- INSERT INTO menu_permissions (menu_id, permission_id)
-- SELECT m.id, p.id
-- FROM menus m, permissions p
-- WHERE m.label = 'Reportes Técnicos' 
--   AND p.key = 'reportes_tecnicos'; -- Ajusta la key del permiso según tu sistema

