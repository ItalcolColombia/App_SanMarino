using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Flag por empresa <c>companies.captura_huevos_en_levante</c>: habilita la captura de la
    /// clasificación de huevos en el Seguimiento Diario de LEVANTE desde la semana 14 y el arrastre
    /// del acumulado al primer registro de Producción al liquidar el levante.
    /// <para>
    /// Default <c>false</c> ⇒ todas las empresas existentes conservan el comportamiento actual
    /// byte a byte. Idempotente (<c>ADD COLUMN IF NOT EXISTS</c>) para tolerar reintentos de deploy.
    /// </para>
    /// </summary>
    public partial class AddCapturaHuevosEnLevanteCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies
                    ADD COLUMN IF NOT EXISTS captura_huevos_en_levante boolean NOT NULL DEFAULT false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies
                    DROP COLUMN IF EXISTS captura_huevos_en_levante;
            ");
        }
    }
}
