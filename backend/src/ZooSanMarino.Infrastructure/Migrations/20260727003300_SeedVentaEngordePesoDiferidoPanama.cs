using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Migración DATA-ONLY: activa <c>companies.venta_engorde_peso_diferido</c> para
    /// <b>ItalcolPanama</b> — la operación de Panamá recibe el peso de báscula al día siguiente,
    /// así que la venta se registra sin peso y el peso se carga al confirmarla.
    /// <para>
    /// Idempotente: lookup por <c>name</c> (los ids difieren entre local y prod) y
    /// <c>IS DISTINCT FROM</c> para no ensuciar <c>updated_at</c> ni reescribir filas ya correctas.
    /// Si la empresa no existe, el UPDATE no afecta ninguna fila y la migración pasa igual.
    /// </para>
    /// <para>
    /// Las demás empresas quedan en <c>false</c> ⇒ peso obligatorio, sin cambios visibles.
    /// </para>
    /// </summary>
    public partial class SeedVentaEngordePesoDiferidoPanama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET venta_engorde_peso_diferido = true
                 WHERE name = 'ItalcolPanama'
                   AND venta_engorde_peso_diferido IS DISTINCT FROM true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET venta_engorde_peso_diferido = false
                 WHERE name = 'ItalcolPanama'
                   AND venta_engorde_peso_diferido IS DISTINCT FROM false;
            ");
        }
    }
}
