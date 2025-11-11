using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldsToProduccionLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ciclo",
                table: "produccion_lote",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "normal");

            migrationBuilder.AddColumn<int>(
                name: "huevos_iniciales",
                table: "produccion_lote",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "nucleo_p",
                table: "produccion_lote",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_nido",
                table: "produccion_lote",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "lote_erp",
                schema: "public",
                table: "lotes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "system_configurations",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_encrypted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_configurations", x => x.key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_configurations");

            migrationBuilder.DropColumn(
                name: "ciclo",
                table: "produccion_lote");

            migrationBuilder.DropColumn(
                name: "huevos_iniciales",
                table: "produccion_lote");

            migrationBuilder.DropColumn(
                name: "nucleo_p",
                table: "produccion_lote");

            migrationBuilder.DropColumn(
                name: "tipo_nido",
                table: "produccion_lote");

            migrationBuilder.DropColumn(
                name: "lote_erp",
                schema: "public",
                table: "lotes");
        }
    }
}
