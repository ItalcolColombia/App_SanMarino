-- =====================================================
-- Script para agregar el módulo "Gestión de Inventario" al menú
-- =====================================================
-- Este script agrega SOLO el menú principal de gestión de inventario
-- Las pestañas (Movimientos, Movimiento Alimento, Stock, Kardex, Catálogo) 
-- se manejan internamente en el componente InventarioTabsComponent
-- NO se crean submenús porque son redundantes

-- PASO 1: Eliminar submenús redundantes si existen
-- (Estos se crearon en versiones anteriores del script)
DO $$
DECLARE
    v_menu_id INTEGER;
BEGIN
    -- Obtener el ID del menú principal si existe
    SELECT id INTO v_menu_id 
    FROM menus 
    WHERE key = 'inventory_management' AND parent_id IS NULL
    LIMIT 1;

    -- Si existe el menú principal, eliminar sus submenús
    IF v_menu_id IS NOT NULL THEN
        -- Eliminar submenús por key
        DELETE FROM menus 
        WHERE parent_id = v_menu_id 
        AND key IN (
            'inventory_movements',
            'inventory_food_movement',
            'inventory_stock',
            'inventory_kardex',
            'inventory_catalog'
        );

        -- También eliminar por label por si acaso (por si no tienen key)
        DELETE FROM menus 
        WHERE parent_id = v_menu_id 
        AND label IN (
            'Movimientos',
            'Movimiento de Alimento',
            'Stock',
            'Kardex',
            'Catálogo de Productos'
        );

        RAISE NOTICE '✅ Submenús redundantes eliminados (si existían)';
    END IF;
END $$;

-- PASO 2: Verificar si ya existe el menú principal de Inventario
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
    WHERE key = 'inventory_management' AND parent_id IS NULL
    LIMIT 1;

    -- Si no existe, insertarlo
    IF v_menu_id IS NULL THEN
        INSERT INTO menus (label, icon, route, parent_id, "order", is_active, key, sort_order, is_group)
        VALUES (
            'Gestión de Inventario',
            'warehouse',
            '/inventario',
            NULL,
            v_order,
            true,
            'inventory_management',
            0,
            false  -- NO es un grupo, es un menú simple que carga el componente con pestañas
        );
        RAISE NOTICE '✅ Menú principal "Gestión de Inventario" creado';
    ELSE
        -- Si ya existe, actualizar para asegurar que is_group = false
        UPDATE menus 
        SET 
            is_group = false,
            icon = 'warehouse',
            route = '/inventario',
            is_active = true
        WHERE id = v_menu_id;
        RAISE NOTICE '✅ Menú principal "Gestión de Inventario" actualizado';
    END IF;

    RAISE NOTICE 'ℹ️  Las pestañas (Movimientos, Movimiento Alimento, Stock, Kardex, Catálogo) se manejan internamente en el componente';
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
WHERE m.key = 'inventory_management' AND m.parent_id IS NULL;
