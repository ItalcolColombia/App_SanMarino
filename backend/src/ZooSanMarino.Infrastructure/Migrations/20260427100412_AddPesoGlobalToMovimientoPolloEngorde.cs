using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPesoGlobalToMovimientoPolloEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "peso_bruto_global",
                table: "movimiento_pollo_engorde",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "peso_neto",
                table: "movimiento_pollo_engorde",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "peso_neto_global",
                table: "movimiento_pollo_engorde",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "peso_tara_global",
                table: "movimiento_pollo_engorde",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "promedio_peso_ave",
                table: "movimiento_pollo_engorde",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "peso_bruto_global",
                table: "movimiento_pollo_engorde");

            migrationBuilder.DropColumn(
                name: "peso_neto",
                table: "movimiento_pollo_engorde");

            migrationBuilder.DropColumn(
                name: "peso_neto_global",
                table: "movimiento_pollo_engorde");

            migrationBuilder.DropColumn(
                name: "peso_tara_global",
                table: "movimiento_pollo_engorde");

            migrationBuilder.DropColumn(
                name: "promedio_peso_ave",
                table: "movimiento_pollo_engorde");
        }
    }
}
