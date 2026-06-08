using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema): 4 permisos para los botones de acción del listado
    /// de Movimientos de Pollo Engorde (barra superior). Quedan disponibles en el módulo de Roles
    /// y Permisos para asignarlos a los roles. La asignación a roles NO se hace aquí (se realiza
    /// desde la pantalla de administración). Idempotente (NOT EXISTS) para soportar re-runs.
    ///
    /// Convención del key: "modulo.accion" (igual que tickets.crear / user.create).
    /// </summary>
    public partial class SeedPermisosBotonesMovimientosPolloEngorde : Migration
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
-- Permisos de los 4 botones de la barra superior (módulo: movimientos_pollo_engorde) ----------
INSERT INTO public.permissions (key, description)
SELECT v.key, v.descr
FROM (VALUES
    ('movimientos_pollo_engorde.descargar_excel', 'Movimientos Pollo Engorde: descargar Excel del listado de ventas'),
    ('movimientos_pollo_engorde.validar_ventas',  'Movimientos Pollo Engorde: validar coherencia de ventas vs disponibilidad (sin cambios)'),
    ('movimientos_pollo_engorde.corregir_ventas', 'Movimientos Pollo Engorde: validar y corregir sobreventas en estado Pendiente'),
    ('movimientos_pollo_engorde.organizar_peso',  'Movimientos Pollo Engorde: organizar/recalcular peso prorrateado por lote')
) AS v(key, descr)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);
";

        private const string DOWN_SQL = @"
-- Quitar referencias (por si se asignaron a roles/menús) y luego los permisos
DELETE FROM public.menu_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN (
    'movimientos_pollo_engorde.descargar_excel',
    'movimientos_pollo_engorde.validar_ventas',
    'movimientos_pollo_engorde.corregir_ventas',
    'movimientos_pollo_engorde.organizar_peso'));
DELETE FROM public.role_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN (
    'movimientos_pollo_engorde.descargar_excel',
    'movimientos_pollo_engorde.validar_ventas',
    'movimientos_pollo_engorde.corregir_ventas',
    'movimientos_pollo_engorde.organizar_peso'));
DELETE FROM public.permissions WHERE key IN (
    'movimientos_pollo_engorde.descargar_excel',
    'movimientos_pollo_engorde.validar_ventas',
    'movimientos_pollo_engorde.corregir_ventas',
    'movimientos_pollo_engorde.organizar_peso');
";
    }
}
