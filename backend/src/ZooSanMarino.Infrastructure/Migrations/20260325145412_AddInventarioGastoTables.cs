using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventarioGastoTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guia_genetica_ecuador_header",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    raza = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    anio_guia = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guia_genetica_ecuador_header", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "item_inventario_ecuador",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_item = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "kg"),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    grupo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tipo_inventario_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    descripcion_tipo_inventario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    descripcion_item = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    concepto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    pais_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_inventario_ecuador", x => x.id);
                    table.ForeignKey(
                        name: "fk_item_inventario_ecuador_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_item_inventario_ecuador_paises_pais_id",
                        column: x => x.pais_id,
                        principalTable: "paises",
                        principalColumn: "pais_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "guia_genetica_ecuador_detalle",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guia_genetica_ecuador_header_id = table.Column<int>(type: "integer", nullable: false),
                    sexo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dia = table.Column<int>(type: "integer", nullable: false),
                    peso_corporal_g = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ganancia_diaria_g = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    promedio_ganancia_diaria_g = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    cantidad_alimento_diario_g = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    alimento_acumulado_g = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ca = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    mortalidad_seleccion_diaria = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guia_genetica_ecuador_detalle", x => x.id);
                    table.ForeignKey(
                        name: "fk_guia_genetica_ecuador_detalle_guia_genetica_ecuador_header_",
                        column: x => x.guia_genetica_ecuador_header_id,
                        principalTable: "guia_genetica_ecuador_header",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventario_gestion_movimiento",
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
                    item_inventario_ecuador_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "kg"),
                    movement_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    from_farm_id = table.Column<int>(type: "integer", nullable: true),
                    from_nucleo_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    from_galpon_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    transfer_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_by_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_gestion_movimiento", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_gestion_movimiento_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_gestion_movimiento_farms_farm_id",
                        column: x => x.farm_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_gestion_movimiento_item_inventario_ecuador_item_",
                        column: x => x.item_inventario_ecuador_id,
                        principalSchema: "public",
                        principalTable: "item_inventario_ecuador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_gestion_movimiento_paises_pais_id",
                        column: x => x.pais_id,
                        principalTable: "paises",
                        principalColumn: "pais_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventario_gestion_stock",
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
                    item_inventario_ecuador_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "kg"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_gestion_stock", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_gestion_stock_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_gestion_stock_farms_farm_id",
                        column: x => x.farm_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_gestion_stock_item_inventario_ecuador_item_inven",
                        column: x => x.item_inventario_ecuador_id,
                        principalSchema: "public",
                        principalTable: "item_inventario_ecuador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_gestion_stock_paises_pais_id",
                        column: x => x.pais_id,
                        principalTable: "paises",
                        principalColumn: "pais_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_guia_genetica_ecuador_detalle_guia_genetica_ecuador_header_",
                table: "guia_genetica_ecuador_detalle",
                columns: new[] { "guia_genetica_ecuador_header_id", "sexo", "dia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_guia_genetica_ecuador_header_company_id_raza_anio_guia",
                table: "guia_genetica_ecuador_header",
                columns: new[] { "company_id", "raza", "anio_guia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_igm_company_id",
                schema: "public",
                table: "inventario_gestion_movimiento",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_igm_farm_item",
                schema: "public",
                table: "inventario_gestion_movimiento",
                columns: new[] { "farm_id", "item_inventario_ecuador_id" });

            migrationBuilder.CreateIndex(
                name: "ix_igm_movement_type",
                schema: "public",
                table: "inventario_gestion_movimiento",
                column: "movement_type");

            migrationBuilder.CreateIndex(
                name: "ix_igm_pais_id",
                schema: "public",
                table: "inventario_gestion_movimiento",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_igm_transfer_group",
                schema: "public",
                table: "inventario_gestion_movimiento",
                column: "transfer_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gestion_movimiento_item_inventario_ecuador_id",
                schema: "public",
                table: "inventario_gestion_movimiento",
                column: "item_inventario_ecuador_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gestion_stock_company_id",
                schema: "public",
                table: "inventario_gestion_stock",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gestion_stock_farm_item_nucleo_galpon",
                schema: "public",
                table: "inventario_gestion_stock",
                columns: new[] { "farm_id", "item_inventario_ecuador_id", "nucleo_id", "galpon_id" });

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gestion_stock_item_inventario_ecuador_id",
                schema: "public",
                table: "inventario_gestion_stock",
                column: "item_inventario_ecuador_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_gestion_stock_pais_id",
                schema: "public",
                table: "inventario_gestion_stock",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_inventario_ecuador_company_pais_codigo",
                schema: "public",
                table: "item_inventario_ecuador",
                columns: new[] { "company_id", "pais_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_inventario_ecuador_pais_id",
                schema: "public",
                table: "item_inventario_ecuador",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_inventario_ecuador_tipo_item",
                schema: "public",
                table: "item_inventario_ecuador",
                column: "tipo_item");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guia_genetica_ecuador_detalle");

            migrationBuilder.DropTable(
                name: "inventario_gestion_movimiento",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inventario_gestion_stock",
                schema: "public");

            migrationBuilder.DropTable(
                name: "guia_genetica_ecuador_header");

            migrationBuilder.DropTable(
                name: "item_inventario_ecuador",
                schema: "public");
        }
    }
}
