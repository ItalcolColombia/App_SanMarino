using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Devuelve <c>companies.nombre_lote_incluye_corrida = false</c> a <c>ItalcolEcuador</c>.
    /// <para>
    /// El flag decide si el nombre del lote lleva el sufijo de corrida desde la PRIMERA apertura
    /// ("96 - 1", Panamá) o sólo desde la segunda ("2604", y "2604 - 2" al reabrir el mismo base en
    /// el mismo galpón, Ecuador). <see cref="AddNombreLoteIncluyeCorrida"/> lo creó en <c>false</c> y
    /// sólo lo prendió para <c>ItalcolPanama</c>; en Ecuador la corrida ya viene codificada en el
    /// nombre del lote base (año + número: 2601, 2602…) y hay un solo lote por galpón por corrida.
    /// </para>
    /// <para>
    /// Ninguna migración lo prendió para Ecuador: se prendió desde la administración de empresas, y
    /// se ve en los datos — hasta el 21-ago-2026 los lotes nacían "2604"; desde el 26-ago, "2604 - 1".
    /// </para>
    /// <para>
    /// Sólo afecta a lotes NUEVOS (el nombre se fija al crear y no se recalcula al editar): no
    /// renombra los ya creados, no toca los "- 2" legítimos —que son huella de un lote eliminado— ni
    /// el flag de ninguna otra empresa. Idempotente por <c>IS DISTINCT FROM</c>.
    /// </para>
    /// </summary>
    public partial class ApagarNombreLoteIncluyeCorridaEcuador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET nombre_lote_incluye_corrida = false
                 WHERE name = 'ItalcolEcuador'
                   AND nombre_lote_incluye_corrida IS DISTINCT FROM false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Inverso exacto: el estado previo está medido (era true), no supuesto.
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET nombre_lote_incluye_corrida = true
                 WHERE name = 'ItalcolEcuador'
                   AND nombre_lote_incluye_corrida IS DISTINCT FROM true;
            ");
        }
    }
}
