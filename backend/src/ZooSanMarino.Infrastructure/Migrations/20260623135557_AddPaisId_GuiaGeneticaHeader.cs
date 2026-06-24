using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega pais_id a guia_genetica_ecuador_header para soportar múltiples países
    /// (Panamá, Ecuador, etc.). Los registros existentes quedan con pais_id = 0 (legado/global).
    /// El índice único pasa de (company_id, raza, anio_guia) a (company_id, pais_id, raza, anio_guia).
    /// Idempotente.
    /// </summary>
    public partial class AddPaisId_GuiaGeneticaHeader : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1. Agregar columna pais_id si no existe (registros existentes = 0 = legado/global)
ALTER TABLE guia_genetica_ecuador_header
    ADD COLUMN IF NOT EXISTS pais_id INTEGER NOT NULL DEFAULT 0;

-- 2. Eliminar índice único anterior (si existe)
DROP INDEX IF EXISTS ix_guia_genetica_ecuador_header_company_id_raza_anio_guia;

-- 3. Crear nuevo índice único que incluye pais_id
CREATE UNIQUE INDEX IF NOT EXISTS ix_guia_genetica_ecuador_header_company_id_pais_id_raza_anio_g
    ON guia_genetica_ecuador_header(company_id, pais_id, raza, anio_guia);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ix_guia_genetica_ecuador_header_company_id_pais_id_raza_anio_g;

ALTER TABLE guia_genetica_ecuador_header DROP COLUMN IF EXISTS pais_id;

CREATE UNIQUE INDEX IF NOT EXISTS ix_guia_genetica_ecuador_header_company_id_raza_anio_guia
    ON guia_genetica_ecuador_header(company_id, raza, anio_guia);
");
        }
    }
}
