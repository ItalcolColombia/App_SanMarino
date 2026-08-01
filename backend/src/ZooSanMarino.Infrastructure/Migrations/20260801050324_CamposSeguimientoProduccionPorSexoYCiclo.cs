using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// D2 (persistir todos): el modal de seguimiento diario de producción siempre envió
    /// uniformidad/CV POR SEXO y ciclo, pero el backend los descartaba (round-trip roto silencioso).
    /// Columnas espejo de la tabla legacy seguimiento_diario_levante (double precision / varchar).
    /// Idempotente: ADD COLUMN IF NOT EXISTS.
    /// </summary>
    public partial class CamposSeguimientoProduccionPorSexoYCiclo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE seguimiento_diario_produccion ADD COLUMN IF NOT EXISTS ciclo character varying(50);
                ALTER TABLE seguimiento_diario_produccion ADD COLUMN IF NOT EXISTS cv_hembras double precision;
                ALTER TABLE seguimiento_diario_produccion ADD COLUMN IF NOT EXISTS cv_machos double precision;
                ALTER TABLE seguimiento_diario_produccion ADD COLUMN IF NOT EXISTS uniformidad_hembras double precision;
                ALTER TABLE seguimiento_diario_produccion ADD COLUMN IF NOT EXISTS uniformidad_machos double precision;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE seguimiento_diario_produccion DROP COLUMN IF EXISTS ciclo;
                ALTER TABLE seguimiento_diario_produccion DROP COLUMN IF EXISTS cv_hembras;
                ALTER TABLE seguimiento_diario_produccion DROP COLUMN IF EXISTS cv_machos;
                ALTER TABLE seguimiento_diario_produccion DROP COLUMN IF EXISTS uniformidad_hembras;
                ALTER TABLE seguimiento_diario_produccion DROP COLUMN IF EXISTS uniformidad_machos;
                """);
        }
    }
}
