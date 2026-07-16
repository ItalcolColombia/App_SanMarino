using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema): menú del módulo Vacunación (grupo "Vacunación" +
    /// 3 hijos: cronograma/registro/reportes). Convertido a migración a pedido explícito del usuario
    /// para que se aplique solo en cada deploy — a diferencia de otros menús del repo (ej.
    /// add_movimiento_pollo_engorde_menu.sql), que siguen siendo manuales por convención previa.
    /// Sincronizada con backend/sql/add_vacunacion_menu.sql — si se edita ese archivo, actualizar aquí
    /// también. Idempotente (WHERE NOT EXISTS por key); sin role_menus automático, a asignar por UI de Roles.
    /// </summary>
    public partial class AddVacunacionMenu : Migration
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
    'Vacunación',
    'syringe',
    NULL,
    NULL,
    (SELECT COALESCE(MAX(m.""order""), 0) + 1 FROM menus m WHERE m.parent_id IS NULL),
    0,
    true,
    true,
    'vacunacion',
    NOW(),
    NOW()
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE key = 'vacunacion');

INSERT INTO menus (label, icon, route, parent_id, ""order"", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT 'Cronograma', 'calendar-check', '/vacunacion/cronograma', p.id, 1, 1, false, true, 'vacunacion.cronograma', NOW(), NOW()
FROM menus p
WHERE p.key = 'vacunacion'
  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'vacunacion.cronograma');

INSERT INTO menus (label, icon, route, parent_id, ""order"", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT 'Registro de Aplicación', 'clipboard-check', '/vacunacion/registro', p.id, 2, 2, false, true, 'vacunacion.registro', NOW(), NOW()
FROM menus p
WHERE p.key = 'vacunacion'
  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'vacunacion.registro');

INSERT INTO menus (label, icon, route, parent_id, ""order"", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT 'Reportes de Cumplimiento', 'chart-line', '/vacunacion/reportes', p.id, 3, 3, false, true, 'vacunacion.reportes', NOW(), NOW()
FROM menus p
WHERE p.key = 'vacunacion'
  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'vacunacion.reportes');
";

        // company_menus/role_menus/menu_permissions referencian menus con ON DELETE CASCADE
        // (ver esquema real de `menus`), así que borrar estas 4 filas limpia todo lo asociado.
        private const string DOWN_SQL = @"
DELETE FROM menus WHERE key IN ('vacunacion.cronograma', 'vacunacion.registro', 'vacunacion.reportes');
DELETE FROM menus WHERE key = 'vacunacion';
";
    }
}
