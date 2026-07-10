using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase3RetireProduccionSeguimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retiro REVERSIBLE (no DROP): la tabla deprecada produccion_seguimiento
            // se renombra a produccion_seguimiento_deprecated. Sus datos (0 filas local,
            // ya backfilleados por Fase3BackfillProduccionDelta) se conservan para
            // rollback/conciliación. Idempotente (IF EXISTS + guard contra choque de nombre).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF to_regclass('public.produccion_seguimiento') IS NOT NULL
                       AND to_regclass('public.produccion_seguimiento_deprecated') IS NULL THEN
                        ALTER TABLE public.produccion_seguimiento RENAME TO produccion_seguimiento_deprecated;
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
                    IF to_regclass('public.produccion_seguimiento_deprecated') IS NOT NULL
                       AND to_regclass('public.produccion_seguimiento') IS NULL THEN
                        ALTER TABLE public.produccion_seguimiento_deprecated RENAME TO produccion_seguimiento;
                    END IF;
                END $$;
            ");
        }
    }
}
