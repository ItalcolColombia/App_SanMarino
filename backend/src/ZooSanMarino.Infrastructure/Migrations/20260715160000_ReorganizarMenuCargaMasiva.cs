using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed/reorg de datos (sin cambios de schema): reorganiza el menú de cargas. Crea el grupo padre
    /// "Carga Masiva" y cuelga como hijos hermanos "Migración Manual" (ex "Migraciones Masivas") e
    /// "Integración Panamá", corrigiendo además la ruta de Panamá a /migraciones/sincronizacion-panama
    /// (la migración previa la dejó apuntando a una ruta inexistente). El grupo hereda role_menus y
    /// company_menus del ítem 'migraciones_masivas'. Idempotente (WHERE NOT EXISTS + UPDATE determinístico).
    /// </summary>
    public partial class ReorganizarMenuCargaMasiva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Grupo padre "Carga Masiva" (top-level, sin ruta, expandible).
            migrationBuilder.Sql(@"
                INSERT INTO menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group)
                SELECT 'Carga Masiva', 'file-import', NULL, NULL,
                       COALESCE((SELECT m.""order"" FROM menus m WHERE m.key = 'migraciones_masivas'),
                                (SELECT COALESCE(MAX(m.""order""), 0) + 1 FROM menus m WHERE m.parent_id IS NULL)),
                       true, 'carga_masiva', 0, true
                WHERE NOT EXISTS (SELECT 1 FROM menus WHERE key = 'carga_masiva');
            ");

            // 2) role_menus: el grupo hereda los roles del ítem 'migraciones_masivas'.
            migrationBuilder.Sql(@"
                INSERT INTO role_menus (role_id, menu_id)
                SELECT DISTINCT rm.role_id, g.id
                FROM role_menus rm
                JOIN menus src ON src.id = rm.menu_id AND src.key = 'migraciones_masivas'
                CROSS JOIN menus g
                WHERE g.key = 'carga_masiva'
                  AND NOT EXISTS (SELECT 1 FROM role_menus x WHERE x.role_id = rm.role_id AND x.menu_id = g.id);
            ");

            // 3) company_menus: el grupo hereda las empresas del ítem 'migraciones_masivas'.
            migrationBuilder.Sql(@"
                INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
                SELECT cm.company_id, g.id, true, cm.sort_order, NULL
                FROM company_menus cm
                JOIN menus src ON src.id = cm.menu_id AND src.key = 'migraciones_masivas'
                CROSS JOIN menus g
                WHERE g.key = 'carga_masiva'
                  AND NOT EXISTS (SELECT 1 FROM company_menus x WHERE x.company_id = cm.company_id AND x.menu_id = g.id);
            ");

            // 4) "Migración Manual" (ex Migraciones Masivas) → hijo de Carga Masiva.
            migrationBuilder.Sql(@"
                UPDATE menus
                SET label = 'Migración Manual',
                    parent_id = (SELECT id FROM menus WHERE key = 'carga_masiva'),
                    ""order"" = 1,
                    sort_order = 1
                WHERE key = 'migraciones_masivas';
            ");

            // 5) "Integración Panamá" → hijo de Carga Masiva + ruta correcta.
            migrationBuilder.Sql(@"
                UPDATE menus
                SET label = 'Integración Panamá',
                    route = '/migraciones/sincronizacion-panama',
                    parent_id = (SELECT id FROM menus WHERE key = 'carga_masiva'),
                    ""order"" = 2,
                    sort_order = 2
                WHERE key = 'sincronizacion_panama';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- Devolver Panamá como hijo de 'migraciones_masivas' con su ruta anterior.
                UPDATE menus
                SET label = 'Integración Panamá',
                    route = '/migraciones-masivas/panama',
                    parent_id = (SELECT id FROM menus WHERE key = 'migraciones_masivas'),
                    ""order"" = 1, sort_order = 1
                WHERE key = 'sincronizacion_panama';

                -- Devolver 'migraciones_masivas' a top-level con su label original.
                UPDATE menus
                SET label = 'Migraciones Masivas', parent_id = NULL, ""order"" = 901, sort_order = 0
                WHERE key = 'migraciones_masivas';

                -- Quitar el grupo 'carga_masiva' (ya sin hijos).
                DELETE FROM role_menus    WHERE menu_id IN (SELECT id FROM menus WHERE key = 'carga_masiva');
                DELETE FROM company_menus WHERE menu_id IN (SELECT id FROM menus WHERE key = 'carga_masiva');
                DELETE FROM menus         WHERE key = 'carga_masiva';
            ");
        }
    }
}
