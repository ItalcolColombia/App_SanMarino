using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLotePosturaBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "estado_operativo_lote",
                schema: "public",
                table: "lote_ave_engorde",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "liquidado_at",
                schema: "public",
                table: "lote_ave_engorde",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "liquidado_por_user_id",
                schema: "public",
                table: "lote_ave_engorde",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_reapertura",
                schema: "public",
                table: "lote_ave_engorde",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reabierto_at",
                schema: "public",
                table: "lote_ave_engorde",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reabierto_por_user_id",
                schema: "public",
                table: "lote_ave_engorde",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lote_postura_base",
                schema: "public",
                columns: table => new
                {
                    lote_postura_base_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    codigo_erp = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    cantidad_hembras = table.Column<int>(type: "integer", nullable: false),
                    cantidad_machos = table.Column<int>(type: "integer", nullable: false),
                    cantidad_mixtas = table.Column<int>(type: "integer", nullable: false),
                    pais_id = table.Column<int>(type: "integer", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lote_postura_base", x => x.lote_postura_base_id);
                    table.CheckConstraint("ck_lpb_nonneg_counts", "cantidad_hembras >= 0 AND cantidad_machos >= 0 AND cantidad_mixtas >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_base_codigo_erp",
                schema: "public",
                table: "lote_postura_base",
                column: "codigo_erp");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_base_company",
                schema: "public",
                table: "lote_postura_base",
                column: "company_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lote_postura_base",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "estado_operativo_lote",
                schema: "public",
                table: "lote_ave_engorde");

            migrationBuilder.DropColumn(
                name: "liquidado_at",
                schema: "public",
                table: "lote_ave_engorde");

            migrationBuilder.DropColumn(
                name: "liquidado_por_user_id",
                schema: "public",
                table: "lote_ave_engorde");

            migrationBuilder.DropColumn(
                name: "motivo_reapertura",
                schema: "public",
                table: "lote_ave_engorde");

            migrationBuilder.DropColumn(
                name: "reabierto_at",
                schema: "public",
                table: "lote_ave_engorde");

            migrationBuilder.DropColumn(
                name: "reabierto_por_user_id",
                schema: "public",
                table: "lote_ave_engorde");
        }
    }
}
