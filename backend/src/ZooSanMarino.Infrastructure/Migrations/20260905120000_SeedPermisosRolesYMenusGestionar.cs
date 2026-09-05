using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Permisos <c>roles.gestionar</c> y <c>menus.gestionar</c>: cierran la escalada de privilegios
    /// que dejaba abierta la policy <c>CanManageRoles</c>.
    ///
    /// <para>
    /// <b>El agujero, probado en vivo el 5-sep-2026.</b> <c>Program.cs</c> declaraba
    /// <c>CanManageRoles</c> y <c>CanManageMenus</c> como <c>RequireAuthenticatedUser()</c> —token
    /// válido y nada más— con un <c>TODO(seguridad)</c> al lado. Con el JWT de un usuario real sin
    /// ningún permiso de administración: <c>GET /api/Roles</c> → <b>200</b> (todos los roles CON sus
    /// permisos), <c>GET /api/Roles/permissions</c> → <b>200</b>, y
    /// <c>POST /api/Roles/999999/permissions/assign</c> → <b>404, no 403</b>: la autorización pasó y
    /// lo único que lo frenó fue que ese rol no existiera. Como las keys se hornean como claims
    /// <c>permission</c> en el token al login, quien escribe <c>role_permissions</c> se asigna
    /// cualquier permiso, vuelve a entrar y se salta <b>todos</b> los demás gates del sistema.
    /// </para>
    ///
    /// <para>
    /// <b>Nombres:</b> convención <c>modulo.accion</c> del repo. ⛔ No se reusan las keys legacy
    /// <c>manage_roles</c> / <c>manage_menus</c> de <c>PermissionSeed.cs</c>: no las consulta nadie,
    /// no respetan la convención y no existen en la base (medido: 45 keys, ninguna de las dos).
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Anti-lockout — el punto delicado.</b> Estos permisos <b>invierten el default</b>: antes
    /// podían todos. Sembrarlos sólo a <c>role_id = 1</c> dejaría sin poder administrar roles a quien
    /// lo hace en cada país, el mismo día del deploy. Se otorgan heredando de <c>role_menus</c> por
    /// la <b>route</b> <c>/config/role-management</c> (patrón de
    /// <c>20260825130000_SeedPermisoUsuariosGestionar</c>, localizando por route y jamás por id, que
    /// difiere local↔producción). Medido el 5-sep-2026 sobre la copia de producción: <b>11 roles</b>
    /// tienen ese menú, y son 15 de los 58 usuarios.
    /// </para>
    ///
    /// <para>
    /// <c>menus.gestionar</c> se otorga <b>también</b> por la route <c>/config/companies</c>: la
    /// pantalla de Empresas lee el árbol global de menús para armar el tab de módulos de la empresa.
    /// Hoy es sólo el rol <c>Admin</c> (que ya entra por la otra route), pero dejarlo fuera haría que
    /// el próximo rol que reciba Empresas sin Roles se encuentre el tab vacío.
    /// </para>
    ///
    /// <para>
    /// ⛔ <b>Lo que esta migración NO hace, a propósito: no toca <c>menu_permissions</c>.</b> Esa
    /// tabla SÍ esconde el ítem de menú a quien no tenga la key. Agregarle una fila para
    /// <c>/config/role-management</c> escondería el módulo, que es un cambio distinto —y más
    /// ruidoso— del que se pidió: acá lo que se cierra es el <b>endpoint</b>, que es lo que un
    /// atacante llama por HTTP directo sin mirar el menú.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Los permisos se congelan en el token al login (60 min).</b> Tras el deploy, quien tenga
    /// una sesión abierta sigue con los claims viejos hasta que vuelva a entrar. En la práctica eso
    /// juega a favor —nadie pierde acceso a mitad de jornada— pero hay que tenerlo en cuenta al
    /// verificar: si un admin ve 403 justo después del deploy, que cierre sesión y vuelva a entrar.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/gate_roles_y_menus_plan.md</c>.
    /// Migración DATA-ONLY: Designer clonado del ModelSnapshot vigente, ModelSnapshot intacto.
    /// Idempotente (<c>WHERE NOT EXISTS</c>), localizando por <c>permissions.key</c> y por
    /// <c>menus.route</c>.
    /// </summary>
    public partial class SeedPermisosRolesYMenusGestionar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UP_SQL);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }

        private const string UP_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- 1) Las dos keys.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.permissions (key, description)
