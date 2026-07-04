-- ============================================================================
-- Fase 3 · Paso 3 · S1 — Menú de Colombia hacia el inventario modelo B unificado
-- ============================================================================
-- Objetivo: que la empresa Colombia (company_id = 1, Agroavicola Sanmarino)
-- VEA en su menú las rutas del inventario modelo B unificado a NIVEL GRANJA:
--   * menu 50 → /gestion-inventario                 (stock, ingresos, traslados,
--                                                     tránsito, histórico, catálogo)
--   * menu 49 → /config/item-inventario-ecuador     (catálogo de ítems, Config)
--
-- Colombia YA consume/opera del modelo B a nivel granja en el backend
-- (gate ModeloBNivelGranja + ColombiaInventarioConsumoService + stock migrado
-- A→B: 17 filas nivel granja, company 1 / pais 1). Lo único que faltaba era que
-- su menú apuntara a esas rutas (hasta ahora solo veía /inventario = modelo A).
--
-- Se DEJAN los menús 10 y 32 (/inventario, modelo A frozen) por ahora: son el
-- histórico del modelo A y no se tocan en este paso.
--
-- IDEMPOTENTE: INSERT ... WHERE NOT EXISTS. Reejecutable sin duplicar filas
-- (PK = company_id + menu_id). No hace DDL. Alcance: SOLO company 1.
-- Ecuador (3) y Panamá (5) NO se tocan.
-- ============================================================================

BEGIN;

-- menu 50 → /gestion-inventario (modelo B unificado, nivel granja para Colombia)
INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT 1, 50, TRUE, 23, NULL
WHERE NOT EXISTS (
    SELECT 1 FROM company_menus WHERE company_id = 1 AND menu_id = 50
);

-- menu 49 → /config/item-inventario-ecuador (catálogo de ítems del modelo B)
INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT 1, 49, TRUE, 24, NULL
WHERE NOT EXISTS (
    SELECT 1 FROM company_menus WHERE company_id = 1 AND menu_id = 49
);

-- role_menus: el menú EFECTIVO requiere company_menus (por empresa) Y role_menus (por rol).
-- Sin esto, los usuarios Colombia (aunque la empresa tenga el menú habilitado) NO lo ven.
-- Roles Colombia que deben operar el inventario modelo B: 1 (Admin), 5 (Director tecnico),
-- 12 (Colombia Administrativa). role_menus es global por rol → queda gateado por company_menus
-- (solo las empresas con 49/50 habilitado lo muestran: EC 3, PA 5 y ahora CO 1). Idempotente.
INSERT INTO role_menus (role_id, menu_id)
SELECT r.role_id, m.menu_id
FROM (VALUES (1),(5),(12)) AS r(role_id)
CROSS JOIN (VALUES (49),(50)) AS m(menu_id)
WHERE NOT EXISTS (
    SELECT 1 FROM role_menus rm WHERE rm.role_id = r.role_id AND rm.menu_id = m.menu_id
);

-- El módulo /gestion-inventario ya es MULTIPAÍS (EC/PA/CO): etiqueta genérica del menú
-- (antes "Gestión de Inventario (EC/PA)", que era engañosa para Colombia). Idempotente.
UPDATE menus SET label = 'Gestión de Inventario' WHERE id = 50 AND label LIKE '%(EC/PA)%';

COMMIT;

-- Verificación (no modifica nada):
-- SELECT company_id, menu_id, is_enabled, sort_order
-- FROM company_menus
-- WHERE company_id = 1 AND menu_id IN (10, 32, 49, 50)
-- ORDER BY menu_id;
-- Esperado tras la ejecución: filas para 10, 32 (modelo A histórico) + 49, 50 (modelo B).
