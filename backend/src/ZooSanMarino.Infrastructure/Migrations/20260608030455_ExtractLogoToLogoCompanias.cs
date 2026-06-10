using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtractLogoToLogoCompanias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Crear tabla destino (idempotente)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS logo_companias (
                    id               integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    company_id       integer NOT NULL,
                    logo_bytes       bytea   NOT NULL,
                    logo_content_type varchar(100) NOT NULL,
                    CONSTRAINT fk_logo_companias_companies_company_id
                        FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_logo_companias_company_id
                    ON logo_companias (company_id);
            ");

            // 2. Migrar logos existentes de companies → logo_companias (idempotente)
            migrationBuilder.Sql(@"
                INSERT INTO logo_companias (company_id, logo_bytes, logo_content_type)
                SELECT id,
                       logo_bytes,
                       COALESCE(NULLIF(logo_content_type, ''), 'image/png')
                FROM   companies
                WHERE  logo_bytes IS NOT NULL
                  AND  octet_length(logo_bytes) > 0
                  AND  id NOT IN (SELECT company_id FROM logo_companias);
            ");

            // 3. Eliminar columnas de companies (idempotente)
            migrationBuilder.Sql(@"
                ALTER TABLE companies DROP COLUMN IF EXISTS logo_bytes;
                ALTER TABLE companies DROP COLUMN IF EXISTS logo_content_type;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "logo_companias");

            migrationBuilder.AddColumn<byte[]>(
                name: "logo_bytes",
                table: "companies",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_content_type",
                table: "companies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
