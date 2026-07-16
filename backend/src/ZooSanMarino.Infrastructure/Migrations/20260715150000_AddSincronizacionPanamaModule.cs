using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema): submódulo "Integración Panamá" bajo "Migraciones Masivas"
    /// + permisos (ver / ejecutar) asignados al rol Admin (role_id=1). Puente ZooPanamaPollo → engorde.
    /// Idempotente (WHERE NOT EXISTS por key). Hereda visibilidad (role_menus/company_menus) del menú
    /// padre 'migraciones_masivas'. Se aplica sola en cada deploy (Database:RunMigrations=true).
    /// </summary>
    public partial class AddSincronizacionPanamaModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Ítem de menú hijo bajo 'migraciones_masivas'.
            migrationBuilder.Sql(@"
                INSERT INTO menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group)
                SELECT 'Integración Panamá', 'cloud-download-alt', '/migraciones-masivas/panama',
                       (SELECT id FROM menus WHERE key = 'migraciones_masivas'),
                       (SELECT COALESCE(MAX(m.""order""), 0) + 1 FROM menus m WHERE m.parent_id = (SELECT id FROM menus WHERE key = 'migraciones_masivas')),
                       true, 'sincronizacion_panama', 1, false
                WHERE EXISTS (SELECT 1 FROM menus WHERE key = 'migraciones_masivas')
                  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'sincronizacion_panama');
            ");

            // 2) role_menus: heredar los roles del menú padre 'migraciones_masivas'.
            migrationBuilder.Sql(@"
                WITH ref AS (SELECT id FROM menus WHERE key = 'migraciones_masivas')
                INSERT INTO role_menus (role_id, menu_id)
                SELECT DISTINCT rm.role_id, nuevo.id
                FROM role_menus rm
                JOIN ref ON ref.id = rm.menu_id
                CROSS JOIN menus nuevo
                WHERE nuevo.key = 'sincronizacion_panama'
                  AND NOT EXISTS (SELECT 1 FROM role_menus x WHERE x.role_id = rm.role_id AND x.menu_id = nuevo.id);
            ");

            // 3) company_menus: heredar las empresas del menú padre 'migraciones_masivas'.
            migrationBuilder.Sql(@"
                WITH ref AS (SELECT id FROM menus WHERE key = 'migraciones_masivas')
                INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
                SELECT cm.company_id, nuevo.id, true, cm.sort_order + 1, NULL
                FROM company_menus cm
                JOIN ref ON ref.id = cm.menu_id
                JOIN menus nuevo ON nuevo.key = 'sincronizacion_panama'
                WHERE NOT EXISTS (SELECT 1 FROM company_menus x WHERE x.company_id = cm.company_id AND x.menu_id = nuevo.id);
            ");

            // 4) Permisos del submódulo (convención modulo.accion).
            migrationBuilder.Sql(@"
                INSERT INTO permissions (key, description)
                SELECT v.key, v.description
                FROM (VALUES
                    ('sincronizacion_panama.ver', 'Integración Panamá: ver y previsualizar la sincronización desde ZooPanamaPollo'),
                    ('sincronizacion_panama.ejecutar', 'Integración Panamá: ejecutar la sincronización (guía genética, lotes, seguimiento y reproductora) desde ZooPanamaPollo')
                ) AS v(key, description)
                WHERE NOT EXISTS (SELECT 1 FROM permissions p WHERE p.key = v.key);
            ");

            // 5) Asignar los permisos al rol Admin (role_id = 1).
            migrationBuilder.Sql(@"
                INSERT INTO role_permissions (role_id, permission_id)
                SELECT 1, p.id
                FROM permissions p
                WHERE p.key LIKE 'sincronizacion_panama.%'
                  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.role_id = 1 AND rp.permission_id = p.id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM role_permissions WHERE permission_id IN (SELECT id FROM permissions WHERE key LIKE 'sincronizacion_panama.%');
                DELETE FROM permissions      WHERE key LIKE 'sincronizacion_panama.%';
                DELETE FROM role_menus        WHERE menu_id IN (SELECT id FROM menus WHERE key = 'sincronizacion_panama');
                DELETE FROM company_menus     WHERE menu_id IN (SELECT id FROM menus WHERE key = 'sincronizacion_panama');
                DELETE FROM menus             WHERE key = 'sincronizacion_panama';
            ");
        }
    }
}
