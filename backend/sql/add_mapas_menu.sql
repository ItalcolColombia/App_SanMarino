-- =============================================================================
-- Menú del módulo Mapas: item principal + subitems Configuraciones y Mapa
-- =============================================================================

-- Ajustar secuencia de id para evitar "duplicate key" si la secuencia está desincronizada
SELECT setval(
    pg_get_serial_sequence('menus', 'id')::regclass,
    COALESCE((SELECT MAX(id) FROM menus), 1)
);

DO $$
DECLARE
    v_order_parent INTEGER;
    v_parent_id    INTEGER;
BEGIN
    SELECT COALESCE(MAX("order"), 0) + 1 INTO v_order_parent
    FROM menus
    WHERE parent_id IS NULL;

    SELECT id INTO v_parent_id FROM menus WHERE key = 'mapas' AND parent_id IS NULL LIMIT 1;
    IF v_parent_id IS NULL THEN
        SELECT id INTO v_parent_id FROM menus WHERE label = 'Mapas' AND parent_id IS NULL LIMIT 1;
    END IF;

    IF v_parent_id IS NULL THEN
        INSERT INTO menus (label, icon, route, parent_id, "order", is_active, key, sort_order, is_group)
        VALUES (
            'Mapas',
            'map',
            '/mapas',
            NULL,
            v_order_parent,
            true,
            'mapas',
            0,
            true
        );
        SELECT id INTO v_parent_id FROM menus WHERE key = 'mapas' AND parent_id IS NULL LIMIT 1;
        RAISE NOTICE '✅ Menú principal Mapas creado';
    END IF;

    IF v_parent_id IS NOT NULL THEN
        IF NOT EXISTS (SELECT 1 FROM menus WHERE parent_id = v_parent_id AND key = 'mapas_configuraciones') THEN
            INSERT INTO menus (label, icon, route, parent_id, "order", is_active, key, sort_order, is_group)
            VALUES (
                'Configuraciones',
                'settings',
                '/mapas/configuraciones',
                v_parent_id,
                1,
                true,
                'mapas_configuraciones',
                1,
                false
            );
            RAISE NOTICE '✅ Submenú Configuraciones creado';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM menus WHERE parent_id = v_parent_id AND key = 'mapas_mapa') THEN
            INSERT INTO menus (label, icon, route, parent_id, "order", is_active, key, sort_order, is_group)
            VALUES (
                'Mapa',
                'map_outline',
                '/mapas/mapa',
                v_parent_id,
                2,
                true,
                'mapas_mapa',
                2,
                false
            );
            RAISE NOTICE '✅ Submenú Mapa creado';
        END IF;
    END IF;
END $$;
