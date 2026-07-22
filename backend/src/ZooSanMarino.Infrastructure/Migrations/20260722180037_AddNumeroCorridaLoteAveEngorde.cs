using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNumeroCorridaLoteAveEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente (regla CLAUDE.md): la app aplica migraciones al arrancar y la BD puede
            // reintentar; ADD COLUMN / CREATE INDEX condicionales evitan fallos si ya existen.
            migrationBuilder.Sql(
                "ALTER TABLE public.lote_ave_engorde ADD COLUMN IF NOT EXISTS numero_corrida integer;");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_lote_ave_engorde_corrida " +
                "ON public.lote_ave_engorde (company_id, lote_base_engorde_id, galpon_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_lote_ave_engorde_corrida;");
            migrationBuilder.Sql(
                "ALTER TABLE public.lote_ave_engorde DROP COLUMN IF EXISTS numero_corrida;");
        }
    }
}
