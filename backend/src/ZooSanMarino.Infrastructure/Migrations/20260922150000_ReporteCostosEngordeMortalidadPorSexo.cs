using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Reporte Diario Costos de pollo engorde: mortalidad y selección POR SEXO en cada galpón
    /// (pedido de Ecuador) — <c>fn_reporte_diario_costos_engorde</c> v4.
    ///
    /// El JSON <c>galpones</c> suma seis claves por galpón y día (<c>mortalidad_hembras</c>,
    /// <c>mortalidad_machos</c>, <c>seleccion_hembras</c>, <c>seleccion_machos</c>,
    /// <c>mort_sel_hembras</c>, <c>mort_sel_machos</c>) sacadas de las mismas columnas de
    /// <c>fn_seguimiento_diario_engorde</c> que ya se sumaban, así que H + M == el valor combinado.
    ///
    /// Firma de retorno igual ⇒ alcanza <c>CREATE OR REPLACE</c>. Medido en transacción revertida
    /// contra la BD local, todas las granjas con lotes de engorde (Ecuador y Panamá, 2.070 filas
    /// entre historia completa y rango por defecto): la salida v4 sin las claves nuevas es IGUAL a la
    /// v3 (0 diferencias) y H + M cuadra en las 9.394 celdas galpón × día.
    ///
    /// Quién ve el desglose lo decide el service con <c>companies.seguimiento_engorde_mixto</c>
    /// (<c>ReporteDiarioCostosEngordeCalculos.MuestraMortalidadPorSexo</c>), no esta fn.
    /// Down = v3 verbatim (el espejo previo, igual al prosrc desplegado).
    /// Espejo: backend/sql/fn_reporte_diario_costos_engorde.sql.
    /// Plan: fase_de_desarrollo/reporte_costos_engorde_mortalidad_por_sexo_plan.md.
    /// </summary>
    public partial class ReporteCostosEngordeMortalidadPorSexo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnReporteCostosEngordeV4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnReporteCostosEngordeV3);
        }
    }
}
