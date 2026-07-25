using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Panamá: código ERP vigente de la granja para lotes de pollo engorde (ej. "4001017").
    /// Los lotes nuevos lo capturan en lote_erp; al cerrar TODOS los lotes del lote base en la
    /// granja el código avanza +1 (4001017 → 4001018 … 4001099 → 4001100). NULL = granja sin la
    /// funcionalidad (otros países siguen igual). Idempotente (IF NOT EXISTS).
    /// </summary>
    public partial class AddCodigoErpEngordeToFarm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.farms
    ADD COLUMN IF NOT EXISTS codigo_erp_engorde VARCHAR(20) NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.farms
    DROP COLUMN IF EXISTS codigo_erp_engorde;
");
        }
    }
}
