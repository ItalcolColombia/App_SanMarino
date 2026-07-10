using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase3RetireSeguimientoLoteLevante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retiro REVERSIBLE (no DROP): la tabla deprecada de levante se renombra a
            // seguimiento_lote_levante_deprecated. Sus datos (360 filas local, ya
            // convertidas/backfilleadas por Fase3ConvergeLevanteFeature13) se conservan
            // para rollback/conciliación. Idempotente (guard IF EXISTS + choque de nombre).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF to_regclass('public.seguimiento_lote_levante') IS NOT NULL
                       AND to_regclass('public.seguimiento_lote_levante_deprecated') IS NULL THEN
                        ALTER TABLE public.seguimiento_lote_levante RENAME TO seguimiento_lote_levante_deprecated;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverso del rename (restaura el nombre original si existe la deprecada).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF to_regclass('public.seguimiento_lote_levante_deprecated') IS NOT NULL
                       AND to_regclass('public.seguimiento_lote_levante') IS NULL THEN
                        ALTER TABLE public.seguimiento_lote_levante_deprecated RENAME TO seguimiento_lote_levante;
                    END IF;
                END $$;
            ");
        }
    }
}
