using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMenu_ReporteTecnico_GenericLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE public.menus
                SET label = 'Reporte Técnico Sanmarino',
                    updated_at = NOW()
                WHERE id = 19
                  AND key = 'reporte_tecnico';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE public.menus
                SET label = 'Reportes Tecnicos levante SanMarino',
                    updated_at = NOW()
                WHERE id = 19
                  AND key = 'reporte_tecnico';
            ");
        }
    }
}
