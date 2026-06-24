using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Renombra el ítem de menú de "Guía genética Ecuador" a "Guia Genetica"
    /// para reflejar que el módulo ahora soporta múltiples países (Panamá, Ecuador).
    /// Idempotente.
    /// </summary>
    public partial class RenameMenu_GuiaGenetica : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE menus
SET label = 'Guia Genetica'
WHERE route = '/config/guia-genetica-ecuador'
  AND label <> 'Guia Genetica';
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE menus
SET label = 'Guía genética Ecuador'
WHERE route = '/config/guia-genetica-ecuador'
  AND label = 'Guia Genetica';
");
        }
    }
}
