using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Backfill: le quita el sufijo de corrida a los lotes de engorde de <c>ItalcolEcuador</c> que
    /// nacieron mientras <c>companies.nombre_lote_incluye_corrida</c> estuvo prendido por error
    /// (ver <see cref="ApagarNombreLoteIncluyeCorridaEcuador"/>, que corre antes y lo apaga).
    /// <para>
    /// Medido el 1-sep-2026 son dos — "2605 - 1" (Kilometro 86) y "2604 - 1" (CAROLINA GALPON 7) —,
    /// pero el <c>UPDATE</c> se acota por REGLA y no por id, asi que tambien alcanza a los que
    /// nazcan con el mismo defecto entre hoy y el deploy. Despues del deploy no puede haber mas: el
    /// flag ya quedo apagado. Los eliminados con el mismo defecto NO se tocan (no se ven en ninguna
    /// pantalla ni reporte).
    /// </para>
    /// <para>
    /// Quedan con el nombre que habrian tenido de nacer con el flag apagado: el del lote base, sin
    /// sufijo. <c>numero_corrida</c> no se toca (sigue en 1, que es lo correcto), y la proxima
    /// apertura del mismo base en el mismo galpon se calcula por <c>MAX(numero_corrida)</c>, no por
    /// el nombre — el rename no altera ninguna corrida futura.
    /// </para>
    /// <para>
    /// El nombre anterior se guarda en <c>_backup_rename_lote_engorde_ecuador_20260901</c>: es la
    /// auditoria de que se cambio en produccion y lo que hace al <c>Down()</c> exacto por
    /// construccion — restaura fila por fila lo que el <c>Up()</c> toco, en vez de adivinarlo con una
    /// ventana de fechas. Idempotente: la 2a pasada no matchea ninguna fila.
    /// </para>
    /// <para>
    /// El <c>NOT EXISTS</c> aborta la fila si el nombre destino ya lo usa otro lote vivo del mismo
    /// galpon. Hoy no pasa (verificado: 0 colisiones), pero la unicidad real de un lote es
    /// compania + granja + GALPON + nombre y no hay indice que la defienda.
    /// </para>
    /// </summary>
    public partial class RenombrarLotesEngordeEcuadorSinSufijoCorrida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS _backup_rename_lote_engorde_ecuador_20260901 (
                    lote_ave_engorde_id    integer PRIMARY KEY,
                    lote_nombre_anterior   text        NOT NULL,
                    renombrado_at          timestamptz NOT NULL DEFAULT now()
                );

                WITH afectados AS (
                    SELECT l.lote_ave_engorde_id AS id, l.lote_nombre AS anterior, b.nombre AS destino
                      FROM lote_ave_engorde l
                      JOIN lote_base_engorde b ON b.id = l.lote_base_engorde_id
                      JOIN companies         c ON c.id = l.company_id
                     WHERE c.name = 'ItalcolEcuador'
                       AND l.numero_corrida = 1
                       AND l.lote_nombre = b.nombre || ' - 1'
                       AND l.deleted_at IS NULL
                       AND l.created_at >= timestamptz '2026-08-22 00:00:00-05'
                       AND NOT EXISTS (
                             SELECT 1 FROM lote_ave_engorde o
                              WHERE o.company_id = l.company_id
                                AND o.galpon_id IS NOT DISTINCT FROM l.galpon_id
                                AND o.lote_nombre = b.nombre
                                AND o.deleted_at IS NULL
                                AND o.lote_ave_engorde_id <> l.lote_ave_engorde_id)
                ), respaldo AS (
                    INSERT INTO _backup_rename_lote_engorde_ecuador_20260901 (lote_ave_engorde_id, lote_nombre_anterior)
                    SELECT id, anterior FROM afectados
                    ON CONFLICT (lote_ave_engorde_id) DO NOTHING
                    RETURNING lote_ave_engorde_id
                )
                UPDATE lote_ave_engorde l
                   SET lote_nombre = a.destino
                  FROM afectados a
                 WHERE l.lote_ave_engorde_id = a.id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Inverso exacto: restaura SOLO lo que el Up() respaldo, con su nombre anterior textual.
            migrationBuilder.Sql(@"
                UPDATE lote_ave_engorde l
                   SET lote_nombre = r.lote_nombre_anterior
                  FROM _backup_rename_lote_engorde_ecuador_20260901 r
                 WHERE l.lote_ave_engorde_id = r.lote_ave_engorde_id;

                DROP TABLE IF EXISTS _backup_rename_lote_engorde_ecuador_20260901;
            ");
        }
    }
}
