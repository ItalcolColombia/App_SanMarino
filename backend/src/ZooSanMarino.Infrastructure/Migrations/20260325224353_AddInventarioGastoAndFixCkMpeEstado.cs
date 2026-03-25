using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventarioGastoAndFixCkMpeEstado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE movimiento_pollo_engorde DROP CONSTRAINT IF EXISTS ck_mpe_estado;
ALTER TABLE movimiento_pollo_engorde ADD CONSTRAINT ck_mpe_estado CHECK (estado IN ('Pendiente', 'Completado', 'Cancelado', 'Anulado'));
");

            migrationBuilder.CreateTable(
                name: "inventario_gasto",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    pais_id = table.Column<int>(type: "integer", nullable: false),
                    farm_id = table.Column<int>(type: "integer", nullable: false),
                    nucleo_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    galpon_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    lote_ave_engorde_id = table.Column<int>(type: "integer", nullable: true),
                    fecha = table.Column<DateTime>(type: "date", nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Activo"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_by_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    deleted_by_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_gasto", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_gasto_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_gasto_farms_farm_id",
                        column: x => x.farm_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_gasto_lote_ave_engorde_lote_ave_engorde_id",
                        column: x => x.lote_ave_engorde_id,
                        principalSchema: "public",
                        principalTable: "lote_ave_engorde",
                        principalColumn: "lote_ave_engorde_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_gasto_paises_pais_id",
                        column: x => x.pais_id,
                        principalTable: "paises",
                        principalColumn: "pais_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventario_gasto_auditoria",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    inventario_gasto_id = table.Column<int>(type: "integer", nullable: false),
                    accion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    detalle = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_gasto_auditoria", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_gasto_auditoria_inventario_gastos_inventario_gas",
                        column: x => x.inventario_gasto_id,
                        principalSchema: "public",
                        principalTable: "inventario_gasto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventario_gasto_detalle",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    inventario_gasto_id = table.Column<int>(type: "integer", nullable: false),
                    item_inventario_ecuador_id = table.Column<int>(type: "integer", nullable: false),
                    concepto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cantidad = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "kg"),
                    stock_antes = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    stock_despues = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_gasto_detalle", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_gasto_detalle_inventario_gasto_inventario_gasto_",
                        column: x => x.inventario_gasto_id,
                        principalSchema: "public",
                        principalTable: "inventario_gasto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventario_gasto_detalle_item_inventario_ecuador_item_inven",
                        column: x => x.item_inventario_ecuador_id,
                        principalSchema: "public",
                        principalTable: "item_inventario_ecuador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gasto_company_pais_farm_fecha",
                schema: "public",
                table: "inventario_gasto",
                columns: new[] { "company_id", "pais_id", "farm_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gasto_estado",
                schema: "public",
                table: "inventario_gasto",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gasto_farm_id",
                schema: "public",
                table: "inventario_gasto",
                column: "farm_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gasto_lote_ave_engorde_id",
                schema: "public",
                table: "inventario_gasto",
                column: "lote_ave_engorde_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gasto_pais_id",
                schema: "public",
                table: "inventario_gasto",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gasto_auditoria_accion",
                schema: "public",
                table: "inventario_gasto_auditoria",
                column: "accion");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gasto_auditoria_gasto",
                schema: "public",
                table: "inventario_gasto_auditoria",
                column: "inventario_gasto_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gasto_detalle_gasto",
                schema: "public",
                table: "inventario_gasto_detalle",
                column: "inventario_gasto_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gasto_detalle_item_inventario_ecuador_id",
                schema: "public",
                table: "inventario_gasto_detalle",
                column: "item_inventario_ecuador_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventario_gasto_auditoria",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inventario_gasto_detalle",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inventario_gasto",
                schema: "public");

            migrationBuilder.Sql(@"
ALTER TABLE movimiento_pollo_engorde DROP CONSTRAINT IF EXISTS ck_mpe_estado;
ALTER TABLE movimiento_pollo_engorde ADD CONSTRAINT ck_mpe_estado CHECK (estado IN ('Pendiente', 'Completado', 'Cancelado'));
");
        }
    }
}
