-- Script para agregar el submódulo "Indicador Ecuador" al menú
-- Este script crea un menú principal "Indicador Ecuador" que permite acceder
-- al módulo de indicadores técnicos de cierre de mes de granja

-- Verificar si ya existe el menú para evitar duplicados
DO $$
DECLARE
    v_order INTEGER;
    v_menu_id INTEGER;
    v_parent_menu_id INTEGER;
BEGIN
    -- Obtener el siguiente order disponible (después del último menú)
    SELECT COALESCE(MAX("order"), 0) + 1 INTO v_order
    FROM menus
    WHERE parent_id IS NULL;

    -- Verificar si ya existe el menú principal
    SELECT id INTO v_menu_id 
    FROM menus 
    WHERE key = 'indicador_ecuador' AND parent_id IS NULL
    LIMIT 1;

    -- Si no existe, insertarlo
    IF v_menu_id IS NULL THEN
        INSERT INTO menus (label, icon, route, parent_id, "order", is_active, key, sort_order, is_group)
        VALUES (
            'Indicador Ecuador',
            'chart-line',
            '/indicador-ecuador',
            NULL,
            v_order,
            true,
            'indicador_ecuador',
            0,
            false  -- NO es un grupo, es un menú simple
        )
        RETURNING id INTO v_menu_id;
        RAISE NOTICE '✅ Menú principal "Indicador Ecuador" creado con order %', v_order;
    ELSE
        -- Si ya existe, actualizar para asegurar que esté correcto
        UPDATE menus 
        SET 
            is_group = false,
            icon = 'chart-line',
            route = '/indicador-ecuador',
            is_active = true,
            label = 'Indicador Ecuador'
        WHERE id = v_menu_id;
        RAISE NOTICE '✅ Menú principal "Indicador Ecuador" actualizado';
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
    m.key,
    m.sort_order,
    m.is_group
FROM menus m
WHERE m.key = 'indicador_ecuador' AND m.parent_id IS NULL;
