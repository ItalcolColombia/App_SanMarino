using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Flag por empresa <c>companies.venta_engorde_peso_diferido</c>: en las VENTAS de pollo
    /// engorde el peso báscula (bruto/tara) deja de ser obligatorio al registrar la venta, porque
    /// llega al día siguiente (Panamá). La venta nace sin peso en estado <c>Pendiente</c> y el peso
    /// se carga al CONFIRMARLA, re-prorrateándose por lote en la misma transacción que la pasa a
    /// <c>Completado</c> ⇒ nunca existe una venta <c>Completado</c> sin peso.
    /// <para>
    /// Default <c>false</c> ⇒ todas las empresas existentes conservan el comportamiento actual byte
    /// a byte. Idempotente (<c>ADD COLUMN IF NOT EXISTS</c>) para tolerar reintentos de deploy.
    /// </para>
    /// </summary>
    public partial class AddVentaEngordePesoDiferidoCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies
                    ADD COLUMN IF NOT EXISTS venta_engorde_peso_diferido boolean NOT NULL DEFAULT false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies
                    DROP COLUMN IF EXISTS venta_engorde_peso_diferido;
            ");
        }
    }
}
