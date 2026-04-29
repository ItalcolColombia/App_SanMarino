using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMovimientosAvesToSeguimientoDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "traslado_aves_entrante",
                schema: "public",
                table: "seguimiento_diario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "traslado_aves_salida",
                schema: "public",
                table: "seguimiento_diario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "venta_aves_cantidad",
                schema: "public",
                table: "seguimiento_diario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "venta_aves_motivo",
                schema: "public",
                table: "seguimiento_diario",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "traslado_aves_entrante",
                schema: "public",
                table: "seguimiento_diario");

            migrationBuilder.DropColumn(
                name: "traslado_aves_salida",
                schema: "public",
                table: "seguimiento_diario");

            migrationBuilder.DropColumn(
                name: "venta_aves_cantidad",
                schema: "public",
                table: "seguimiento_diario");

            migrationBuilder.DropColumn(
                name: "venta_aves_motivo",
                schema: "public",
                table: "seguimiento_diario");
        }
    }
}
