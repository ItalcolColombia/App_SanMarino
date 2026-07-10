using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoAlimentoHembrasMachosSeguimientoDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Alimento independiente Hembras/Machos en el seguimiento diario de levante.
            // Nombre resuelto al guardar (mismo criterio que tipo_alimento): texto congelado, no id.
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    ADD COLUMN IF NOT EXISTS tipo_alimento_hembras VARCHAR(100) NULL;
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    ADD COLUMN IF NOT EXISTS tipo_alimento_machos VARCHAR(100) NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    DROP COLUMN IF EXISTS tipo_alimento_hembras;
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    DROP COLUMN IF EXISTS tipo_alimento_machos;
            ");
        }
    }
}
