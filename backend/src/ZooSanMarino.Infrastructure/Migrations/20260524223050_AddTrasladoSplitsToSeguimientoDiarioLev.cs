using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrasladoSplitsToSeguimientoDiarioLev : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_machos  INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_salida_hembras  INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_salida_machos   INTEGER NOT NULL DEFAULT 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    DROP COLUMN IF EXISTS traslado_ingreso_hembras,
                    DROP COLUMN IF EXISTS traslado_ingreso_machos,
                    DROP COLUMN IF EXISTS traslado_salida_hembras,
                    DROP COLUMN IF EXISTS traslado_salida_machos;
            ");
        }
    }
}
