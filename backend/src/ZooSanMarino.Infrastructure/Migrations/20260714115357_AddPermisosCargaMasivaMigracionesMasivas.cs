using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema): permisos de acceso por línea al módulo Migraciones
    /// Masivas. Sin el permiso correspondiente, el frontend deshabilita (gris, "Sin permisos") los
    /// tiles de esa línea — igual criterio que <see cref="AddPermisoVentaLotesCerradosMovimientoPolloEngorde"/>:
    /// enforcement 100% en el frontend, el backend no valida esto en el controller. Quedan
    /// disponibles en el módulo de Roles y Permisos para asignarlos a los roles; la asignación a
    /// roles NO se hace aquí. Idempotente (NOT EXISTS) para soportar re-runs.
    /// </summary>
    public partial class AddPermisosCargaMasivaMigracionesMasivas : Migration
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
-- Permisos de acceso por línea al módulo Migraciones Masivas (carga masiva por Excel)
INSERT INTO public.permissions (key, description)
SELECT v.key, v.descr
FROM (VALUES
    ('carga_masiva_pollo_engorde', 'Migraciones Masivas: acceso a la carga masiva de Pollo Engorde (Lotes, Seguimiento y Venta)'),
    ('carga_masiva_postura',       'Migraciones Masivas: acceso a la carga masiva de Postura (Granjas, Núcleos, Galpones, Seguimientos, Ventas y Movimientos)')
) AS v(key, descr)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);
";

        private const string DOWN_SQL = @"
-- Quitar referencias (por si se asignaron a roles/menús) y luego los permisos
DELETE FROM public.menu_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN (
    'carga_masiva_pollo_engorde',
    'carga_masiva_postura'));
DELETE FROM public.role_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN (
    'carga_masiva_pollo_engorde',
    'carga_masiva_postura'));
DELETE FROM public.permissions WHERE key IN (
    'carga_masiva_pollo_engorde',
    'carga_masiva_postura');
";
    }
}
