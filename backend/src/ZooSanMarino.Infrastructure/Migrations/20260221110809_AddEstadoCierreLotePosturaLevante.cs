using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadoCierreLotePosturaLevante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at",
                table: "menus");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "menus");

            migrationBuilder.AddColumn<int>(
                name: "parent_menu_id",
                table: "company_menus",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "company_menus",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "lote_postura_levante",
                schema: "public",
                columns: table => new
                {
                    lote_postura_levante_id = table.Column<int>(type: "integer", nullable: false)
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
                    lote_id = table.Column<int>(type: "integer", nullable: true),
                    lote_padre_id = table.Column<int>(type: "integer", nullable: true),
                    lote_postura_levante_padre_id = table.Column<int>(type: "integer", nullable: true),
                    aves_h_inicial = table.Column<int>(type: "integer", nullable: true),
                    aves_m_inicial = table.Column<int>(type: "integer", nullable: true),
                    aves_h_actual = table.Column<int>(type: "integer", nullable: true),
                    aves_m_actual = table.Column<int>(type: "integer", nullable: true),
                    empresa_id = table.Column<int>(type: "integer", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: true),
                    estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    etapa = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    edad = table.Column<int>(type: "integer", nullable: true),
                    estado_cierre = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lote_postura_levante", x => x.lote_postura_levante_id);
                    table.CheckConstraint("ck_lpl_nonneg_counts", "(hembras_l >= 0 OR hembras_l IS NULL) AND (machos_l >= 0 OR machos_l IS NULL) AND (mixtas >= 0 OR mixtas IS NULL) AND (aves_encasetadas >= 0 OR aves_encasetadas IS NULL)");
                    table.CheckConstraint("ck_lpl_nonneg_pesos", "(peso_inicial_h >= 0 OR peso_inicial_h IS NULL) AND (peso_inicial_m >= 0 OR peso_inicial_m IS NULL) AND (peso_mixto >= 0 OR peso_mixto IS NULL)");
                    table.ForeignKey(
                        name: "fk_lote_postura_levante_farms_granja_id",
                        column: x => x.granja_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lote_postura_levante_galpones_galpon_id",
                        column: x => x.galpon_id,
                        principalSchema: "public",
                        principalTable: "galpones",
                        principalColumn: "galpon_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lote_postura_levante_lote_postura_levante_lote_postura_leva",
                        column: x => x.lote_postura_levante_padre_id,
                        principalSchema: "public",
                        principalTable: "lote_postura_levante",
                        principalColumn: "lote_postura_levante_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lote_postura_levante_lotes_lote_id",
                        column: x => x.lote_id,
                        principalSchema: "public",
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lote_postura_levante_nucleos_nucleo_id_granja_id",
                        columns: x => new { x.nucleo_id, x.granja_id },
                        principalSchema: "public",
                        principalTable: "nucleos",
                        principalColumns: new[] { "nucleo_id", "granja_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimiento_pollo_engorde",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_movimiento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fecha_movimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tipo_movimiento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    lote_ave_engorde_origen_id = table.Column<int>(type: "integer", nullable: true),
                    lote_reproductora_ave_engorde_origen_id = table.Column<int>(type: "integer", nullable: true),
                    granja_origen_id = table.Column<int>(type: "integer", nullable: true),
                    nucleo_origen_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    galpon_origen_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    lote_ave_engorde_destino_id = table.Column<int>(type: "integer", nullable: true),
                    lote_reproductora_ave_engorde_destino_id = table.Column<int>(type: "integer", nullable: true),
                    granja_destino_id = table.Column<int>(type: "integer", nullable: true),
                    nucleo_destino_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    galpon_destino_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    planta_destino = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cantidad_hembras = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_machos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cantidad_mixtas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    motivo_movimiento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    usuario_movimiento_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha_procesamiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_cancelacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    numero_despacho = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    edad_aves = table.Column<int>(type: "integer", nullable: true),
                    total_pollos_galpon = table.Column<int>(type: "integer", nullable: true),
                    raza = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    placa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    hora_salida = table.Column<TimeOnly>(type: "time", nullable: true),
                    guia_agrocalidad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sellos = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ayuno = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    conductor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    peso_bruto = table.Column<double>(type: "double precision", nullable: true),
                    peso_tara = table.Column<double>(type: "double precision", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimiento_pollo_engorde", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimiento_pollo_engorde_farms_granja_destino_id",
                        column: x => x.granja_destino_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimiento_pollo_engorde_farms_granja_origen_id",
                        column: x => x.granja_origen_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimiento_pollo_engorde_lote_ave_engorde_lote_ave_engorde_",
                        column: x => x.lote_ave_engorde_destino_id,
                        principalSchema: "public",
                        principalTable: "lote_ave_engorde",
                        principalColumn: "lote_ave_engorde_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimiento_pollo_engorde_lote_ave_engorde_lote_ave_engorde_1",
                        column: x => x.lote_ave_engorde_origen_id,
                        principalSchema: "public",
                        principalTable: "lote_ave_engorde",
                        principalColumn: "lote_ave_engorde_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimiento_pollo_engorde_lote_reproductora_ave_engorde_lote",
                        column: x => x.lote_reproductora_ave_engorde_destino_id,
                        principalTable: "lote_reproductora_ave_engorde",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimiento_pollo_engorde_lote_reproductora_ave_engorde_lote1",
                        column: x => x.lote_reproductora_ave_engorde_origen_id,
                        principalTable: "lote_reproductora_ave_engorde",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lote_postura_produccion",
                schema: "public",
                columns: table => new
                {
                    lote_postura_produccion_id = table.Column<int>(type: "integer", nullable: false)
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
                    fecha_inicio_produccion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    hembras_iniciales_prod = table.Column<int>(type: "integer", nullable: true),
                    machos_iniciales_prod = table.Column<int>(type: "integer", nullable: true),
                    huevos_iniciales = table.Column<int>(type: "integer", nullable: true),
                    tipo_nido = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    nucleo_p = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ciclo_produccion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fecha_fin_produccion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aves_fin_hembras_prod = table.Column<int>(type: "integer", nullable: true),
                    aves_fin_machos_prod = table.Column<int>(type: "integer", nullable: true),
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
                    peso_huevo = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    lote_id = table.Column<int>(type: "integer", nullable: true),
                    lote_padre_id = table.Column<int>(type: "integer", nullable: true),
                    lote_postura_levante_id = table.Column<int>(type: "integer", nullable: true),
                    aves_h_inicial = table.Column<int>(type: "integer", nullable: true),
                    aves_m_inicial = table.Column<int>(type: "integer", nullable: true),
                    aves_h_actual = table.Column<int>(type: "integer", nullable: true),
                    aves_m_actual = table.Column<int>(type: "integer", nullable: true),
                    empresa_id = table.Column<int>(type: "integer", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: true),
                    estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    etapa = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    edad = table.Column<int>(type: "integer", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lote_postura_produccion", x => x.lote_postura_produccion_id);
                    table.CheckConstraint("ck_lpp_nonneg_counts", "(hembras_l >= 0 OR hembras_l IS NULL) AND (machos_l >= 0 OR machos_l IS NULL) AND (mixtas >= 0 OR mixtas IS NULL) AND (aves_encasetadas >= 0 OR aves_encasetadas IS NULL)");
                    table.CheckConstraint("ck_lpp_nonneg_pesos", "(peso_inicial_h >= 0 OR peso_inicial_h IS NULL) AND (peso_inicial_m >= 0 OR peso_inicial_m IS NULL) AND (peso_mixto >= 0 OR peso_mixto IS NULL)");
                    table.ForeignKey(
                        name: "fk_lote_postura_produccion_farms_granja_id",
                        column: x => x.granja_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lote_postura_produccion_galpones_galpon_id",
                        column: x => x.galpon_id,
                        principalSchema: "public",
                        principalTable: "galpones",
                        principalColumn: "galpon_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lote_postura_produccion_lote_postura_levante_lote_postura_l",
                        column: x => x.lote_postura_levante_id,
                        principalSchema: "public",
                        principalTable: "lote_postura_levante",
                        principalColumn: "lote_postura_levante_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lote_postura_produccion_lotes_lote_id",
                        column: x => x.lote_id,
                        principalSchema: "public",
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lote_postura_produccion_nucleos_nucleo_id_granja_id",
                        columns: x => new { x.nucleo_id, x.granja_id },
                        principalSchema: "public",
                        principalTable: "nucleos",
                        principalColumns: new[] { "nucleo_id", "granja_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "historial_lote_pollo_engorde",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    tipo_lote = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    lote_ave_engorde_id = table.Column<int>(type: "integer", nullable: true),
                    lote_reproductora_ave_engorde_id = table.Column<int>(type: "integer", nullable: true),
                    tipo_registro = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValue: "Inicio"),
                    aves_hembras = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    aves_machos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    aves_mixtas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    fecha_registro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    movimiento_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_historial_lote_pollo_engorde", x => x.id);
                    table.ForeignKey(
                        name: "fk_historial_lote_pollo_engorde_lote_ave_engorde_lote_ave_engo",
                        column: x => x.lote_ave_engorde_id,
                        principalSchema: "public",
                        principalTable: "lote_ave_engorde",
                        principalColumn: "lote_ave_engorde_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_historial_lote_pollo_engorde_lote_reproductora_ave_engorde_",
                        column: x => x.lote_reproductora_ave_engorde_id,
                        principalTable: "lote_reproductora_ave_engorde",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_historial_lote_pollo_engorde_movimiento_pollo_engorde_movim",
                        column: x => x.movimiento_id,
                        principalTable: "movimiento_pollo_engorde",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "historico_lote_postura",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    tipo_lote = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    lote_postura_levante_id = table.Column<int>(type: "integer", nullable: true),
                    lote_postura_produccion_id = table.Column<int>(type: "integer", nullable: true),
                    tipo_registro = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValue: "Creacion"),
                    fecha_registro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: true),
                    snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_historico_lote_postura", x => x.id);
                    table.CheckConstraint("ck_hlp_lote_ref", "(tipo_lote = 'LotePosturaLevante' AND lote_postura_levante_id IS NOT NULL AND lote_postura_produccion_id IS NULL) OR (tipo_lote = 'LotePosturaProduccion' AND lote_postura_levante_id IS NULL AND lote_postura_produccion_id IS NOT NULL)");
                    table.CheckConstraint("ck_hlp_tipo_lote", "tipo_lote IN ('LotePosturaLevante', 'LotePosturaProduccion')");
                    table.CheckConstraint("ck_hlp_tipo_registro", "tipo_registro IN ('Creacion', 'Actualizacion')");
                    table.ForeignKey(
                        name: "fk_historico_lote_postura_lote_postura_levante_lote_postura_le",
                        column: x => x.lote_postura_levante_id,
                        principalSchema: "public",
                        principalTable: "lote_postura_levante",
                        principalColumn: "lote_postura_levante_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_historico_lote_postura_lote_postura_produccion_lote_postura",
                        column: x => x.lote_postura_produccion_id,
                        principalSchema: "public",
                        principalTable: "lote_postura_produccion",
                        principalColumn: "lote_postura_produccion_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_historial_lote_pollo_engorde_movimiento_id",
                table: "historial_lote_pollo_engorde",
                column: "movimiento_id");

            migrationBuilder.CreateIndex(
                name: "ix_hlpe_company_id",
                table: "historial_lote_pollo_engorde",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_hlpe_fecha_registro",
                table: "historial_lote_pollo_engorde",
                column: "fecha_registro");

            migrationBuilder.CreateIndex(
                name: "ix_hlpe_lote_ave_engorde_id",
                table: "historial_lote_pollo_engorde",
                column: "lote_ave_engorde_id");

            migrationBuilder.CreateIndex(
                name: "ix_hlpe_lote_reproductora_id",
                table: "historial_lote_pollo_engorde",
                column: "lote_reproductora_ave_engorde_id");

            migrationBuilder.CreateIndex(
                name: "ix_hlpe_tipo_lote",
                table: "historial_lote_pollo_engorde",
                column: "tipo_lote");

            migrationBuilder.CreateIndex(
                name: "ix_historico_lote_postura_company",
                schema: "public",
                table: "historico_lote_postura",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_historico_lote_postura_fecha",
                schema: "public",
                table: "historico_lote_postura",
                column: "fecha_registro");

            migrationBuilder.CreateIndex(
                name: "ix_historico_lote_postura_levante",
                schema: "public",
                table: "historico_lote_postura",
                column: "lote_postura_levante_id",
                filter: "lote_postura_levante_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_historico_lote_postura_produccion",
                schema: "public",
                table: "historico_lote_postura",
                column: "lote_postura_produccion_id",
                filter: "lote_postura_produccion_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_historico_lote_postura_tipo",
                schema: "public",
                table: "historico_lote_postura",
                column: "tipo_lote");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_levante_galpon",
                schema: "public",
                table: "lote_postura_levante",
                column: "galpon_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_levante_granja",
                schema: "public",
                table: "lote_postura_levante",
                column: "granja_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_levante_lote",
                schema: "public",
                table: "lote_postura_levante",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_levante_nucleo",
                schema: "public",
                table: "lote_postura_levante",
                column: "nucleo_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_levante_nucleo_id_granja_id",
                schema: "public",
                table: "lote_postura_levante",
                columns: new[] { "nucleo_id", "granja_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_levante_padre",
                schema: "public",
                table: "lote_postura_levante",
                column: "lote_postura_levante_padre_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_produccion_galpon",
                schema: "public",
                table: "lote_postura_produccion",
                column: "galpon_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_produccion_granja",
                schema: "public",
                table: "lote_postura_produccion",
                column: "granja_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_produccion_levante",
                schema: "public",
                table: "lote_postura_produccion",
                column: "lote_postura_levante_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_produccion_lote",
                schema: "public",
                table: "lote_postura_produccion",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_produccion_nucleo",
                schema: "public",
                table: "lote_postura_produccion",
                column: "nucleo_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_postura_produccion_nucleo_id_granja_id",
                schema: "public",
                table: "lote_postura_produccion",
                columns: new[] { "nucleo_id", "granja_id" });

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_pollo_engorde_company_id",
                table: "movimiento_pollo_engorde",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_pollo_engorde_estado",
                table: "movimiento_pollo_engorde",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_pollo_engorde_fecha",
                table: "movimiento_pollo_engorde",
                column: "fecha_movimiento");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_pollo_engorde_granja_destino_id",
                table: "movimiento_pollo_engorde",
                column: "granja_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_pollo_engorde_granja_origen_id",
                table: "movimiento_pollo_engorde",
                column: "granja_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_pollo_engorde_lote_ave_engorde_destino_id",
                table: "movimiento_pollo_engorde",
                column: "lote_ave_engorde_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_pollo_engorde_lote_ave_engorde_origen_id",
                table: "movimiento_pollo_engorde",
                column: "lote_ave_engorde_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_pollo_engorde_lote_reproductora_ave_engorde_dest",
                table: "movimiento_pollo_engorde",
                column: "lote_reproductora_ave_engorde_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_pollo_engorde_lote_reproductora_ave_engorde_orig",
                table: "movimiento_pollo_engorde",
                column: "lote_reproductora_ave_engorde_origen_id");

            migrationBuilder.CreateIndex(
                name: "uq_movimiento_pollo_engorde_numero",
                table: "movimiento_pollo_engorde",
                column: "numero_movimiento",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historial_lote_pollo_engorde");

            migrationBuilder.DropTable(
                name: "historico_lote_postura",
                schema: "public");

            migrationBuilder.DropTable(
                name: "movimiento_pollo_engorde");

            migrationBuilder.DropTable(
                name: "lote_postura_produccion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "lote_postura_levante",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "parent_menu_id",
                table: "company_menus");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "company_menus");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "menus",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "menus",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
