using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// fn_seguimiento_diario_produccion v3: con el flag de empresa
    /// <c>permite_multiples_seguimientos_diarios</c> ON, agrupa por día calendario Bogotá en vez
    /// de dedupear (quedarse con el timestamp más temprano). La firma de retorno NO cambia
    /// (mismas columnas/tipos) ⇒ alcanza <c>CREATE OR REPLACE</c>, sin DROP ni tocar las 3 fns
    /// semanales derivadas (heredan el comportamiento nuevo automáticamente, sin cambio de código).
    /// Detalle y regla de agregación por campo: backend/sql/fn_seguimiento_diario_produccion.sql
    /// (changelog v3) y fase_de_desarrollo/seguimiento_produccion_multiples_registros_dia_plan.md.
    /// </summary>
    public partial class FnSeguimientoDiarioProduccionV3MultiplesRegistros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnV3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnV2);
        }
    }
}
