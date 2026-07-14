using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema): permiso para saltear, en el modal "Nueva venta por
    /// granja" de Movimientos Pollo Engorde, el bloqueo por defecto de cargar cantidades en lotes
    /// cerrados o de una corrida anterior en el mismo galpón (regla aplicada 100% en el frontend).
    /// Queda disponible en el módulo de Roles y Permisos para asignarlo a los roles; la asignación
    /// a roles NO se hace aquí. Idempotente (NOT EXISTS) para soportar re-runs.
    ///
    /// Convención del key: "modulo.accion" (igual que SeedPermisosBotonesMovimientosPolloEngorde).
    /// </summary>
    public partial class AddPermisoVentaLotesCerradosMovimientoPolloEngorde : Migration
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
-- Permiso de bypass del bloqueo de lotes cerrados/corrida anterior en Venta por granja (módulo: movimientos_pollo_engorde)
INSERT INTO public.permissions (key, description)
SELECT 'movimientos_pollo_engorde.vender_lotes_cerrados',
       'Movimientos Pollo Engorde: permite cargar cantidades en Venta por granja para lotes cerrados o de una corrida anterior en el mismo galpón (bypass del bloqueo por defecto)'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions WHERE key = 'movimientos_pollo_engorde.vender_lotes_cerrados');
";

        private const string DOWN_SQL = @"
-- Quitar referencias (por si se asignó a roles/menús) y luego el permiso
DELETE FROM public.menu_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'movimientos_pollo_engorde.vender_lotes_cerrados');
DELETE FROM public.role_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'movimientos_pollo_engorde.vender_lotes_cerrados');
DELETE FROM public.permissions WHERE key = 'movimientos_pollo_engorde.vender_lotes_cerrados';
";
    }
}
