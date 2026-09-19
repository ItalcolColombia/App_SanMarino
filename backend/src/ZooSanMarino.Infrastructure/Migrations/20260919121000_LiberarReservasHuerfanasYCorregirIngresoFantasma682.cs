using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only, sin cambio de modelo) de lo que dejó apagar la doble validación de
    /// Santa Reyes con pendientes (18-sep-2026). Complementa a
    /// <c>ValidarPendientesSinDobleValidacionProduccionColombia</c>, que valida los 6 registros colgados.
    ///
    /// <para>
    /// <b>1) Reservas huérfanas (genérico).</b> Toda reserva de alimento o de aves ACTIVA cuyo registro dueño
    /// YA NO EXISTE pasa a LIBERADA (<c>liberada_at = now()</c>), que es lo que hace <c>LiberarAsync</c> cuando
    /// se borra un registro con la doble validación encendida. Una reserva ACTIVA sin dueño solo compromete
    /// stock y aves para siempre. Mismo criterio de «dueño inexistente» que
    /// <c>backend/sql/verificar_validado_sin_reserva.sql</c> ([4]): producción cuenta como inexistente si está
    /// borrado. Caso medido: alimento 982 kg del #682.
    /// </para>
    ///
    /// <para>
    /// <b>2) Ingreso fantasma del #682 (específico, con guardas).</b> Al borrar el #682 con el flag YA apagado,
    /// el camino «devolver stock» registró un <c>Ingreso</c> de 982 kg (movimiento #16859,
    /// <c>Seguimiento producción #682 (devolución por eliminación)</c>) <b>sin ningún Consumo previo</b>: el
    /// registro se había creado con el flag encendido, o sea que solo tenía el alimento SEPARADO y nunca salió
    /// nada. Y lo acreditó sobre el ítem 373 <b>de inventario</b> («MQ PREPICO ARRANQUE SR INTELLA»), no sobre
    /// el 365 que consumen los registros reales: tomó el <c>catalogItemId</c> 373 como si fuera un
    /// <c>item_inventario.id</c> (existe, y es OTRO ítem). Resultado: un renglón de stock de 982 kg de un
    /// alimento que no se tiene.
    /// </para>
    ///
    /// <para>
    /// Se compensa con un movimiento <c>AjusteStock</c> («Ajuste manual», el mismo tipo que escribe «Ajustar
    /// stock» de la pantalla) y se baja ese renglón exactamente esos kilos. El movimiento original se
    /// CONSERVA (auditoría): un ledger se corrige con otro asiento, no borrando. Solo se aplica si (a) el
    /// registro #682 ya no existe, (b) no hay ningún <c>Consumo</c> de <c>Seguimiento producción #682</c> y
    /// (c) el renglón conserva <b>al menos</b> esos kilos (nadie lo usó). Sin cumplirlas no toca nada y lo dice
    /// con <c>NOTICE</c>. El asiento lleva la referencia <c>Correccion del movimiento #&lt;id&gt;</c>, que es
    /// también su marca de idempotencia.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente</b> (las reservas liberadas salen del criterio; el ajuste ya existe) y <b>sin reversa</b>.
    /// El arranque nunca se cae: el bloque del ingreso captura sus errores.
    /// </para>
    /// </summary>
    public partial class LiberarReservasHuerfanasYCorregirIngresoFantasma682 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Reservas ACTIVAS sin registro dueño -> LIBERADA (LiberarAsync).
            migrationBuilder.Sql(@"
UPDATE public.seguimiento_reserva_alimento r
   SET estado = 'LIBERADA', liberada_at = now()
 WHERE r.estado = 'ACTIVA'
   AND (   (r.origen_modulo = 'PRODUCCION'
            AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_produccion s
                             WHERE s.id = r.origen_seguimiento_id AND s.deleted_at IS NULL))
        OR (r.origen_modulo = 'LEVANTE'
            AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_levante s WHERE s.id = r.origen_seguimiento_id))
        OR (r.origen_modulo = 'ENGORDE'
            AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_aves_engorde s WHERE s.id = r.origen_seguimiento_id))
        OR (r.origen_modulo = 'REPRODUCTORA'
            AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_lote_reproductora_aves_engorde s
                             WHERE s.id = r.origen_seguimiento_id)));

