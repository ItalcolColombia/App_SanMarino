using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Índice único por DÍA CALENDARIO (UTC) en las tres tablas de seguimiento que todavía no lo
    /// tenían: pollo engorde, levante y reproductora. Producción ya lo tiene desde
    /// <c>20260801070000_IndiceUnicoSeguimientoProduccionDia</c>, y esta migración copia ese patrón.
    ///
    /// <para>
    /// <b>El problema.</b> Los índices únicos vigentes son sobre <c>(lote, fecha)</c> con <c>fecha</c>
    /// de tipo <c>timestamptz</c> ⇒ comparan el INSTANTE, no el día. Los escritores no usan la misma
    /// hora: el formulario manual escribe a <c>12:00Z</c> (y hasta jul-2026 escribía a <c>17:00Z</c>,
    /// mediodía local) mientras el trigger del cruce de reproductora escribe a <c>00:00Z</c>. Dos
    /// filas del mismo día calendario conviven sin que el índice se entere. Medido el 28-ago-2026
    /// contra la copia de producción: 5 días duplicados en engorde (todos ItalcolPanama, todos con el
    /// patrón cruce+manual) y 1 en levante (Demo). Reproductora: 0.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué el índice es PARCIAL y no se borra nada.</b> Las filas históricas duplicadas ya
    /// aplicaron su efecto: cada una tiene su movimiento en <c>inventario_gestion_movimiento</c> y su
    /// fila en <c>lote_registro_historico_unificado</c>. Borrarlas dejaría el movimiento huérfano y el
    /// histórico apuntando a un seguimiento inexistente — el histórico se ANULA, nunca se abandona.
    /// Así que se excluyen por id del índice, con nombre y apellido, y se protege todo lo demás
    /// (incluido cualquier alta futura sobre esos mismos días, que sí tendría id nuevo y sí entraría
    /// al índice).
    /// </para>
    ///
    /// <para>
    /// <b>Fail-soft, como el precedente.</b> Si al correr quedan duplicados FUERA de la lista de
    /// excluidos, el índice no se crea y queda un <c>RAISE WARNING</c> en el log del deploy. JAMÁS se
    /// tira el arranque de producción por esto (lección del incidente SIGSEGV de migraciones).
    /// Idempotente: <c>IF NOT EXISTS</c> + chequeo previo.
    /// </para>
    ///
    /// <para>
    /// Plan: <c>fase_de_desarrollo/indice_unico_dia_seguimientos_plan.md</c>.
    /// Diagnóstico reproducible: <c>backend/sql/verificar_duplicados_dia_seguimiento.sql</c>.
    /// </para>
    /// </summary>
    public partial class IndiceUnicoDiaSeguimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $mig$
                BEGIN
                    -- ─────────────────────────────────────────────────────────────────────────
                    -- POLLO ENGORDE
                    --
                    -- Excluidos (medidos el 28-ago-2026 sobre la copia de produccion; cada uno es
                    -- la fila MANUAL que colisiona con una fila del CRUCE del mismo dia):
                    --   10859  lote 161 (DONA MARIA)  2026-06-28   ya aplico inventario + historico
                    --   10860  lote 161               2026-06-29   ya aplico inventario + historico
                    --   10861  lote 161               2026-06-30   ya aplico inventario + historico
                    --   11224  lote 178 (TROFARELLO)  2026-07-27   ya aplico inventario + historico
                    --   12676  lote 216 (DAYLAND)     2026-08-17   sin aplicar; se borra por la UI
                    --
                    -- 12676 se excluye igual para que la migracion no dependa de que alguien lo haya
                    -- borrado antes: si ya no existe, la exclusion queda inerte.
                    -- ─────────────────────────────────────────────────────────────────────────
                    IF EXISTS (
                        SELECT 1 FROM seguimiento_diario_aves_engorde
                         WHERE id NOT IN (10859, 10860, 10861, 11224, 12676)
                         GROUP BY lote_ave_engorde_id, ((fecha AT TIME ZONE 'UTC')::date)
                        HAVING count(*) > 1
                    ) THEN
                        RAISE WARNING 'seguimiento_diario_aves_engorde: hay duplicados por (lote, dia UTC) fuera de la lista de excluidos; el indice unico por dia NO se creo. Correr backend/sql/verificar_duplicados_dia_seguimiento.sql y depurar.';
                    ELSE
                        CREATE UNIQUE INDEX IF NOT EXISTS ux_seg_diario_aves_engorde_lote_dia_utc
                            ON seguimiento_diario_aves_engorde (lote_ave_engorde_id, ((fecha AT TIME ZONE 'UTC')::date))
                         WHERE id NOT IN (10859, 10860, 10861, 11224, 12676);
                    END IF;

                    -- ─────────────────────────────────────────────────────────────────────────
                    -- LEVANTE — clave (tipo, lote, reproductora, dia). Espeja uq_sdlr_tipo_lote_rep_fecha.
                    --
                    -- Excluido: 1090 (empresa Demo, lote 127, 2026-07-11, guardado a 00:00Z). Su par
                    -- 1089 (misma fecha a 17:00Z) queda DENTRO del indice. Los dos ya aplicaron
                    -- inventario.
                    -- ─────────────────────────────────────────────────────────────────────────
                    IF EXISTS (
                        SELECT 1 FROM seguimiento_diario_levante
                         WHERE id NOT IN (1090)
                         GROUP BY tipo_seguimiento, lote_id, COALESCE(reproductora_id, ''), ((fecha AT TIME ZONE 'UTC')::date)
                        HAVING count(*) > 1
                    ) THEN
                        RAISE WARNING 'seguimiento_diario_levante: hay duplicados por (tipo, lote, reproductora, dia UTC) fuera de la lista de excluidos; el indice unico por dia NO se creo.';
                    ELSE
                        CREATE UNIQUE INDEX IF NOT EXISTS ux_sdlr_tipo_lote_rep_dia_utc
                            ON seguimiento_diario_levante (tipo_seguimiento, lote_id, COALESCE(reproductora_id, ''), ((fecha AT TIME ZONE 'UTC')::date))
                         WHERE id NOT IN (1090);
                    END IF;

                    -- Levante, segunda clave: la del indice parcial de produccion (uq_sdlr_prod_lote_fecha).
                    -- Medido: 0 duplicados por dia, asi que va sin exclusiones.
                    IF EXISTS (
                        SELECT 1 FROM seguimiento_diario_levante
                         WHERE tipo_seguimiento = 'produccion' AND lote_id_int IS NOT NULL
                         GROUP BY lote_id_int, ((fecha AT TIME ZONE 'UTC')::date)
                        HAVING count(*) > 1
                    ) THEN
                        RAISE WARNING 'seguimiento_diario_levante (produccion): hay duplicados por (lote_id_int, dia UTC); el indice unico por dia NO se creo.';
                    ELSE
                        CREATE UNIQUE INDEX IF NOT EXISTS ux_sdlr_prod_lote_dia_utc
                            ON seguimiento_diario_levante (lote_id_int, ((fecha AT TIME ZONE 'UTC')::date))
                         WHERE tipo_seguimiento = 'produccion' AND lote_id_int IS NOT NULL;
                    END IF;

                    -- ─────────────────────────────────────────────────────────────────────────
                    -- REPRODUCTORA — 0 duplicados medidos, indice limpio sin exclusiones.
                    -- ─────────────────────────────────────────────────────────────────────────
                    IF EXISTS (
                        SELECT 1 FROM seguimiento_diario_lote_reproductora_aves_engorde
                         GROUP BY lote_reproductora_ave_engorde_id, ((fecha AT TIME ZONE 'UTC')::date)
                        HAVING count(*) > 1
                    ) THEN
                        RAISE WARNING 'seguimiento_diario_lote_reproductora_aves_engorde: hay duplicados por (lote, dia UTC); el indice unico por dia NO se creo.';
                    ELSE
                        CREATE UNIQUE INDEX IF NOT EXISTS ux_seg_diario_lrae_lote_dia_utc
                            ON seguimiento_diario_lote_reproductora_aves_engorde (lote_reproductora_ave_engorde_id, ((fecha AT TIME ZONE 'UTC')::date));
                    END IF;
                END
                $mig$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ux_seg_diario_aves_engorde_lote_dia_utc;
                DROP INDEX IF EXISTS ux_sdlr_tipo_lote_rep_dia_utc;
                DROP INDEX IF EXISTS ux_sdlr_prod_lote_dia_utc;
                DROP INDEX IF EXISTS ux_seg_diario_lrae_lote_dia_utc;
                """);
        }
    }
}
