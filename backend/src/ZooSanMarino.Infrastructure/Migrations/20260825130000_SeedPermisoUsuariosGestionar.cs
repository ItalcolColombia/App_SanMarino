using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Permiso <c>usuarios.gestionar</c>: separa VER de ESCRIBIR en Gestión de Usuarios.
    ///
    /// <para>
    /// <b>Pedido:</b> hoy cualquier sesión que llegue a <c>/config/users</c> puede crear, editar,
    /// eliminar, resetear contraseñas y asignar granjas — no hay un solo gate, ni en el front ni en
    /// el backend. Con el permiso se puede escribir; sin él se ve el listado y el detalle, nada más.
    /// </para>
    ///
    /// <para>
    /// <b>Nombre:</b> convención <c>modulo.accion</c> del repo (documentada en
    /// <c>20260714112951_AddPermisosCargaMasivaMigracionesMasivas</c>). ⛔ No se reusa la key legacy
    /// <c>manage_users</c> de <c>PermissionSeed.cs</c>: no la consulta nadie, no respeta la
    /// convención y arrastra un link de <c>menu_permissions</c> del seed.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Anti-lockout — el punto delicado.</b> Este permiso <b>invierte el default</b>: antes
    /// podían todos. Sembrarlo solo a <c>role_id = 1</c> dejaría sin poder crear un usuario a quien
    /// administra usuarios en cada país, el mismo día del deploy. Por eso se otorga heredando de
    /// <c>role_menus</c> por la <b>route</b> <c>/config/users</c> (patrón de
    /// <c>20260815010000_SeedPermisosValidarSeguimientos</c>, localizando por route y jamás por id):
    /// todo rol que hoy ve el módulo lo conserva. Medido el 25-ago-2026 sobre la copia de
    /// producción: <b>12 roles</b> tienen ese menú.
    /// </para>
    ///
    /// <para>
    /// ⛔ <b>Lo que esta migración NO hace, a propósito: no toca <c>menu_permissions</c>.</b> Esa
    /// tabla SÍ gatea del lado del servidor (<c>MenuService</c> y <c>RoleCompositeService</c> filtran
    /// los menús por <c>RequiredKeys.Intersect(userPermKeys)</c>). Hoy tiene 17 filas, todas de
    /// tickets/ItalJira/gerencia, y <b>ninguna</b> del menú «Usuarios» (id 13) — por eso el módulo se
    /// ve. Agregarle una fila lo <b>escondería</b> para quien no tenga la key, que es lo contrario de
    /// lo pedido: el listado sigue abierto.
    /// </para>
    ///
    /// <para>
    /// <b>Dos deudas de permisos que se arrastran en el mismo archivo</b>, porque son el mismo riesgo
    /// y dejarlas afuera sería pasar al lado de un botón roto:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     <c>usuarios.revocar_sesion</c> está declarada en <c>RevocacionSesionCalculos</c>, se exige
    ///     en <c>SessionController</c> y se testea — pero <b>ninguna migración la insertó nunca</b>.
    ///     Efecto medible hoy: el botón «Sesiones activas» devuelve <b>403 a todo el que no sea super
    ///     admin</b>, y la key ni siquiera es asignable desde la pantalla de Roles. Sin sembrarla,
    ///     ese 403 se leería como un bug del permiso nuevo.
    ///   </description></item>
    ///   <item><description>
    ///     <c>abrir_lote</c>, <c>liquidar_lote</c>, <c>cuadrar_ingresos_traslados_seguimiento</c> y
    ///     <c>confirmar_despacho</c> existen en la tabla <c>permissions</c> de producción (medido)
    ///     pero <b>no están en ningún seed ni migración</b>: se insertaron a mano. Si alguna vez se
    ///     recrea la BD desde migraciones, esos cuatro botones desaparecen para todos y nadie va a
    ///     saber por qué. El <c>WHERE NOT EXISTS</c> las vuelve un no-op en producción y una red de
    ///     seguridad en cualquier entorno nuevo.
    ///   </description></item>
    /// </list>
    ///
    /// Plan: <c>fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md</c> §3.
    /// Migración DATA-ONLY: Designer clonado del ModelSnapshot vigente, ModelSnapshot intacto.
    /// Idempotente (<c>WHERE NOT EXISTS</c>), localizando por <c>permissions.key</c> y por
    /// <c>menus.route</c>.
    /// </summary>
    public partial class SeedPermisoUsuariosGestionar : Migration
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
-- 1) Las dos keys nuevas del modulo de usuarios.
--    `usuarios.revocar_sesion` NO es nueva en el codigo (RevocacionSesionCalculos ya la exige):
--    es nueva en la BD, donde nunca se sembro.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.permissions (key, description)
SELECT v.key, v.descripcion
FROM (VALUES
    ('usuarios.gestionar',
     'Gestion de Usuarios: crear, editar, eliminar usuarios, restablecer contrasenas y asignar granjas. Sin este permiso el modulo queda de solo lectura: se ve el listado y el detalle.'),
    ('usuarios.revocar_sesion',
     'Gestion de Usuarios: ver las sesiones activas de otro usuario y cerrarlas (una o todas).')
) AS v(key, descripcion)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Red de seguridad: las 4 keys que el front gatea y que ninguna migracion sembro nunca.
--    En produccion ya existen => no-op. En una BD recreada desde migraciones, sin esto los
--    botones que gatean desaparecen para todo el mundo.
--    Las descripciones son las que hoy tiene produccion, copiadas tal cual.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.permissions (key, description)
SELECT v.key, v.descripcion
FROM (VALUES
    ('abrir_lote',        'Reabrir un lote que ya fue liquidado'),
    ('liquidar_lote',     'Cerrar operativamente un lote (liquidacion)'),
    ('confirmar_despacho','Completar/confirmar un despacho de aves engorde'),
    ('cuadrar_ingresos_traslados_seguimiento',
     'Cuadre de saldo de alimento: agrega y acomoda fechas de ingresos, traslados, salida y entrada')
) AS v(key, descripcion)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) Asignable en TODAS las empresas. company_permissions es FAIL-CLOSED
--    (AuthService.PermisosEfectivosAsync): sin esta fila el permiso NO viaja en el JWT aunque el
--    rol lo tenga, y tampoco se ofrece en el tab Permisos del modal de rol.
--    Ojo: SembrarCatalogoCompletoSiVaciaAsync solo siembra empresas VACIAS, no rellena keys nuevas
--    en empresas ya configuradas => este paso es obligatorio, no opcional.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT c.id, p.id, true
FROM public.companies c
CROSS JOIN public.permissions p
WHERE p.key IN ('usuarios.gestionar', 'usuarios.revocar_sesion',
                'abrir_lote', 'liquidar_lote', 'confirmar_despacho',
                'cuadrar_ingresos_traslados_seguimiento')
  AND NOT EXISTS (
        SELECT 1 FROM public.company_permissions x
        WHERE x.company_id = c.id AND x.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) ANTI-LOCKOUT. Todo rol que HOY ve el menu /config/users conserva lo que hoy puede hacer.
