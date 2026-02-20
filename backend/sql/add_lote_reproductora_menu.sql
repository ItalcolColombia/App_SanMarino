-- Script para agregar el módulo "Lote Reproductora" al menú
-- Ejecutar después de que el sistema esté funcionando
-- 
-- Este script inserta el menú principal "Lote Reproductora"
-- que permite acceder al módulo de gestión de lotes reproductoras

-- Verificar si ya existe el menú para evitar duplicados
DO $$
DECLARE
    v_order INTEGER;
    v_menu_id INTEGER;
BEGIN
    -- Obtener el siguiente order disponible (después del último menú)
    SELECT COALESCE(MAX("order"), 0) + 1 INTO v_order
    FROM menus
    WHERE parent_id IS NULL;

    -- Verificar si ya existe el menú principal
    SELECT id INTO v_menu_id 
    FROM menus 
    WHERE key = 'lote_reproductora' AND parent_id IS NULL
    LIMIT 1;

    -- Si no existe, insertarlo
    IF v_menu_id IS NULL THEN
        INSERT INTO menus (label, icon, route, parent_id, "order", is_active, key, sort_order, is_group)
        VALUES (
            'Lote Reproductora',
            'egg',
            '/lote-reproductora',
            NULL,
            v_order,
            true,
            'lote_reproductora',
            0,
            false  -- NO es un grupo, es un menú simple
        );
        RAISE NOTICE '✅ Menú principal "Lote Reproductora" creado con order %', v_order;
    ELSE
        -- Si ya existe, actualizar para asegurar que esté correcto
        UPDATE menus 
        SET 
            is_group = false,
            icon = 'egg',
            route = '/lote-reproductora',
            is_active = true,
            label = 'Lote Reproductora'
        WHERE id = v_menu_id;
        RAISE NOTICE '✅ Menú principal "Lote Reproductora" actualizado';
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
WHERE m.key = 'lote_reproductora' AND m.parent_id IS NULL;
