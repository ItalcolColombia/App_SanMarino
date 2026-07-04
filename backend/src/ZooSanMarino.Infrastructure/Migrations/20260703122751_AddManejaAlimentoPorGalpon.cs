using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManejaAlimentoPorGalpon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IDEMPOTENTE (regla del repo): ADD COLUMN IF NOT EXISTS. Sin default en la app;
            // la resolución efectiva es farm.maneja ?? company.maneja (AlimentoNivelResolver).
            migrationBuilder.Sql(@"
                ALTER TABLE public.farms     ADD COLUMN IF NOT EXISTS maneja_alimento_por_galpon boolean NULL;
                ALTER TABLE public.companies ADD COLUMN IF NOT EXISTS maneja_alimento_por_galpon boolean NOT NULL DEFAULT false;
            ");

            // Seed PRESERVANDO EL COMPORTAMIENTO POR PAÍS: empresas de Ecuador (2) o Panamá (3)
            // manejaban alimento a nivel GALPÓN → true. Colombia (1) queda en false (nivel granja).
            // Las granjas quedan NULL (heredan la empresa). Idempotente (solo toca las que siguen en false).
            migrationBuilder.Sql(@"
                UPDATE public.companies c
                   SET maneja_alimento_por_galpon = true
                 WHERE c.maneja_alimento_por_galpon = false
                   AND EXISTS (
                       SELECT 1 FROM public.company_pais cp
                        WHERE cp.company_id = c.id AND cp.pais_id IN (2, 3)
                   );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.farms     DROP COLUMN IF EXISTS maneja_alimento_por_galpon;
                ALTER TABLE public.companies DROP COLUMN IF EXISTS maneja_alimento_por_galpon;
            ");
        }
    }
}
