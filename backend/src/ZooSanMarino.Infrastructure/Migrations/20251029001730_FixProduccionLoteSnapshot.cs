using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixProduccionLoteSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_produccion_lote_lotes_lote_id",
                table: "produccion_lote");

            migrationBuilder.DropForeignKey(
                name: "fk_produccion_seguimiento_produccion_lote_produccion_lote_id",
                table: "produccion_seguimiento");

            migrationBuilder.DropPrimaryKey(
                name: "pk_produccion_lote",
                table: "produccion_lote");

            migrationBuilder.RenameTable(
                name: "produccion_lote",
                newName: "produccion_lotes");

            migrationBuilder.RenameColumn(
                name: "fecha_inicio",
                table: "produccion_lotes",
                newName: "fecha_inicio_produccion");

            migrationBuilder.RenameColumn(
                name: "aves_iniciales_m",
                table: "produccion_lotes",
                newName: "machos_iniciales");

            migrationBuilder.RenameColumn(
                name: "aves_iniciales_h",
                table: "produccion_lotes",
                newName: "hembras_iniciales");

            migrationBuilder.AlterColumn<string>(
                name: "nucleo_p",
                table: "produccion_lotes",
                type: "character varying",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                table: "produccion_lotes",
                type: "character varying",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateTime>(
                name: "fecha_inicio_produccion",
                table: "produccion_lotes",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.AddColumn<string>(
                name: "galpon_id",
                table: "produccion_lotes",
                type: "character varying",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "granja_id",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "nucleo_id",
                table: "produccion_lotes",
                type: "character varying",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "pk_produccion_lotes",
                table: "produccion_lotes",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_produccion_seguimiento_produccion_lotes_produccion_lote_id",
                table: "produccion_seguimiento",
                column: "produccion_lote_id",
                principalTable: "produccion_lotes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_produccion_seguimiento_produccion_lotes_produccion_lote_id",
                table: "produccion_seguimiento");

            migrationBuilder.DropPrimaryKey(
                name: "pk_produccion_lotes",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "galpon_id",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "granja_id",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "nucleo_id",
                table: "produccion_lotes");

            migrationBuilder.RenameTable(
                name: "produccion_lotes",
                newName: "produccion_lote");

            migrationBuilder.RenameColumn(
                name: "machos_iniciales",
                table: "produccion_lote",
                newName: "aves_iniciales_m");

            migrationBuilder.RenameColumn(
                name: "hembras_iniciales",
                table: "produccion_lote",
                newName: "aves_iniciales_h");

            migrationBuilder.RenameColumn(
                name: "fecha_inicio_produccion",
                table: "produccion_lote",
                newName: "fecha_inicio");

            migrationBuilder.AlterColumn<string>(
                name: "nucleo_p",
                table: "produccion_lote",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "lote_id",
                table: "produccion_lote",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying");

            migrationBuilder.AlterColumn<DateTime>(
                name: "fecha_inicio",
                table: "produccion_lote",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddPrimaryKey(
                name: "pk_produccion_lote",
                table: "produccion_lote",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_produccion_lote_lotes_lote_id",
                table: "produccion_lote",
                column: "lote_id",
                principalSchema: "public",
                principalTable: "lotes",
                principalColumn: "lote_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_produccion_seguimiento_produccion_lote_produccion_lote_id",
                table: "produccion_seguimiento",
                column: "produccion_lote_id",
                principalTable: "produccion_lote",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
