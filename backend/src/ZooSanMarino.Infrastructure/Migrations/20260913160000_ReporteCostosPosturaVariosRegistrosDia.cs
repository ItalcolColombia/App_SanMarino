using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Reporte Diario Costos Postura con VARIOS registros el mismo día (Santa Reyes, flag
    /// <c>permite_multiples_seguimientos_diarios</c>): <c>fn_reporte_diario_costos_postura</c> v3.
    ///
    /// <list type="bullet">
    /// <item>Levante: la fila del día SUMA los registros (antes <c>DISTINCT ON</c> se quedaba con el primero
    /// y perdía mortalidad, selección, error de sexaje, venta y consumo de los demás).</item>
    /// <item>Alimentos: los ítems se explotan POR REGISTRO y el fallback por <c>tipo_alimento</c> se decide
    /// por registro (antes salían solo los de un registro del día, también en producción).</item>
    /// <item>Producción: venta de aves = suma de los registros del día (antes la del <c>seg_id</c>, el primero).</item>
    /// </list>
    ///
    /// Firma de retorno igual ⇒ alcanza <c>CREATE OR REPLACE</c>. Sin el flag cada CTE recorre el camino de
    /// v2: medido con <c>backend/sql/verificar_paridad_reporte_costos_postura.sql</c> ⇒ 0 diferencias en
    /// Sanmarino y Demo. Down = v2 verbatim (el espejo previo, igual al prosrc desplegado).
    /// Espejo: backend/sql/fn_reporte_diario_costos_postura.sql. Contrato C#:
    /// <c>ReporteDiarioCostosPosturaVariosRegistrosCalculos</c>.
    /// Plan: fase_de_desarrollo/reporte_diario_costos_postura_varios_registros_dia_plan.md.
    /// </summary>
    public partial class ReporteCostosPosturaVariosRegistrosDia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnReporteCostosPosturaV3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnReporteCostosPosturaV2);
        }
    }
}
