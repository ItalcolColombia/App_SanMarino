using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Flag tipado <c>companies.limita_tipos_inventario_alimento_y_aves</c> (F6 del plan Italapp
    /// Santa Reyes, ver §7 de <c>fase_de_desarrollo/santa_reyes_requerimientos_italapp_plan.md</c>):
    /// el catálogo de ítems de inventario (<c>CatalogItem.ItemType</c>) sólo ofrece «Alimento» y
    /// «Aves» al crear/editar/filtrar, en vez de los 6 tipos de siempre.
    /// <para>
    /// Se enciende SOLO para Santa Reyes — esta misma migración construye lo que lo consume
    /// (`CatalogoAlimentosListComponent`). Idempotente (ADD COLUMN IF NOT EXISTS + UPDATE con
    /// guarda): re-ejecutable sin romper.
    /// </para>
    /// </summary>
    public partial class AddFlagLimitaTiposInventarioAlimentoYAves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE companies ADD COLUMN IF NOT EXISTS limita_tipos_inventario_alimento_y_aves boolean NOT NULL DEFAULT false;");

            migrationBuilder.Sql(@"
UPDATE public.companies
   SET limita_tipos_inventario_alimento_y_aves = true
 WHERE name = 'Santa Reyes'
   AND limita_tipos_inventario_alimento_y_aves IS DISTINCT FROM true;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE companies DROP COLUMN IF EXISTS limita_tipos_inventario_alimento_y_aves;");
        }
    }
}
