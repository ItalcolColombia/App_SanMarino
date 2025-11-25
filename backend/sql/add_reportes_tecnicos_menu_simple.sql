-- Script simple para agregar el módulo "Reportes Técnicos" al menú
-- Ejecutar directamente en PostgreSQL

-- Insertar el módulo principal "Reportes Técnicos"
-- Orden 8 (después de "Traslados Aves" que tiene order 7)
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Reportes Técnicos',
    'file-alt',
    '/reportes-tecnicos',
    NULL,
    8,
    true,
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM menus WHERE label = 'Reportes Técnicos' AND parent_id IS NULL
);

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


