using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVacunacionModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vacunacion_configuracion",
                schema: "public",
                columns: table => new
                {
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    pais_id = table.Column<int>(type: "integer", nullable: false),
                    dias_umbral_incumplido = table.Column<int>(type: "integer", nullable: false, defaultValue: 14),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacunacion_configuracion", x => new { x.company_id, x.pais_id });
                    table.CheckConstraint("ck_vc_umbral_positivo", "dias_umbral_incumplido > 0");
                });

            migrationBuilder.CreateTable(
                name: "vacunacion_cronograma_item",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pais_id = table.Column<int>(type: "integer", nullable: true),
                    linea_productiva = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lote_postura_levante_id = table.Column<int>(type: "integer", nullable: true),
                    lote_postura_produccion_id = table.Column<int>(type: "integer", nullable: true),
                    lote_ave_engorde_id = table.Column<int>(type: "integer", nullable: true),
                    granja_id = table.Column<int>(type: "integer", nullable: false),
                    nucleo_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    galpon_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    item_inventario_id = table.Column<int>(type: "integer", nullable: false),
                    unidad_objetivo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    valor_objetivo = table.Column<int>(type: "integer", nullable: true),
                    fecha_objetivo = table.Column<DateTime>(type: "date", nullable: true),
                    rango_dias_antes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rango_dias_despues = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    orden = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notas = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacunacion_cronograma_item", x => x.id);
                    table.CheckConstraint("ck_vci_linea_valida", "linea_productiva IN ('Levante', 'Produccion', 'Engorde')");
                    table.CheckConstraint("ck_vci_objetivo_coherente", "(unidad_objetivo IN ('Semana','Dia') AND valor_objetivo IS NOT NULL AND fecha_objetivo IS NULL) OR (unidad_objetivo = 'Fecha' AND fecha_objetivo IS NOT NULL AND valor_objetivo IS NULL)");
                    table.CheckConstraint("ck_vci_rango_nonneg", "rango_dias_antes >= 0 AND rango_dias_despues >= 0");
                    table.CheckConstraint("ck_vci_un_solo_lote", "(CASE WHEN lote_postura_levante_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN lote_postura_produccion_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN lote_ave_engorde_id IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.CheckConstraint("ck_vci_unidad_valida", "unidad_objetivo IN ('Semana', 'Dia', 'Fecha')");
                    table.ForeignKey(
                        name: "fk_vacunacion_cronograma_item_farms_granja_id",
                        column: x => x.granja_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vacunacion_cronograma_item_galpones_galpon_id",
                        column: x => x.galpon_id,
                        principalSchema: "public",
                        principalTable: "galpones",
                        principalColumn: "galpon_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vacunacion_cronograma_item_item_inventario_item_inventario_",
                        column: x => x.item_inventario_id,
                        principalSchema: "public",
                        principalTable: "item_inventario_ecuador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vacunacion_cronograma_item_lote_ave_engorde_lote_ave_engord",
                        column: x => x.lote_ave_engorde_id,
                        principalSchema: "public",
                        principalTable: "lote_ave_engorde",
                        principalColumn: "lote_ave_engorde_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vacunacion_cronograma_item_lote_postura_levante_lote_postur",
                        column: x => x.lote_postura_levante_id,
                        principalSchema: "public",
                        principalTable: "lote_postura_levante",
                        principalColumn: "lote_postura_levante_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vacunacion_cronograma_item_lote_postura_produccion_lote_pos",
                        column: x => x.lote_postura_produccion_id,
                        principalSchema: "public",
                        principalTable: "lote_postura_produccion",
                        principalColumn: "lote_postura_produccion_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vacunacion_cronograma_item_nucleos_nucleo_id_granja_id",
                        columns: x => new { x.nucleo_id, x.granja_id },
                        principalSchema: "public",
                        principalTable: "nucleos",
                        principalColumns: new[] { "nucleo_id", "granja_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vacunacion_registro_aplicacion",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pais_id = table.Column<int>(type: "integer", nullable: true),
                    vacunacion_cronograma_item_id = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_aplicacion = table.Column<DateTime>(type: "date", nullable: true),
                    dias_desviacion = table.Column<int>(type: "integer", nullable: true),
                    incumplido = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    motivo_descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    usuario_registra_id = table.Column<int>(type: "integer", nullable: false),
                    aplicado_por_user_id = table.Column<int>(type: "integer", nullable: true),
                    aplicado_por_nombre_libre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacunacion_registro_aplicacion", x => x.id);
                    table.CheckConstraint("ck_vra_aplicado_por_coherente", "estado = 'Pendiente' OR estado = 'NoAplicado' OR ((aplicado_por_user_id IS NOT NULL) <> (aplicado_por_nombre_libre IS NOT NULL AND length(trim(aplicado_por_nombre_libre)) > 0))");
                    table.CheckConstraint("ck_vra_estado_valido", "estado IN ('Pendiente', 'Aplicado', 'AplicadoTardio', 'AplicadoAdelantado', 'NoAplicado')");
                    table.CheckConstraint("ck_vra_motivo_obligatorio", "estado NOT IN ('NoAplicado', 'AplicadoTardio', 'AplicadoAdelantado') OR (motivo_descripcion IS NOT NULL AND length(trim(motivo_descripcion)) > 0)");
                    table.ForeignKey(
                        name: "fk_vacunacion_registro_aplicacion_vacunacion_cronograma_item_v",
                        column: x => x.vacunacion_cronograma_item_id,
                        principalSchema: "public",
                        principalTable: "vacunacion_cronograma_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vacunacion_cronograma_item_engorde",
                schema: "public",
                table: "vacunacion_cronograma_item",
                column: "lote_ave_engorde_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacunacion_cronograma_item_galpon_id",
                schema: "public",
                table: "vacunacion_cronograma_item",
                column: "galpon_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacunacion_cronograma_item_granja",
                schema: "public",
                table: "vacunacion_cronograma_item",
                column: "granja_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacunacion_cronograma_item_item_inventario",
                schema: "public",
                table: "vacunacion_cronograma_item",
                column: "item_inventario_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacunacion_cronograma_item_levante",
                schema: "public",
                table: "vacunacion_cronograma_item",
                column: "lote_postura_levante_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacunacion_cronograma_item_nucleo_id_granja_id",
                schema: "public",
                table: "vacunacion_cronograma_item",
                columns: new[] { "nucleo_id", "granja_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vacunacion_cronograma_item_produccion",
                schema: "public",
                table: "vacunacion_cronograma_item",
                column: "lote_postura_produccion_id");

            migrationBuilder.CreateIndex(
                name: "ux_vacunacion_registro_aplicacion_item",
                schema: "public",
                table: "vacunacion_registro_aplicacion",
                column: "vacunacion_cronograma_item_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vacunacion_configuracion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "vacunacion_registro_aplicacion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "vacunacion_cronograma_item",
                schema: "public");
        }
    }
}
