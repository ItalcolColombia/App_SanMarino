-- ============================================================================
-- Unificación de inventario — Colombia · PASO 4: menú
-- Quita el inventario VIEJO del menú de Colombia (company 1). Deja SOLO el nuevo.
--
-- Colombia tenía 3 entradas de "Gestión de Inventario":
--   menu 10  "Gestion de Inventario"  -> /inventario        (VIEJO)   <- quitar
--   menu 32  "Gestión de Inventario"  -> /inventario        (VIEJO, duplicado) <- quitar
--   menu 50  "Gestión de Inventario"  -> /gestion-inventario (NUEVO)  <- se queda
--   menu 49  "Ítems inventario"       -> /config/item-inventario-ecuador (NUEVO) <- se queda
--
-- El menú EFECTIVO se gatea por company_menus (empresa). Quitando 10/32 de company_menus(1)
-- desaparecen del sidebar de Colombia. NO se borra el código del módulo viejo todavía
-- (eso es después de validar). role_menus se limpia también para no dejar huérfanos.
-- IDEMPOTENTE: DELETE es no-op si ya no están.
-- ============================================================================
DELETE FROM public.company_menus WHERE company_id = 1 AND menu_id IN (10, 32);

-- Limpia role_menus de los menús viejos para los roles de Colombia (1 Admin, 5 Director técnico, 12 Colombia Administrativa).
DELETE FROM public.role_menus WHERE menu_id IN (10, 32) AND role_id IN (1, 5, 12);