UPDATE public.seguimiento_reserva_aves r
   SET estado = 'LIBERADA', liberada_at = now()
 WHERE r.estado = 'ACTIVA'
   AND (   (r.origen_modulo = 'PRODUCCION'
            AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_produccion s
                             WHERE s.id = r.origen_seguimiento_id AND s.deleted_at IS NULL))
        OR (r.origen_modulo = 'LEVANTE'
            AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_levante s WHERE s.id = r.origen_seguimiento_id))
        OR (r.origen_modulo = 'ENGORDE'
            AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_aves_engorde s WHERE s.id = r.origen_seguimiento_id))
        OR (r.origen_modulo = 'REPRODUCTORA'
            AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_lote_reproductora_aves_engorde s
                             WHERE s.id = r.origen_seguimiento_id)));
");

            // 2) Ingreso fantasma del #682: se compensa con un AjusteStock (con guardas).
            migrationBuilder.Sql(@"
DO $$
DECLARE
    mov          record;
    v_stock_id   integer;
    v_stock_qty  numeric;
    v_corregidos integer := 0;
BEGIN
    FOR mov IN
        SELECT m.id, m.company_id, m.pais_id, m.farm_id, m.silo_id, m.item_inventario_id, m.quantity, m.unit
          FROM public.inventario_gestion_movimiento m
         WHERE m.movement_type = 'Ingreso'
           AND m.reference = 'Seguimiento producción #682 (devolución por eliminación)'
           AND m.nucleo_id IS NULL AND m.galpon_id IS NULL
           -- (a) el registro ya no existe
           AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_produccion s WHERE s.id = 682)
           -- (b) nunca salio nada de ese registro: no hubo ningun Consumo suyo
           AND NOT EXISTS (SELECT 1 FROM public.inventario_gestion_movimiento c
                            WHERE c.movement_type = 'Consumo' AND c.company_id = m.company_id
                              AND c.reference LIKE 'Seguimiento producción #682 %')
           -- ya compensado (idempotencia)
           AND NOT EXISTS (SELECT 1 FROM public.inventario_gestion_movimiento a
                            WHERE a.movement_type = 'AjusteStock'
                              AND a.reference = 'Correccion del movimiento #' || m.id)
         ORDER BY m.id
    LOOP
        BEGIN
            v_stock_id := NULL;
            v_stock_qty := NULL;
            SELECT st.id, st.quantity INTO v_stock_id, v_stock_qty
              FROM public.inventario_gestion_stock st
             WHERE st.farm_id = mov.farm_id AND st.item_inventario_id = mov.item_inventario_id
               AND st.nucleo_id IS NULL AND st.galpon_id IS NULL
               AND st.silo_id IS NOT DISTINCT FROM mov.silo_id;

            -- (c) el renglon conserva al menos esos kilos: nadie lo uso.
            IF v_stock_id IS NULL OR v_stock_qty < mov.quantity THEN
                RAISE EXCEPTION 'el renglon de stock ya no tiene esos kilos (disponible %, a compensar %)',
                    coalesce(v_stock_qty, 0), mov.quantity;
            END IF;

            UPDATE public.inventario_gestion_stock
               SET quantity = quantity - mov.quantity, updated_at = now()
             WHERE id = v_stock_id AND quantity >= mov.quantity;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'el stock cambio mientras se compensaba';
            END IF;

            INSERT INTO public.inventario_gestion_movimiento
                (company_id, pais_id, farm_id, nucleo_id, galpon_id, silo_id, item_inventario_id, quantity, unit,
                 movement_type, estado, reference, reason, created_at, created_by_user_id)
            VALUES
                (mov.company_id, mov.pais_id, mov.farm_id, NULL, NULL, mov.silo_id, mov.item_inventario_id,
                 mov.quantity, mov.unit, 'AjusteStock', 'Ajuste manual',
                 'Correccion del movimiento #' || mov.id,
                 'Ajuste manual. Anterior: ' || v_stock_qty || ' ' || mov.unit || '. Nuevo: ' ||
                 (v_stock_qty - mov.quantity) || ' ' || mov.unit ||
                 '. Motivo: la devolucion por eliminacion del seguimiento 682 acredito un item que no era (catalogo 373 tomado como inventario 373) y nunca hubo consumo que devolver.',
                 now(), NULL);

            v_corregidos := v_corregidos + 1;
        EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE 'Ingreso fantasma #%: no se compensa: %', mov.id, SQLERRM;
        END;
    END LOOP;

    RAISE NOTICE 'Ingreso fantasma del #682: % compensados', v_corregidos;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin reversa a propósito: volver a dejar ACTIVAS reservas sin dueño o reponer un renglón de stock
            // de un alimento que no se tiene recrearía el defecto.
        }
    }
}
