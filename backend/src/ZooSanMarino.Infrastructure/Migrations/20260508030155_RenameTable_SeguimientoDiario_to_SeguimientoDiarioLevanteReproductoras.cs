using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameTable_SeguimientoDiario_to_SeguimientoDiarioLevanteReproductoras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PK real en BD: seguimiento_diario_pkey (no pk_seguimiento_diario)
            migrationBuilder.DropPrimaryKey(
                name: "seguimiento_diario_pkey",
                schema: "public",
                table: "seguimiento_diario");

            migrationBuilder.RenameTable(
                name: "seguimiento_diario",
                schema: "public",
                newName: "seguimiento_diario_levante_reproductoras",
                newSchema: "public");

            // Todos los índices adicionales (incluyendo unique indexes) — usar ALTER INDEX
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_seguimiento_diario_fecha RENAME TO ix_sdlr_fecha;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_seguimiento_diario_lote_id RENAME TO ix_sdlr_lote_id;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_seguimiento_diario_lote_id_int RENAME TO ix_sdlr_lote_id_int;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_seguimiento_diario_lote_postura_levante_id RENAME TO ix_sdlr_lote_postura_levante_id;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_seguimiento_diario_lote_postura_produccion_id RENAME TO ix_sdlr_lote_postura_produccion_id;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_seguimiento_diario_lote_reproductora_fecha RENAME TO ix_sdlr_lote_reproductora_fecha;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_seguimiento_diario_tipo RENAME TO ix_sdlr_tipo;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_seguimiento_diario_tipo_lote_fecha RENAME TO ix_sdlr_tipo_lote_fecha;");
            // Unique indexes (creados con CREATE UNIQUE INDEX, no con CONSTRAINT)
            migrationBuilder.Sql("ALTER INDEX IF EXISTS uq_seguimiento_diario_prod_lote_fecha RENAME TO uq_sdlr_prod_lote_fecha;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS uq_seguimiento_diario_tipo_lote_rep_fecha RENAME TO uq_sdlr_tipo_lote_rep_fecha;");

            migrationBuilder.AddPrimaryKey(
                name: "pk_seguimiento_diario_levante_reproductoras",
                schema: "public",
                table: "seguimiento_diario_levante_reproductoras",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_seguimiento_diario_levante_reproductoras",
                schema: "public",
                table: "seguimiento_diario_levante_reproductoras");

            migrationBuilder.RenameTable(
                name: "seguimiento_diario_levante_reproductoras",
                schema: "public",
                newName: "seguimiento_diario",
                newSchema: "public");

            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_sdlr_fecha RENAME TO ix_seguimiento_diario_fecha;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_sdlr_lote_id RENAME TO ix_seguimiento_diario_lote_id;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_sdlr_lote_id_int RENAME TO ix_seguimiento_diario_lote_id_int;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_sdlr_lote_postura_levante_id RENAME TO ix_seguimiento_diario_lote_postura_levante_id;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_sdlr_lote_postura_produccion_id RENAME TO ix_seguimiento_diario_lote_postura_produccion_id;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_sdlr_lote_reproductora_fecha RENAME TO ix_seguimiento_diario_lote_reproductora_fecha;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_sdlr_tipo RENAME TO ix_seguimiento_diario_tipo;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_sdlr_tipo_lote_fecha RENAME TO ix_seguimiento_diario_tipo_lote_fecha;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS uq_sdlr_prod_lote_fecha RENAME TO uq_seguimiento_diario_prod_lote_fecha;");
            migrationBuilder.Sql("ALTER INDEX IF EXISTS uq_sdlr_tipo_lote_rep_fecha RENAME TO uq_seguimiento_diario_tipo_lote_rep_fecha;");

            migrationBuilder.AddPrimaryKey(
                name: "seguimiento_diario_pkey",
                schema: "public",
                table: "seguimiento_diario",
                column: "id");
        }
    }
}
