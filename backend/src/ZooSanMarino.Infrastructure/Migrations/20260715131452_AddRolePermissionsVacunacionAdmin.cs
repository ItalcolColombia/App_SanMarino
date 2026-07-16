using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema): completa la asignación de los 4 permisos de
    /// Vacunación al rol Admin (role_id=1) — ya tenía 2/4 (cronograma.ver, registro.aplicar)
    /// asignados manualmente vía Roles/UI; faltaban cronograma.administrar (oculta el botón
    /// "+ Agregar vacuna" en el tab Cronograma) y reportes.ver (403 al consultar Reportes de
    /// cumplimiento). A pedido explícito del usuario, vía migración en vez de UI manual.
    /// Idempotente (NOT EXISTS).
    /// </summary>
    public partial class AddRolePermissionsVacunacionAdmin : Migration
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
SELECT 1, p.id
FROM public.permissions p
WHERE p.key LIKE 'vacunacion.%'
  AND NOT EXISTS (
      SELECT 1 FROM public.role_permissions rp
      WHERE rp.role_id = 1 AND rp.permission_id = p.id
  );
";

        private const string DOWN_SQL = @"
DELETE FROM public.role_permissions
WHERE role_id = 1
  AND permission_id IN (SELECT id FROM public.permissions WHERE key LIKE 'vacunacion.%');
";
    }
}
