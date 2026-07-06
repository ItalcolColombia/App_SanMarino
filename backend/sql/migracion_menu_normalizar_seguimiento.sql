-- ============================================================================
-- Normalización del menú: nombres cortos (el ítem padre ya indica el contexto) +
-- retirar del menú el módulo "Reporte Técnico Producción" (centralizado, ya no se usa).
-- Data-only + IDEMPOTENTE (UPDATE por label / DELETE). Targeting por label/route
-- (robusto entre entornos). NO se borra el código del módulo ni la fila de menú viejo.
-- ============================================================================

-- Renombrar el grupo padre y sus hijos a nombres cortos.
UPDATE public.menus SET label = 'Seguimiento Diario' WHERE label = 'Registros Diarios';
UPDATE public.menus SET label = 'Levante'            WHERE label = 'Seguimiento Diario de Levante';
UPDATE public.menus SET label = 'Producción'         WHERE label = 'Seguimiento Diario de Producción';
UPDATE public.menus SET label = 'Pollo Engorde'      WHERE label = 'Seguimiento Diario Pollo Engorde';
UPDATE public.menus SET label = 'Lote Reproductora'  WHERE label = 'Seguimiento Diario Lote Reproductora';

-- Quitar "Reporte Técnico Producción" del menú (se retira de company_menus/role_menus;
-- el módulo web y la fila de menú se eliminan por separado tras validar).
DELETE FROM public.company_menus
 WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/reporte-tecnico-produccion');
DELETE FROM public.role_menus
 WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/reporte-tecnico-produccion');
