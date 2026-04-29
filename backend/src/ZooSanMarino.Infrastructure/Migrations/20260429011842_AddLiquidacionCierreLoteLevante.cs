using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiquidacionCierreLoteLevante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "liquidacion_cierre_lote_levante",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_postura_levante_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_cierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    hembras_encasetadas = table.Column<int>(type: "integer", nullable: true),
                    machos_encasetados = table.Column<int>(type: "integer", nullable: true),
                    porcentaje_mortalidad_hembras = table.Column<decimal>(type: "numeric", nullable: false),
                    porcentaje_seleccion_hembras = table.Column<decimal>(type: "numeric", nullable: false),
                    porcentaje_error_sexaje_hembras = table.Column<decimal>(type: "numeric", nullable: false),
                    porcentaje_retiro_acumulado = table.Column<decimal>(type: "numeric", nullable: false),
                    consumo_alimento_real_gramos = table.Column<decimal>(type: "numeric", nullable: false),
                    consumo_alimento_guia_gramos = table.Column<decimal>(type: "numeric", nullable: true),
                    porcentaje_diferencia_consumo = table.Column<decimal>(type: "numeric", nullable: true),
                    peso_semana25real = table.Column<decimal>(type: "numeric", nullable: true),
                    peso_semana25guia = table.Column<decimal>(type: "numeric", nullable: true),
                    porcentaje_diferencia_peso = table.Column<decimal>(type: "numeric", nullable: true),
                    uniformidad_real = table.Column<decimal>(type: "numeric", nullable: true),
                    uniformidad_guia = table.Column<decimal>(type: "numeric", nullable: true),
                    porcentaje_diferencia_uniformidad = table.Column<decimal>(type: "numeric", nullable: true),
                    porcentaje_retiro_guia = table.Column<decimal>(type: "numeric", nullable: true),
                    raza_guia = table.Column<string>(type: "text", nullable: true),
                    ano_guia = table.Column<int>(type: "integer", nullable: true),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_liquidacion_cierre_lote_levante", x => x.id);
                    table.ForeignKey(
                        name: "fk_liquidacion_cierre_lote_levante_lote_postura_levante_lote_p",
                        column: x => x.lote_postura_levante_id,
                        principalSchema: "public",
                        principalTable: "lote_postura_levante",
                        principalColumn: "lote_postura_levante_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_liquidacion_cierre_lote_levante_lote_postura_levante_id",
                table: "liquidacion_cierre_lote_levante",
                column: "lote_postura_levante_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "liquidacion_cierre_lote_levante");
        }
    }
}
