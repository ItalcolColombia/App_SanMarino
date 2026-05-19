using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitSeguimientoDiarioAvesEngordeByCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "seguimiento_diario_aves_engorde_ecuador",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_ave_engorde_id = table.Column<int>(type: "integer", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    mortalidad_hembras = table.Column<int>(type: "integer", nullable: true),
                    mortalidad_machos = table.Column<int>(type: "integer", nullable: true),
                    sel_h = table.Column<int>(type: "integer", nullable: true),
                    sel_m = table.Column<int>(type: "integer", nullable: true),
                    error_sexaje_hembras = table.Column<int>(type: "integer", nullable: true),
                    error_sexaje_machos = table.Column<int>(type: "integer", nullable: true),
                    consumo_kg_hembras = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    consumo_kg_machos = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    tipo_alimento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    ciclo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    peso_prom_hembras = table.Column<double>(type: "double precision", nullable: true),
                    peso_prom_machos = table.Column<double>(type: "double precision", nullable: true),
                    uniformidad_hembras = table.Column<double>(type: "double precision", nullable: true),
                    uniformidad_machos = table.Column<double>(type: "double precision", nullable: true),
                    cv_hembras = table.Column<double>(type: "double precision", nullable: true),
                    cv_machos = table.Column<double>(type: "double precision", nullable: true),
                    consumo_agua_diario = table.Column<double>(type: "double precision", nullable: true),
                    consumo_agua_ph = table.Column<double>(type: "double precision", nullable: true),
                    consumo_agua_orp = table.Column<double>(type: "double precision", nullable: true),
                    consumo_agua_temperatura = table.Column<double>(type: "double precision", nullable: true),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    items_adicionales = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    kcal_al_h = table.Column<double>(type: "double precision", nullable: true),
                    prot_al_h = table.Column<double>(type: "double precision", nullable: true),
                    kcal_ave_h = table.Column<double>(type: "double precision", nullable: true),
                    prot_ave_h = table.Column<double>(type: "double precision", nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    saldo_alimento_kg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    historico_consumo_alimento = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seguimiento_diario_aves_engorde_ecuador", x => x.id);
                    table.ForeignKey(
                        name: "fk_seguimiento_diario_aves_engorde_ecuador_lote_ave_engorde_lo",
                        column: x => x.lote_ave_engorde_id,
                        principalSchema: "public",
                        principalTable: "lote_ave_engorde",
                        principalColumn: "lote_ave_engorde_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seguimiento_diario_aves_engorde_panama",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_ave_engorde_id = table.Column<int>(type: "integer", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    mortalidad_hembras = table.Column<int>(type: "integer", nullable: true),
                    mortalidad_machos = table.Column<int>(type: "integer", nullable: true),
                    sel_h = table.Column<int>(type: "integer", nullable: true),
                    sel_m = table.Column<int>(type: "integer", nullable: true),
                    error_sexaje_hembras = table.Column<int>(type: "integer", nullable: true),
                    error_sexaje_machos = table.Column<int>(type: "integer", nullable: true),
                    consumo_kg_hembras = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    consumo_kg_machos = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    tipo_alimento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    ciclo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    peso_prom_hembras = table.Column<double>(type: "double precision", nullable: true),
                    peso_prom_machos = table.Column<double>(type: "double precision", nullable: true),
                    uniformidad_hembras = table.Column<double>(type: "double precision", nullable: true),
                    uniformidad_machos = table.Column<double>(type: "double precision", nullable: true),
                    cv_hembras = table.Column<double>(type: "double precision", nullable: true),
                    cv_machos = table.Column<double>(type: "double precision", nullable: true),
                    consumo_agua_diario = table.Column<double>(type: "double precision", nullable: true),
                    consumo_agua_ph = table.Column<double>(type: "double precision", nullable: true),
                    consumo_agua_orp = table.Column<double>(type: "double precision", nullable: true),
                    consumo_agua_temperatura = table.Column<double>(type: "double precision", nullable: true),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    items_adicionales = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    kcal_al_h = table.Column<double>(type: "double precision", nullable: true),
                    prot_al_h = table.Column<double>(type: "double precision", nullable: true),
                    kcal_ave_h = table.Column<double>(type: "double precision", nullable: true),
                    prot_ave_h = table.Column<double>(type: "double precision", nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    saldo_alimento_kg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    historico_consumo_alimento = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    qq_mixtas = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    qq_hembras = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    qq_machos = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seguimiento_diario_aves_engorde_panama", x => x.id);
                    table.ForeignKey(
                        name: "fk_seguimiento_diario_aves_engorde_panama_lote_ave_engorde_lot",
                        column: x => x.lote_ave_engorde_id,
                        principalSchema: "public",
                        principalTable: "lote_ave_engorde",
                        principalColumn: "lote_ave_engorde_id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Migrate data from old table to Ecuador table (safe: only runs if old table exists)
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'seguimiento_diario_aves_engorde'
    ) THEN
        INSERT INTO seguimiento_diario_aves_engorde_ecuador (
            id, lote_ave_engorde_id, fecha, mortalidad_hembras, mortalidad_machos,
            sel_h, sel_m, error_sexaje_hembras, error_sexaje_machos,
            consumo_kg_hembras, consumo_kg_machos, tipo_alimento, observaciones, ciclo,
            peso_prom_hembras, peso_prom_machos, uniformidad_hembras, uniformidad_machos,
            cv_hembras, cv_machos, consumo_agua_diario, consumo_agua_ph, consumo_agua_orp,
            consumo_agua_temperatura, metadata, items_adicionales,
            kcal_al_h, prot_al_h, kcal_ave_h, prot_ave_h,
            created_by_user_id, created_at, updated_at, saldo_alimento_kg, historico_consumo_alimento
        )
        SELECT
            id, lote_ave_engorde_id, fecha, mortalidad_hembras, mortalidad_machos,
            sel_h, sel_m, error_sexaje_hembras, error_sexaje_machos,
            consumo_kg_hembras, consumo_kg_machos, tipo_alimento, observaciones, ciclo,
            peso_prom_hembras, peso_prom_machos, uniformidad_hembras, uniformidad_machos,
            cv_hembras, cv_machos, consumo_agua_diario, consumo_agua_ph, consumo_agua_orp,
            consumo_agua_temperatura, metadata, items_adicionales,
            kcal_al_h, prot_al_h, kcal_ave_h, prot_ave_h,
            created_by_user_id, created_at, updated_at, saldo_alimento_kg, historico_consumo_alimento
        FROM seguimiento_diario_aves_engorde;

        -- Reset sequence to avoid ID collisions after data copy
        PERFORM setval(
            pg_get_serial_sequence('seguimiento_diario_aves_engorde_ecuador', 'id'),
            COALESCE((SELECT MAX(id) FROM seguimiento_diario_aves_engorde_ecuador), 0) + 1,
            false
        );

        DROP TABLE seguimiento_diario_aves_engorde CASCADE;
    END IF;
END $$;");

            migrationBuilder.CreateIndex(
                name: "ix_seguimiento_diario_aves_engorde_ecuador_fecha",
                table: "seguimiento_diario_aves_engorde_ecuador",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "ix_seguimiento_diario_aves_engorde_ecuador_lote",
                table: "seguimiento_diario_aves_engorde_ecuador",
                column: "lote_ave_engorde_id");

            migrationBuilder.CreateIndex(
                name: "ix_seguimiento_diario_aves_engorde_panama_fecha",
                table: "seguimiento_diario_aves_engorde_panama",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "ix_seguimiento_diario_aves_engorde_panama_lote",
                table: "seguimiento_diario_aves_engorde_panama",
                column: "lote_ave_engorde_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seguimiento_diario_aves_engorde_ecuador");

            migrationBuilder.DropTable(
                name: "seguimiento_diario_aves_engorde_panama");
        }
    }
}
