using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProduccionAvicolaRawOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_departamentos_paises_pais_id",
                table: "departamentos");

            migrationBuilder.DropIndex(
                name: "ix_menus_parent_id",
                table: "menus");

            migrationBuilder.DropIndex(
                name: "ix_farms_company_name",
                table: "farms");

            migrationBuilder.DropIndex(
                name: "ix_departamentos_pais_id",
                table: "departamentos");

            migrationBuilder.AlterColumn<bool>(
                name: "is_active",
                table: "menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "icon",
                table: "menus",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

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

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "farms",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "A",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "regional_id",
                table: "farms",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "produccion_avicola_raw",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    anio_guia = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    raza = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    edad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    mort_sem_h = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    retiro_ac_h = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    mort_sem_m = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    retiro_ac_m = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cons_ac_h = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cons_ac_m = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    gr_ave_dia_h = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    gr_ave_dia_m = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    peso_h = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    peso_m = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    uniformidad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    h_total_aa = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    prod_porcentaje = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    h_inc_aa = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    aprov_sem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    peso_huevo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    masa_huevo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    grasa_porcentaje = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    nacim_porcentaje = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    pollito_aa = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    kcal_ave_dia_h = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    kcal_ave_dia_m = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    aprov_ac = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    gr_huevo_t = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    gr_huevo_inc = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    gr_pollito = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    valor_1000 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    valor_150 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    apareo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    peso_mh = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produccion_avicola_raw", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "produccion_resultado_levante",
                columns: table => new
                {
                    lote_id = table.Column<string>(type: "text", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    edad_semana = table.Column<int>(type: "integer", nullable: true),
                    hembra_viva = table.Column<int>(type: "integer", nullable: true),
                    mort_h = table.Column<int>(type: "integer", nullable: false),
                    sel_h_out = table.Column<int>(type: "integer", nullable: false),
                    err_h = table.Column<int>(type: "integer", nullable: false),
                    cons_kg_h = table.Column<double>(type: "double precision", nullable: true),
                    peso_h = table.Column<double>(type: "double precision", nullable: true),
                    unif_h = table.Column<double>(type: "double precision", nullable: true),
                    cv_h = table.Column<double>(type: "double precision", nullable: true),
                    mort_h_pct = table.Column<double>(type: "double precision", nullable: true),
                    sel_h_pct = table.Column<double>(type: "double precision", nullable: true),
                    err_h_pct = table.Column<double>(type: "double precision", nullable: true),
                    ms_eh_h = table.Column<int>(type: "integer", nullable: false),
                    ac_mort_h = table.Column<int>(type: "integer", nullable: false),
                    ac_sel_h = table.Column<int>(type: "integer", nullable: false),
                    ac_err_h = table.Column<int>(type: "integer", nullable: false),
                    ac_cons_kg_h = table.Column<double>(type: "double precision", nullable: true),
                    cons_ac_gr_h = table.Column<double>(type: "double precision", nullable: true),
                    gr_ave_dia_h = table.Column<double>(type: "double precision", nullable: true),
                    dif_cons_h_pct = table.Column<double>(type: "double precision", nullable: true),
                    dif_peso_h_pct = table.Column<double>(type: "double precision", nullable: true),
                    retiro_h_pct = table.Column<double>(type: "double precision", nullable: true),
                    retiro_h_ac_pct = table.Column<double>(type: "double precision", nullable: true),
                    macho_vivo = table.Column<int>(type: "integer", nullable: true),
                    mort_m = table.Column<int>(type: "integer", nullable: false),
                    sel_m_out = table.Column<int>(type: "integer", nullable: false),
                    err_m = table.Column<int>(type: "integer", nullable: false),
                    cons_kg_m = table.Column<double>(type: "double precision", nullable: true),
                    peso_m = table.Column<double>(type: "double precision", nullable: true),
                    unif_m = table.Column<double>(type: "double precision", nullable: true),
                    cv_m = table.Column<double>(type: "double precision", nullable: true),
                    mort_m_pct = table.Column<double>(type: "double precision", nullable: true),
                    sel_m_pct = table.Column<double>(type: "double precision", nullable: true),
                    err_m_pct = table.Column<double>(type: "double precision", nullable: true),
                    ms_em_m = table.Column<int>(type: "integer", nullable: false),
                    ac_mort_m = table.Column<int>(type: "integer", nullable: false),
                    ac_sel_m = table.Column<int>(type: "integer", nullable: false),
                    ac_err_m = table.Column<int>(type: "integer", nullable: false),
                    ac_cons_kg_m = table.Column<double>(type: "double precision", nullable: true),
                    cons_ac_gr_m = table.Column<double>(type: "double precision", nullable: true),
                    gr_ave_dia_m = table.Column<double>(type: "double precision", nullable: true),
                    dif_cons_m_pct = table.Column<double>(type: "double precision", nullable: true),
                    dif_peso_m_pct = table.Column<double>(type: "double precision", nullable: true),
                    retiro_m_pct = table.Column<double>(type: "double precision", nullable: true),
                    retiro_m_ac_pct = table.Column<double>(type: "double precision", nullable: true),
                    rel_m_h_pct = table.Column<double>(type: "double precision", nullable: true),
                    peso_h_guia = table.Column<double>(type: "double precision", nullable: true),
                    unif_h_guia = table.Column<double>(type: "double precision", nullable: true),
                    cons_ac_gr_h_guia = table.Column<double>(type: "double precision", nullable: true),
                    gr_ave_dia_h_guia = table.Column<double>(type: "double precision", nullable: true),
                    mort_h_pct_guia = table.Column<double>(type: "double precision", nullable: true),
                    peso_m_guia = table.Column<double>(type: "double precision", nullable: true),
                    unif_m_guia = table.Column<double>(type: "double precision", nullable: true),
                    cons_ac_gr_m_guia = table.Column<double>(type: "double precision", nullable: true),
                    gr_ave_dia_m_guia = table.Column<double>(type: "double precision", nullable: true),
                    mort_m_pct_guia = table.Column<double>(type: "double precision", nullable: true),
                    alimento_h_guia = table.Column<string>(type: "text", nullable: true),
                    alimento_m_guia = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "role_menus",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    menu_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_menus", x => new { x.role_id, x.menu_id });
                    table.ForeignKey(
                        name: "fk_role_menus_menus_menu_id",
                        column: x => x.menu_id,
                        principalTable: "menus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_menus_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_menus_parent_id_order",
                table: "menus",
                columns: new[] { "parent_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ux_farms_company_name",
                table: "farms",
                columns: new[] { "company_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_departamentos_pais_nombre",
                table: "departamentos",
                columns: new[] { "pais_id", "departamento_nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_produccion_avicola_raw_anio_guia",
                table: "produccion_avicola_raw",
                column: "anio_guia");

            migrationBuilder.CreateIndex(
                name: "ix_produccion_avicola_raw_company_id",
                table: "produccion_avicola_raw",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_produccion_avicola_raw_raza",
                table: "produccion_avicola_raw",
                column: "raza");

            migrationBuilder.CreateIndex(
                name: "ix_role_menus_menu_id",
                table: "role_menus",
                column: "menu_id");

            migrationBuilder.AddForeignKey(
                name: "fk_departamentos_paises_pais_id",
                table: "departamentos",
                column: "pais_id",
                principalTable: "paises",
                principalColumn: "pais_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_departamentos_paises_pais_id",
                table: "departamentos");

            migrationBuilder.DropTable(
                name: "produccion_avicola_raw");

            migrationBuilder.DropTable(
                name: "produccion_resultado_levante");

            migrationBuilder.DropTable(
                name: "role_menus");

            migrationBuilder.DropIndex(
                name: "ix_menus_parent_id_order",
                table: "menus");

            migrationBuilder.DropIndex(
                name: "ux_farms_company_name",
                table: "farms");

            migrationBuilder.DropIndex(
                name: "ux_departamentos_pais_nombre",
                table: "departamentos");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "menus");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "menus");

            migrationBuilder.AlterColumn<bool>(
                name: "is_active",
                table: "menus",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "icon",
                table: "menus",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "farms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1)",
                oldMaxLength: 1,
                oldDefaultValue: "A");

            migrationBuilder.AlterColumn<int>(
                name: "regional_id",
                table: "farms",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_menus_parent_id",
                table: "menus",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_farms_company_name",
                table: "farms",
                columns: new[] { "company_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_departamentos_pais_id",
                table: "departamentos",
                column: "pais_id");

            migrationBuilder.AddForeignKey(
                name: "fk_departamentos_paises_pais_id",
                table: "departamentos",
                column: "pais_id",
                principalTable: "paises",
                principalColumn: "pais_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