SELECT v.key, v.descripcion
FROM (VALUES
    ('roles.gestionar',
     'Roles y permisos: crear, editar y eliminar roles, y asignar o quitar sus permisos. Sin este permiso no se puede administrar el modulo ni consultar el mapa de permisos de los roles.'),
    ('menus.gestionar',
     'Roles y permisos: consultar el catalogo GLOBAL de menus (el arbol de modulos de todas las empresas). Lo usan la pantalla de Roles y la de Empresas.')
) AS v(key, descripcion)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Asignable en TODAS las empresas. company_permissions es FAIL-CLOSED
--    (AuthService.PermisosEfectivosAsync intersecta role_permissions x company_permissions): sin
--    esta fila el permiso NO viaja en el JWT aunque el rol lo tenga, y tampoco se ofrece en el tab
--    Permisos del modal de rol. Es la trampa ya medida: `carga_masiva_pollo_engorde` esta asignada
--    por rol a 13 usuarios y solo llega al token de 8, justamente por esto.
--    Ojo: SembrarCatalogoCompletoSiVaciaAsync solo siembra empresas VACIAS, no rellena keys nuevas
--    en empresas ya configuradas => este paso es obligatorio, no opcional.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT c.id, p.id, true
FROM public.companies c
CROSS JOIN public.permissions p
WHERE p.key IN ('roles.gestionar', 'menus.gestionar')
  AND NOT EXISTS (
        SELECT 1 FROM public.company_permissions x
        WHERE x.company_id = c.id AND x.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) ANTI-LOCKOUT. Todo rol que HOY ve el modulo de Roles conserva lo que hoy puede hacer.
--    Se localiza por route, nunca por id de menu (los ids difieren local <-> produccion).
--    Medido 05-sep-2026 sobre la copia de produccion: 11 roles, 15 usuarios.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rm.role_id, p.id
FROM public.role_menus rm
JOIN public.menus m ON m.id = rm.menu_id AND m.route = '/config/role-management'
CROSS JOIN public.permissions p
WHERE p.key IN ('roles.gestionar', 'menus.gestionar')
  AND NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = rm.role_id AND rp.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) La pantalla de Empresas tambien lee el arbol global de menus (tab de modulos de la empresa).
--    Solo `menus.gestionar`: administrar empresas no es administrar roles.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rm.role_id, p.id
FROM public.role_menus rm
JOIN public.menus m ON m.id = rm.menu_id AND m.route = '/config/companies'
CROSS JOIN public.permissions p
WHERE p.key = 'menus.gestionar'
  AND NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = rm.role_id AND rp.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 5) Y al rol Admin, que puede no tener el menu cableado y igual tiene que poder administrar.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT 1, p.id
FROM public.permissions p
WHERE p.key IN ('roles.gestionar', 'menus.gestionar')
  AND EXISTS (SELECT 1 FROM public.roles r WHERE r.id = 1)
  AND NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = 1 AND rp.permission_id = p.id);
";

        // Borra SOLO lo que esta migracion crea. Revertirla devuelve el sistema al estado anterior,
        // que es el agujero: el Down existe para poder deshacer un deploy, no porque quedarse sin las
        // keys sea deseable.
        private const string DOWN_SQL = @"
DELETE FROM public.role_permissions    WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN ('roles.gestionar','menus.gestionar'));
DELETE FROM public.company_permissions WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN ('roles.gestionar','menus.gestionar'));
DELETE FROM public.permissions         WHERE key IN ('roles.gestionar','menus.gestionar');
";
    }
}
