using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Menú «Panel de control» (<c>/tickets/panel</c>): indicadores del módulo y descarga del
    /// reporte detallado a Excel.
    /// </summary>
    /// <remarks>
    /// Igual que <c>SeedMenusTableroRoadmapEnRolesYEmpresas</c>, no alcanza con crear la fila en
    /// <c>menus</c>: hay que darle <c>menu_permissions</c> (el gate) y copiarlo a los
    /// <c>role_menus</c> / <c>company_menus</c> de quien ya ve las vistas de gestión, o no lo ve
    /// nadie en el sidebar.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto, idempotente por
    /// <c>WHERE NOT EXISTS</c>.
    /// </remarks>
    public partial class SeedMenuPanelIndicadoresTickets : Migration
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
-- 1) El menú, colgado del grupo Tickets.
INSERT INTO public.menus (key, label, icon, route, ""order"", sort_order, is_group, is_active, parent_id, created_at, updated_at)
SELECT 'tickets.panel', 'Panel de control', 'chart-bar', '/tickets/panel', 6, 6, false, true, p.id, now(), now()
FROM public.menus p
WHERE p.key = 'tickets'
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key = 'tickets.panel');

-- 2) Gate por permiso: es una vista de gestión.
INSERT INTO public.menu_permissions (menu_id, permission_id)
SELECT m.id, p.id
FROM public.menus m
JOIN public.permissions p ON p.key IN ('tickets.gestionar','tickets.admin')
WHERE m.key = 'tickets.panel'
  AND NOT EXISTS (
        SELECT 1 FROM public.menu_permissions mp
        WHERE mp.menu_id = m.id AND mp.permission_id = p.id);

-- 3) Visible para los roles que ya ven las otras vistas de gestión del módulo.
INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rm.role_id, nuevo.id
FROM public.role_menus rm
JOIN public.menus origen ON origen.id = rm.menu_id AND origen.key IN ('tickets.admin', 'tickets.gestion')
CROSS JOIN public.menus nuevo
WHERE nuevo.key = 'tickets.panel'
  AND NOT EXISTS (
        SELECT 1 FROM public.role_menus x
        WHERE x.role_id = rm.role_id AND x.menu_id = nuevo.id);

-- 4) Habilitado en las empresas que ya tienen el módulo (hereda is_enabled del menú de origen).
INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT DISTINCT ON (cm.company_id, nuevo.id)
       cm.company_id, nuevo.id, cm.is_enabled, nuevo.sort_order, nuevo.parent_id
FROM public.company_menus cm
JOIN public.menus origen ON origen.id = cm.menu_id AND origen.key IN ('tickets.admin', 'tickets.gestion')
CROSS JOIN public.menus nuevo
WHERE nuevo.key = 'tickets.panel'
  AND NOT EXISTS (
        SELECT 1 FROM public.company_menus y
        WHERE y.company_id = cm.company_id AND y.menu_id = nuevo.id)
ORDER BY cm.company_id, nuevo.id, cm.is_enabled DESC;
";

        private const string DOWN_SQL = @"
DELETE FROM public.role_menus    WHERE menu_id IN (SELECT id FROM public.menus WHERE key = 'tickets.panel');
DELETE FROM public.company_menus WHERE menu_id IN (SELECT id FROM public.menus WHERE key = 'tickets.panel');
DELETE FROM public.menu_permissions WHERE menu_id IN (SELECT id FROM public.menus WHERE key = 'tickets.panel');
DELETE FROM public.menus WHERE key = 'tickets.panel';
";
    }
}
