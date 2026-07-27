using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadoCierreAuditoriaLotePosturaProduccion : Migration
    {
        /// <summary>
        /// Auditoría del cierre/reapertura del lote de producción (quién, cuándo y por qué). Hasta
        /// ahora <c>estado_cierre</c> no lo escribía nadie: nacía en "Abierta" y se quedaba ahí.
        /// <para>
        /// Idempotente (<c>ADD COLUMN IF NOT EXISTS</c>) según la regla de CLAUDE.md, porque el
        /// deploy aplica las migraciones al arrancar. Nullable y sin backfill: los lotes existentes
        /// nunca se cerraron, así que NULL es el valor correcto y no hay bloqueo de tabla.
        /// </para>
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_postura_produccion
                    ADD COLUMN IF NOT EXISTS estado_cierre_motivo  text,
                    ADD COLUMN IF NOT EXISTS estado_cierre_fecha   timestamp with time zone,
                    ADD COLUMN IF NOT EXISTS estado_cierre_user_id integer;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_postura_produccion
                    DROP COLUMN IF EXISTS estado_cierre_motivo,
                    DROP COLUMN IF EXISTS estado_cierre_fecha,
                    DROP COLUMN IF EXISTS estado_cierre_user_id;
            ");
        }
    }
}
