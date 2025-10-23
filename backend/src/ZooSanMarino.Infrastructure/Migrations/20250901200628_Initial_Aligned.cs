using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Aligned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalogo_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogo_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    identifier = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    country = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    state = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    visual_permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    mobile_access = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "logins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    is_email_login = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_logins", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_lists",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_master_lists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "menus",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    icon = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    route = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    parent_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_menus", x => x.id);
                    table.ForeignKey(
                        name: "fk_menus_menus_parent_id",
                        column: x => x.parent_id,
                        principalTable: "menus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "paises",
                columns: table => new
                {
                    pais_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pais_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paises", x => x.pais_id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    sur_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cedula = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ubicacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    locked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "farms",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    regional_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false, defaultValue: "A"),
                    zone_id = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farms", x => x.id);
                    table.ForeignKey(
                        name: "fk_farms_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "regionales",
                columns: table => new
                {
                    regional_cia = table.Column<int>(type: "integer", nullable: false),
                    regional_id = table.Column<int>(type: "integer", nullable: false),
                    regional_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    regional_estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    regional_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regionales", x => new { x.regional_cia, x.regional_id });
                    table.ForeignKey(
                        name: "fk_regionales_companies_regional_cia",
                        column: x => x.regional_cia,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zonas",
                columns: table => new
                {
                    zona_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    zona_cia = table.Column<int>(type: "integer", nullable: false),
                    zona_nombre = table.Column<string>(type: "text", nullable: false),
                    zona_estado = table.Column<string>(type: "text", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zonas", x => x.zona_id);
                    table.ForeignKey(
                        name: "fk_zonas_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "master_list_options",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    master_list_id = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_master_list_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_master_list_options_master_lists_master_list_id",
                        column: x => x.master_list_id,
                        principalTable: "master_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "departamentos",
                columns: table => new
                {
                    departamento_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    departamento_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pais_id = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departamento", x => x.departamento_id);
                    table.ForeignKey(
                        name: "fk_departamentos_paises_pais_id",
                        column: x => x.pais_id,
                        principalTable: "paises",
                        principalColumn: "pais_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "menu_permissions",
                columns: table => new
                {
                    menu_id = table.Column<int>(type: "integer", nullable: false),
                    permission_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_menu_permissions", x => new { x.menu_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_menu_permissions_menus_menu_id",
                        column: x => x.menu_id,
                        principalTable: "menus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_menu_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_companies",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_companies", x => new { x.role_id, x.company_id });
                    table.ForeignKey(
                        name: "fk_role_companies_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_companies_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    permission_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_companies",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_companies", x => new { x.user_id, x.company_id });
                    table.ForeignKey(
                        name: "fk_user_companies_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_companies_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_locked_by_admin = table.Column<bool>(type: "boolean", nullable: false),
                    lock_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.user_id, x.login_id });
                    table.ForeignKey(
                        name: "fk_user_logins_logins_login_id",
                        column: x => x.login_id,
                        principalTable: "logins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nucleos",
                columns: table => new
                {
                    nucleo_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    granja_id = table.Column<int>(type: "integer", nullable: false),
                    nucleo_nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nucleos", x => new { x.nucleo_id, x.granja_id });
                    table.ForeignKey(
                        name: "fk_nucleos_farms_granja_id",
                        column: x => x.granja_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "municipios",
                columns: table => new
                {
                    municipio_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    municipio_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    departamento_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipio", x => x.municipio_id);
                    table.ForeignKey(
                        name: "fk_municipios_departamentos_departamento_id",
                        column: x => x.departamento_id,
                        principalTable: "departamentos",
                        principalColumn: "departamento_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "galpones",
                columns: table => new
                {
                    galpon_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nucleo_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    granja_id = table.Column<int>(type: "integer", nullable: false),
                    galpon_nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ancho = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    largo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tipo_galpon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_galpones", x => x.galpon_id);
                    table.ForeignKey(
                        name: "fk_galpones_farms_granja_id",
                        column: x => x.granja_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_galpones_nucleos_nucleo_id_granja_id",
                        columns: x => new { x.nucleo_id, x.granja_id },
                        principalTable: "nucleos",
                        principalColumns: new[] { "nucleo_id", "granja_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lotes",
                columns: table => new
                {
                    lote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    lote_nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    granja_id = table.Column<int>(type: "integer", nullable: false),
                    nucleo_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    galpon_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    regional = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
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
                    tecnico = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    mixtas = table.Column<int>(type: "integer", nullable: true),
                    peso_mixto = table.Column<double>(type: "double precision", nullable: true),
                    aves_encasetadas = table.Column<int>(type: "integer", nullable: true),
                    edad_inicial = table.Column<int>(type: "integer", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lotes", x => x.lote_id);
                    table.ForeignKey(
                        name: "fk_lotes_farms_granja_id",
                        column: x => x.granja_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lotes_galpones_galpon_id",
                        column: x => x.galpon_id,
                        principalTable: "galpones",
                        principalColumn: "galpon_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lotes_nucleos_nucleo_id_granja_id",
                        columns: x => new { x.nucleo_id, x.granja_id },
                        principalTable: "nucleos",
                        principalColumns: new[] { "nucleo_id", "granja_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_gramaje_galpon",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    galpon_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    semana_desde = table.Column<int>(type: "integer", nullable: false),
                    semana_hasta = table.Column<int>(type: "integer", nullable: false),
                    tipo_alimento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gramaje_gr_por_ave = table.Column<double>(type: "double precision", precision: 12, scale: 3, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_gramaje_galpon", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_gramaje_galpon_galpones_galpon_id",
                        column: x => x.galpon_id,
                        principalTable: "galpones",
                        principalColumn: "galpon_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lote_reproductoras",
                columns: table => new
                {
                    lote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reproductora_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre_lote = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    fecha_encasetamiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    m = table.Column<int>(type: "integer", nullable: true),
                    h = table.Column<int>(type: "integer", nullable: true),
                    mixtas = table.Column<int>(type: "integer", nullable: true),
                    mort_caja_h = table.Column<int>(type: "integer", nullable: true),
                    mort_caja_m = table.Column<int>(type: "integer", nullable: true),
                    unif_h = table.Column<int>(type: "integer", nullable: true),
                    unif_m = table.Column<int>(type: "integer", nullable: true),
                    peso_inicial_m = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    peso_inicial_h = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    peso_mixto = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lote_reproductoras", x => new { x.lote_id, x.reproductora_id });
                    table.ForeignKey(
                        name: "fk_lote_reproductoras_lotes_lote_id",
                        column: x => x.lote_id,
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "produccion_lotes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    lote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fecha_inicio_produccion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    hembras_iniciales = table.Column<int>(type: "integer", nullable: false),
                    machos_iniciales = table.Column<int>(type: "integer", nullable: false),
                    huevos_iniciales = table.Column<int>(type: "integer", nullable: false),
                    tipo_nido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    granja_id = table.Column<int>(type: "integer", nullable: false),
                    nucleo_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    galpon_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ciclo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Normal"),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produccion_lotes", x => x.id);
                    table.ForeignKey(
                        name: "fk_produccion_lotes_galpones_galpon_id",
                        column: x => x.galpon_id,
                        principalTable: "galpones",
                        principalColumn: "galpon_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produccion_lotes_lotes_lote_id",
                        column: x => x.lote_id,
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produccion_lotes_nucleos_nucleo_id_granja_id",
                        columns: x => new { x.nucleo_id, x.granja_id },
                        principalTable: "nucleos",
                        principalColumns: new[] { "nucleo_id", "granja_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seguimiento_lote_levante",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    lote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fecha_registro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    mortalidad_hembras = table.Column<int>(type: "integer", nullable: false),
                    mortalidad_machos = table.Column<int>(type: "integer", nullable: false),
                    sel_h = table.Column<int>(type: "integer", nullable: false),
                    sel_m = table.Column<int>(type: "integer", nullable: false),
                    error_sexaje_hembras = table.Column<int>(type: "integer", nullable: false),
                    error_sexaje_machos = table.Column<int>(type: "integer", nullable: false),
                    consumo_kg_hembras = table.Column<double>(type: "double precision", precision: 12, scale: 3, nullable: false),
                    tipo_alimento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    kcal_al_h = table.Column<double>(type: "double precision", precision: 12, scale: 3, nullable: true),
                    prot_al_h = table.Column<double>(type: "double precision", precision: 12, scale: 3, nullable: true),
                    kcal_ave_h = table.Column<double>(type: "double precision", precision: 12, scale: 3, nullable: true),
                    prot_ave_h = table.Column<double>(type: "double precision", precision: 12, scale: 3, nullable: true),
                    ciclo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Normal")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seguimiento_lote_levante", x => x.id);
                    table.ForeignKey(
                        name: "fk_seguimiento_lote_levante_lotes_lote_id",
                        column: x => x.lote_id,
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lote_galpones",
                columns: table => new
                {
                    lote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reproductora_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    galpon_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    macho = table.Column<int>(type: "integer", nullable: true),
                    hembra = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lote_galpones", x => new { x.lote_id, x.reproductora_id, x.galpon_id });
                    table.ForeignKey(
                        name: "fk_lote_galpones_galpones_galpon_id",
                        column: x => x.galpon_id,
                        principalTable: "galpones",
                        principalColumn: "galpon_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lote_galpones_lote_reproductoras_lote_id_reproductora_id",
                        columns: x => new { x.lote_id, x.reproductora_id },
                        principalTable: "lote_reproductoras",
                        principalColumns: new[] { "lote_id", "reproductora_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lote_seguimientos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lote_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reproductora_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    peso_inicial = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    peso_final = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    mortalidad_m = table.Column<int>(type: "integer", nullable: true),
                    mortalidad_h = table.Column<int>(type: "integer", nullable: true),
                    sel_m = table.Column<int>(type: "integer", nullable: true),
                    sel_h = table.Column<int>(type: "integer", nullable: true),
                    error_m = table.Column<int>(type: "integer", nullable: true),
                    error_h = table.Column<int>(type: "integer", nullable: true),
                    tipo_alimento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    consumo_alimento = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
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
                    table.PrimaryKey("pk_lote_seguimientos", x => x.id);
                    table.ForeignKey(
                        name: "fk_lote_seguimientos_lote_reproductoras_lote_id_reproductora_id",
                        columns: x => new { x.lote_id, x.reproductora_id },
                        principalTable: "lote_reproductoras",
                        principalColumns: new[] { "lote_id", "reproductora_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lote_seguimientos_lotes_lote_id",
                        column: x => x.lote_id,
                        principalTable: "lotes",
                        principalColumn: "lote_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "produccion_diaria",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_id = table.Column<string>(type: "text", nullable: false),
                    fecha_registro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    mortalidad_hembras = table.Column<int>(type: "integer", nullable: false),
                    mortalidad_machos = table.Column<int>(type: "integer", nullable: false),
                    sel_h = table.Column<int>(type: "integer", nullable: false),
                    cons_kg_h = table.Column<double>(type: "double precision", nullable: false),
                    cons_kg_m = table.Column<double>(type: "double precision", nullable: false),
                    huevo_tot = table.Column<int>(type: "integer", nullable: false),
                    huevo_inc = table.Column<int>(type: "integer", nullable: false),
                    tipo_alimento = table.Column<string>(type: "text", nullable: false),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    peso_huevo = table.Column<double>(type: "double precision", nullable: true),
                    etapa = table.Column<int>(type: "integer", nullable: false),
                    lote_produccion_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produccion_diaria", x => x.id);
                    table.ForeignKey(
                        name: "fk_produccion_diaria_produccion_lotes_lote_produccion_id",
                        column: x => x.lote_produccion_id,
                        principalTable: "produccion_lotes",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_items_activo",
                table: "catalogo_items",
                column: "activo");

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_items_nombre",
                table: "catalogo_items",
                column: "nombre");

            migrationBuilder.CreateIndex(
                name: "ux_catalogo_items_codigo",
                table: "catalogo_items",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_departamentos_pais_id",
                table: "departamentos",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_farms_company_id",
                table: "farms",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_galpones_granja_id",
                table: "galpones",
                column: "granja_id");

            migrationBuilder.CreateIndex(
                name: "ix_galpones_nucleo_id_granja_id",
                table: "galpones",
                columns: new[] { "nucleo_id", "granja_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lote_galpones_galpon_id",
                table: "lote_galpones",
                column: "galpon_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_seguimientos_lote_id_reproductora_id",
                table: "lote_seguimientos",
                columns: new[] { "lote_id", "reproductora_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lotes_galpon_id",
                table: "lotes",
                column: "galpon_id");

            migrationBuilder.CreateIndex(
                name: "ix_lotes_granja_id",
                table: "lotes",
                column: "granja_id");

            migrationBuilder.CreateIndex(
                name: "ix_lotes_nucleo_id_granja_id",
                table: "lotes",
                columns: new[] { "nucleo_id", "granja_id" });

            migrationBuilder.CreateIndex(
                name: "ix_master_list_options_master_list_id",
                table: "master_list_options",
                column: "master_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_master_lists_key",
                table: "master_lists",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_menu_permissions_permission_id",
                table: "menu_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_menus_parent_id",
                table: "menus",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_municipios_departamento_id",
                table: "municipios",
                column: "departamento_id");

            migrationBuilder.CreateIndex(
                name: "ix_nucleos_granja_id",
                table: "nucleos",
                column: "granja_id");

            migrationBuilder.CreateIndex(
                name: "ix_permissions_key",
                table: "permissions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plan_gramaje_galpon_galpon_id",
                table: "plan_gramaje_galpon",
                column: "galpon_id");

            migrationBuilder.CreateIndex(
                name: "ix_produccion_diaria_lote_produccion_id",
                table: "produccion_diaria",
                column: "lote_produccion_id");

            migrationBuilder.CreateIndex(
                name: "ix_produccion_lotes_galpon_id",
                table: "produccion_lotes",
                column: "galpon_id");

            migrationBuilder.CreateIndex(
                name: "ix_produccion_lotes_lote_id",
                table: "produccion_lotes",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_produccion_lotes_nucleo_id_granja_id",
                table: "produccion_lotes",
                columns: new[] { "nucleo_id", "granja_id" });

            migrationBuilder.CreateIndex(
                name: "ix_role_companies_company_id",
                table: "role_companies",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seguimiento_lote_levante_lote_id",
                table: "seguimiento_lote_levante",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_companies_company_id",
                table: "user_companies",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_login_id",
                table: "user_logins",
                column: "login_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_company_id",
                table: "user_roles",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_zonas_company_id",
                table: "zonas",
                column: "company_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalogo_items");

            migrationBuilder.DropTable(
                name: "lote_galpones");

            migrationBuilder.DropTable(
                name: "lote_seguimientos");

            migrationBuilder.DropTable(
                name: "master_list_options");

            migrationBuilder.DropTable(
                name: "menu_permissions");

            migrationBuilder.DropTable(
                name: "municipios");

            migrationBuilder.DropTable(
                name: "plan_gramaje_galpon");

            migrationBuilder.DropTable(
                name: "produccion_diaria");

            migrationBuilder.DropTable(
                name: "regionales");

            migrationBuilder.DropTable(
                name: "role_companies");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "seguimiento_lote_levante");

            migrationBuilder.DropTable(
                name: "user_companies");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "zonas");

            migrationBuilder.DropTable(
                name: "lote_reproductoras");

            migrationBuilder.DropTable(
                name: "master_lists");

            migrationBuilder.DropTable(
                name: "menus");

            migrationBuilder.DropTable(
                name: "departamentos");

            migrationBuilder.DropTable(
                name: "produccion_lotes");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "logins");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "paises");

            migrationBuilder.DropTable(
                name: "lotes");

            migrationBuilder.DropTable(
                name: "galpones");

            migrationBuilder.DropTable(
                name: "nucleos");

            migrationBuilder.DropTable(
                name: "farms");

            migrationBuilder.DropTable(
                name: "companies");
        }
    }
}
