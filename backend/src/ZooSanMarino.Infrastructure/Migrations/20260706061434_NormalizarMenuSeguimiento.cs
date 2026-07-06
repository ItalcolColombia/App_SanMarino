using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Normaliza el menú: nombres cortos para el grupo de seguimiento diario (el ítem padre ya da
    /// el contexto) y retira del menú el módulo "Reporte Técnico Producción" (centralizado, sin uso).
    ///
    /// Data-only + IDEMPOTENTE (UPDATE por label / DELETE por route). No borra el código del módulo
    /// ni las filas de menú viejas (solo se quitan de company_menus/role_menus).
    /// Fuente/spec probada en local: backend/sql/migracion_menu_normalizar_seguimiento.sql.
    /// </summary>
    public partial class NormalizarMenuSeguimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Renombrar el grupo padre y sus hijos a nombres cortos (idempotente por label).
            migrationBuilder.Sql(@"
UPDATE public.menus SET label = 'Seguimiento Diario' WHERE label = 'Registros Diarios';
UPDATE public.menus SET label = 'Levante'            WHERE label = 'Seguimiento Diario de Levante';
UPDATE public.menus SET label = 'Producción'         WHERE label = 'Seguimiento Diario de Producción';
UPDATE public.menus SET label = 'Pollo Engorde'      WHERE label = 'Seguimiento Diario Pollo Engorde';
UPDATE public.menus SET label = 'Lote Reproductora'  WHERE label = 'Seguimiento Diario Lote Reproductora';");

            // Quitar 'Reporte Técnico Producción' del menú (módulo centralizado). No borra el módulo web ni la fila de menú.
            migrationBuilder.Sql(@"
DELETE FROM public.company_menus WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/reporte-tecnico-produccion');
DELETE FROM public.role_menus    WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/reporte-tecnico-produccion');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir solo los nombres (los company_menus/role_menus del reporte no se restauran
            // automáticamente; se re-habilitan por configuración si hiciera falta).
            migrationBuilder.Sql(@"
UPDATE public.menus SET label = 'Registros Diarios'                    WHERE label = 'Seguimiento Diario';
UPDATE public.menus SET label = 'Seguimiento Diario de Levante'        WHERE label = 'Levante';
UPDATE public.menus SET label = 'Seguimiento Diario de Producción'     WHERE label = 'Producción';
UPDATE public.menus SET label = 'Seguimiento Diario Pollo Engorde'     WHERE label = 'Pollo Engorde';
UPDATE public.menus SET label = 'Seguimiento Diario Lote Reproductora' WHERE label = 'Lote Reproductora';");
        }
    }
}
