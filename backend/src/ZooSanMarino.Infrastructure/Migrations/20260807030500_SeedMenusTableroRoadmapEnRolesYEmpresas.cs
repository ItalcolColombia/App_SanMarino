using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Hace VISIBLES en el sidebar los dos menús que creó <c>AddTicketsJiraCasosTareas</c>
    /// (<c>tickets.tablero</c> y <c>tickets.roadmap</c>).
    /// </summary>
    /// <remarks>
    /// Crear la fila en <c>menus</c> + <c>menu_permissions</c> NO alcanza: el árbol efectivo se
    /// arma desde <c>role_menus</c> (ver <c>RoleCompositeService.Menus_GetForUserAsync</c>) y solo
    /// cae al filtro por permisos cuando el rol no tiene NINGÚN menú asignado. Sin estas filas los
    /// menús existen pero no los ve nadie, y encima no aparecen como asignables en la UI de roles
    /// hasta que la empresa los tenga habilitados en <c>company_menus</c>.
    ///
    /// Regla de alcance: se copia exactamente a quien YA tiene el menú de administración o el de
    /// gestión de tickets. No habilita nada a nadie nuevo — el gate real sigue siendo
    /// <c>menu_permissions</c> (tickets.gestionar / tickets.admin).
    ///
    /// Migración DATA-ONLY: Designer clonado y ModelSnapshot intacto (no hay cambios de modelo).
    /// Idempotente por <c>WHERE NOT EXISTS</c>.
    /// </remarks>
    public partial class SeedMenusTableroRoadmapEnRolesYEmpresas : Migration
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
-- 1) role_menus: los roles que ya ven ""Administración"" o ""Bandeja de gestión"" de tickets
--    pasan a ver también el tablero y el roadmap.
INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rm.role_id, nuevo.id
FROM public.role_menus rm
JOIN public.menus origen ON origen.id = rm.menu_id AND origen.key IN ('tickets.admin', 'tickets.gestion')
CROSS JOIN public.menus nuevo
WHERE nuevo.key IN ('tickets.tablero', 'tickets.roadmap')
  AND NOT EXISTS (
        SELECT 1 FROM public.role_menus x
        WHERE x.role_id = rm.role_id AND x.menu_id = nuevo.id);

-- 2) company_menus: las empresas que ya tienen habilitado el módulo de tickets lo tienen
--    también para las dos vistas nuevas (así aparecen como asignables en la UI de roles).
--    Se hereda is_enabled del menú de origen para no encender el módulo donde estaba apagado.
INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT DISTINCT ON (cm.company_id, nuevo.id)
       cm.company_id, nuevo.id, cm.is_enabled, nuevo.sort_order, nuevo.parent_id
FROM public.company_menus cm
JOIN public.menus origen ON origen.id = cm.menu_id AND origen.key IN ('tickets.admin', 'tickets.gestion')
CROSS JOIN public.menus nuevo
WHERE nuevo.key IN ('tickets.tablero', 'tickets.roadmap')
  AND NOT EXISTS (
        SELECT 1 FROM public.company_menus y
        WHERE y.company_id = cm.company_id AND y.menu_id = nuevo.id)
ORDER BY cm.company_id, nuevo.id, cm.is_enabled DESC;
";

        private const string DOWN_SQL = @"
DELETE FROM public.role_menus
WHERE menu_id IN (SELECT id FROM public.menus WHERE key IN ('tickets.tablero', 'tickets.roadmap'));

DELETE FROM public.company_menus
WHERE menu_id IN (SELECT id FROM public.menus WHERE key IN ('tickets.tablero', 'tickets.roadmap'));
";
    }
}
