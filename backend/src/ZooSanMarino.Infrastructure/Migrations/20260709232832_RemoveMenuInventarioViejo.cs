using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMenuInventarioViejo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quita el inventario VIEJO (modelo A): menús id 10 y 32, ambos ruta '/inventario'.
            // El canónico multipaís es el NUEVO (menu 50 -> '/gestion-inventario'). DML de datos de
            // configuración (menús), NO cambia schema. Idempotente y acotado a id 10/32.
            migrationBuilder.Sql(@"
                -- 0) SEGURIDAD: a toda empresa que tenga el viejo (10/32) y NO el nuevo (50), darle el nuevo
                --    para no dejarla sin menú de inventario (caso ""Demo"" / company 4). Idempotente.
                INSERT INTO public.company_menus (company_id, menu_id)
                SELECT DISTINCT cm.company_id, 50
                FROM public.company_menus cm
                WHERE cm.menu_id IN (10, 32)
                  AND NOT EXISTS (
                        SELECT 1 FROM public.company_menus x
                        WHERE x.company_id = cm.company_id AND x.menu_id = 50)
                  AND EXISTS (SELECT 1 FROM public.menus m WHERE m.id = 50);

                -- 1) Desvincular el viejo de TODAS las empresas y roles (no solo company 1)
                DELETE FROM public.role_menus       WHERE menu_id IN (10, 32);
                DELETE FROM public.company_menus    WHERE menu_id IN (10, 32);
                DELETE FROM public.menu_permissions WHERE menu_id IN (10, 32);

                -- 2) Borrar los menús viejos (solo si su ruta sigue siendo la vieja — guarda de seguridad)
                DELETE FROM public.menus WHERE id IN (10, 32) AND route ILIKE '%/inventario';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op intencional. No se re-siembran los menús viejos (id 10/32 -> '/inventario'):
            // el módulo front 'features/inventario' se elimina en esta misma fase, así que restaurar
            // un menú apuntando a un módulo inexistente no tiene sentido. Si hiciera falta revertir,
            // el rollback correcto es re-agregar el módulo front, no el menú.
        }
    }
}
