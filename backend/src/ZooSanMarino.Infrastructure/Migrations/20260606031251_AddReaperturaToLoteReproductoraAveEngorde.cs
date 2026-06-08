using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReaperturaToLoteReproductoraAveEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente (CLAUDE.md): columnas de reapertura de lote reproductora cerrado.
            migrationBuilder.Sql(@"
ALTER TABLE lote_reproductora_ave_engorde ADD COLUMN IF NOT EXISTS novedad_apertura text NULL;
ALTER TABLE lote_reproductora_ave_engorde ADD COLUMN IF NOT EXISTS reabierto boolean NOT NULL DEFAULT false;
ALTER TABLE lote_reproductora_ave_engorde ADD COLUMN IF NOT EXISTS reabierto_at timestamptz NULL;
ALTER TABLE lote_reproductora_ave_engorde ADD COLUMN IF NOT EXISTS reabierto_por integer NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "novedad_apertura",
                table: "lote_reproductora_ave_engorde");

            migrationBuilder.DropColumn(
                name: "reabierto",
                table: "lote_reproductora_ave_engorde");

            migrationBuilder.DropColumn(
                name: "reabierto_at",
                table: "lote_reproductora_ave_engorde");

            migrationBuilder.DropColumn(
                name: "reabierto_por",
                table: "lote_reproductora_ave_engorde");
        }
    }
}
