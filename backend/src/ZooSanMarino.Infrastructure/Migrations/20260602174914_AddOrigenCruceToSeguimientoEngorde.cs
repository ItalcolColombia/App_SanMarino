using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrigenCruceToSeguimientoEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente: la columna puede haberse creado por script SQL previo.
            migrationBuilder.Sql(@"
                ALTER TABLE seguimiento_diario_aves_engorde
                    ADD COLUMN IF NOT EXISTS origen_cruce boolean NOT NULL DEFAULT false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "origen_cruce",
                table: "seguimiento_diario_aves_engorde");
        }
    }
}
