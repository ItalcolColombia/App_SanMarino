using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// F10 §9 del plan de Santa Reyes: <c>traslado_huevos.TotalHuevos</c> deja de ser una propiedad
    /// calculada en C# (nunca mapeada por EF) y pasa a columna real, porque un traslado por ÍTEMS del
    /// catálogo (<c>Metadata</c>, nueva acá) deja las 11 <c>cantidad_*</c> en 0 — el service la fija
    /// explícitamente en los dos flujos. Idempotente (<c>IF NOT EXISTS</c>) + backfill de las filas
    /// existentes (recomputar la misma suma de siempre es inofensivo, no hace falta guarda).
    /// </summary>
    public partial class SantaReyesF10TrasladoHuevosPorItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.traslado_huevos
    ADD COLUMN IF NOT EXISTS metadata jsonb NULL;
");

            migrationBuilder.Sql(@"
ALTER TABLE public.traslado_huevos
    ADD COLUMN IF NOT EXISTS total_huevos integer NOT NULL DEFAULT 0;
");

            migrationBuilder.Sql(@"
UPDATE public.traslado_huevos
   SET total_huevos = cantidad_limpio + cantidad_tratado + cantidad_sucio + cantidad_deforme +
                       cantidad_blanco + cantidad_doble_yema + cantidad_piso + cantidad_pequeno +
                       cantidad_roto + cantidad_desecho + cantidad_otro
 WHERE total_huevos = 0;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "metadata",
                table: "traslado_huevos");

            migrationBuilder.DropColumn(
                name: "total_huevos",
                table: "traslado_huevos");
        }
    }
}
