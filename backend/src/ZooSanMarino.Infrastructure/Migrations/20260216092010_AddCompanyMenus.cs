using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_lote_reproductoras_lotes_lote_id",
                schema: "public",
                table: "lote_reproductoras");

            migrationBuilder.DropForeignKey(
                name: "fk_lote_seguimientos_lotes_lote_id",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropForeignKey(
                name: "fk_produccion_diaria_produccion_lotes_lote_produccion_id",
                table: "produccion_diaria");

            migrationBuilder.DropForeignKey(
                name: "fk_produccion_seguimiento_produccion_lotes_produccion_lote_id",
                table: "produccion_seguimiento");

            migrationBuilder.DropTable(
                name: "seguimiento_produccion");

            migrationBuilder.DropIndex(
                name: "IX_produccion_seguimiento_lote_fecha_unique",
                table: "produccion_seguimiento");

            migrationBuilder.DropIndex(
                name: "ix_produccion_diaria_lote_produccion_id",
                table: "produccion_diaria");

            migrationBuilder.DropIndex(
                name: "ix_master_lists_key",
                table: "master_lists");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ls_nonneg_pesos",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropIndex(
                name: "ux_catalogo_items_codigo",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropIndex(
                name: "ix_farm_product_inventory_farm_id",
                table: "farm_product_inventory");

            migrationBuilder.DropColumn(
                name: "observaciones",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "lote_produccion_id",
                table: "produccion_diaria");

            migrationBuilder.RenameTable(
                name: "farm_product_inventory",
                newName: "farm_product_inventory",
                newSchema: "public");

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_diario",
                table: "seguimiento_lote_levante",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_orp",
                table: "seguimiento_lote_levante",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_ph",
                table: "seguimiento_lote_levante",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_temperatura",
                table: "seguimiento_lote_levante",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "items_adicionales",
                table: "seguimiento_lote_levante",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "metadata",
                table: "seguimiento_lote_levante",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "produccion_lote_id",
                table: "produccion_seguimiento",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "lote_id",
                table: "produccion_seguimiento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "machos_iniciales",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "huevos_iniciales",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "hembras_iniciales",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "aves_fin_hembras",
                table: "produccion_lotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "aves_fin_machos",
                table: "produccion_lotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_fin",
                table: "produccion_lotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "peso_huevo",
                table: "produccion_diaria",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "lote_id",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<decimal>(
                name: "coeficiente_variacion",
                table: "produccion_diaria",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_diario",
                table: "produccion_diaria",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_orp",
                table: "produccion_diaria",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_ph",
                table: "produccion_diaria",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_temperatura",
                table: "produccion_diaria",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "huevo_blanco",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "huevo_deforme",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "huevo_desecho",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "huevo_doble_yema",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "huevo_limpio",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "huevo_otro",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "huevo_pequeno",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "huevo_piso",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "huevo_roto",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "huevo_sucio",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "huevo_tratado",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "metadata",
                table: "produccion_diaria",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observaciones_pesaje",
                table: "produccion_diaria",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "peso_h",
                table: "produccion_diaria",
                type: "numeric(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "peso_m",
                table: "produccion_diaria",
                type: "numeric(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sel_m",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "uniformidad",
                table: "produccion_diaria",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "alim_h",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "alim_m",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "codigo_guia_genetica",
                table: "produccion_avicola_raw",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hembras",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kcal_h",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kcal_m",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kcal_sem_h",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kcal_sem_m",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "machos",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prot_h",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prot_h_sem",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prot_m",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prot_sem_m",
                table: "produccion_avicola_raw",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ayuno",
                table: "movimiento_aves",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "conductor",
                table: "movimiento_aves",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "movimiento_aves",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "edad_aves",
                table: "movimiento_aves",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "guia_agrocalidad",
                table: "movimiento_aves",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "hora_salida",
                table: "movimiento_aves",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "peso_bruto",
                table: "movimiento_aves",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "peso_tara",
                table: "movimiento_aves",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "placa",
                table: "movimiento_aves",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "planta_destino",
                table: "movimiento_aves",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "raza",
                table: "movimiento_aves",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sellos",
                table: "movimiento_aves",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_pollos_galpon",
                table: "movimiento_aves",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "company_id",
                table: "master_lists",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_name",
                table: "master_lists",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "country_id",
                table: "master_lists",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country_name",
                table: "master_lists",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "aves_fin_hembras_prod",
                schema: "public",
                table: "lotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "aves_fin_machos_prod",
                schema: "public",
                table: "lotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ciclo_produccion",
                schema: "public",
                table: "lotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "empresa_nombre",
                schema: "public",
                table: "lotes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado_traslado",
                schema: "public",
                table: "lotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fase",
                schema: "public",
                table: "lotes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Levante");

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_fin_produccion",
                schema: "public",
                table: "lotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_inicio_produccion",
                schema: "public",
                table: "lotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "hembras_iniciales_prod",
                schema: "public",
                table: "lotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "huevos_iniciales",
                schema: "public",
                table: "lotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lote_padre_id",
                schema: "public",
                table: "lotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "machos_iniciales_prod",
                schema: "public",
                table: "lotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nucleo_p",
                schema: "public",
                table: "lotes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pais_id",
                schema: "public",
                table: "lotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pais_nombre",
                schema: "public",
                table: "lotes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_nido",
                schema: "public",
                table: "lotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                schema: "public",
                table: "lote_seguimientos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "ciclo",
                schema: "public",
                table: "lote_seguimientos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_diario",
                schema: "public",
                table: "lote_seguimientos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_orp",
                schema: "public",
                table: "lote_seguimientos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_ph",
                schema: "public",
                table: "lote_seguimientos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "consumo_agua_temperatura",
                schema: "public",
                table: "lote_seguimientos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "consumo_kg_machos",
                schema: "public",
                table: "lote_seguimientos",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "cv_h",
                schema: "public",
                table: "lote_seguimientos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "cv_m",
                schema: "public",
                table: "lote_seguimientos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "items_adicionales",
                schema: "public",
                table: "lote_seguimientos",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "metadata",
                schema: "public",
                table: "lote_seguimientos",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "peso_prom_h",
                schema: "public",
                table: "lote_seguimientos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "peso_prom_m",
                schema: "public",
                table: "lote_seguimientos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "uniformidad_h",
                schema: "public",
                table: "lote_seguimientos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "uniformidad_m",
                schema: "public",
                table: "lote_seguimientos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                schema: "public",
                table: "lote_reproductoras",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "aves_inicio_hembras",
                schema: "public",
                table: "lote_reproductoras",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "aves_inicio_machos",
                schema: "public",
                table: "lote_reproductoras",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                schema: "public",
                table: "lote_galpones",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "company_id",
                schema: "public",
                table: "farm_inventory_movements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "documento_origen",
                schema: "public",
                table: "farm_inventory_movements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_movimiento",
                schema: "public",
                table: "farm_inventory_movements",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "galpon_destino_id",
                schema: "public",
                table: "farm_inventory_movements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "item_type",
                schema: "public",
                table: "farm_inventory_movements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pais_id",
                schema: "public",
                table: "farm_inventory_movements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "tipo_entrada",
                schema: "public",
                table: "farm_inventory_movements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "company_id",
                schema: "public",
                table: "catalogo_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "item_type",
                schema: "public",
                table: "catalogo_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "alimento");

            migrationBuilder.AddColumn<int>(
                name: "pais_id",
                schema: "public",
                table: "catalogo_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "public",
                table: "farm_product_inventory",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost",
                schema: "public",
                table: "farm_product_inventory",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "unit",
                schema: "public",
                table: "farm_product_inventory",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "kg",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "responsible_user_id",
                schema: "public",
                table: "farm_product_inventory",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                schema: "public",
                table: "farm_product_inventory",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "lot_number",
                schema: "public",
                table: "farm_product_inventory",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "location",
                schema: "public",
                table: "farm_product_inventory",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "expiration_date",
                schema: "public",
                table: "farm_product_inventory",
                type: "timestamptz",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                schema: "public",
                table: "farm_product_inventory",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "active",
                schema: "public",
                table: "farm_product_inventory",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<int>(
                name: "company_id",
                schema: "public",
                table: "farm_product_inventory",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "pais_id",
                schema: "public",
                table: "farm_product_inventory",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "company_menus",
                columns: table => new
                {
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    menu_id = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_menus", x => new { x.company_id, x.menu_id });
                    table.ForeignKey(
                        name: "fk_company_menus_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_company_menus_menus_menu_id",
                        column: x => x.menu_id,
                        principalTable: "menus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_pais",
                columns: table => new
                {
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    pais_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_pais", x => new { x.company_id, x.pais_id });
                    table.ForeignKey(
                        name: "fk_company_pais_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_company_pais_paises_pais_id",
                        column: x => x.pais_id,
                        principalTable: "paises",
                        principalColumn: "pais_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_queue",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    to_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    email_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    error_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_retries = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_queue", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "historial_traslado_lote",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_original_id = table.Column<int>(type: "integer", nullable: false),
                    lote_nuevo_id = table.Column<int>(type: "integer", nullable: false),
                    granja_origen_id = table.Column<int>(type: "integer", nullable: false),
                    granja_destino_id = table.Column<int>(type: "integer", nullable: false),
                    nucleo_destino_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    galpon_destino_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_historial_traslado_lote", x => x.id);
                    table.ForeignKey(
                        name: "fk_historial_traslado_lote_farms_granja_destino_id",
                        column: x => x.granja_destino_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historial_traslado_lote_farms_granja_origen_id",
                        column: x => x.granja_origen_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historial_traslado_lote_lotes_lote_nuevo_id",
                        column: x => x.lote_nuevo_id,
                        principalSchema: "public",
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historial_traslado_lote_lotes_lote_original_id",
                        column: x => x.lote_original_id,
                        principalSchema: "public",
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lote_ave_engorde",
                schema: "public",
                columns: table => new
                {
                    lote_ave_engorde_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    granja_id = table.Column<int>(type: "integer", nullable: false),
                    nucleo_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    galpon_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    regional = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fecha_encaset = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    hembras_l = table.Column<int>(type: "integer", nullable: true),
                    machos_l = table.Column<int>(type: "integer", nullable: true),
                    peso_inicial_h = table.Column<double>(type: "double precision", nullable: true),
                    peso_inicial_m = table.Column<double>(type: "double precision", nullable: true),
                    unif_h = table.Column<double>(type: "double precision", nullable: true),
                    unif_m = table.Column<double>(type: "double precision", nullable: true),
                    mort_caja_h = table.Column<int>(type: "integer", nullable: true),
                    mort_caja_m = table.Column<int>(type: "integer", nullable: true),
                    raza = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ano_tabla_genetica = table.Column<int>(type: "integer", nullable: true),
                    linea = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    tipo_linea = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    codigo_guia_genetica = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    linea_genetica_id = table.Column<int>(type: "integer", nullable: true),
                    tecnico = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    mixtas = table.Column<int>(type: "integer", nullable: true),
                    peso_mixto = table.Column<double>(type: "double precision", nullable: true),
                    aves_encasetadas = table.Column<int>(type: "integer", nullable: true),
                    edad_inicial = table.Column<int>(type: "integer", nullable: true),
                    lote_erp = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    estado_traslado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    pais_id = table.Column<int>(type: "integer", nullable: true),
                    pais_nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    empresa_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lote_ave_engorde", x => x.lote_ave_engorde_id);
                    table.CheckConstraint("ck_lae_nonneg_counts", "(hembras_l >= 0 OR hembras_l IS NULL) AND (machos_l >= 0 OR machos_l IS NULL) AND (mixtas >= 0 OR mixtas IS NULL) AND (aves_encasetadas >= 0 OR aves_encasetadas IS NULL)");
                    table.CheckConstraint("ck_lae_nonneg_pesos", "(peso_inicial_h >= 0 OR peso_inicial_h IS NULL) AND (peso_inicial_m >= 0 OR peso_inicial_m IS NULL) AND (peso_mixto >= 0 OR peso_mixto IS NULL)");
                    table.ForeignKey(
                        name: "fk_lote_ave_engorde_farms_granja_id",
                        column: x => x.granja_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lote_ave_engorde_galpones_galpon_id",
                        column: x => x.galpon_id,
                        principalSchema: "public",
                        principalTable: "galpones",
                        principalColumn: "galpon_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lote_ave_engorde_nucleos_nucleo_id_granja_id",
                        columns: x => new { x.nucleo_id, x.granja_id },
                        principalSchema: "public",
                        principalTable: "nucleos",
                        principalColumns: new[] { "nucleo_id", "granja_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lote_etapa_levante",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_id = table.Column<int>(type: "integer", nullable: false),
                    aves_inicio_hembras = table.Column<int>(type: "integer", nullable: false),
                    aves_inicio_machos = table.Column<int>(type: "integer", nullable: false),
                    fecha_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_fin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aves_fin_hembras = table.Column<int>(type: "integer", nullable: true),
                    aves_fin_machos = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lote_etapa_levante", x => x.id);
                    table.ForeignKey(
                        name: "fk_lote_etapa_levante_lotes_lote_id",
                        column: x => x.lote_id,
                        principalSchema: "public",
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reporte_tecnico_guia",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    lote_id = table.Column<int>(type: "integer", nullable: false),
                    semana = table.Column<int>(type: "integer", nullable: false),
                    porc_mort_h_guia = table.Column<double>(type: "double precision", precision: 8, scale: 3, nullable: true),
                    retiro_h_guia = table.Column<double>(type: "double precision", precision: 8, scale: 3, nullable: true),
                    cons_ac_gr_h_guia = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: true),
                    gr_ave_dia_guia_h = table.Column<double>(type: "double precision", precision: 8, scale: 2, nullable: true),
                    incr_cons_h_guia = table.Column<double>(type: "double precision", precision: 8, scale: 2, nullable: true),
                    peso_h_guia = table.Column<double>(type: "double precision", precision: 8, scale: 2, nullable: true),
                    unif_h_guia = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: true),
                    porc_mort_m_guia = table.Column<double>(type: "double precision", precision: 8, scale: 3, nullable: true),
                    retiro_m_guia = table.Column<double>(type: "double precision", precision: 8, scale: 3, nullable: true),
                    cons_ac_gr_m_guia = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: true),
                    gr_ave_dia_guia_m = table.Column<double>(type: "double precision", precision: 8, scale: 2, nullable: true),
                    incr_cons_m_guia = table.Column<double>(type: "double precision", precision: 8, scale: 2, nullable: true),
                    peso_m_guia = table.Column<double>(type: "double precision", precision: 8, scale: 2, nullable: true),
                    unif_m_guia = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: true),
                    alim_h_guia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    kcal_sem_h_guia = table.Column<double>(type: "double precision", precision: 12, scale: 3, nullable: true),
                    prot_sem_h_guia = table.Column<double>(type: "double precision", precision: 8, scale: 3, nullable: true),
                    alim_m_guia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    kcal_sem_m_guia = table.Column<double>(type: "double precision", precision: 12, scale: 3, nullable: true),
                    prot_sem_m_guia = table.Column<double>(type: "double precision", precision: 8, scale: 3, nullable: true),
                    err_sex_ac_h = table.Column<int>(type: "integer", nullable: true),
                    err_sex_ac_m = table.Column<int>(type: "integer", nullable: true),
                    cod_guia = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    id_lote_rap = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    traslado = table.Column<int>(type: "integer", nullable: true),
                    nucleo_l = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    anon = table.Column<int>(type: "integer", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reporte_tecnico_guia", x => x.id);
                    table.ForeignKey(
                        name: "fk_reporte_tecnico_guia_lotes_lote_id",
                        column: x => x.lote_id,
                        principalSchema: "public",
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seguimiento_diario",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo_seguimiento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lote_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reproductora_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    peso_inicial = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    peso_final = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    kcal_al_h = table.Column<double>(type: "double precision", nullable: true),
                    prot_al_h = table.Column<double>(type: "double precision", nullable: true),
                    kcal_ave_h = table.Column<double>(type: "double precision", nullable: true),
                    prot_ave_h = table.Column<double>(type: "double precision", nullable: true),
                    huevo_tot = table.Column<int>(type: "integer", nullable: true),
                    huevo_inc = table.Column<int>(type: "integer", nullable: true),
                    huevo_limpio = table.Column<int>(type: "integer", nullable: true),
                    huevo_tratado = table.Column<int>(type: "integer", nullable: true),
                    huevo_sucio = table.Column<int>(type: "integer", nullable: true),
                    huevo_deforme = table.Column<int>(type: "integer", nullable: true),
                    huevo_blanco = table.Column<int>(type: "integer", nullable: true),
                    huevo_doble_yema = table.Column<int>(type: "integer", nullable: true),
                    huevo_piso = table.Column<int>(type: "integer", nullable: true),
                    huevo_pequeno = table.Column<int>(type: "integer", nullable: true),
                    huevo_roto = table.Column<int>(type: "integer", nullable: true),
                    huevo_desecho = table.Column<int>(type: "integer", nullable: true),
                    huevo_otro = table.Column<int>(type: "integer", nullable: true),
                    peso_huevo = table.Column<double>(type: "double precision", nullable: true),
                    etapa = table.Column<int>(type: "integer", nullable: true),
                    peso_h = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    peso_m = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    uniformidad = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    coeficiente_variacion = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    observaciones_pesaje = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seguimiento_diario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "traslado_huevos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('traslado_huevos_id_seq')"),
                    numero_traslado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fecha_traslado = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tipo_operacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    granja_origen_id = table.Column<int>(type: "integer", nullable: false),
                    granja_destino_id = table.Column<int>(type: "integer", nullable: true),
                    lote_destino_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tipo_destino = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    cantidad_limpio = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_tratado = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_sucio = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_deforme = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_blanco = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_doble_yema = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_piso = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_pequeno = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_roto = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_desecho = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_otro = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    usuario_traslado_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha_procesamiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_cancelacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_traslado_huevos", x => x.id);
                    table.ForeignKey(
                        name: "fk_traslado_huevos_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lote_reproductora_ave_engorde",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_ave_engorde_id = table.Column<int>(type: "integer", nullable: false),
                    reproductora_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nombre_lote = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fecha_encasetamiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    m = table.Column<int>(type: "integer", nullable: true),
                    h = table.Column<int>(type: "integer", nullable: true),
                    aves_inicio_hembras = table.Column<int>(type: "integer", nullable: true),
                    aves_inicio_machos = table.Column<int>(type: "integer", nullable: true),
                    mixtas = table.Column<int>(type: "integer", nullable: true),
                    mort_caja_h = table.Column<int>(type: "integer", nullable: true),
                    mort_caja_m = table.Column<int>(type: "integer", nullable: true),
                    unif_h = table.Column<int>(type: "integer", nullable: true),
                    unif_m = table.Column<int>(type: "integer", nullable: true),
                    peso_inicial_m = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    peso_inicial_h = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    peso_mixto = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lote_reproductora_ave_engorde", x => x.id);
                    table.ForeignKey(
                        name: "fk_lote_reproductora_ave_engorde_lote_ave_engorde_lote_ave_eng",
                        column: x => x.lote_ave_engorde_id,
                        principalSchema: "public",
                        principalTable: "lote_ave_engorde",
                        principalColumn: "lote_ave_engorde_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seguimiento_diario_aves_engorde",
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
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seguimiento_diario_aves_engorde", x => x.id);
                    table.ForeignKey(
                        name: "fk_seguimiento_diario_aves_engorde_lote_ave_engorde_lote_ave_e",
                        column: x => x.lote_ave_engorde_id,
                        principalSchema: "public",
                        principalTable: "lote_ave_engorde",
                        principalColumn: "lote_ave_engorde_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seguimiento_diario_lote_reproductora_aves_engorde",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_reproductora_ave_engorde_id = table.Column<int>(type: "integer", nullable: false),
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
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seguimiento_diario_lote_reproductora_aves_engorde", x => x.id);
                    table.ForeignKey(
                        name: "fk_seguimiento_diario_lote_reproductora_aves_engorde_lote_repr",
                        column: x => x.lote_reproductora_ave_engorde_id,
                        principalTable: "lote_reproductora_ave_engorde",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_companies_user_id",
                table: "user_companies",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_produccion_seguimiento_lote_fecha_unique",
                table: "produccion_seguimiento",
                columns: new[] { "lote_id", "fecha_registro" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_produccion_seguimiento_produccion_lote_id",
                table: "produccion_seguimiento",
                column: "produccion_lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_produccion_diaria_lote_id_fecha_registro",
                table: "produccion_diaria",
                columns: new[] { "lote_id", "fecha_registro" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_lists_key",
                table: "master_lists",
                column: "key",
                unique: true,
                filter: "company_id IS NULL AND country_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_master_lists_key_company_id_country_id",
                table: "master_lists",
                columns: new[] { "key", "company_id", "country_id" },
                unique: true,
                filter: "company_id IS NOT NULL AND country_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_lote_padre",
                schema: "public",
                table: "lotes",
                column: "lote_padre_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ls_nonneg_pesos",
                schema: "public",
                table: "lote_seguimientos",
                sql: "(peso_inicial >= 0 OR peso_inicial IS NULL) AND (peso_final >= 0 OR peso_final IS NULL) AND (consumo_alimento >= 0 OR consumo_alimento IS NULL) AND (consumo_kg_machos >= 0 OR consumo_kg_machos IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ls_uniformidad",
                schema: "public",
                table: "lote_seguimientos",
                sql: "(uniformidad_h >= 0 AND uniformidad_h <= 100 OR uniformidad_h IS NULL) AND (uniformidad_m >= 0 AND uniformidad_m <= 100 OR uniformidad_m IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_farm_inventory_movements_company_id",
                schema: "public",
                table: "farm_inventory_movements",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_inventory_movements_documento_origen",
                schema: "public",
                table: "farm_inventory_movements",
                column: "documento_origen");

            migrationBuilder.CreateIndex(
                name: "ix_farm_inventory_movements_fecha_movimiento",
                schema: "public",
                table: "farm_inventory_movements",
                column: "fecha_movimiento");

            migrationBuilder.CreateIndex(
                name: "ix_farm_inventory_movements_item_type",
                schema: "public",
                table: "farm_inventory_movements",
                column: "item_type");

            migrationBuilder.CreateIndex(
                name: "ix_farm_inventory_movements_pais_id",
                schema: "public",
                table: "farm_inventory_movements",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_inventory_movements_tipo_entrada",
                schema: "public",
                table: "farm_inventory_movements",
                column: "tipo_entrada");

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_items_company_activo",
                schema: "public",
                table: "catalogo_items",
                columns: new[] { "company_id", "activo" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_items_company_id",
                schema: "public",
                table: "catalogo_items",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_items_company_pais",
                schema: "public",
                table: "catalogo_items",
                columns: new[] { "company_id", "pais_id" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_items_company_type",
                schema: "public",
                table: "catalogo_items",
                columns: new[] { "company_id", "item_type" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_items_company_type_activo",
                schema: "public",
                table: "catalogo_items",
                columns: new[] { "company_id", "item_type", "activo" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_items_item_type",
                schema: "public",
                table: "catalogo_items",
                column: "item_type");

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_items_pais_id",
                schema: "public",
                table: "catalogo_items",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ux_catalogo_items_codigo_company_pais",
                schema: "public",
                table: "catalogo_items",
                columns: new[] { "company_id", "pais_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_farm_product_inventory_company_id",
                schema: "public",
                table: "farm_product_inventory",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_product_inventory_farm_catalog_item",
                schema: "public",
                table: "farm_product_inventory",
                columns: new[] { "farm_id", "catalog_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_farm_product_inventory_pais_id",
                schema: "public",
                table: "farm_product_inventory",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_menus_menu_id",
                table: "company_menus",
                column: "menu_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_pais_company_id",
                table: "company_pais",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_pais_pais_id",
                table: "company_pais",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "idx_email_queue_created_at",
                table: "email_queue",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_email_queue_email_type",
                table: "email_queue",
                column: "email_type");

            migrationBuilder.CreateIndex(
                name: "idx_email_queue_status",
                table: "email_queue",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_historial_traslado_lote_company_id",
                table: "historial_traslado_lote",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_historial_traslado_lote_created_at",
                table: "historial_traslado_lote",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_historial_traslado_lote_granja_destino_id",
                table: "historial_traslado_lote",
                column: "granja_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_historial_traslado_lote_granja_origen_id",
                table: "historial_traslado_lote",
                column: "granja_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_historial_traslado_lote_lote_nuevo_id",
                table: "historial_traslado_lote",
                column: "lote_nuevo_id");

            migrationBuilder.CreateIndex(
                name: "ix_historial_traslado_lote_lote_original_id",
                table: "historial_traslado_lote",
                column: "lote_original_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_ave_engorde_galpon",
                schema: "public",
                table: "lote_ave_engorde",
                column: "galpon_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_ave_engorde_granja",
                schema: "public",
                table: "lote_ave_engorde",
                column: "granja_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_ave_engorde_nucleo",
                schema: "public",
                table: "lote_ave_engorde",
                column: "nucleo_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_ave_engorde_nucleo_id_granja_id",
                schema: "public",
                table: "lote_ave_engorde",
                columns: new[] { "nucleo_id", "granja_id" });

            migrationBuilder.CreateIndex(
                name: "uq_lote_etapa_levante_lote",
                schema: "public",
                table: "lote_etapa_levante",
                column: "lote_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lote_reproductora_ave_engorde_fecha",
                table: "lote_reproductora_ave_engorde",
                column: "fecha_encasetamiento");

            migrationBuilder.CreateIndex(
                name: "ix_lote_reproductora_ave_engorde_lote",
                table: "lote_reproductora_ave_engorde",
                column: "lote_ave_engorde_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_reproductora_ave_engorde_reproductora",
                table: "lote_reproductora_ave_engorde",
                column: "reproductora_id");

            migrationBuilder.CreateIndex(
                name: "ix_reporte_tecnico_guia_lote_semana",
                schema: "public",
                table: "reporte_tecnico_guia",
                columns: new[] { "lote_id", "semana" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seguimiento_diario_aves_engorde_fecha",
                table: "seguimiento_diario_aves_engorde",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "ix_seguimiento_diario_aves_engorde_lote",
                table: "seguimiento_diario_aves_engorde",
                column: "lote_ave_engorde_id");

            migrationBuilder.CreateIndex(
                name: "ix_seg_diario_lrae_fecha",
                table: "seguimiento_diario_lote_reproductora_aves_engorde",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "ix_seg_diario_lrae_lote_reproductora",
                table: "seguimiento_diario_lote_reproductora_aves_engorde",
                column: "lote_reproductora_ave_engorde_id");

            migrationBuilder.CreateIndex(
                name: "ix_traslado_huevos_company_id",
                table: "traslado_huevos",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_traslado_huevos_estado",
                table: "traslado_huevos",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_traslado_huevos_fecha_traslado",
                table: "traslado_huevos",
                column: "fecha_traslado");

            migrationBuilder.CreateIndex(
                name: "ix_traslado_huevos_lote_id",
                table: "traslado_huevos",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_traslado_huevos_numero_traslado",
                table: "traslado_huevos",
                column: "numero_traslado",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_farm_inventory_movements_companies_company_id",
                schema: "public",
                table: "farm_inventory_movements",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_farm_inventory_movements_paises_pais_id",
                schema: "public",
                table: "farm_inventory_movements",
                column: "pais_id",
                principalTable: "paises",
                principalColumn: "pais_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_farm_product_inventory_companies_company_id",
                schema: "public",
                table: "farm_product_inventory",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_farm_product_inventory_paises_pais_id",
                schema: "public",
                table: "farm_product_inventory",
                column: "pais_id",
                principalTable: "paises",
                principalColumn: "pais_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_lotes_lotes_lote_padre_id",
                schema: "public",
                table: "lotes",
                column: "lote_padre_id",
                principalSchema: "public",
                principalTable: "lotes",
                principalColumn: "lote_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_produccion_seguimiento_lotes_lote_id",
                table: "produccion_seguimiento",
                column: "lote_id",
                principalSchema: "public",
                principalTable: "lotes",
                principalColumn: "lote_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_produccion_seguimiento_produccion_lotes_produccion_lote_id",
                table: "produccion_seguimiento",
                column: "produccion_lote_id",
                principalTable: "produccion_lotes",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_farm_inventory_movements_companies_company_id",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropForeignKey(
                name: "fk_farm_inventory_movements_paises_pais_id",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropForeignKey(
                name: "fk_farm_product_inventory_companies_company_id",
                schema: "public",
                table: "farm_product_inventory");

            migrationBuilder.DropForeignKey(
                name: "fk_farm_product_inventory_paises_pais_id",
                schema: "public",
                table: "farm_product_inventory");

            migrationBuilder.DropForeignKey(
                name: "fk_lotes_lotes_lote_padre_id",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropForeignKey(
                name: "fk_produccion_seguimiento_lotes_lote_id",
                table: "produccion_seguimiento");

            migrationBuilder.DropForeignKey(
                name: "fk_produccion_seguimiento_produccion_lotes_produccion_lote_id",
                table: "produccion_seguimiento");

            migrationBuilder.DropTable(
                name: "company_menus");

            migrationBuilder.DropTable(
                name: "company_pais");

            migrationBuilder.DropTable(
                name: "email_queue");

            migrationBuilder.DropTable(
                name: "historial_traslado_lote");

            migrationBuilder.DropTable(
                name: "lote_etapa_levante",
                schema: "public");

            migrationBuilder.DropTable(
                name: "reporte_tecnico_guia",
                schema: "public");

            migrationBuilder.DropTable(
                name: "seguimiento_diario",
                schema: "public");

            migrationBuilder.DropTable(
                name: "seguimiento_diario_aves_engorde");

            migrationBuilder.DropTable(
                name: "seguimiento_diario_lote_reproductora_aves_engorde");

            migrationBuilder.DropTable(
                name: "traslado_huevos");

            migrationBuilder.DropTable(
                name: "lote_reproductora_ave_engorde");

            migrationBuilder.DropTable(
                name: "lote_ave_engorde",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_user_companies_user_id",
                table: "user_companies");

            migrationBuilder.DropIndex(
                name: "IX_produccion_seguimiento_lote_fecha_unique",
                table: "produccion_seguimiento");

            migrationBuilder.DropIndex(
                name: "ix_produccion_seguimiento_produccion_lote_id",
                table: "produccion_seguimiento");

            migrationBuilder.DropIndex(
                name: "ix_produccion_diaria_lote_id_fecha_registro",
                table: "produccion_diaria");

            migrationBuilder.DropIndex(
                name: "ix_master_lists_key",
                table: "master_lists");

            migrationBuilder.DropIndex(
                name: "ix_master_lists_key_company_id_country_id",
                table: "master_lists");

            migrationBuilder.DropIndex(
                name: "ix_lote_padre",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ls_nonneg_pesos",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ls_uniformidad",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropIndex(
                name: "ix_farm_inventory_movements_company_id",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropIndex(
                name: "ix_farm_inventory_movements_documento_origen",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropIndex(
                name: "ix_farm_inventory_movements_fecha_movimiento",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropIndex(
                name: "ix_farm_inventory_movements_item_type",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropIndex(
                name: "ix_farm_inventory_movements_pais_id",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropIndex(
                name: "ix_farm_inventory_movements_tipo_entrada",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropIndex(
                name: "ix_catalogo_items_company_activo",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropIndex(
                name: "ix_catalogo_items_company_id",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropIndex(
                name: "ix_catalogo_items_company_pais",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropIndex(
                name: "ix_catalogo_items_company_type",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropIndex(
                name: "ix_catalogo_items_company_type_activo",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropIndex(
                name: "ix_catalogo_items_item_type",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropIndex(
                name: "ix_catalogo_items_pais_id",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropIndex(
                name: "ux_catalogo_items_codigo_company_pais",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropIndex(
                name: "ix_farm_product_inventory_company_id",
                schema: "public",
                table: "farm_product_inventory");

            migrationBuilder.DropIndex(
                name: "ix_farm_product_inventory_farm_catalog_item",
                schema: "public",
                table: "farm_product_inventory");

            migrationBuilder.DropIndex(
                name: "ix_farm_product_inventory_pais_id",
                schema: "public",
                table: "farm_product_inventory");

            migrationBuilder.DropColumn(
                name: "consumo_agua_diario",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "consumo_agua_orp",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "consumo_agua_ph",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "consumo_agua_temperatura",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "items_adicionales",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "metadata",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "lote_id",
                table: "produccion_seguimiento");

            migrationBuilder.DropColumn(
                name: "aves_fin_hembras",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "aves_fin_machos",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "fecha_fin",
                table: "produccion_lotes");

            migrationBuilder.DropColumn(
                name: "coeficiente_variacion",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "consumo_agua_diario",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "consumo_agua_orp",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "consumo_agua_ph",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "consumo_agua_temperatura",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "huevo_blanco",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "huevo_deforme",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "huevo_desecho",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "huevo_doble_yema",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "huevo_limpio",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "huevo_otro",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "huevo_pequeno",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "huevo_piso",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "huevo_roto",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "huevo_sucio",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "huevo_tratado",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "metadata",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "observaciones_pesaje",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "peso_h",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "peso_m",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "sel_m",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "uniformidad",
                table: "produccion_diaria");

            migrationBuilder.DropColumn(
                name: "alim_h",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "alim_m",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "codigo_guia_genetica",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "hembras",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "kcal_h",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "kcal_m",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "kcal_sem_h",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "kcal_sem_m",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "machos",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "prot_h",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "prot_h_sem",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "prot_m",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "prot_sem_m",
                table: "produccion_avicola_raw");

            migrationBuilder.DropColumn(
                name: "ayuno",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "conductor",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "edad_aves",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "guia_agrocalidad",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "hora_salida",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "peso_bruto",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "peso_tara",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "placa",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "planta_destino",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "raza",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "sellos",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "total_pollos_galpon",
                table: "movimiento_aves");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "master_lists");

            migrationBuilder.DropColumn(
                name: "company_name",
                table: "master_lists");

            migrationBuilder.DropColumn(
                name: "country_id",
                table: "master_lists");

            migrationBuilder.DropColumn(
                name: "country_name",
                table: "master_lists");

            migrationBuilder.DropColumn(
                name: "aves_fin_hembras_prod",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "aves_fin_machos_prod",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "ciclo_produccion",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "empresa_nombre",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "estado_traslado",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "fase",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "fecha_fin_produccion",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "fecha_inicio_produccion",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "hembras_iniciales_prod",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "huevos_iniciales",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "lote_padre_id",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "machos_iniciales_prod",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "nucleo_p",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "pais_id",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "pais_nombre",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "tipo_nido",
                schema: "public",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "ciclo",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "consumo_agua_diario",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "consumo_agua_orp",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "consumo_agua_ph",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "consumo_agua_temperatura",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "consumo_kg_machos",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "cv_h",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "cv_m",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "items_adicionales",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "metadata",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "peso_prom_h",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "peso_prom_m",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "uniformidad_h",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "uniformidad_m",
                schema: "public",
                table: "lote_seguimientos");

            migrationBuilder.DropColumn(
                name: "aves_inicio_hembras",
                schema: "public",
                table: "lote_reproductoras");

            migrationBuilder.DropColumn(
                name: "aves_inicio_machos",
                schema: "public",
                table: "lote_reproductoras");

            migrationBuilder.DropColumn(
                name: "company_id",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropColumn(
                name: "documento_origen",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropColumn(
                name: "fecha_movimiento",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropColumn(
                name: "galpon_destino_id",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropColumn(
                name: "item_type",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropColumn(
                name: "pais_id",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropColumn(
                name: "tipo_entrada",
                schema: "public",
                table: "farm_inventory_movements");

            migrationBuilder.DropColumn(
                name: "company_id",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropColumn(
                name: "item_type",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropColumn(
                name: "pais_id",
                schema: "public",
                table: "catalogo_items");

            migrationBuilder.DropColumn(
                name: "company_id",
                schema: "public",
                table: "farm_product_inventory");

            migrationBuilder.DropColumn(
                name: "pais_id",
                schema: "public",
                table: "farm_product_inventory");

            migrationBuilder.RenameTable(
                name: "farm_product_inventory",
                schema: "public",
                newName: "farm_product_inventory");

            migrationBuilder.AlterColumn<int>(
                name: "produccion_lote_id",
                table: "produccion_seguimiento",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "machos_iniciales",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "huevos_iniciales",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "hembras_iniciales",
                table: "produccion_lotes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "observaciones",
                table: "produccion_lotes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "peso_huevo",
                table: "produccion_diaria",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<string>(
                name: "lote_id",
                table: "produccion_diaria",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "produccion_diaria",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddColumn<int>(
                name: "lote_produccion_id",
                table: "produccion_diaria",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "lote_id",
                schema: "public",
                table: "lote_seguimientos",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<int>(
                name: "lote_id",
                schema: "public",
                table: "lote_reproductoras",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<int>(
                name: "lote_id",
                schema: "public",
                table: "lote_galpones",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "farm_product_inventory",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamptz",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost",
                table: "farm_product_inventory",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "unit",
                table: "farm_product_inventory",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "kg");

            migrationBuilder.AlterColumn<string>(
                name: "responsible_user_id",
                table: "farm_product_inventory",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "farm_product_inventory",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<string>(
                name: "lot_number",
                table: "farm_product_inventory",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "location",
                table: "farm_product_inventory",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "expiration_date",
                table: "farm_product_inventory",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "farm_product_inventory",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamptz",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "active",
                table: "farm_product_inventory",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.CreateTable(
                name: "seguimiento_produccion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    lote_id = table.Column<int>(type: "integer", nullable: false),
                    cons_kg_h = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    cons_kg_m = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    etapa = table.Column<int>(type: "integer", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    huevo_inc = table.Column<int>(type: "integer", nullable: false),
                    huevo_tot = table.Column<int>(type: "integer", nullable: false),
                    mortalidad_h = table.Column<int>(type: "integer", nullable: false),
                    mortalidad_m = table.Column<int>(type: "integer", nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    peso_huevo = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    sel_h = table.Column<int>(type: "integer", nullable: false),
                    tipo_alimento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_produccion_seguimiento_lote_fecha_unique",
                table: "produccion_seguimiento",
                columns: new[] { "produccion_lote_id", "fecha_registro" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_produccion_diaria_lote_produccion_id",
                table: "produccion_diaria",
                column: "lote_produccion_id");

            migrationBuilder.CreateIndex(
                name: "ix_master_lists_key",
                table: "master_lists",
                column: "key",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ls_nonneg_pesos",
                schema: "public",
                table: "lote_seguimientos",
                sql: "(peso_inicial >= 0 OR peso_inicial IS NULL) AND (peso_final >= 0 OR peso_final IS NULL) AND (consumo_alimento >= 0 OR consumo_alimento IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ux_catalogo_items_codigo",
                schema: "public",
                table: "catalogo_items",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_farm_product_inventory_farm_id",
                table: "farm_product_inventory",
                column: "farm_id");

            migrationBuilder.CreateIndex(
                name: "ix_seguimiento_produccion_lote_id_fecha",
                table: "seguimiento_produccion",
                columns: new[] { "lote_id", "fecha" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_lote_reproductoras_lotes_lote_id",
                schema: "public",
                table: "lote_reproductoras",
                column: "lote_id",
                principalSchema: "public",
                principalTable: "lotes",
                principalColumn: "lote_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lote_seguimientos_lotes_lote_id",
                schema: "public",
                table: "lote_seguimientos",
                column: "lote_id",
                principalSchema: "public",
                principalTable: "lotes",
                principalColumn: "lote_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_produccion_diaria_produccion_lotes_lote_produccion_id",
                table: "produccion_diaria",
                column: "lote_produccion_id",
                principalTable: "produccion_lotes",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_produccion_seguimiento_produccion_lotes_produccion_lote_id",
                table: "produccion_seguimiento",
                column: "produccion_lote_id",
                principalTable: "produccion_lotes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
