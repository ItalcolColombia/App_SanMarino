using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema): menú del módulo Implementación (grupo "Implementación"
    /// + 2 hijos: planes / mis tareas), mismo patrón que 20260714193209_AddVacunacionMenu.
    /// Sincronizada con backend/sql/add_implementacion_menu.sql — si se edita ese archivo, actualizar
    /// aquí también. Idempotente (WHERE NOT EXISTS por key); sin role_menus automático, a asignar por
    /// la UI de Roles. Íconos elegidos entre los ya mapeados en ICON_MAP del front (menu.service.ts).
    /// </summary>
    public partial class AddImplementacionMenu : Migration
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
INSERT INTO menus (label, icon, route, parent_id, ""order"", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT
    'Implementación',
    'clipboard-list',
    NULL,
    NULL,
    (SELECT COALESCE(MAX(m.""order""), 0) + 1 FROM menus m WHERE m.parent_id IS NULL),
    0,
    true,
    true,
    'implementacion',
    NOW(),
    NOW()
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE key = 'implementacion');

INSERT INTO menus (label, icon, route, parent_id, ""order"", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT 'Planes de implementación', 'calendar-day', '/implementacion/planes', p.id, 1, 1, false, true, 'implementacion.planes', NOW(), NOW()
FROM menus p
WHERE p.key = 'implementacion'
  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'implementacion.planes');

INSERT INTO menus (label, icon, route, parent_id, ""order"", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT 'Mis tareas', 'list', '/implementacion/mis-tareas', p.id, 2, 2, false, true, 'implementacion.mis-tareas', NOW(), NOW()
FROM menus p
WHERE p.key = 'implementacion'
  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'implementacion.mis-tareas');
";

        // company_menus/role_menus/menu_permissions referencian menus con ON DELETE CASCADE,
        // así que borrar estas filas limpia todo lo asociado.
        private const string DOWN_SQL = @"
DELETE FROM menus WHERE key IN ('implementacion.planes', 'implementacion.mis-tareas');
DELETE FROM menus WHERE key = 'implementacion';
";
    }
}
