using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignColombiaMenuInventarioModeloB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Alinea el menú de Colombia (company 1) al inventario modelo B unificado (/gestion-inventario).
            // Data-only + IDEMPOTENTE (INSERT ... WHERE NOT EXISTS / UPDATE condicional). Sin DDL.
            // Reemplaza el script manual backend/sql/fase3_menu_colombia_modelo_b.sql (ahora deploya solo por EF).
            // El menú EFECTIVO requiere company_menus (por empresa) Y role_menus (por rol).
            migrationBuilder.Sql(@"
                -- company_menus: menús 50 (/gestion-inventario) y 49 (catálogo ítems) para Colombia (1)
                INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
                SELECT 1, 50, TRUE, 23, NULL
                WHERE NOT EXISTS (SELECT 1 FROM company_menus WHERE company_id = 1 AND menu_id = 50);

                INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
                SELECT 1, 49, TRUE, 24, NULL
                WHERE NOT EXISTS (SELECT 1 FROM company_menus WHERE company_id = 1 AND menu_id = 49);

                -- role_menus: roles Colombia (1 Admin, 5 Director tecnico, 12 Colombia Administrativa)
                -- deben VER los menús 49/50 (global por rol, gateado por company_menus).
                INSERT INTO role_menus (role_id, menu_id)
                SELECT r.role_id, m.menu_id
                FROM (VALUES (1),(5),(12)) AS r(role_id)
                CROSS JOIN (VALUES (49),(50)) AS m(menu_id)
                WHERE NOT EXISTS (SELECT 1 FROM role_menus rm WHERE rm.role_id = r.role_id AND rm.menu_id = m.menu_id);

                -- Etiqueta genérica del menú 50 (módulo multipaís EC/PA/CO; antes decía '(EC/PA)').
                UPDATE menus SET label = 'Gestión de Inventario' WHERE id = 50 AND label LIKE '%(EC/PA)%';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE menus SET label = 'Gestión de Inventario (EC/PA)' WHERE id = 50 AND label = 'Gestión de Inventario';
                DELETE FROM role_menus  WHERE menu_id IN (49,50) AND role_id IN (1,5,12);
                DELETE FROM company_menus WHERE company_id = 1 AND menu_id IN (49,50);
            ");
        }
    }
}
