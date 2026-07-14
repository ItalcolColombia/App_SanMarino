using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema): permisos del módulo Vacunación. No se crea un rol
    /// nuevo ("Administrador de Vacunación") — el usuario pidió que la asignación a roles existentes
    /// se maneje desde el módulo de Roles/UI, no hardcodeada aquí. Convención del key: "modulo.accion"
    /// (igual que AddPermisoVentaLotesCerradosMovimientoPolloEngorde). Idempotente (NOT EXISTS).
    /// </summary>
    public partial class AddPermisosVacunacion : Migration
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
INSERT INTO public.permissions (key, description)
SELECT v.key, v.description
FROM (VALUES
    ('vacunacion.cronograma.ver', 'Vacunación: ver el cronograma de vacunación del lote'),
    ('vacunacion.cronograma.administrar', 'Vacunación: crear/editar el cronograma de vacunación del lote (perfil administrador)'),
    ('vacunacion.registro.aplicar', 'Vacunación: registrar aplicación (aplicado/no aplicado) de un ítem del cronograma (perfil operador)'),
    ('vacunacion.reportes.ver', 'Vacunación: ver reportes y gráficas de cumplimiento de vacunación')
) AS v(key, description)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);
";

        private const string DOWN_SQL = @"
DELETE FROM public.menu_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key LIKE 'vacunacion.%');
DELETE FROM public.role_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key LIKE 'vacunacion.%');
DELETE FROM public.permissions WHERE key LIKE 'vacunacion.%';
";
    }
}
