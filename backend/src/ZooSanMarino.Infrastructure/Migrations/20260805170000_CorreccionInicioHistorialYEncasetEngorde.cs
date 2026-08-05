using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only) de la REFERENCIA de conservación de pollo engorde: los lotes
    /// cuyo historial <c>Inicio</c> no coincide con <c>aves_encasetadas</c>, que por eso quedan fuera
    /// del detector <c>fn_cuadre_aves_engorde</c> (<c>referencia_confiable = false</c>) y no se pueden
    /// auditar. Complementa a <c>20260805150000_CorreccionMaestroAvesEngordeIdentidad</c>, que solo
    /// actúa sobre lotes cuya referencia YA era confiable.
    ///
    /// <para>
    /// Son dos causas distintas y se corrigen en sentidos opuestos, cada una con su evidencia:
    /// </para>
    ///
    /// <para>
    /// <b>Bloque 1 — el Inicio es una plantilla de la carga inicial.</b> Seis lotes recibieron el
    /// mismo <c>25.000 H / 25.000 M / 35-36 mixtas</c> el 2026-03-23. En cuatro de ellos
    /// <c>aves_encasetadas</c> también quedó en 50.000 (los dos números son de plantilla, sin
    /// actividad que permita deducir el real: se dejan intactos y quedan documentados abajo). En los
    /// otros dos el encaset sí se corrigió después al valor real, y el galpón lo confirma: el G0050
    /// manejó 24.384 / 22.535 / 24.000 aves en sus otros ciclos y el G0051, 24.617 / 22.000 / 24.000
    /// — 50.000 es el doble de la capacidad física. Se reescribe el Inicio con el reparto por sexo
    /// que exige la conservación de lo YA registrado (maestro + ventas + bajas aplicadas + ajustes
    /// fantasma). El resultado se corrobora solo: con ese Inicio, el lote 7 cierra en <b>0 exacto en
    /// ambos sexos</b> (se despachó completo) y el 5 deja 161 hembras.
    /// </para>
    ///
    /// <para>
    /// <b>Bloque 2 — el Inicio es el correcto y <c>aves_encasetadas</c> está inflado.</b> Acá la
    /// prueba es que, bajo el Inicio, <b>los dos sexos cierran en 0 exacto</b>
    /// (<c>Inicio − bajas − ventas = 0</c> por sexo), mientras que bajo <c>aves_encasetadas</c> quedan
    /// 700 hembras y 700 machos sobrantes — el mismo excedente repartido en partes iguales, firma de
    /// un encaset digitado de más. Se baja <c>aves_encasetadas</c> al total del Inicio y se realinea
    /// el maestro con la identidad.
    /// </para>
    ///
    /// <para>
    /// <b>Guardas</b> (ninguna regla nombra ids: todas se apoyan en evidencia registrada, así que si
    /// en producción los datos difieren, la fila simplemente no entra):
    /// (1) el bloque 1 exige que existan VENTAS registradas — sin actividad no hay nada que pruebe
    /// cuál era el Inicio real, y los cuatro lotes de plantilla pura quedan fuera;
    /// (2) el bloque 1 exige además que el total deducido dé <b>exactamente</b>
    /// <c>aves_encasetadas</c>; (3) el bloque 2 exige el cierre en 0 en AMBOS sexos, no en el total;
    /// (4) nunca se escribe un valor negativo; (5) <c>IS DISTINCT FROM</c> ⇒ idempotente.
    /// </para>
    ///
    /// <para>
    /// <b>Efecto medido</b> (dump tipo-prod del 05-ago-2026, simulado en transacción + ROLLBACK):
    /// bloque 1 alcanza 2 lotes (id 5 · Sacachun 3b · G0050, e id 7 · Sacachun 2 · G0051) y bloque 2
    /// alcanza 1 (id 30 · SAN GUILLERMO · G0030, encaset 12.700 → 11.300 y maestro 1.744/2.140 →
    /// 1.044/1.440). Ninguna otra fila de la base entra en las condiciones. Después de aplicarla, los
    /// lotes sin referencia confiable bajan de 4 a 1 y <c>fn_cuadre_aves_engorde</c> sigue con 0
    /// descuadrados.
    /// </para>
    ///
    /// <para>
    /// <b>Fuera de alcance a propósito — id 132</b> (Sacachun 3b · G0049 · 2604, encaset 19.387 vs
    /// Inicio 19.187): lote ACTIVO y sin ventas todavía, así que la conservación no puede discriminar
    /// cuál de los dos números es el real. Son 200 aves y hoy muestra bien. Necesita el documento
    /// físico de encasetamiento; no se toca hasta que operación decida.
    /// </para>
    ///
    /// <para>
    /// <b>Fuera de alcance a propósito — ids 3, 4, 6 y 8</b> (CAROLINA G0057/G0058, Kilometro 61
    /// G0037, Sacachun 3A G0043): encaset 50.000 Y el Inicio de plantilla, todos encasetados el
    /// 2026-01-31, entre 0 y 14 días de seguimiento y <b>cero movimientos</b>. Los DOS números son
    /// ficticios y no hay actividad de la cual deducir el real. Pasan desapercibidos al detector
    /// porque su <c>referencia_confiable</c> compara <c>ih + im</c> (25.000 + 25.000 = 50.000) sin las
    /// mixtas. Requieren decisión de negocio: corregirlos o darlos de baja.
    /// </para>
    ///
    /// <para>
    /// <b><c>Down()</c> es no-op deliberado</b>, igual que su migración hermana: el <c>Up()</c> no
    /// inventa valores, los deduce de ventas y bajas ya registradas; revertirlo reintroduciría una
    /// referencia que se sabe falsa. Copia trazable del SQL en
    /// <c>backend/sql/correccion_inicio_historial_y_encaset_engorde.sql</c>.
    /// </para>
    /// </summary>
    public partial class CorreccionInicioHistorialYEncasetEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Bloque 1: reescribir el Inicio de plantilla con el reparto que exige la conservación.
            migrationBuilder.Sql("""
                WITH ini AS (
                    SELECT DISTINCT ON (lote_ave_engorde_id) lote_ave_engorde_id AS id, id AS fila_id,
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
                    SELECT i.fila_id,
                           COALESCE(l.hembras_l, 0) + COALESCE(v.vh, 0) + COALESCE(ap.ph, 0) + COALESCE(aj.ah, 0) AS nh,
                           COALESCE(l.machos_l, 0)  + COALESCE(v.vm, 0) + COALESCE(ap.pm, 0) + COALESCE(aj.am, 0) AS nm
                    FROM lote_ave_engorde l
                    JOIN ini i ON i.id = l.lote_ave_engorde_id
                    LEFT JOIN aj ON aj.id = l.lote_ave_engorde_id
                    LEFT JOIN v  ON v.id  = l.lote_ave_engorde_id
                    LEFT JOIN ap ON ap.id = l.lote_ave_engorde_id
                    WHERE l.deleted_at IS NULL
                      AND COALESCE(l.aves_encasetadas, 0) > 0
                      -- firma de la plantilla de carga inicial
                      AND i.ih = 25000 AND i.im = 25000
                      -- y ese Inicio es demostrablemente incongruente con el encaset
                      AND i.ih + i.im + i.ix <> l.aves_encasetadas
                      -- solo con ventas registradas hay evidencia de cual era el Inicio real
                      AND COALESCE(v.vh, 0) + COALESCE(v.vm, 0) > 0
                      -- y el total deducido tiene que dar EXACTAMENTE el encaset
                      AND (COALESCE(l.hembras_l, 0) + COALESCE(v.vh, 0) + COALESCE(ap.ph, 0) + COALESCE(aj.ah, 0))
                        + (COALESCE(l.machos_l, 0) + COALESCE(v.vm, 0) + COALESCE(ap.pm, 0) + COALESCE(aj.am, 0))
                          = l.aves_encasetadas
                )
                UPDATE historial_lote_pollo_engorde h
                SET aves_hembras = o.nh, aves_machos = o.nm, aves_mixtas = 0
                FROM objetivo o
                WHERE h.id = o.fila_id
                  AND o.nh >= 0 AND o.nm >= 0
                  AND (h.aves_hembras IS DISTINCT FROM o.nh
                    OR h.aves_machos  IS DISTINCT FROM o.nm
                    OR h.aves_mixtas  IS DISTINCT FROM 0);
                """);

            // ── Bloque 2: el Inicio manda; bajar aves_encasetadas y realinear el maestro.
            migrationBuilder.Sql("""
                WITH ini AS (
                    SELECT DISTINCT ON (lote_ave_engorde_id) lote_ave_engorde_id AS id,
                           COALESCE(aves_hembras, 0) AS ih, COALESCE(aves_machos, 0) AS im, COALESCE(aves_mixtas, 0) AS ix
                    FROM historial_lote_pollo_engorde
                    WHERE tipo_lote = 'LoteAveEngorde' AND tipo_registro = 'Inicio' AND lote_ave_engorde_id IS NOT NULL
                    ORDER BY lote_ave_engorde_id, fecha_registro, id
                ), v AS (
                    SELECT lote_ave_engorde_origen_id AS id,
                           SUM(cantidad_hembras) AS vh, SUM(cantidad_machos) AS vm
                    FROM movimiento_pollo_engorde
                    WHERE estado = 'Completado' AND deleted_at IS NULL AND lote_ave_engorde_origen_id IS NOT NULL
                    GROUP BY lote_ave_engorde_origen_id
                ), sg AS (
                    SELECT lote_ave_engorde_id AS id,
                           SUM(COALESCE(mortalidad_hembras, 0) + COALESCE(sel_h, 0) + COALESCE(error_sexaje_hembras, 0)) AS sh,
                           SUM(COALESCE(mortalidad_machos, 0)  + COALESCE(sel_m, 0) + COALESCE(error_sexaje_machos, 0))  AS sm
                    FROM seguimiento_diario_aves_engorde
                    GROUP BY lote_ave_engorde_id
                ), ap AS (
                    SELECT lote_ave_engorde_id AS id,
                           SUM(COALESCE(cantidad_hembras, 0)) AS ph, SUM(COALESCE(cantidad_machos, 0)) AS pm
                    FROM lote_registro_historico_unificado
                    WHERE tipo_evento = 'BAJA_SEGUIMIENTO' AND NOT anulado AND lote_ave_engorde_id IS NOT NULL
                    GROUP BY lote_ave_engorde_id
                ), objetivo AS (
                    SELECT l.lote_ave_engorde_id AS id,
                           i.ih + i.im + i.ix AS nuevo_encaset,
                           i.ih - COALESCE(v.vh, 0) - COALESCE(ap.ph, 0) AS nh,
                           i.im - COALESCE(v.vm, 0) - COALESCE(ap.pm, 0) AS nm
                    FROM lote_ave_engorde l
                    JOIN ini i ON i.id = l.lote_ave_engorde_id
                    LEFT JOIN v  ON v.id  = l.lote_ave_engorde_id
                    LEFT JOIN sg ON sg.id = l.lote_ave_engorde_id
                    LEFT JOIN ap ON ap.id = l.lote_ave_engorde_id
                    WHERE l.deleted_at IS NULL
                      AND COALESCE(l.aves_encasetadas, 0) > 0
                      AND i.ix = 0
                      AND i.ih + i.im + i.ix <> l.aves_encasetadas
                      -- prueba: bajo el Inicio, AMBOS sexos cierran exactamente en 0
                      AND i.ih - COALESCE(sg.sh, 0) - COALESCE(v.vh, 0) = 0
                      AND i.im - COALESCE(sg.sm, 0) - COALESCE(v.vm, 0) = 0
                )
                UPDATE lote_ave_engorde l
                SET aves_encasetadas = o.nuevo_encaset, hembras_l = o.nh, machos_l = o.nm
                FROM objetivo o
                WHERE l.lote_ave_engorde_id = o.id
                  AND o.nuevo_encaset > 0 AND o.nh >= 0 AND o.nm >= 0
                  AND (l.aves_encasetadas IS DISTINCT FROM o.nuevo_encaset
                    OR l.hembras_l        IS DISTINCT FROM o.nh
                    OR l.machos_l         IS DISTINCT FROM o.nm);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op deliberado: el Up() deduce la referencia de las ventas y bajas ya registradas.
            // Revertirlo devolvería un Inicio de plantilla (25.000/25.000) que se sabe falso y un
            // encaset inflado. Los valores previos exactos están en
            // backend/sql/correccion_inicio_historial_y_encaset_engorde.sql.
        }
    }
}
