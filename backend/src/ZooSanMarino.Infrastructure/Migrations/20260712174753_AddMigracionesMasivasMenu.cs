using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigracionesMasivasMenu : Migration
    {
        // Registra el ítem de menú "Migraciones Masivas" (ruta /migraciones-masivas) y hereda
        // su visibilidad (role_menus + company_menus) del módulo "Lotes", presente donde hay Postura.
        // Idempotente (WHERE NOT EXISTS) → se aplica sola en cada deploy sin duplicar.
        // Si tu instalación usa otra ruta/label para Lotes, ajustá el matcher de la CTE `ref`.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Ítem de menú (top-level). Columnas según el esquema actual de `menus`
            //    (key/sort_order/is_group NOT NULL); key es el identificador único del menú.
            migrationBuilder.Sql(@"
                INSERT INTO menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group)
                SELECT 'Migraciones Masivas', 'file-import', '/migraciones-masivas', NULL,
                       (SELECT COALESCE(MAX(m.""order""), 0) + 1 FROM menus m WHERE m.parent_id IS NULL),
                       true, 'migraciones_masivas', 0, false
                WHERE NOT EXISTS (
                    SELECT 1 FROM menus WHERE key = 'migraciones_masivas' OR route = '/migraciones-masivas'
                );
            ");

            // 2) role_menus: heredar los roles del módulo Lotes
            migrationBuilder.Sql(@"
                WITH ref AS (
                    SELECT id FROM menus
                    WHERE (route = '/lote' OR route LIKE '/lote%' OR label ILIKE '%lote%')
                      AND parent_id IS NULL
                    ORDER BY id LIMIT 1
                )
                INSERT INTO role_menus (role_id, menu_id)
                SELECT DISTINCT rm_ref.role_id, nuevo.id
                FROM role_menus rm_ref
                JOIN ref ON ref.id = rm_ref.menu_id
                CROSS JOIN menus nuevo
                WHERE nuevo.route = '/migraciones-masivas'
                  AND NOT EXISTS (
                      SELECT 1 FROM role_menus rm
                      WHERE rm.role_id = rm_ref.role_id AND rm.menu_id = nuevo.id
                  );
            ");

            // 3) company_menus: heredar las empresas del módulo Lotes
            migrationBuilder.Sql(@"
                WITH ref AS (
                    SELECT id FROM menus
                    WHERE (route = '/lote' OR route LIKE '/lote%' OR label ILIKE '%lote%')
                      AND parent_id IS NULL
                    ORDER BY id LIMIT 1
                )
                INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
                SELECT cm_ref.company_id, nuevo.id, true, cm_ref.sort_order + 1, NULL
                FROM company_menus cm_ref
                JOIN ref ON ref.id = cm_ref.menu_id
                JOIN menus nuevo ON nuevo.route = '/migraciones-masivas'
                WHERE NOT EXISTS (
                    SELECT 1 FROM company_menus cm
                    WHERE cm.company_id = cm_ref.company_id AND cm.menu_id = nuevo.id
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM role_menus    WHERE menu_id IN (SELECT id FROM menus WHERE route = '/migraciones-masivas');
                DELETE FROM company_menus WHERE menu_id IN (SELECT id FROM menus WHERE route = '/migraciones-masivas');
                DELETE FROM menus         WHERE route = '/migraciones-masivas';
            ");
        }
    }
}
