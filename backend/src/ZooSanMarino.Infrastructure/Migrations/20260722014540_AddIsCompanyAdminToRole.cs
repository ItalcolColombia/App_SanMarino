using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsCompanyAdminToRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente: no falla si la columna ya existe (deploy re-aplicable).
            migrationBuilder.Sql(
                "ALTER TABLE roles ADD COLUMN IF NOT EXISTS is_company_admin boolean NOT NULL DEFAULT false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE roles DROP COLUMN IF EXISTS is_company_admin;");
        }
    }
}
