using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLotePosturaProduccionIdToSeguimientoDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "lote_postura_levante_id",
                schema: "public",
                table: "seguimiento_diario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lote_postura_produccion_id",
                schema: "public",
                table: "seguimiento_diario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lote_postura_produccion_id",
                table: "produccion_diaria",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_produccion_diaria_lote_postura_produccion_id",
                table: "produccion_diaria",
                column: "lote_postura_produccion_id",
                filter: "lote_postura_produccion_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_produccion_diaria_lote_postura_produccion_id",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "lote_postura_levante_id",
                schema: "public",
                table: "seguimiento_diario");

            migrationBuilder.DropColumn(
                name: "lote_postura_produccion_id",
                schema: "public",
                table: "seguimiento_diario");

            migrationBuilder.DropColumn(
                name: "lote_postura_produccion_id",
                table: "produccion_diaria");
        }
    }
}
