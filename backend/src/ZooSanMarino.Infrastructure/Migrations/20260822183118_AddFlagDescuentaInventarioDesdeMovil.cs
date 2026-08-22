using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Flag tipado <c>companies.descuenta_inventario_desde_movil</c>: el kill switch de F5
    /// (plan <c>fase_de_desarrollo/descuento_inventario_movil_plan.md</c>). La app móvil manda
    /// ítems de inventario reales en vez del escalar de hoy solo para las empresas con el flag en
    /// <c>true</c>. Default <c>false</c> = comportamiento actual, ninguna empresa descuenta desde
    /// el móvil todavía.
    /// Idempotente (<c>ADD COLUMN IF NOT EXISTS</c>): re-ejecutable sin romper.
    /// </summary>
    public partial class AddFlagDescuentaInventarioDesdeMovil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE companies ADD COLUMN IF NOT EXISTS descuenta_inventario_desde_movil boolean NOT NULL DEFAULT false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE companies DROP COLUMN IF EXISTS descuenta_inventario_desde_movil;");
        }
    }
}
