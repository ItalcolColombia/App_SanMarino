using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProduccionLoteYSeguimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_produccion_lotes_galpones_galpon_id",
                table: "produccion_lotes");

            migrationBuilder.DropForeignKey(
                name: "fk_produccion_lotes_lotes_lote_id",
                table: "produccion_lotes");

            migrationBuilder.DropForeignKey(
                name: "fk_produccion_lotes_nucleos_nucleo_id_granja_id",
                table: "produccion_lotes");

            migrationBuilder.DropPrimaryKey(
                name: "pk_produccion_lotes",
                table: "produccion_lotes");

            migrationBuilder.DropIndex(
                name: "ix_produccion_lotes_galpon_id",
                table: "produccion_lotes");

            migrationBuilder.DropIndex(
                name: "ix_produccion_lotes_lote_id",
                table: "produccion_lotes");

            migrationBuilder.DropIndex(
                name: "ix_produccion_lotes_nucleo_id_granja_id",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "ciclo",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "fecha_inicio_produccion",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "galpon_id",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "granja_id",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "hembras_iniciales",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "huevos_iniciales",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "machos_iniciales",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "nucleo_id",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "tipo_nido",
                table: "produccion_lotes");

            migrationBuilder.RenameTable(
                name: "produccion_lotes",
                newName: "produccion_lote");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "produccion_lote",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddColumn<int>(
                name: "aves_iniciales_h",
                table: "produccion_lote",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "aves_iniciales_m",
                table: "produccion_lote",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_inicio",
                table: "produccion_lote",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "observaciones",
                table: "produccion_lote",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_produccion_lote",
                table: "produccion_lote",
                column: "id");

            migrationBuilder.CreateTable(
                name: "produccion_seguimiento",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    produccion_lote_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_registro = table.Column<DateTime>(type: "date", nullable: false),
                    mortalidad_h = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    mortalidad_m = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consumo_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    huevos_totales = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    huevos_incubables = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    peso_huevo = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false, defaultValue: 0m),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produccion_seguimiento", x => x.id);
                    table.ForeignKey(
                        name: "fk_produccion_seguimiento_produccion_lote_produccion_lote_id",
                        column: x => x.produccion_lote_id,
                        principalTable: "produccion_lote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_produccion_lote_fecha_inicio",
                table: "produccion_lote",
                column: "fecha_inicio");

            migrationBuilder.CreateIndex(
                name: "IX_produccion_lote_lote_id_unique",
                table: "produccion_lote",
                column: "lote_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_produccion_seguimiento_fecha_registro",
                table: "produccion_seguimiento",
                column: "fecha_registro");

            migrationBuilder.CreateIndex(
                name: "IX_produccion_seguimiento_lote_fecha_unique",
                table: "produccion_seguimiento",
                columns: new[] { "produccion_lote_id", "fecha_registro" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_produccion_lote_lotes_lote_id",
                table: "produccion_lote",
                column: "lote_id",
                principalSchema: "public",
                principalTable: "lotes",
                principalColumn: "lote_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_produccion_lote_lotes_lote_id",
                table: "produccion_lote");

            migrationBuilder.DropTable(
                name: "produccion_seguimiento");

            migrationBuilder.DropPrimaryKey(
                name: "pk_produccion_lote",
                table: "produccion_lote");

            migrationBuilder.DropIndex(
                name: "IX_produccion_lote_fecha_inicio",
                table: "produccion_lote");

            migrationBuilder.DropIndex(
                name: "IX_produccion_lote_lote_id_unique",
                table: "produccion_lote");

            migrationBuilder.DropColumn(
                name: "aves_iniciales_h",
                table: "produccion_lote");

            migrationBuilder.DropColumn(
                name: "aves_iniciales_m",
                table: "produccion_lote");

            migrationBuilder.DropColumn(
                name: "fecha_inicio",
                table: "produccion_lote");

            migrationBuilder.DropColumn(
                name: "observaciones",
                table: "produccion_lote");

            migrationBuilder.RenameTable(
                name: "produccion_lote",
                newName: "produccion_lotes");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "ciclo",
                table: "produccion_lotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_inicio_produccion",
                table: "produccion_lotes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "galpon_id",
                table: "produccion_lotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "granja_id",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "hembras_iniciales",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "huevos_iniciales",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "machos_iniciales",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "nucleo_id",
                table: "produccion_lotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tipo_nido",
                table: "produccion_lotes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "pk_produccion_lotes",
                table: "produccion_lotes",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_produccion_lotes_galpon_id",
                table: "produccion_lotes",
                column: "galpon_id");

            migrationBuilder.CreateIndex(
                name: "ix_produccion_lotes_lote_id",
                table: "produccion_lotes",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_produccion_lotes_nucleo_id_granja_id",
                table: "produccion_lotes",
                columns: new[] { "nucleo_id", "granja_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_produccion_lotes_galpones_galpon_id",
                table: "produccion_lotes",
                column: "galpon_id",
                principalSchema: "public",
                principalTable: "galpones",
                principalColumn: "galpon_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_produccion_lotes_lotes_lote_id",
                table: "produccion_lotes",
                column: "lote_id",
                principalSchema: "public",
                principalTable: "lotes",
                principalColumn: "lote_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_produccion_lotes_nucleos_nucleo_id_granja_id",
                table: "produccion_lotes",
                columns: new[] { "nucleo_id", "granja_id" },
                principalSchema: "public",
                principalTable: "nucleos",
                principalColumns: new[] { "nucleo_id", "granja_id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
