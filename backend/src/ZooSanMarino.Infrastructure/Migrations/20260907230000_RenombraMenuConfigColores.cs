using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Renombra el ítem de menú del explorador de BD a la identidad-señuelo
    /// <b>«Configuración de colores»</b>: <c>label</c>, <c>route</c> (<c>/config/db-studio</c> →
    /// <c>/config/config-colores</c>), <c>key</c> e <c>icon</c>. Que el módulo no sea fácil de
    /// identificar en la app. Ver <c>fase_de_desarrollo/renombrar_db_studio_a_config_colores_plan.md</c>.
    /// </summary>
    /// <remarks>
    /// <b>Data-only.</b> No hay cambio de esquema; el <c>.Designer.cs</c> es clon del de la migración
    /// anterior y <c>ZooSanMarinoContextModelSnapshot</c> no se toca.
    ///
    /// <b>Localiza por <c>route</c>, jamás por id.</b> Los ids de <c>menus</c> difieren entre local y
    /// producción (acá es 17, en prod puede ser cualquiera). <c>role_menus</c> / <c>company_menus</c>
    /// referencian <c>menu_id</c>, así que la fila —y todos sus vínculos— quedan intactos.
    ///
    /// <b>Idempotente y sin ensuciar el histórico.</b> El <c>UPDATE</c> del <c>Up</c> matchea la ruta
    /// vieja; en una segunda corrida ya no hay filas con <c>/config/db-studio</c> y no hace nada. El
    /// <c>Down</c> es la reversa exacta por la ruta nueva.
    /// </remarks>
    public partial class RenombraMenuConfigColores : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE public.menus
                   SET label      = 'Configuración de colores',
                       route      = '/config/config-colores',
                       key        = 'config-colores',
                       icon       = 'palette',
                       updated_at = NOW()
                 WHERE route = '/config/db-studio';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE public.menus
                   SET label      = 'db_studio',
                       route      = '/config/db-studio',
                       key        = 'db-studio',
                       icon       = 'warehouse',
                       updated_at = NOW()
                 WHERE route = '/config/config-colores';
            ");
        }
    }
}
