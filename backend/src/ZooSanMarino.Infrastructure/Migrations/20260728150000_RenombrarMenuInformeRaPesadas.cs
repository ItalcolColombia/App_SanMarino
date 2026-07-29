using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Renombra la etiqueta del menu de "Reporte Tecnico Semanal" a
    /// "Informe RA Pesadas": el modulo ya no es un solo reporte sino dos modos
    /// (Resumen semanal de todos los lotes + Detalle de un lote, con sus hojas
    /// de Alimento por fase y Clasificacion de huevo).
    ///
    /// La RUTA no cambia a proposito: es la que ya esta sembrada en menus y
    /// role_menus, asi que no hay que re-asignar permisos.
    ///
    /// Se localiza por `route`, NUNCA por id: los ids difieren entre local y
    /// produccion. Data-only (Designer clonado, ModelSnapshot intacto) e
    /// idempotente (el WHERE deja de aplicar una vez renombrado).
    /// </summary>
    public partial class RenombrarMenuInformeRaPesadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE menus
   SET label = 'Informe RA Pesadas',
       updated_at = now()
 WHERE route = '/reporte-tecnico-semanal'
   AND label IS DISTINCT FROM 'Informe RA Pesadas';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE menus
   SET label = 'Reporte Tecnico Semanal',
       updated_at = now()
 WHERE route = '/reporte-tecnico-semanal'
   AND label IS DISTINCT FROM 'Reporte Tecnico Semanal';");
        }
    }
}
