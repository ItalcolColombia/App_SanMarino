using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Flag por empresa: <c>permite_multiples_seguimientos_diarios</c>. Con el flag ON, el
    /// seguimiento diario de producción y de levante aceptan más de un registro por lote+día
    /// calendario UTC (se agrupan para reportes/indicadores: aditivos se suman, peso promedio
    /// se pondera por aves vivas, uniformidad/CV%/observaciones toman el último registro del
    /// día). Nace de Santa Reyes.
    /// Plan: <c>fase_de_desarrollo/seguimiento_produccion_multiples_registros_dia_plan.md</c>.
    /// </summary>
    public partial class AddFlagPermiteMultiplesSeguimientosDiarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE companies ADD COLUMN IF NOT EXISTS permite_multiples_seguimientos_diarios boolean NOT NULL DEFAULT false;");

            migrationBuilder.Sql(@"
UPDATE public.companies
   SET permite_multiples_seguimientos_diarios = true
 WHERE name = 'Santa Reyes'
   AND permite_multiples_seguimientos_diarios IS DISTINCT FROM true;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE companies DROP COLUMN IF EXISTS permite_multiples_seguimientos_diarios;");
        }
    }
}
