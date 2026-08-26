-- ============================================================================
-- verificar_menu_usuario_paridad.sql — diagnóstico de SOLO LECTURA
-- ----------------------------------------------------------------------------
-- Prueba que `fn_menu_usuario` no cambió nada salvo lo que vino a cambiar.
--
-- Dos invariantes, usuario por usuario y empresa por empresa:
--
--   1. NUEVO ⊆ VIEJO      — la función no puede hacer aparecer ningún menú que la regla
--                           anterior no mostrara. Cualquier fila acá es una regresión.
--   2. VIEJO \ NUEVO      — sólo pueden faltar menús que la empresa NO tiene habilitados
--                           en `company_menus`. Cualquier otra cosa es un daño colateral.
--
-- «VIEJO» es la regla que tenía `RoleCompositeService.Menus_GetForUserAsync` antes del
-- 26-ago-2026, reescrita acá en SQL a partir del C#: role_menus ∩ menus.is_active ∩
-- menu_permissions, más los ancestros, y SIN mirar `company_menus`.
--
-- Todo corre dentro de una transacción que se revierte: la función auxiliar no queda.
--
-- Uso:
--   psql -h 127.0.0.1 -p 5433 -U postgres -d sanmarinoapplocal -X -f backend/sql/verificar_menu_usuario_paridad.sql
--
-- Requiere que `fn_menu_usuario` ya exista (la aplica la migración FnMenuUsuario).
-- ============================================================================

\set ON_ERROR_STOP on
\pset pager off

BEGIN;

-- ── La regla VIEJA, tal como estaba en C#, devuelta como ids planos ──────────
CREATE OR REPLACE FUNCTION pg_temp.menu_usuario_ids_viejo(
    p_user_id    uuid,
    p_company_id integer
)
RETURNS TABLE (menu_id integer)
LANGUAGE sql
STABLE
AS $$
WITH RECURSIVE
roles AS (
    SELECT DISTINCT ur.role_id
      FROM user_roles ur
     WHERE ur.user_id = p_user_id
       AND (p_company_id IS NULL OR ur.company_id = p_company_id)
),
perm_keys AS (
    SELECT DISTINCT lower(p.key) AS key
      FROM role_permissions rp
      JOIN roles r       ON r.role_id = rp.role_id
      JOIN permissions p ON p.id      = rp.permission_id
),
activos AS (
    SELECT m.id, m.parent_id FROM menus m WHERE m.is_active
),
permitidos AS (
    SELECT a.id
      FROM activos a
     WHERE NOT EXISTS (SELECT 1 FROM menu_permissions mp WHERE mp.menu_id = a.id)
        OR EXISTS (
               SELECT 1 FROM menu_permissions mp
                 JOIN permissions p ON p.id = mp.permission_id
                WHERE mp.menu_id = a.id AND lower(p.key) IN (SELECT key FROM perm_keys)
           )
),
asignados_crudos AS (
    SELECT DISTINCT rm.menu_id AS id
      FROM role_menus rm
      JOIN roles r ON r.role_id = rm.role_id
),
asignados AS (
    SELECT a.id FROM activos a WHERE a.id IN (SELECT id FROM asignados_crudos)
),
semilla AS (
    SELECT id FROM asignados
    UNION
    SELECT id FROM permitidos WHERE NOT EXISTS (SELECT 1 FROM asignados_crudos)
),
con_ancestros AS (
    SELECT a.id, a.parent_id FROM activos a WHERE a.id IN (SELECT id FROM semilla)
    UNION
    SELECT p.id, p.parent_id FROM activos p JOIN con_ancestros c ON p.id = c.parent_id
),
finales AS (
    SELECT c.id
      FROM con_ancestros c
     WHERE NOT EXISTS (SELECT 1 FROM asignados_crudos)
        OR c.id IN (SELECT id FROM permitidos)
),
alcanzables AS (
    SELECT a.id FROM activos a
     WHERE a.id IN (SELECT id FROM finales) AND a.parent_id IS NULL
    UNION   -- UNION y no UNION ALL: dedupea contra lo ya producido, asi un ciclo de parent_id corta
    SELECT a.id FROM activos a JOIN alcanzables al ON al.id = a.parent_id
     WHERE a.id IN (SELECT id FROM finales)
)
SELECT DISTINCT id FROM alcanzables;
$$;

