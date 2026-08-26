-- ============================================================================
-- Permiso `guia_genetica.gestionar`.
--
-- ESPEJO LEGIBLE de la migración
--   20260826160100_SeedPermisoGuiaGeneticaGestionar
-- que es la que lo aplica en producción. Este archivo NO se corre solo: nada aplica
-- `backend/sql/` (ni el arranque de la app ni el deploy), sólo las migraciones EF.
--
-- La key la exige GuiaGeneticaEscrituraGuard en GuiaGeneticaSantaReyesController. INVIERTE EL
-- DEFAULT (hasta hoy escribía cualquier sesión autenticada): sin sembrarla, toda escritura del
-- módulo nuevo responde 403 y la key ni siquiera es asignable desde la pantalla de Roles.
--
-- Tres pasos, ninguno opcional:
--   1) permissions            — la key (convención `modulo.accion`).
--   2) company_permissions    — FAIL-CLOSED en AuthService.PermisosEfectivosAsync: sin la fila el
--                               permiso NO viaja en el JWT aunque el rol lo tenga.
--   3) role_permissions       — anti-lockout: todo rol que HOY ve alguno de los tres ítems de
--                               guía, localizado por `route` (jamás por id).
--
-- NO toca menu_permissions a propósito: esa tabla ESCONDE el menú a quien no tenga la key, y las
-- lecturas de la guía quedan abiertas. La escritura la corta el guard del controller.
-- ============================================================================

-- 1) La key.
INSERT INTO public.permissions (key, description)
SELECT 'guia_genetica.gestionar',
       'Guia Genetica: crear, editar, importar y dar de baja lineas de la guia. Sin este permiso el modulo queda de solo lectura: se consulta y se exporta.'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = 'guia_genetica.gestionar');

-- 2) Habilitada en TODAS las empresas (fail-closed).
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT c.id, p.id, true
  FROM public.companies c
 CROSS JOIN public.permissions p
 WHERE p.key = 'guia_genetica.gestionar'
   AND NOT EXISTS (SELECT 1 FROM public.company_permissions x
                    WHERE x.company_id = c.id AND x.permission_id = p.id);

-- 3) ANTI-LOCKOUT: todo rol que HOY ve alguno de los tres ítems de guía.
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rm.role_id, p.id
  FROM public.role_menus rm
  JOIN public.menus m ON m.id = rm.menu_id
                     AND m.route IN ('/config/guia-genetica',
                                     '/config/guia-genetica-ecuador',
                                     '/config/guia-genetica-santa-reyes')
 CROSS JOIN public.permissions p
 WHERE p.key = 'guia_genetica.gestionar'
   AND NOT EXISTS (SELECT 1 FROM public.role_permissions rp
                    WHERE rp.role_id = rm.role_id AND rp.permission_id = p.id);

-- 4) Y al rol Admin, que puede no tener el menú cableado y igual tiene que poder administrar.
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT 1, p.id
  FROM public.permissions p
 WHERE p.key = 'guia_genetica.gestionar'
   AND EXISTS (SELECT 1 FROM public.roles r WHERE r.id = 1)
   AND NOT EXISTS (SELECT 1 FROM public.role_permissions rp
                    WHERE rp.role_id = 1 AND rp.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- Verificación
-- ─────────────────────────────────────────────────────────────────────────────
-- SELECT r.id, r.name
--   FROM public.role_permissions rp
--   JOIN public.roles r       ON r.id = rp.role_id
--   JOIN public.permissions p ON p.id = rp.permission_id
--  WHERE p.key = 'guia_genetica.gestionar'
--  ORDER BY r.id;
