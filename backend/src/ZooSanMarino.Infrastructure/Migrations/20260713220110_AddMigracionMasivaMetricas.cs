using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigracionMasivaMetricas : Migration
    {
        // Agrega las 3 métricas nuevas de F2 (duración, filas omitidas, si fue dry-run) a la
        // auditoría del módulo de Migraciones Masivas. Idempotente (ADD COLUMN/DROP COLUMN
        // IF [NOT] EXISTS) según CLAUDE.md — seguro de reintentar si quedó a mitad de camino.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE migracion_masiva ADD COLUMN IF NOT EXISTS filas_omitidas integer NOT NULL DEFAULT 0;
                ALTER TABLE migracion_masiva ADD COLUMN IF NOT EXISTS duracion_ms bigint NULL;
                ALTER TABLE migracion_masiva ADD COLUMN IF NOT EXISTS fue_dry_run boolean NOT NULL DEFAULT false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE migracion_masiva DROP COLUMN IF EXISTS filas_omitidas;
                ALTER TABLE migracion_masiva DROP COLUMN IF EXISTS duracion_ms;
                ALTER TABLE migracion_masiva DROP COLUMN IF EXISTS fue_dry_run;
            ");
        }
    }
}