-- ── Pares (usuario, empresa) reales ─────────────────────────────────────────
CREATE TEMP VIEW pares AS
    SELECT DISTINCT ur.user_id, ur.company_id FROM user_roles ur;

CREATE TEMP VIEW viejo AS
    SELECT p.user_id, p.company_id, v.menu_id
      FROM pares p, LATERAL pg_temp.menu_usuario_ids_viejo(p.user_id, p.company_id) v;

CREATE TEMP VIEW nuevo AS
    SELECT p.user_id, p.company_id, (e->>'id')::int AS menu_id
      FROM pares p,
           LATERAL jsonb_array_elements(fn_menu_usuario(p.user_id, p.company_id)) e
    UNION
    SELECT p.user_id, p.company_id, (h->>'id')::int
      FROM pares p,
           LATERAL jsonb_array_elements(fn_menu_usuario(p.user_id, p.company_id)) e,
           LATERAL jsonb_array_elements(e->'children') h
    UNION
    SELECT p.user_id, p.company_id, (n->>'id')::int
      FROM pares p,
           LATERAL jsonb_array_elements(fn_menu_usuario(p.user_id, p.company_id)) e,
           LATERAL jsonb_array_elements(e->'children') h,
           LATERAL jsonb_array_elements(h->'children') n;

\echo ''
\echo '=== Invariante 1: NUEVO ⊆ VIEJO — cualquier fila acá es una REGRESION ==='
SELECT c.name AS empresa, n.menu_id, m.label, m.route, count(*) AS usuarios
  FROM nuevo n
  LEFT JOIN viejo v USING (user_id, company_id, menu_id)
  JOIN companies c ON c.id = n.company_id
  JOIN menus m     ON m.id = n.menu_id
 WHERE v.menu_id IS NULL
 GROUP BY 1,2,3,4
 ORDER BY 1,2;

\echo ''
\echo '=== Invariante 2: VIEJO \ NUEVO — solo lo que la empresa NO habilita ==='
SELECT c.name AS empresa,
       v.menu_id,
       m.label,
       m.route,
       count(*) AS usuarios,
       CASE
           WHEN EXISTS (SELECT 1 FROM company_menus cm
                         WHERE cm.company_id = v.company_id AND cm.menu_id = v.menu_id AND cm.is_enabled)
           THEN '*** COLATERAL: la empresa SI lo habilita ***'
           ELSE 'esperado (no habilitado para la empresa)'
       END AS causa
  FROM viejo v
  LEFT JOIN nuevo n USING (user_id, company_id, menu_id)
  JOIN companies c ON c.id = v.company_id
  JOIN menus m     ON m.id = v.menu_id
 WHERE n.menu_id IS NULL
 GROUP BY 1,2,3,4,6
 ORDER BY 6 DESC, 1, 2;

\echo ''
\echo '=== Resumen ==='
SELECT
    (SELECT count(*) FROM nuevo n LEFT JOIN viejo v USING (user_id, company_id, menu_id)
      WHERE v.menu_id IS NULL)                                            AS regresiones,
    (SELECT count(*) FROM viejo v LEFT JOIN nuevo n USING (user_id, company_id, menu_id)
      WHERE n.menu_id IS NULL
        AND EXISTS (SELECT 1 FROM company_menus cm
                     WHERE cm.company_id = v.company_id AND cm.menu_id = v.menu_id AND cm.is_enabled))
                                                                          AS colaterales,
    (SELECT count(*) FROM viejo v LEFT JOIN nuevo n USING (user_id, company_id, menu_id)
      WHERE n.menu_id IS NULL)                                            AS ocultados_por_el_gate,
    (SELECT count(*) FROM pares)                                          AS pares_usuario_empresa;

ROLLBACK;
