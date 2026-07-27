using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <c>hora_encasetamiento</c> en <c>lote_ave_engorde</c> y <c>lote_reproductora_ave_engorde</c>:
    /// la hora a la que llegaron las aves decide si el PRIMER registro de seguimiento va el mismo día
    /// del encasetamiento o el siguiente. Desde las <b>13:00</b> las aves ya no alcanzan a consumir ese
    /// día, así que el primer consumo pasa al día siguiente.
    /// <para>
    /// La fecha de encasetamiento NO cambia (sigue siendo el día real de llegada) y la edad se sigue
    /// contando desde ella: un lote tardío arranca en edad 1. Por eso ninguna función SQL cambia.
    /// </para>
    /// <para>
    /// Columna NULLABLE y sin backfill a propósito: los lotes existentes quedan sin hora ⇒
    /// comportamiento idéntico al previo. Idempotente (<c>ADD COLUMN IF NOT EXISTS</c>) para tolerar
    /// reintentos de deploy.
    /// </para>
    /// </summary>
    public partial class AddHoraEncasetamientoLotesEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_ave_engorde
                    ADD COLUMN IF NOT EXISTS hora_encasetamiento time NULL;

                ALTER TABLE lote_reproductora_ave_engorde
                    ADD COLUMN IF NOT EXISTS hora_encasetamiento time NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_ave_engorde
                    DROP COLUMN IF EXISTS hora_encasetamiento;

                ALTER TABLE lote_reproductora_ave_engorde
                    DROP COLUMN IF EXISTS hora_encasetamiento;
            ");
        }
    }
}
