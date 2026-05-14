using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameTable_ProduccionDiaria_to_SeguimientoDiarioProduccionReproductoras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PK pk_produccion_diaria sí existe en BD con ese nombre
            migrationBuilder.DropPrimaryKey(
                name: "pk_produccion_diaria",
                table: "produccion_diaria");

            migrationBuilder.RenameTable(
                name: "produccion_diaria",
                newName: "seguimiento_diario_produccion_reproductoras");

            // Índice rastreado por EF (existe en BD)
            migrationBuilder.RenameIndex(
                name: "ix_produccion_diaria_lote_postura_produccion_id",
                table: "seguimiento_diario_produccion_reproductoras",
                newName: "ix_seguimiento_diario_produccion_reproductoras_lpp_id");

            // Índices adicionales en BD no rastreados por EF
            migrationBuilder.Sql("ALTER INDEX IF EXISTS idx_produccion_diaria_metadata_gin RENAME TO idx_sdpr_metadata_gin;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_produccion_diaria_lote_produccion_id RENAME TO ix_sdpr_lote_produccion_id;");

            migrationBuilder.AddPrimaryKey(
                name: "pk_seguimiento_diario_produccion_reproductoras",
                table: "seguimiento_diario_produccion_reproductoras",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_seguimiento_diario_produccion_reproductoras",
                table: "seguimiento_diario_produccion_reproductoras");

            migrationBuilder.RenameTable(
                name: "seguimiento_diario_produccion_reproductoras",
                newName: "produccion_diaria");

            migrationBuilder.RenameIndex(
                name: "ix_seguimiento_diario_produccion_reproductoras_lpp_id",
                table: "produccion_diaria",
                newName: "ix_produccion_diaria_lote_postura_produccion_id");

            migrationBuilder.Sql("ALTER INDEX IF EXISTS idx_sdpr_metadata_gin RENAME TO idx_produccion_diaria_metadata_gin;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_sdpr_lote_produccion_id RENAME TO ix_produccion_diaria_lote_produccion_id;");

            migrationBuilder.AddPrimaryKey(
                name: "pk_produccion_diaria",
                table: "produccion_diaria",
                column: "id");
        }
    }
}
