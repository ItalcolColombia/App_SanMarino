// Partial de la migracion FnMenuUsuario: la constante SQL, para que el archivo principal se pueda
// leer. Es backend/sql/fn_menu_usuario.sql TAL CUAL (espejo).

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    public partial class FnMenuUsuario
    {
        /// <summary>
        /// El menu efectivo del usuario en su empresa, ya armado como arbol jsonb.
        /// Espejo exacto de backend/sql/fn_menu_usuario.sql.
        /// </summary>
        private const string FnMenuUsuarioSql = """
-- ============================================================================
-- fn_menu_usuario — el menú EFECTIVO de un usuario dentro de una empresa,
--                   ya armado como árbol jsonb.
-- ----------------------------------------------------------------------------
-- Reemplaza lo que hacía RoleCompositeService.Menus_GetForUserAsync con cuatro
-- viajes a la BD + armado del árbol en memoria del backend. Acá se resuelve en
-- UNA llamada, donde viven los índices; el backend deserializa y responde.
--
-- La regla:
--
--     visibles =
--         menus.is_active
--       ∩ ( role_menus de los roles del usuario en la empresa      -- si tiene alguno
--           | todo el catálogo                                     -- fallback sin role_menus
--         )
--       ∩ HABILITADOS PARA LA EMPRESA (company_menus)              ← el gate que faltaba
--       + ancestros de cada visible
--       ∩ menu_permissions ⊆ permisos del usuario                  -- sólo en la rama asignada
--
-- Cuatro decisiones, con su razón (plan: fase_de_desarrollo/menu_efectivo_por_empresa_plan.md):
--
--   D1 — «habilitado para la empresa» = fila en company_menus con is_enabled = true.
--        La fila ausente y is_enabled = false ocultan igual: es lo que escribe la
--        pantalla Configuración → Empresas → Menús.
--
--   D2 — Empresa SIN ninguna fila en company_menus ⇒ NO se filtra (fail-open por
--        empresa). CompanyService.CreateAsync siembra company_permissions pero NO
--        company_menus: con fail-closed, una empresa nueva nacería con el menú vacío
--        y sin forma de arreglarlo desde la app (para asignar menús hay que entrar a
--        Configuración, que es un ítem del menú que no se vería). Fail-open sobre la
--        tabla vacía no puede empeorar lo de hoy — hoy no se filtra nunca.
--
--   D3 — Los ancestros se incluyen solos, y por eso el gate de empresa se aplica a la
--        SEMILLA (lo asignado) y no al conjunto final: un grupo padre que no esté en
--        company_menus pero con hijos habilitados se muestra igual, porque si no el
--        submenú entero desaparece.
--
--   D4 — El orden y la jerarquía salen de menus."order" / menus.parent_id, NO de
--        company_menus.sort_order / parent_menu_id. Esas dos columnas existen y hoy el
--        sidebar las ignora; usarlas reordenaría las cinco empresas en el mismo commit
--        que arregla la visibilidad. Fuera de alcance a propósito.
--
-- Empates de "order": se desempata por id. Antes quedaba al azar del motor (hay cuatro
-- empates reales, p. ej. Carga Masiva e ItalJira comparten order 901).
--
-- p_company_id NULL ⇒ sin filtro por empresa y sin recorte de roles: es el modo del
-- endpoint de administración por usuario.
--
-- Devuelve: jsonb array de { id, label, icon, route, order, children[] } — el mismo
-- contrato que MenuItemDto, camelCase.
-- ============================================================================

CREATE OR REPLACE FUNCTION fn_menu_usuario(
    p_user_id    uuid,
    p_company_id integer DEFAULT NULL
)
RETURNS jsonb
LANGUAGE plpgsql
STABLE
AS $fn$
DECLARE
    v_planos jsonb;   -- [{id, label, icon, route, order, parentId, nivel}]
    v_max    integer;
    v_nivel  integer;
    v_hijos  jsonb := '{}'::jsonb;   -- { "<parent_id>": [ nodo, ... ] }
    v_result jsonb;
BEGIN
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
          JOIN roles r          ON r.role_id = rp.role_id
          JOIN permissions p    ON p.id      = rp.permission_id
    ),
    activos AS (
        SELECT m.id, m.label, m.icon, m.route, m."order" AS orden, m.parent_id
          FROM menus m
         WHERE m.is_active
    ),
    -- El menú no exige permisos, o el usuario tiene al menos uno de los que exige.
    permitidos AS (
        SELECT a.id
          FROM activos a
         WHERE NOT EXISTS (SELECT 1 FROM menu_permissions mp WHERE mp.menu_id = a.id)
            OR EXISTS (
                   SELECT 1
                     FROM menu_permissions mp
                     JOIN permissions p ON p.id = mp.permission_id
                    WHERE mp.menu_id = a.id
                      AND lower(p.key) IN (SELECT key FROM perm_keys)
               )
    ),
    -- Ojo con la diferencia entre estas dos: la RAMA se decide con role_menus crudo (es lo que
    -- miraba el C#), pero la semilla sólo puede traer menús activos. Un usuario cuyos role_menus
    -- apunten todos a menús inactivos se queda sin menú — NO cae al fallback, que sería más
    -- permisivo justo donde no corresponde.
    asignados_crudos AS (
        SELECT DISTINCT rm.menu_id AS id
          FROM role_menus rm
          JOIN roles r ON r.role_id = rm.role_id
    ),
    asignados AS (
        SELECT a.id FROM activos a WHERE a.id IN (SELECT id FROM asignados_crudos)
    ),
    -- D2: la empresa filtra sólo si alguien la configuró.
    empresa_filtra AS (
        SELECT p_company_id IS NOT NULL
           AND EXISTS (SELECT 1 FROM company_menus cm WHERE cm.company_id = p_company_id) AS si
    ),
    habilitados_empresa AS (
        SELECT a.id
          FROM activos a
         WHERE NOT (SELECT si FROM empresa_filtra)
            OR EXISTS (
                   SELECT 1
                     FROM company_menus cm
                    WHERE cm.company_id = p_company_id
                      AND cm.menu_id    = a.id
                      AND cm.is_enabled
               )
    ),
    -- D3: el gate de empresa se aplica ACÁ, antes de subir por los ancestros.
    semilla AS (
        SELECT id FROM asignados
         WHERE id IN (SELECT id FROM habilitados_empresa)
        UNION
        SELECT id FROM permitidos
         WHERE id IN (SELECT id FROM habilitados_empresa)
           AND NOT EXISTS (SELECT 1 FROM asignados_crudos)
    ),
    -- Ancestros: se sube por la cadena de padres, siempre dentro de los menús activos.
    -- Una cadena cortada por un ancestro inactivo deja al nodo huérfano, y el armado de
    -- abajo lo descarta — igual que hoy.
    con_ancestros AS (
        SELECT a.id, a.parent_id
          FROM activos a
         WHERE a.id IN (SELECT id FROM semilla)
        UNION
        SELECT p.id, p.parent_id
          FROM activos p
          JOIN con_ancestros c ON p.id = c.parent_id
    ),
    -- En la rama asignada el filtro de permisos se aplica también a los ancestros (es lo
    -- que hace el C# de hoy); en el fallback no, porque ahí los ancestros ya entraron por
    -- fuera del filtro.
    finales AS (
        SELECT c.id
          FROM con_ancestros c
         WHERE NOT EXISTS (SELECT 1 FROM asignados_crudos)
            OR c.id IN (SELECT id FROM permitidos)
    ),
    -- Nivel = profundidad desde una raíz visible. Lo que no se alcanza desde una raíz
    -- (padre fuera del conjunto) no se pinta, igual que BuildTree.
    niveles AS (
        SELECT a.id, 0 AS nivel
          FROM activos a
         WHERE a.id IN (SELECT id FROM finales)
           AND a.parent_id IS NULL
        UNION ALL
        SELECT a.id, n.nivel + 1
          FROM activos a
          JOIN niveles n ON n.id = a.parent_id
         WHERE a.id IN (SELECT id FROM finales)
           AND n.nivel < 20   -- corta un ciclo de parent_id: es UNION ALL y no dedupe solo
    )
    SELECT jsonb_agg(
               jsonb_build_object(
                   'id',       a.id,
                   'label',    a.label,
                   'icon',     a.icon,
                   'route',    a.route,
                   'order',    a.orden,
                   'parentId', a.parent_id,
                   'nivel',    n.nivel
               )
           )
      INTO v_planos
      FROM niveles n
      JOIN activos a ON a.id = n.id;

    IF v_planos IS NULL THEN
        RETURN '[]'::jsonb;
    END IF;

    SELECT max((e->>'nivel')::int) INTO v_max
      FROM jsonb_array_elements(v_planos) e;

    -- Se pliega de la hoja hacia la raíz: en cada vuelta, los nodos del nivel N se agrupan
    -- por su padre y se guardan en v_hijos, que la vuelta siguiente (nivel N-1) consume.
    FOR v_nivel IN REVERSE v_max..1 LOOP
        SELECT v_hijos || COALESCE(jsonb_object_agg(s.pid, s.arr), '{}'::jsonb)
          INTO v_hijos
          FROM (
              SELECT e->>'parentId' AS pid,
                     jsonb_agg(
                         jsonb_build_object(
                             'id',       (e->>'id')::int,
                             'label',    e->>'label',
                             'icon',     e->'icon',
                             'route',    e->'route',
                             'order',    (e->>'order')::int,
                             'children', COALESCE(v_hijos -> (e->>'id'), '[]'::jsonb)
                         )
                         ORDER BY (e->>'order')::int, (e->>'id')::int
                     ) AS arr
                FROM jsonb_array_elements(v_planos) e
               WHERE (e->>'nivel')::int = v_nivel
               GROUP BY e->>'parentId'
          ) s;
    END LOOP;

    SELECT COALESCE(
               jsonb_agg(
                   jsonb_build_object(
                       'id',       (e->>'id')::int,
                       'label',    e->>'label',
                       'icon',     e->'icon',
                       'route',    e->'route',
                       'order',    (e->>'order')::int,
                       'children', COALESCE(v_hijos -> (e->>'id'), '[]'::jsonb)
                   )
                   ORDER BY (e->>'order')::int, (e->>'id')::int
               ),
               '[]'::jsonb
           )
      INTO v_result
      FROM jsonb_array_elements(v_planos) e
     WHERE (e->>'nivel')::int = 0;

    RETURN v_result;
END;
$fn$;

COMMENT ON FUNCTION fn_menu_usuario(uuid, integer) IS
    'Menu efectivo de un usuario en una empresa, ya armado como arbol jsonb. Interseca role_menus, menu_permissions y company_menus (este ultimo solo si la empresa tiene configuracion). Espejo: backend/sql/fn_menu_usuario.sql';
""";
    }
}
