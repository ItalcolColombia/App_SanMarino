using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only) del maestro de aves de pollo engorde: realinea
    /// <c>lote_ave_engorde.hembras_l / machos_l</c> con la identidad de conservación derivada de lo
    /// que ya está registrado:
    /// <code>
    /// maestro = inicio(historial) − ventas Completado − bajas BAJA_SEGUIMIENTO − ajustes fantasma
    /// </code>
    /// <para>
    /// <b>Origen:</b> ticket 05-ago-2026 (el seguimiento diario y la venta mostraban aves distintas).
    /// El fix de fórmula viajó en el commit <c>3998aa2</c>; esta migración corrige los lotes cuyo
    /// maestro quedó desalineado. Copia trazable del SQL en
    /// <c>backend/sql/correccion_maestro_aves_engorde_identidad.sql</c>.
    /// </para>
    /// <para>
    /// <b>Los ajustes fantasma se restan a propósito.</b> Los descuentos deliberados de aves que
    /// nunca existieron viven como <c>historial_lote_pollo_engorde</c> con
    /// <c>tipo_registro='Ajuste'</c> (corrección de los lotes «2601», todos liquidados). Omitirlos
    /// haría que esta migración les devolviera 1.552 aves fantasma a lotes cerrados — por eso entran
    /// en la identidad y esos lotes quedan intactos.
    /// </para>
    /// <para>
    /// <b>Guardas:</b> (1) solo lotes donde el historial <c>Inicio</c> cuadra con
    /// <c>aves_encasetadas</c> — si no cuadra, la referencia no es confiable y el lote se deja como
    /// está; (2) nunca se escribe un maestro negativo; (3) <c>IS DISTINCT FROM</c> ⇒ idempotente,
    /// re-ejecutarla no toca ninguna fila.
    /// </para>
    /// <para>
    /// <b>Efecto medido</b> (dump tipo-prod, simulado en transacción + ROLLBACK): Panamá 0 lotes;
    /// Ecuador 2 — id 107 (Km 61 · G1 · 2604) −17 aves, que son exactamente la fila
    /// <c>BAJA_SEGUIMIENTO</c> del 24-07 cuyo descuento nunca llegó al maestro; e id 184
    /// (SAN GUILLERMO · G1 · 2604) que solo corrige el reparto por sexo, con el total invariante.
    /// Tras aplicarla, ambos coinciden con <c>fn_seguimiento_diario_engorde</c>.
    /// </para>
    /// <para>
    /// <b><c>Down()</c> es intencionalmente no-op.</b> El <c>Up()</c> no inventa un valor: lleva el
    /// maestro al que se deduce de las ventas y bajas ya registradas, así que revertirlo sería
    /// reintroducir un descuadre conocido. Los valores previos exactos quedan documentados en el
    /// archivo SQL para una reversión manual si alguna vez hiciera falta.
    /// </para>
    /// </summary>
    public partial class CorreccionMaestroAvesEngordeIdentidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH ini AS (
                    SELECT DISTINCT ON (lote_ave_engorde_id) lote_ave_engorde_id AS id,
                           COALESCE(aves_hembras, 0) AS ih, COALESCE(aves_machos, 0) AS im, COALESCE(aves_mixtas, 0) AS ix
                    FROM historial_lote_pollo_engorde
                    WHERE tipo_lote = 'LoteAveEngorde' AND tipo_registro = 'Inicio' AND lote_ave_engorde_id IS NOT NULL
                    ORDER BY lote_ave_engorde_id, fecha_registro, id
                ), aj AS (
                    SELECT lote_ave_engorde_id AS id,
                           SUM(COALESCE(aves_hembras, 0)) AS ah, SUM(COALESCE(aves_machos, 0)) AS am
                    FROM historial_lote_pollo_engorde
                    WHERE tipo_lote = 'LoteAveEngorde' AND tipo_registro = 'Ajuste' AND lote_ave_engorde_id IS NOT NULL
                    GROUP BY lote_ave_engorde_id
                ), v AS (
                    SELECT lote_ave_engorde_origen_id AS id,
                           SUM(cantidad_hembras) AS vh, SUM(cantidad_machos) AS vm
                    FROM movimiento_pollo_engorde
                    WHERE estado = 'Completado' AND deleted_at IS NULL AND lote_ave_engorde_origen_id IS NOT NULL
                    GROUP BY lote_ave_engorde_origen_id
                ), ap AS (
                    SELECT lote_ave_engorde_id AS id,
                           SUM(COALESCE(cantidad_hembras, 0)) AS ph, SUM(COALESCE(cantidad_machos, 0)) AS pm
                    FROM lote_registro_historico_unificado
                    WHERE tipo_evento = 'BAJA_SEGUIMIENTO' AND NOT anulado AND lote_ave_engorde_id IS NOT NULL
                    GROUP BY lote_ave_engorde_id
                ), objetivo AS (
                    SELECT l.lote_ave_engorde_id AS id,
                           i.ih - COALESCE(v.vh, 0) - COALESCE(ap.ph, 0) - COALESCE(aj.ah, 0) AS nh,
                           i.im - COALESCE(v.vm, 0) - COALESCE(ap.pm, 0) - COALESCE(aj.am, 0) AS nm
                    FROM lote_ave_engorde l
                    JOIN ini i ON i.id = l.lote_ave_engorde_id
                    LEFT JOIN aj ON aj.id = l.lote_ave_engorde_id
                    LEFT JOIN v  ON v.id  = l.lote_ave_engorde_id
                    LEFT JOIN ap ON ap.id = l.lote_ave_engorde_id
                    WHERE l.deleted_at IS NULL
                      AND COALESCE(l.aves_encasetadas, 0) > 0
                      AND i.ih + i.im + i.ix = l.aves_encasetadas
                )
                UPDATE lote_ave_engorde l
                SET hembras_l = o.nh, machos_l = o.nm
                FROM objetivo o
                WHERE l.lote_ave_engorde_id = o.id
                  AND o.nh >= 0 AND o.nm >= 0
                  AND (l.hembras_l IS DISTINCT FROM o.nh OR l.machos_l IS DISTINCT FROM o.nm);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op deliberado: el Up() no escribe un valor arbitrario sino el que se deduce de las
            // ventas y bajas ya registradas. Revertirlo reintroduciría el descuadre. Los valores
            // previos están documentados en backend/sql/correccion_maestro_aves_engorde_identidad.sql.
        }
    }
}
