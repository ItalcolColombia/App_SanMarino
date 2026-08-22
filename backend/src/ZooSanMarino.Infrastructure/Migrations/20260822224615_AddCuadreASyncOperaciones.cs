using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// F7 del plan <c>descuento_inventario_movil_plan.md</c>: columnas de detalle y de resolución
    /// en <c>sync_operaciones</c> para la bandeja de <c>requiere_cuadre</c>. Sin
    /// <c>cuadre_resuelto_at</c> la bandeja no se vacía nunca.
    /// Idempotente (<c>ADD COLUMN IF NOT EXISTS</c> / <c>CREATE INDEX IF NOT EXISTS</c>).
    /// </summary>
    public partial class AddCuadreASyncOperaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE sync_operaciones ADD COLUMN IF NOT EXISTS detalle text NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE sync_operaciones ADD COLUMN IF NOT EXISTS cuadre_resuelto_at timestamp with time zone NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE sync_operaciones ADD COLUMN IF NOT EXISTS cuadre_resuelto_por integer NULL;");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_sync_operaciones_bandeja_cuadre ON sync_operaciones (company_id, estado) " +
                "WHERE cuadre_resuelto_at IS NULL AND estado = 'requiere_cuadre';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_sync_operaciones_bandeja_cuadre;");
            migrationBuilder.Sql("ALTER TABLE sync_operaciones DROP COLUMN IF EXISTS cuadre_resuelto_por;");
            migrationBuilder.Sql("ALTER TABLE sync_operaciones DROP COLUMN IF EXISTS cuadre_resuelto_at;");
            migrationBuilder.Sql("ALTER TABLE sync_operaciones DROP COLUMN IF EXISTS detalle;");
        }
    }
}
