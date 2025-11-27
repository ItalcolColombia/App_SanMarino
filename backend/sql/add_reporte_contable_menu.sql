-- Script simple para agregar el módulo "Reporte Contable" al menú
-- Ejecutar directamente en PostgreSQL

-- Insertar el módulo principal "Reporte Contable"
-- Orden 9 (después de "Reportes Técnicos" que tiene order 8)
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Reporte Contable',
    'dollar-sign',
    '/reporte-contable',
    NULL,
    9,
    true,
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM menus WHERE label = 'Reporte Contable' AND parent_id IS NULL
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
WHERE m.label = 'Reporte Contable'
ORDER BY m.parent_id, m."order";

