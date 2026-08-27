-- ============================================================================
-- Guía Genética — los TRES ítems de menú, uno por modelo de datos.
--
-- ESPEJO LEGIBLE de la migración
--   20260826160000_SeedMenusGuiaGeneticaTresModulos
-- que es la que lo aplica en producción. Este archivo NO se corre solo: nada aplica
-- `backend/sql/` (ni el arranque de la app ni el deploy), sólo las migraciones EF.
-- Está acá para poder leer el objeto sin abrir el .cs.
--
-- ┌───────────────────────────────┬─────────────────────────────────────┬──────────────────────────┐
-- │ Guía Genética Pollo Engorde   │ /config/guia-genetica-ecuador       │ Ecuador + Panamá         │
-- │ Guía Genética Sanmarino       │ /config/guia-genetica               │ Sanmarino / Demo         │
-- │ Guía Genética Santa Reyes     │ /config/guia-genetica-santa-reyes   │ perfil 'reducida' (NUEVO)│
-- └───────────────────────────────┴─────────────────────────────────────┴──────────────────────────┘
--
-- Supera a `add_guia_genetica_menu.sql` y `add_guia_genetica_ecuador_menu.sql`: esos dos se
-- corrieron A MANO en producción (por eso el repo no puede probar qué filas existen realmente) y
-- además hoy fallarían — les falta `menus.key`, que es NOT NULL UNIQUE desde entonces.
--
-- Reglas: localizar SIEMPRE por `route` (los ids difieren local ↔ prod), INSERT ... WHERE NOT
-- EXISTS, UPDATE con IS DISTINCT FROM, y DESACTIVAR (is_enabled=false) en vez de borrar.
-- ============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- 1) Las TRES filas de `menus`.
--    El segundo NOT EXISTS (por `key`) evita reventar donde la key ya esté tomada por otra ruta.
--    El icono es 'clipboard-list' y no 'dna' porque el ICON_MAP del front
--    (frontend/src/app/shared/services/menu.service.ts) no conoce 'dna': el ítem de Ecuador se
--    dibuja hoy sin icono.
-- ─────────────────────────────────────────────────────────────────────────────

-- 1a) Guía Genética Sanmarino (tabla ancha compartida). Normalmente YA existe.
INSERT INTO public.menus (label, icon, route, parent_id, "order", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT 'Guía Genética Sanmarino',
       'clipboard-list',
       '/config/guia-genetica',
       (SELECT m.id FROM public.menus m
         WHERE (m.route = '/config' OR m.label ILIKE '%config%') AND m.parent_id IS NULL
         ORDER BY m.id LIMIT 1),
       5, true, 'guia_genetica', 0, false, now(), now()
WHERE NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.route = '/config/guia-genetica')
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key  = 'guia_genetica');

-- 1b) Guía Genética Pollo Engorde (Ecuador + Panamá). Normalmente YA existe.
INSERT INTO public.menus (label, icon, route, parent_id, "order", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT 'Guía Genética Pollo Engorde',
       'clipboard-list',
       '/config/guia-genetica-ecuador',
       COALESCE(
         (SELECT m.parent_id FROM public.menus m WHERE m.route = '/config/guia-genetica' LIMIT 1),
         (SELECT m.id FROM public.menus m
           WHERE (m.route = '/config' OR m.label ILIKE '%config%') AND m.parent_id IS NULL
           ORDER BY m.id LIMIT 1)
       ),
       12, true, 'guia_genetica_ecuador', 0, false, now(), now()
WHERE NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.route = '/config/guia-genetica-ecuador')
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key  = 'guia_genetica_ecuador');

-- 1c) Guía Genética Santa Reyes — el ítem NUEVO. Hereda el `order` del que reemplaza.
INSERT INTO public.menus (label, icon, route, parent_id, "order", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT 'Guía Genética Santa Reyes',
       'clipboard-list',
       '/config/guia-genetica-santa-reyes',
       COALESCE(
         (SELECT m.parent_id FROM public.menus m WHERE m.route = '/config/guia-genetica'         LIMIT 1),
         (SELECT m.parent_id FROM public.menus m WHERE m.route = '/config/guia-genetica-ecuador' LIMIT 1),
         (SELECT m.id FROM public.menus m
           WHERE (m.route = '/config' OR m.label ILIKE '%config%') AND m.parent_id IS NULL
           ORDER BY m.id LIMIT 1)
       ),
       COALESCE((SELECT m."order" FROM public.menus m WHERE m.route = '/config/guia-genetica' LIMIT 1), 5),
       true, 'guia_genetica_santa_reyes', 0, false, now(), now()
WHERE NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.route = '/config/guia-genetica-santa-reyes')
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key  = 'guia_genetica_santa_reyes');

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Los rótulos, CON tildes. 20260623080001_RenameMenu_GuiaGenetica había dejado el ítem de
--    ENGORDE rotulado 'Guia Genetica' y el de Sanmarino 'Guía Genética': indistinguibles.
-- ─────────────────────────────────────────────────────────────────────────────
UPDATE public.menus
   SET label = 'Guía Genética Pollo Engorde', updated_at = now()
 WHERE route = '/config/guia-genetica-ecuador'
   AND label IS DISTINCT FROM 'Guía Genética Pollo Engorde';