--    Se localiza por route, nunca por id de menu (los ids difieren local <-> produccion).
--    Solo aplica a las 2 keys nuevas: las 4 de la red de seguridad ya tienen sus asignaciones
--    reales en produccion y no hay que inventarles ninguna.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rm.role_id, p.id
FROM public.role_menus rm
JOIN public.menus m ON m.id = rm.menu_id AND m.route = '/config/users'
CROSS JOIN public.permissions p
WHERE p.key IN ('usuarios.gestionar', 'usuarios.revocar_sesion')
  AND NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = rm.role_id AND rp.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 5) Y al rol Admin, que puede no tener el menu cableado y igual tiene que poder administrar.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT 1, p.id
FROM public.permissions p
WHERE p.key IN ('usuarios.gestionar', 'usuarios.revocar_sesion')
  AND EXISTS (SELECT 1 FROM public.roles r WHERE r.id = 1)
  AND NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = 1 AND rp.permission_id = p.id);
";

        // Solo se deshacen las DOS keys que esta migracion crea. Las 4 de la red de seguridad NO se
        // borran: existian antes en produccion y borrarlas al revertir se llevaria puestos permisos
        // que esta migracion no creo.
        private const string DOWN_SQL = @"
DELETE FROM public.role_permissions    WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN ('usuarios.gestionar','usuarios.revocar_sesion'));
DELETE FROM public.company_permissions WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN ('usuarios.gestionar','usuarios.revocar_sesion'));
DELETE FROM public.permissions         WHERE key IN ('usuarios.gestionar','usuarios.revocar_sesion');
";
    }
}
