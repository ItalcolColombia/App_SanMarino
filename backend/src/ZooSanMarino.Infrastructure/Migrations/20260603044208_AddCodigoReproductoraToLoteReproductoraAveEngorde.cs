using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodigoReproductoraToLoteReproductoraAveEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE lote_reproductora_ave_engorde ADD COLUMN IF NOT EXISTS codigo_reproductora VARCHAR(100);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "codigo_reproductora",
                table: "lote_reproductora_ave_engorde");
        }
    }
}
