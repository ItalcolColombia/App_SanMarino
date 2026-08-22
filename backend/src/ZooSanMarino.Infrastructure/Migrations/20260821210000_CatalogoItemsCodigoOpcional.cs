using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <c>catalogo_items.codigo</c> deja de ser <c>NOT NULL</c>. Un ítem puede nacer sin código ERP
    /// (p. ej. un ítem de huevo sembrado antes de que el cliente confirme el código real, ver
    /// <c>SeedProductosNoConformesSantaReyes</c>) y completarse después, una sola vez —
    /// <c>CatalogItemService.UpdateAsync</c> lo bloquea apenas deja de estar vacío (clave natural).
    /// </summary>
    /// <remarks>
    /// <b>Por qué es seguro.</b> El índice único <c>ux_catalogo_items_codigo_company_pais</c>
    /// (<c>company_id, pais_id, codigo</c>) no se toca: en Postgres cada <c>NULL</c> es distinto de
    /// cualquier otro en un índice único estándar, así que varios ítems sin código conviven sin
    /// violar la unicidad — es exactamente el comportamiento que se necesita.
    ///
    /// <b>Idempotente:</b> <c>DROP COLUMN NOT NULL</c> sobre una columna ya nullable no falla; corre
    /// dos veces sin error.
    /// </remarks>
    public partial class CatalogoItemsCodigoOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE catalogo_items ALTER COLUMN codigo DROP NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir exige que NINGÚN ítem tenga codigo NULL a esa altura; si lo hay, el ALTER
            // falla con un error claro (23502) en vez de dejar filas corruptas en silencio.
            migrationBuilder.Sql("ALTER TABLE catalogo_items ALTER COLUMN codigo SET NOT NULL;");
        }
    }
}
