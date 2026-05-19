using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGestionClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clientes",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tipo_documento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    numero_identificacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tipo_cliente = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    pais = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provincia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    distrito = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    planta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    zona = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false, defaultValue: "A"),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clientes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clientes_company_status",
                schema: "public",
                table: "clientes",
                columns: new[] { "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_clientes_company_nro_identificacion",
                schema: "public",
                table: "clientes",
                columns: new[] { "company_id", "numero_identificacion" },
                unique: true);

            // ── MENÚ: Gestión de Clientes (sin padre) ─────────────────────────────────
            migrationBuilder.Sql(@"
INSERT INTO menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT
    'Gestión de Clientes',
    'users',
    '/config/clientes',
    NULL,
    COALESCE((SELECT MAX(m.""order"") FROM menus m WHERE m.parent_id IS NULL), 0) + 1,
    true,
    'gestion_clientes',
    0,
    false,
    timezone('utc', now()),
    timezone('utc', now())
WHERE NOT EXISTS (
    SELECT 1 FROM menus WHERE route = '/config/clientes'
);

-- Asignar a todos los roles que tienen acceso a '/config'
INSERT INTO role_menus (role_id, menu_id)
SELECT DISTINCT rm.role_id, nuevo.id
FROM menus nuevo
CROSS JOIN (
    SELECT DISTINCT rm2.role_id
    FROM role_menus rm2
    INNER JOIN menus m ON m.id = rm2.menu_id
    WHERE m.route = '/config'
) rm
WHERE nuevo.route = '/config/clientes'
  AND NOT EXISTS (
      SELECT 1 FROM role_menus ex
      WHERE ex.role_id = rm.role_id AND ex.menu_id = nuevo.id
  );

-- Asignar a todas las empresas que tienen '/config' habilitado
INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT DISTINCT
    cm.company_id,
    nuevo.id,
    true,
    cm.sort_order + 1,
    cm.parent_menu_id
FROM company_menus cm
INNER JOIN menus m ON m.id = cm.menu_id AND m.route = '/config'
INNER JOIN menus nuevo ON nuevo.route = '/config/clientes'
WHERE NOT EXISTS (
    SELECT 1 FROM company_menus ex
    WHERE ex.company_id = cm.company_id AND ex.menu_id = nuevo.id
);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM company_menus WHERE menu_id = (SELECT id FROM menus WHERE route = '/config/clientes' LIMIT 1);
DELETE FROM role_menus     WHERE menu_id = (SELECT id FROM menus WHERE route = '/config/clientes' LIMIT 1);
DELETE FROM menus          WHERE route   = '/config/clientes';
");
            migrationBuilder.DropTable(
                name: "clientes",
                schema: "public");
        }
    }
}
