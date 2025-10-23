using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeguimientoProduccionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_movimiento_aves_galpones_galpon_destino_id",
                table: "movimiento_aves");

            migrationBuilder.DropForeignKey(
                name: "fk_movimiento_aves_galpones_galpon_origen_id",
                table: "movimiento_aves");

            migrationBuilder.DropForeignKey(
                name: "fk_movimiento_aves_nucleos_nucleo_destino_nucleo_id_nucleo_des",
                table: "movimiento_aves");

            migrationBuilder.DropForeignKey(
                name: "fk_movimiento_aves_nucleos_nucleo_origen_nucleo_id_nucleo_orig",
                table: "movimiento_aves");

            migrationBuilder.DropIndex(
                name: "ix_movimiento_aves_galpon_destino_id",
                table: "movimiento_aves");

            migrationBuilder.DropIndex(
                name: "ix_movimiento_aves_galpon_origen_id",
                table: "movimiento_aves");

            migrationBuilder.DropIndex(
                name: "ix_movimiento_aves_nucleo_destino_nucleo_id_nucleo_destino_gra",
                table: "movimiento_aves");

            migrationBuilder.DropIndex(
                name: "ix_movimiento_aves_nucleo_origen_nucleo_id_nucleo_origen_granj",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "nucleo_destino_granja_id",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "nucleo_destino_nucleo_id",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "nucleo_origen_granja_id",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "nucleo_origen_nucleo_id",
                table: "movimiento_aves");

            migrationBuilder.CreateTable(
                name: "seguimiento_produccion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lote_id = table.Column<int>(type: "integer", nullable: false),
                    mortalidad_h = table.Column<int>(type: "integer", nullable: false),
                    mortalidad_m = table.Column<int>(type: "integer", nullable: false),
                    sel_h = table.Column<int>(type: "integer", nullable: false),
                    cons_kg_h = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    cons_kg_m = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    huevo_tot = table.Column<int>(type: "integer", nullable: false),
                    huevo_inc = table.Column<int>(type: "integer", nullable: false),
                    tipo_alimento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    peso_huevo = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    etapa = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seguimiento_produccion", x => x.id);
                    table.ForeignKey(
                        name: "fk_seguimiento_produccion_lotes_lote_id",
                        column: x => x.lote_id,
                        principalSchema: "public",
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_farms",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<int>(type: "integer", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_farms", x => new { x.user_id, x.farm_id });
                    table.ForeignKey(
                        name: "fk_user_farms_farms_farm_id",
                        column: x => x.farm_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_farms_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_seguimiento_produccion_lote_id_fecha",
                table: "seguimiento_produccion",
                columns: new[] { "lote_id", "fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_farms_farm_id",
                table: "user_farms",
                column: "farm_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_farms_is_default",
                table: "user_farms",
                column: "is_default");

            migrationBuilder.CreateIndex(
                name: "ix_user_farms_user_id",
                table: "user_farms",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_farms_user_farm",
                table: "user_farms",
                columns: new[] { "user_id", "farm_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seguimiento_produccion");

            migrationBuilder.DropTable(
                name: "user_farms");

            migrationBuilder.AddColumn<int>(
                name: "nucleo_destino_granja_id",
                table: "movimiento_aves",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nucleo_destino_nucleo_id",
                table: "movimiento_aves",
                type: "character varying(64)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "nucleo_origen_granja_id",
                table: "movimiento_aves",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nucleo_origen_nucleo_id",
                table: "movimiento_aves",
                type: "character varying(64)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_galpon_destino_id",
                table: "movimiento_aves",
                column: "galpon_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_galpon_origen_id",
                table: "movimiento_aves",
                column: "galpon_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_nucleo_destino_nucleo_id_nucleo_destino_gra",
                table: "movimiento_aves",
                columns: new[] { "nucleo_destino_nucleo_id", "nucleo_destino_granja_id" });

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_nucleo_origen_nucleo_id_nucleo_origen_granj",
                table: "movimiento_aves",
                columns: new[] { "nucleo_origen_nucleo_id", "nucleo_origen_granja_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_movimiento_aves_galpones_galpon_destino_id",
                table: "movimiento_aves",
                column: "galpon_destino_id",
                principalSchema: "public",
                principalTable: "galpones",
                principalColumn: "galpon_id");

            migrationBuilder.AddForeignKey(
                name: "fk_movimiento_aves_galpones_galpon_origen_id",
                table: "movimiento_aves",
                column: "galpon_origen_id",
                principalSchema: "public",
                principalTable: "galpones",
                principalColumn: "galpon_id");

            migrationBuilder.AddForeignKey(
                name: "fk_movimiento_aves_nucleos_nucleo_destino_nucleo_id_nucleo_des",
                table: "movimiento_aves",
                columns: new[] { "nucleo_destino_nucleo_id", "nucleo_destino_granja_id" },
                principalSchema: "public",
                principalTable: "nucleos",
                principalColumns: new[] { "nucleo_id", "granja_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_movimiento_aves_nucleos_nucleo_origen_nucleo_id_nucleo_orig",
                table: "movimiento_aves",
                columns: new[] { "nucleo_origen_nucleo_id", "nucleo_origen_granja_id" },
                principalSchema: "public",
                principalTable: "nucleos",
                principalColumns: new[] { "nucleo_id", "granja_id" });
        }
    }
}
