-- Script simple para agregar el módulo "Reporte Técnico Producción SanMarino" al menú
-- Ejecutar directamente en PostgreSQL

-- Insertar el módulo principal "Reporte Técnico Producción SanMarino"
-- Orden 11 (después de otros reportes técnicos)
-- Ajusta el orden según la posición que desees en el menú
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT 
    'Reporte Técnico Producción SanMarino',
    'chart-line',
    '/reporte-tecnico-produccion',
    NULL,
    11,
    true,
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM menus WHERE label = 'Reporte Técnico Producción SanMarino' AND parent_id IS NULL
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
WHERE m.label = 'Reporte Técnico Producción SanMarino'
ORDER BY m.parent_id, m."order";
