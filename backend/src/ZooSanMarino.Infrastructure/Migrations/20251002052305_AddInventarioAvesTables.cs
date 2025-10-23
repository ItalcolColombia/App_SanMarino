using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventarioAvesTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventario_aves",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    granja_id = table.Column<int>(type: "integer", nullable: false),
                    nucleo_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    galpon_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cantidad_hembras = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_machos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_mixtas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    fecha_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Activo"),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_aves", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_aves_farms_granja_id",
                        column: x => x.granja_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_aves_galpones_galpon_id",
                        column: x => x.galpon_id,
                        principalSchema: "public",
                        principalTable: "galpones",
                        principalColumn: "galpon_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_aves_lotes_lote_id",
                        column: x => x.lote_id,
                        principalSchema: "public",
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventario_aves_nucleos_nucleo_id_granja_id",
                        columns: x => new { x.nucleo_id, x.granja_id },
                        principalSchema: "public",
                        principalTable: "nucleos",
                        principalColumns: new[] { "nucleo_id", "granja_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimiento_aves",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_movimiento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fecha_movimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tipo_movimiento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    inventario_origen_id = table.Column<int>(type: "integer", nullable: true),
                    lote_origen_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    granja_origen_id = table.Column<int>(type: "integer", nullable: true),
                    nucleo_origen_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    galpon_origen_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    inventario_destino_id = table.Column<int>(type: "integer", nullable: true),
                    lote_destino_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    granja_destino_id = table.Column<int>(type: "integer", nullable: true),
                    nucleo_destino_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    galpon_destino_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cantidad_hembras = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_machos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_mixtas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    motivo_movimiento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    usuario_movimiento_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha_procesamiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_cancelacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    nucleo_origen_nucleo_id = table.Column<string>(type: "character varying(64)", nullable: true),
                    nucleo_origen_granja_id = table.Column<int>(type: "integer", nullable: true),
                    nucleo_destino_nucleo_id = table.Column<string>(type: "character varying(64)", nullable: true),
                    nucleo_destino_granja_id = table.Column<int>(type: "integer", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimiento_aves", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimiento_aves_galpones_galpon_destino_id",
                        column: x => x.galpon_destino_id,
                        principalSchema: "public",
                        principalTable: "galpones",
                        principalColumn: "galpon_id");
                    table.ForeignKey(
                        name: "fk_movimiento_aves_galpones_galpon_origen_id",
                        column: x => x.galpon_origen_id,
                        principalSchema: "public",
                        principalTable: "galpones",
                        principalColumn: "galpon_id");
                    table.ForeignKey(
                        name: "fk_movimiento_aves_granja_destino_id",
                        column: x => x.granja_destino_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimiento_aves_granja_origen_id",
                        column: x => x.granja_origen_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimiento_aves_inventario_destino_id",
                        column: x => x.inventario_destino_id,
                        principalTable: "inventario_aves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimiento_aves_inventario_origen_id",
                        column: x => x.inventario_origen_id,
                        principalTable: "inventario_aves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimiento_aves_lote_destino_id",
                        column: x => x.lote_destino_id,
                        principalSchema: "public",
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimiento_aves_lote_origen_id",
                        column: x => x.lote_origen_id,
                        principalSchema: "public",
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimiento_aves_nucleos_nucleo_destino_nucleo_id_nucleo_des",
                        columns: x => new { x.nucleo_destino_nucleo_id, x.nucleo_destino_granja_id },
                        principalSchema: "public",
                        principalTable: "nucleos",
                        principalColumns: new[] { "nucleo_id", "granja_id" });
                    table.ForeignKey(
                        name: "fk_movimiento_aves_nucleos_nucleo_origen_nucleo_id_nucleo_orig",
                        columns: x => new { x.nucleo_origen_nucleo_id, x.nucleo_origen_granja_id },
                        principalSchema: "public",
                        principalTable: "nucleos",
                        principalColumns: new[] { "nucleo_id", "granja_id" });
                });

            migrationBuilder.CreateTable(
                name: "historial_inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    inventario_id = table.Column<int>(type: "integer", nullable: false),
                    lote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fecha_cambio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tipo_cambio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    movimiento_id = table.Column<int>(type: "integer", nullable: true),
                    cantidad_hembras_anterior = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_machos_anterior = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_mixtas_anterior = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_hembras_nueva = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_machos_nueva = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_mixtas_nueva = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    granja_id = table.Column<int>(type: "integer", nullable: false),
                    nucleo_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    galpon_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    usuario_cambio_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_historial_inventario", x => x.id);
                    table.ForeignKey(
                        name: "fk_historial_inventario_galpon_id",
                        column: x => x.galpon_id,
                        principalSchema: "public",
                        principalTable: "galpones",
                        principalColumn: "galpon_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historial_inventario_granja_id",
                        column: x => x.granja_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historial_inventario_inventario_id",
                        column: x => x.inventario_id,
                        principalTable: "inventario_aves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historial_inventario_lote_id",
                        column: x => x.lote_id,
                        principalSchema: "public",
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historial_inventario_movimiento_id",
                        column: x => x.movimiento_id,
                        principalTable: "movimiento_aves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historial_inventario_nucleo_id_granja_id",
                        columns: x => new { x.nucleo_id, x.granja_id },
                        principalSchema: "public",
                        principalTable: "nucleos",
                        principalColumns: new[] { "nucleo_id", "granja_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_historial_inventario_company_id",
                table: "historial_inventario",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_historial_inventario_fecha_cambio",
                table: "historial_inventario",
                column: "fecha_cambio");

            migrationBuilder.CreateIndex(
                name: "ix_historial_inventario_galpon_id",
                table: "historial_inventario",
                column: "galpon_id");

            migrationBuilder.CreateIndex(
                name: "ix_historial_inventario_inventario_id",
                table: "historial_inventario",
                column: "inventario_id");

            migrationBuilder.CreateIndex(
                name: "ix_historial_inventario_lote_id",
                table: "historial_inventario",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_historial_inventario_movimiento_id",
                table: "historial_inventario",
                column: "movimiento_id");

            migrationBuilder.CreateIndex(
                name: "ix_historial_inventario_nucleo_id_granja_id",
                table: "historial_inventario",
                columns: new[] { "nucleo_id", "granja_id" });

            migrationBuilder.CreateIndex(
                name: "ix_historial_inventario_tipo_cambio",
                table: "historial_inventario",
                column: "tipo_cambio");

            migrationBuilder.CreateIndex(
                name: "ix_historial_inventario_ubicacion",
                table: "historial_inventario",
                columns: new[] { "granja_id", "nucleo_id", "galpon_id" });

            migrationBuilder.CreateIndex(
                name: "ix_historial_inventario_usuario_cambio_id",
                table: "historial_inventario",
                column: "usuario_cambio_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_aves_company_id",
                table: "inventario_aves",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_aves_estado",
                table: "inventario_aves",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_aves_fecha_actualizacion",
                table: "inventario_aves",
                column: "fecha_actualizacion");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_aves_galpon_id",
                table: "inventario_aves",
                column: "galpon_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_aves_lote_id",
                table: "inventario_aves",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_aves_nucleo_id_granja_id",
                table: "inventario_aves",
                columns: new[] { "nucleo_id", "granja_id" });

            migrationBuilder.CreateIndex(
                name: "ix_inventario_aves_ubicacion",
                table: "inventario_aves",
                columns: new[] { "granja_id", "nucleo_id", "galpon_id" });

            migrationBuilder.CreateIndex(
                name: "uq_inventario_aves_lote_ubicacion_company",
                table: "inventario_aves",
                columns: new[] { "lote_id", "granja_id", "nucleo_id", "galpon_id", "company_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_company_id",
                table: "movimiento_aves",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_estado",
                table: "movimiento_aves",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_fecha_movimiento",
                table: "movimiento_aves",
                column: "fecha_movimiento");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_galpon_destino_id",
                table: "movimiento_aves",
                column: "galpon_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_galpon_origen_id",
                table: "movimiento_aves",
                column: "galpon_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_granja_destino_id",
                table: "movimiento_aves",
                column: "granja_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_granjas",
                table: "movimiento_aves",
                columns: new[] { "granja_origen_id", "granja_destino_id" });

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_inventario_destino_id",
                table: "movimiento_aves",
                column: "inventario_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_inventario_origen_id",
                table: "movimiento_aves",
                column: "inventario_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_lote_destino_id",
                table: "movimiento_aves",
                column: "lote_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_lote_origen_id",
                table: "movimiento_aves",
                column: "lote_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_nucleo_destino_nucleo_id_nucleo_destino_gra",
                table: "movimiento_aves",
                columns: new[] { "nucleo_destino_nucleo_id", "nucleo_destino_granja_id" });

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_nucleo_origen_nucleo_id_nucleo_origen_granj",
                table: "movimiento_aves",
                columns: new[] { "nucleo_origen_nucleo_id", "nucleo_origen_granja_id" });

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_tipo_movimiento",
                table: "movimiento_aves",
                column: "tipo_movimiento");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_aves_usuario_movimiento_id",
                table: "movimiento_aves",
                column: "usuario_movimiento_id");

            migrationBuilder.CreateIndex(
                name: "uq_movimiento_aves_numero_movimiento",
                table: "movimiento_aves",
                column: "numero_movimiento",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historial_inventario");

            migrationBuilder.DropTable(
                name: "movimiento_aves");

            migrationBuilder.DropTable(
                name: "inventario_aves");
        }
    }
}
