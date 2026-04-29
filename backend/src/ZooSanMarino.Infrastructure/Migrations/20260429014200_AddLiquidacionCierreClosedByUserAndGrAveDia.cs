using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiquidacionCierreClosedByUserAndGrAveDia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "closed_by_user_id",
                table: "liquidacion_cierre_lote_levante",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "consumo_gr_ave_dia_semana25guia",
                table: "liquidacion_cierre_lote_levante",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "closed_by_user_id",
                table: "liquidacion_cierre_lote_levante");

            migrationBuilder.DropColumn(
                name: "consumo_gr_ave_dia_semana25guia",
                table: "liquidacion_cierre_lote_levante");
        }
    }
}
