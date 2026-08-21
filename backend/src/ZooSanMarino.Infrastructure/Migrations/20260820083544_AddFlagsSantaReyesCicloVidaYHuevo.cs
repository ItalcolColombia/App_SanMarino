using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlagsSantaReyesCicloVidaYHuevo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente (CLAUDE.md §🗄️): ADD COLUMN IF NOT EXISTS en vez del AddColumn generado,
            // para poder re-correr la migración sin romper si la columna ya está en la BD.
            migrationBuilder.Sql(
                "ALTER TABLE companies ADD COLUMN IF NOT EXISTS consumo_alimento_solo_hembras boolean NOT NULL DEFAULT false;");

            migrationBuilder.Sql(
                "ALTER TABLE companies ADD COLUMN IF NOT EXISTS huevo_primera_postura_hasta_semana integer NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE companies ADD COLUMN IF NOT EXISTS oculta_machos_en_postura boolean NOT NULL DEFAULT false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE companies DROP COLUMN IF EXISTS consumo_alimento_solo_hembras;");
            migrationBuilder.Sql("ALTER TABLE companies DROP COLUMN IF EXISTS huevo_primera_postura_hasta_semana;");
            migrationBuilder.Sql("ALTER TABLE companies DROP COLUMN IF EXISTS oculta_machos_en_postura;");
        }
    }
}
