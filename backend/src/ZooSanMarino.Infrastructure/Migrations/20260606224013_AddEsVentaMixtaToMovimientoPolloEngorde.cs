using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEsVentaMixtaToMovimientoPolloEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente (regla de migraciones): la columna puede existir por trabajo manual previo.
            migrationBuilder.Sql(
                "ALTER TABLE movimiento_pollo_engorde ADD COLUMN IF NOT EXISTS es_venta_mixta boolean NOT NULL DEFAULT false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE movimiento_pollo_engorde DROP COLUMN IF EXISTS es_venta_mixta;");
        }
    }
}
