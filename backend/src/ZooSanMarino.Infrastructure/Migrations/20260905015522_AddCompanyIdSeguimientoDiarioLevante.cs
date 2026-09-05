using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <c>company_id</c> denormalizado en <c>seguimiento_diario_levante</c>, espejo de
    /// <c>seguimiento_diario_produccion.company_id</c>. Nullable, SIN backfill histórico: las
    /// filas existentes quedan en NULL y el índice único por día las sigue tratando como
    /// protegidas (solo una fila NULL no excluye del índice de unicidad). Se popula desde
    /// <c>SeguimientoDiarioService.CreateAsync</c> al resolver el lote, y hace falta para poder
    /// excluir empresas con <c>permite_multiples_seguimientos_diarios</c> del índice único parcial
    /// (Postgres no admite subconsultas en el predicado de un índice parcial).
    /// Plan: <c>fase_de_desarrollo/seguimiento_produccion_multiples_registros_dia_plan.md</c>.
    /// </summary>
    public partial class AddCompanyIdSeguimientoDiarioLevante : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE seguimiento_diario_levante ADD COLUMN IF NOT EXISTS company_id integer NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE seguimiento_diario_levante DROP COLUMN IF EXISTS company_id;");
        }
    }
}
