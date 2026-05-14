using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameTable_ProduccionAvicolaRaw_to_GuiaGenetica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PK real en BD: produccion_avicola_raw_pkey (no pk_produccion_avicola_raw)
            migrationBuilder.DropPrimaryKey(
                name: "produccion_avicola_raw_pkey",
                table: "produccion_avicola_raw");

            migrationBuilder.RenameTable(
                name: "produccion_avicola_raw",
                newName: "guia_genetica_sanmarino_colombia");

            migrationBuilder.RenameIndex(
                name: "ix_produccion_avicola_raw_raza",
                table: "guia_genetica_sanmarino_colombia",
                newName: "ix_guia_genetica_sanmarino_colombia_raza");

            migrationBuilder.RenameIndex(
                name: "ix_produccion_avicola_raw_company_id",
                table: "guia_genetica_sanmarino_colombia",
                newName: "ix_guia_genetica_sanmarino_colombia_company_id");

            migrationBuilder.RenameIndex(
                name: "ix_produccion_avicola_raw_anio_guia",
                table: "guia_genetica_sanmarino_colombia",
                newName: "ix_guia_genetica_sanmarino_colombia_anio_guia");

            // Índice adicional en BD no rastreado por EF
            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_produccion_avicola_raw_created_at RENAME TO ix_guia_genetica_sanmarino_colombia_created_at;");

            migrationBuilder.AddPrimaryKey(
                name: "pk_guia_genetica_sanmarino_colombia",
                table: "guia_genetica_sanmarino_colombia",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_guia_genetica_sanmarino_colombia",
                table: "guia_genetica_sanmarino_colombia");

            migrationBuilder.RenameTable(
                name: "guia_genetica_sanmarino_colombia",
                newName: "produccion_avicola_raw");

            migrationBuilder.RenameIndex(
                name: "ix_guia_genetica_sanmarino_colombia_raza",
                table: "produccion_avicola_raw",
                newName: "ix_produccion_avicola_raw_raza");

            migrationBuilder.RenameIndex(
                name: "ix_guia_genetica_sanmarino_colombia_company_id",
                table: "produccion_avicola_raw",
                newName: "ix_produccion_avicola_raw_company_id");

            migrationBuilder.RenameIndex(
                name: "ix_guia_genetica_sanmarino_colombia_anio_guia",
                table: "produccion_avicola_raw",
                newName: "ix_produccion_avicola_raw_anio_guia");

            migrationBuilder.Sql("ALTER INDEX IF EXISTS ix_guia_genetica_sanmarino_colombia_created_at RENAME TO ix_produccion_avicola_raw_created_at;");

            migrationBuilder.AddPrimaryKey(
                name: "produccion_avicola_raw_pkey",
                table: "produccion_avicola_raw",
                column: "id");
        }
    }
}
