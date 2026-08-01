using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// El modelo EF declara UNIQUE (lote_id, fecha_registro) (SeguimientoProduccionConfiguration:232)
    /// pero la BD nunca lo tuvo (drift verificado en local/prod-dump: solo PK + GIN + 2 btree).
    /// Se crean DOS índices únicos de forma DEFENSIVA:
    ///  1. (lote_id, fecha_registro) — alinea BD ↔ modelo (nombre del snapshot).
    ///  2. (lote_id, día UTC de fecha_registro) — el invariante REAL «un registro por lote y día»
    ///     que hoy solo sostiene la aplicación (timezone(text,timestamptz) es IMMUTABLE, el índice
    ///     por expresión es válido).
    /// Si hay duplicados, el índice NO se crea y queda un RAISE WARNING en el log del deploy —
    /// JAMÁS se tira el arranque de prod por esto (lección del incidente SIGSEGV de migraciones).
    /// Local 2026-08-01: 0 duplicados por timestamp y 0 por día UTC (verificado).
    /// Idempotente: IF NOT EXISTS + chequeo previo.
    /// </summary>
    public partial class IndiceUnicoSeguimientoProduccionDia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $mig$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM seguimiento_diario_produccion
                         GROUP BY lote_id, fecha_registro
                        HAVING count(*) > 1
                    ) THEN
                        RAISE WARNING 'seguimiento_diario_produccion: hay duplicados por (lote_id, fecha_registro); el indice unico NO se creo. Depurar y re-ejecutar.';
                    ELSE
                        CREATE UNIQUE INDEX IF NOT EXISTS ix_seguimiento_diario_produccion_lote_id_fecha_registro
                            ON seguimiento_diario_produccion (lote_id, fecha_registro);
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM seguimiento_diario_produccion
                         GROUP BY lote_id, ((fecha_registro AT TIME ZONE 'UTC')::date)
                        HAVING count(*) > 1
                    ) THEN
                        RAISE WARNING 'seguimiento_diario_produccion: hay duplicados por (lote_id, dia UTC); el indice unico por dia NO se creo. Depurar y re-ejecutar.';
                    ELSE
                        CREATE UNIQUE INDEX IF NOT EXISTS ux_seguimiento_diario_produccion_lote_dia_utc
                            ON seguimiento_diario_produccion (lote_id, ((fecha_registro AT TIME ZONE 'UTC')::date));
                    END IF;
                END
                $mig$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ux_seguimiento_diario_produccion_lote_dia_utc;
                DROP INDEX IF EXISTS ix_seguimiento_diario_produccion_lote_id_fecha_registro;
                """);
        }
    }
}
