using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Asigna el permiso <c>registros.fecha_retroactiva</c> al rol <b>«Ecuador Administrador»</b> de
    /// ItalcolEcuador.
    ///
    /// <para>
    /// <b>Pedido:</b> la usuaria de Ecuador necesita cargar registros con fecha anterior a la ventana
    /// base (mes en curso ∪ últimos 15 días) que valida <c>VentanaFechaRegistroCalculos</c>. El futuro
    /// sigue cerrado para todos, con permiso o sin él.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué alcanza con esto:</b> el permiso ya existe (lo creó
    /// <c>20260820160000_SeedPermisoFechaRetroactivaRegistros</c>) y ya está habilitado en
    /// <c>company_permissions</c> para ItalcolEcuador — medido el 25-ago-2026 sobre la copia de
    /// producción: la empresa 3 tiene 19 permisos habilitados y este es uno de ellos. Lo único que
    /// faltaba era la fila de <c>role_permissions</c>.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Por qué el rol se localiza por NOMBRE y no por id:</b> los ids difieren entre local y
    /// producción (regla de CLAUDE.md, la misma por la que los menús se localizan por <c>route</c>).
    /// Se cruza además contra <c>role_companies</c> + <c>companies.name</c> porque «Administrador» es
    /// un nombre que se repite entre empresas: sin ese join, un rol homónimo de otro país recibiría
    /// el permiso.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué el <c>EXISTS</c> sobre <c>company_permissions</c> no es decorativo:</b>
    /// <c>AuthService.PermisosEfectivosAsync</c> es fail-closed por empresa. Si la empresa no lo
    /// tuviera habilitado, la fila de <c>role_permissions</c> quedaría huérfana —no viaja en el JWT,
    /// la UI la muestra tachada— y la migración diría «OK» sin cumplir el pedido.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Alcanza a los DOS usuarios del rol</b>, no solo a quien lo pidió: un permiso se otorga a
    /// un rol, no a una persona. Está declarado a propósito en el plan; separarlo exigiría un rol
    /// propio, que es una decisión de operación.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>No se ve hasta re-login.</b> Los permisos viajan dentro de la sesión, no se consultan por
    /// acción: la usuaria tiene que cerrar sesión y volver a entrar (o cambiar de empresa).
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md</c> §4.
    /// Migración DATA-ONLY: Designer clonado del ModelSnapshot vigente, ModelSnapshot intacto.
    /// Idempotente (<c>WHERE NOT EXISTS</c>).
    /// </summary>
    public partial class AsignaFechaRetroactivaEcuadorAdministrador : Migration
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
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.roles r
JOIN public.role_companies rc ON rc.role_id = r.id
JOIN public.companies     c  ON c.id = rc.company_id
CROSS JOIN public.permissions p
WHERE r.name = 'Ecuador Administrador'
  AND c.name = 'ItalcolEcuador'
  AND p.key  = 'registros.fecha_retroactiva'
  -- Fail-closed: sin la fila habilitada de la empresa, la asignacion nace huerfana.
  AND EXISTS (
        SELECT 1 FROM public.company_permissions cp
        WHERE cp.company_id = c.id AND cp.permission_id = p.id AND cp.is_enabled)
  AND NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = r.id AND rp.permission_id = p.id);
";

        // Quita SOLO la asignacion que agrego esta migracion. El permiso, su fila de
        // company_permissions y el resto de los roles que lo tengan quedan intactos: los creo
        // 20260820160000 y revertir esto no debe deshacer aquello.
        private const string DOWN_SQL = @"
DELETE FROM public.role_permissions rp
USING public.roles r, public.role_companies rc, public.companies c, public.permissions p
WHERE rp.role_id = r.id
  AND rc.role_id = r.id
  AND c.id = rc.company_id
  AND rp.permission_id = p.id
  AND r.name = 'Ecuador Administrador'
  AND c.name = 'ItalcolEcuador'
  AND p.key  = 'registros.fecha_retroactiva';
";
    }
}
