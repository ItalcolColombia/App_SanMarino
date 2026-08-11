using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <c>companies.nombre_lote_incluye_corrida</c>: cómo se llama el lote que se abre desde una
    /// programación.
    /// <list type="bullet">
    /// <item><c>true</c> (ItalcolPanama) — "{lote base} - {corrida}" desde la primera: "96 - 1". Es lo
    /// que ya hace producción, por eso el seed va en esta misma migración: sin él, el flag nuevo
    /// (default <c>false</c>) le cambiaría el nombre a los lotes de Panamá.</item>
    /// <item><c>false</c> (default, ItalcolEcuador) — el nombre del lote ES el del lote base: "2603".
    /// En Ecuador la corrida ya está codificada en el nombre del base (año + número: 2601, 2602…) y
    /// hay un solo lote por galpón en cada corrida, así que el sufijo sólo aparece desde la SEGUNDA
    /// apertura del mismo base en el mismo galpón — lo único que evita dos lotes con el mismo nombre
    /// dentro de un galpón.</item>
    /// </list>
    /// Idempotente (<c>IF NOT EXISTS</c> + <c>IS DISTINCT FROM</c>).
    /// </summary>
    public partial class AddNombreLoteIncluyeCorrida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies
                  ADD COLUMN IF NOT EXISTS nombre_lote_incluye_corrida boolean NOT NULL DEFAULT false;

                UPDATE companies
                   SET nombre_lote_incluye_corrida = true
                 WHERE name = 'ItalcolPanama'
                   AND nombre_lote_incluye_corrida IS DISTINCT FROM true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE companies DROP COLUMN IF EXISTS nombre_lote_incluye_corrida;");
        }
    }
}
