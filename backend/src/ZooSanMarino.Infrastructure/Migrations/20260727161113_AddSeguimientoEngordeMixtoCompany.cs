using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Flag por empresa <c>companies.seguimiento_engorde_mixto</c>: la empresa NO maneja el pollo
    /// engorde por sexo una vez que el lote sale de reproductora (Panamá), así que la carga masiva
    /// del seguimiento diario se digita como un único total MIXTO.
    /// <para>
    /// Solo cambia la PRESENTACIÓN: la plantilla descargable emite columnas «Mort Mixta»,
    /// «Consumo Mixto (kg)»… en lugar del par H/M. El dato sigue guardándose en los mismos campos
    /// (<c>consumo_kg_hembras</c>, <c>mortalidad_hembras</c>…), porque el sistema suma H+M en todos
    /// sus cálculos. Los encabezados por sexo se siguen aceptando ⇒ los archivos viejos cargan igual.
    /// </para>
    /// <para>
    /// Default <c>false</c> ⇒ todas las empresas existentes conservan el comportamiento actual byte
    /// a byte. Idempotente (<c>ADD COLUMN IF NOT EXISTS</c>) para tolerar reintentos de deploy.
    /// </para>
    /// </summary>
    public partial class AddSeguimientoEngordeMixtoCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies
                    ADD COLUMN IF NOT EXISTS seguimiento_engorde_mixto boolean NOT NULL DEFAULT false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies
                    DROP COLUMN IF EXISTS seguimiento_engorde_mixto;
            ");
        }
    }
}
