using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Permiso <c>registros.fecha_retroactiva</c>: destraba el campo de fecha de los registros
    /// cargados a mano (movimientos de inventario, movimientos de aves, movimientos y ventas de
    /// pollo engorde, traslados de aves y de huevos, gastos de inventario) más allá de la ventana
    /// base —mes en curso ∪ últimos 15 días— que valida <c>VentanaFechaRegistroCalculos</c>. Con el
    /// permiso se admite cualquier fecha pasada; el futuro sigue cerrado para todos.
    ///
    /// <para>
    /// <b>Por qué se siembra en TODAS las empresas</b> (a diferencia de
    /// <c>MenuGerenciaPanelControl</c>, que solo lo hace donde ya existe un módulo relacionado): el
    /// alcance de este permiso son los movimientos y la gestión de inventario, que son módulos base
    /// que toda empresa del repo ya tiene. No hay "empresa que aún no usa esto" contra la cual
    /// piggy-backear, así que <c>company_permissions</c> se siembra <c>CROSS JOIN companies</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué hace falta la fila de <c>company_permissions</c>:</b> es fail-closed por empresa
    /// (<c>CompanyPermissionCalculos</c>, regla R1). Sin esta fila el permiso no es asignable desde
    /// el modal de Roles y no viaja en el JWT aunque un rol lo tenga.
    /// </para>
    ///
    /// <para>
    /// <b>Lo que esta migración NO hace, a propósito:</b> no asigna el permiso a ningún rol salvo
    /// Admin (<c>role_id = 1</c>, mismo patrón que <c>AddSincronizacionPanamaModule</c>), y no toca
    /// ningún menú — este permiso no habilita pantallas, solo destraba un campo dentro de pantallas
    /// que ya existen.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/permiso_fecha_retroactiva_registros_plan.md</c>.
    /// Migración DATA-ONLY: Designer clonado del ModelSnapshot (diff de solo 4 líneas), ModelSnapshot
    /// intacto. Idempotente (<c>WHERE NOT EXISTS</c>), localizando por <c>permissions.key</c>.
    /// </summary>
    public partial class SeedPermisoFechaRetroactivaRegistros : Migration
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
-- 1) El permiso. Nombrado por el COMPORTAMIENTO (fecha retroactiva), no por un módulo ni un tenant.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.permissions (key, description)
SELECT 'registros.fecha_retroactiva',
       'Permite fechar hacia atrás sin límite los registros cargados a mano (movimientos de inventario, de aves, de pollo engorde, traslados y gastos) más allá del mes en curso y los últimos 15 días. El futuro sigue cerrado para todos.'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions WHERE key = 'registros.fecha_retroactiva');

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Asignable en TODAS las empresas: el alcance (movimientos, gestión de inventario, gastos) son
--    módulos base que toda empresa ya tiene, a diferencia de un submódulo opcional (Panamá,
--    indicadores de gerencia) que solo se habilita donde ya existe algo relacionado.
--    company_permissions es fail-closed: sin esta fila el permiso no viaja en el JWT.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT c.id, p.id, true
FROM public.companies c
CROSS JOIN public.permissions p
WHERE p.key = 'registros.fecha_retroactiva'
  AND NOT EXISTS (
        SELECT 1 FROM public.company_permissions x
        WHERE x.company_id = c.id AND x.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) Asignado al rol Admin (role_id = 1), mismo patrón que AddSincronizacionPanamaModule. Cualquier
--    otro rol lo recibe desde la pantalla de Roles y Permisos, no desde una migración.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT 1, p.id
FROM public.permissions p
WHERE p.key = 'registros.fecha_retroactiva'
  AND NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = 1 AND rp.permission_id = p.id);
";

        private const string DOWN_SQL = @"
DELETE FROM public.role_permissions    WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'registros.fecha_retroactiva');
DELETE FROM public.company_permissions WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'registros.fecha_retroactiva');
DELETE FROM public.permissions         WHERE key = 'registros.fecha_retroactiva';
";
    }
}