UPDATE public.menus
   SET label = 'Guía Genética Sanmarino', updated_at = now()
 WHERE route = '/config/guia-genetica'
   AND label IS DISTINCT FROM 'Guía Genética Sanmarino';

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) company_menus — ALTA del ítem nuevo para las empresas de perfil reducido.
--    `reducidas` se define por COMPORTAMIENTO (companies.guia_genetica_perfil) o por DATOS
--    (EXISTS sobre la tabla propia), nunca por nombre de empresa (CLAUDE.md §🏢).
--
--    🔴 El último EXISTS ("la empresa ya tiene alguna fila en company_menus") NO es redundante:
--    fn_menu_usuario es FAIL-OPEN por empresa (D2) — una empresa SIN ninguna fila no se filtra y ve
--    todo el catálogo. Insertarle UNA sola fila la convertiría en filtrada y le dejaría el menú
--    reducido a ese único ítem.
-- ─────────────────────────────────────────────────────────────────────────────
WITH reducidas AS (
    SELECT c.id
      FROM public.companies c
     WHERE c.guia_genetica_perfil = 'reducida'
        OR EXISTS (SELECT 1 FROM public.guia_genetica_santa_reyes g WHERE g.company_id = c.id)
)
INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT r.id,
       nuevo.id,
       true,
       COALESCE((SELECT cm.sort_order
                   FROM public.company_menus cm
                   JOIN public.menus mo ON mo.id = cm.menu_id
                  WHERE cm.company_id = r.id
                    AND mo.route IN ('/config/guia-genetica', '/config/guia-genetica-ecuador')
                  ORDER BY cm.sort_order
                  LIMIT 1), 0),
       NULL
  FROM reducidas r
 CROSS JOIN (SELECT m.id FROM public.menus m WHERE m.route = '/config/guia-genetica-santa-reyes') AS nuevo
 WHERE NOT EXISTS (SELECT 1 FROM public.company_menus x
                    WHERE x.company_id = r.id AND x.menu_id = nuevo.id)
   AND EXISTS     (SELECT 1 FROM public.company_menus x WHERE x.company_id = r.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) role_menus — ANTI-LOCKOUT. fn_menu_usuario interseca role_menus ∩ company_menus: sin esta
--    fila el ítem no se ve aunque la empresa lo habilite.
-- ─────────────────────────────────────────────────────────────────────────────
WITH reducidas AS (
    SELECT c.id
      FROM public.companies c
     WHERE c.guia_genetica_perfil = 'reducida'
        OR EXISTS (SELECT 1 FROM public.guia_genetica_santa_reyes g WHERE g.company_id = c.id)
)
INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rm.role_id, nuevo.id
  FROM public.role_menus rm
  JOIN public.menus m           ON m.id = rm.menu_id
                               AND m.route IN ('/config/guia-genetica', '/config/guia-genetica-ecuador')
  JOIN public.role_companies rc ON rc.role_id = rm.role_id
  JOIN reducidas r              ON r.id = rc.company_id
 CROSS JOIN (SELECT m2.id FROM public.menus m2 WHERE m2.route = '/config/guia-genetica-santa-reyes') AS nuevo
 WHERE NOT EXISTS (SELECT 1 FROM public.role_menus x
                    WHERE x.role_id = rm.role_id AND x.menu_id = nuevo.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 5) company_menus — BAJA de los dos ítems viejos: is_enabled=false, NO delete, y SÓLO si el ítem
--    nuevo ya quedó habilitado para esa misma empresa. Ese EXISTS es el seguro: si el paso 3 no
--    hubiera pegado, la empresa se quedaría sin ninguna pantalla de guía.
-- ─────────────────────────────────────────────────────────────────────────────
WITH reducidas AS (
    SELECT c.id
      FROM public.companies c
     WHERE c.guia_genetica_perfil = 'reducida'
        OR EXISTS (SELECT 1 FROM public.guia_genetica_santa_reyes g WHERE g.company_id = c.id)
)
UPDATE public.company_menus cm
   SET is_enabled = false
  FROM public.menus m, reducidas r
 WHERE m.id = cm.menu_id
   AND cm.company_id = r.id
   AND m.route IN ('/config/guia-genetica', '/config/guia-genetica-ecuador')
   AND cm.is_enabled IS DISTINCT FROM false
   AND EXISTS (SELECT 1
                 FROM public.company_menus cx
                 JOIN public.menus mx ON mx.id = cx.menu_id
                WHERE cx.company_id = r.id
                  AND mx.route = '/config/guia-genetica-santa-reyes'
                  AND cx.is_enabled);

-- ─────────────────────────────────────────────────────────────────────────────
-- Verificación
-- ─────────────────────────────────────────────────────────────────────────────
-- SELECT m.id, m.label, m.route, m.icon, m."order", m.key, m.is_active
--   FROM public.menus m
--  WHERE m.route LIKE '/config/guia-genetica%'
--  ORDER BY m."order", m.id;
--
-- SELECT co.name AS empresa, m.route, cm.is_enabled
--   FROM public.company_menus cm
--   JOIN public.menus m      ON m.id  = cm.menu_id
--   JOIN public.companies co ON co.id = cm.company_id
--  WHERE m.route LIKE '/config/guia-genetica%'
--  ORDER BY co.id, m.route;
