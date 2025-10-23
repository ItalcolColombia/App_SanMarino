using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class add_extras_to_seguimiento_levante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "consumo_kg_machos",
                table: "seguimiento_lote_levante",
                type: "double precision",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "cv_h",
                table: "seguimiento_lote_levante",
                type: "double precision",
                precision: 6,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "cv_m",
                table: "seguimiento_lote_levante",
                type: "double precision",
                precision: 6,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "peso_prom_h",
                table: "seguimiento_lote_levante",
                type: "double precision",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "peso_prom_m",
                table: "seguimiento_lote_levante",
                type: "double precision",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "uniformidad_h",
                table: "seguimiento_lote_levante",
                type: "double precision",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "uniformidad_m",
                table: "seguimiento_lote_levante",
                type: "double precision",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "consumo_kg_machos",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "cv_h",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "cv_m",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "peso_prom_h",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "peso_prom_m",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "uniformidad_h",
                table: "seguimiento_lote_levante");

            migrationBuilder.DropColumn(
                name: "uniformidad_m",
                table: "seguimiento_lote_levante");
        }
    }
}
