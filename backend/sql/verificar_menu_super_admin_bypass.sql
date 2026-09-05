-- =============================================================================
-- verificar_menu_super_admin_bypass.sql — diagnóstico de SOLO LECTURA
-- -----------------------------------------------------------------------------
-- Prueba que la regla D5 de `fn_menu_usuario` (el gate de `company_menus` no se le
-- aplica al super admin) cambió EXACTAMENTE lo que vino a cambiar y nada más.
--
-- Dos invariantes, par (usuario, empresa) por par:
--
--   1. NINGÚN no-super-admin cambia — ni gana ni pierde un solo menú. Cualquier fila
--      acá es una regresión: D5 tiene que ser invisible para el 99,9% de la gente.
--
--   2. El super admin SÓLO puede ganar, y lo ganado tiene que ser menús que ya están
--      en sus `role_menus` y que su empresa activa no habilita. Si pierde algo, o si
--      gana algo que su rol no le da, el bypass se pasó de alcance.
--
-- USO — el mismo comando las dos veces, sin flags:
--
--     psql ... -f backend/sql/verificar_menu_super_admin_bypass.sql   <- congela la línea base
--     ... se aplica la migración FnMenuUsuarioSuperAdmin ...
--     psql ... -f backend/sql/verificar_menu_super_admin_bypass.sql   <- compara contra la base
--
-- Para empezar de cero:  DROP TABLE _menu_bypass_base;
--
-- SOLO LECTURA sobre datos de negocio: lo único que escribe es su propia tabla de
-- línea base. Requiere que `fn_menu_usuario` exista.
--
-- SIN-MIGRACION: diagnóstico de solo lectura, no crea ningún objeto que la app consulte.
-- =============================================================================

\set ON_ERROR_STOP on
\pset pager off

-- ── Foto de hoy: un renglón por (usuario, empresa, menú visible) ─────────────
DROP TABLE IF EXISTS _menu_bypass_hoy;

CREATE TEMP TABLE _menu_bypass_hoy AS
WITH RECURSIVE pares AS (
    -- Los pares reales: cada usuario en cada empresa donde tiene algún rol.
    SELECT DISTINCT ur.user_id, ur.company_id
      FROM user_roles ur
),
arbol AS (
    SELECT p.user_id,
           p.company_id,
           fn_menu_usuario(p.user_id, p.company_id) AS menu
      FROM pares p
),
-- El árbol viene anidado; se aplana recursivamente para poder comparar por id.
planos AS (
    SELECT a.user_id, a.company_id, e AS nodo
      FROM arbol a, jsonb_array_elements(a.menu) e
    UNION ALL
    SELECT p.user_id, p.company_id, h
      FROM planos p, jsonb_array_elements(p.nodo -> 'children') h
)
SELECT pl.user_id,
       pl.company_id,
       (pl.nodo ->> 'id')::int AS menu_id,
       u.is_super_admin
  FROM planos pl
  JOIN users u ON u.id = pl.user_id;

CREATE INDEX ON _menu_bypass_hoy (user_id, company_id, menu_id);

-- ── Primera corrida: congela y sale ──────────────────────────────────────────
DO $$
BEGIN
    IF to_regclass('public._menu_bypass_base') IS NULL THEN
        CREATE TABLE public._menu_bypass_base AS
            SELECT now() AS tomada_el, * FROM _menu_bypass_hoy;
        RAISE NOTICE 'Línea base congelada: % filas. Aplicá el cambio y volvé a correr este mismo archivo.',
                     (SELECT count(*) FROM public._menu_bypass_base);
    ELSE
        RAISE NOTICE 'Línea base del % — comparando.',
                     (SELECT max(tomada_el) FROM public._menu_bypass_base);
    END IF;
END $$;

-- ── Invariante 1: ningún no-super-admin cambia ───────────────────────────────
\echo ''
\echo '== INVARIANTE 1 — no-super-admins que cambiaron (tiene que dar 0 filas) =='

SELECT COALESCE(b.user_id, h.user_id)       AS user_id,
       COALESCE(b.company_id, h.company_id) AS company_id,
       c.name                               AS empresa,
       COALESCE(b.menu_id, h.menu_id)       AS menu_id,
       m.route,
       CASE WHEN b.menu_id IS NULL THEN 'GANADO (regresión)' ELSE 'PERDIDO (colateral)' END AS que_paso
  FROM public._menu_bypass_base b
  FULL JOIN _menu_bypass_hoy h
         ON h.user_id    = b.user_id
        AND h.company_id = b.company_id
        AND h.menu_id    = b.menu_id
  LEFT JOIN companies c ON c.id = COALESCE(b.company_id, h.company_id)
  LEFT JOIN menus     m ON m.id = COALESCE(b.menu_id, h.menu_id)
 WHERE (b.menu_id IS NULL OR h.menu_id IS NULL)
   AND NOT COALESCE(b.is_super_admin, h.is_super_admin, false)
 ORDER BY 1, 2, 4;

-- ── Invariante 2: el super admin sólo gana, y sólo lo que su rol le da ───────
\echo ''
\echo '== INVARIANTE 2 — cambios del super admin: PERDIDO y GANADO-SIN-ROL tienen que dar 0 =='

SELECT COALESCE(b.user_id, h.user_id)       AS user_id,
       c.name                               AS empresa,
       m.route,
       CASE
           WHEN b.menu_id IS NULL AND EXISTS (
                    SELECT 1
                      FROM role_menus rm
                      JOIN user_roles ur ON ur.role_id = rm.role_id
                     WHERE ur.user_id    = h.user_id
                       AND ur.company_id = h.company_id
                       AND rm.menu_id    = h.menu_id
                ) THEN 'GANADO (esperado: lo da su rol y la empresa no lo habilita)'
           WHEN b.menu_id IS NULL THEN 'GANADO-SIN-ROL (el bypass se pasó de alcance)'
           ELSE 'PERDIDO (el bypass no debería quitar nada)'
       END AS que_paso
  FROM public._menu_bypass_base b
  FULL JOIN _menu_bypass_hoy h
         ON h.user_id    = b.user_id
        AND h.company_id = b.company_id
        AND h.menu_id    = b.menu_id
  LEFT JOIN companies c ON c.id = COALESCE(b.company_id, h.company_id)
  LEFT JOIN menus     m ON m.id = COALESCE(b.menu_id, h.menu_id)
 WHERE (b.menu_id IS NULL OR h.menu_id IS NULL)
   AND COALESCE(b.is_super_admin, h.is_super_admin, false)
 ORDER BY 2, 3;

-- ── Resumen ──────────────────────────────────────────────────────────────────
\echo ''
\echo '== RESUMEN =='

SELECT count(*) FILTER (
           WHERE NOT COALESCE(b.is_super_admin, h.is_super_admin, false)
       ) AS cambios_de_no_super_admins_debe_ser_0,
       count(*) FILTER (
           WHERE COALESCE(b.is_super_admin, h.is_super_admin, false) AND h.menu_id IS NULL
       ) AS menus_perdidos_por_el_super_admin_debe_ser_0,
       count(*) FILTER (
           WHERE COALESCE(b.is_super_admin, h.is_super_admin, false) AND b.menu_id IS NULL
       ) AS menus_ganados_por_el_super_admin
  FROM public._menu_bypass_base b
  FULL JOIN _menu_bypass_hoy h
         ON h.user_id    = b.user_id
        AND h.company_id = b.company_id
        AND h.menu_id    = b.menu_id
 WHERE b.menu_id IS NULL OR h.menu_id IS NULL;
