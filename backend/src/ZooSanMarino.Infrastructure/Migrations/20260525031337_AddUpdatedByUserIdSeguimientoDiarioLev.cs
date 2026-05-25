using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedByUserIdSeguimientoDiarioLev : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    ADD COLUMN IF NOT EXISTS updated_by_user_id VARCHAR(64) NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    DROP COLUMN IF EXISTS updated_by_user_id;
            ");
        }
    }
}
