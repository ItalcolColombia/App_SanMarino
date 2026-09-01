using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo SQL de la devolución de alimento de reproductoras borradas. La documentación de qué
    /// hace y por qué está en <c>20260831150000_DevolverAlimentoDeReproductorasBorradas.cs</c>.
    /// </summary>
    public partial class DevolverAlimentoDeReproductorasBorradas
    {
        private const string DEVOLUCION_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- Devolución del alimento de los seguimientos de reproductora borrados estando
-- confirmados: su consumo se aplicó y el borrado nunca lo restituyó.
--   1) Ingreso de devolución, fechado en el DÍA DEL SEGUIMIENTO
--   2) los kilos vuelven al stock de esa ubicación exacta
--   3) la reserva pasa a LIBERADA (alimento y aves)
-- Idempotente: se salta la reserva que ya tenga su ingreso de devolución.
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    r      record;
    v_ref  text;
    v_n    integer := 0;
    v_kg   numeric(18,3) := 0;
    v_aves integer := 0;
BEGIN
    FOR r IN
        SELECT ra.id            AS reserva_id,
               ra.origen_seguimiento_id AS seg,
               ra.company_id, ra.farm_id, ra.nucleo_id, ra.galpon_id, ra.silo_id,
               ra.item_inventario_ecuador_id AS item,
               ra.cantidad_kg, COALESCE(ra.unit, 'kg') AS unit,
               ra.fecha_seguimiento,
               -- El país sale de la propia reserva: `farms` no lo tiene (su geografía cuelga de
               -- departamento/municipio), y la reserva ya lo guardó cuando se creó.
               ra.pais_id
        FROM public.seguimiento_reserva_alimento ra
        WHERE ra.origen_modulo = 'REPRODUCTORA'
          AND ra.estado = 'APLICADA'
          AND ra.cantidad_kg > 0
          -- Huérfana: el seguimiento que la originó ya no existe, así que nadie la va a liberar.
          AND NOT EXISTS (
              SELECT 1 FROM public.seguimiento_diario_lote_reproductora_aves_engorde s
              WHERE s.id = ra.origen_seguimiento_id)
        ORDER BY ra.id
    LOOP
        v_ref := 'Seguimiento reproductora #' || r.seg || ' (devolucion por eliminacion, remediacion 31-ago-2026)';

        -- Idempotencia por la referencia exacta: si ya se devolvió, no se devuelve otra vez.
        CONTINUE WHEN EXISTS (
            SELECT 1 FROM public.inventario_gestion_movimiento m
            WHERE m.movement_type = 'Ingreso' AND m.reference = v_ref);

        -- 1) El ingreso de devolución. Mismo tipo, estado y forma que escribe RegistrarIngresoAsync
        --    sin origen declarado, para que el kardex y el histórico lo lean como cualquier entrada.
        INSERT INTO public.inventario_gestion_movimiento
            (company_id, pais_id, farm_id, nucleo_id, galpon_id, silo_id,
             item_inventario_ecuador_id, quantity, unit, movement_type, estado,
             reference, reason, created_at, registrado_at, para_proximo_ciclo)
        VALUES
            (r.company_id, r.pais_id, r.farm_id, r.nucleo_id, r.galpon_id, r.silo_id,
             r.item, r.cantidad_kg, r.unit, 'Ingreso', 'Entrada granja',
             v_ref,
             'Devolucion de alimento de un seguimiento de reproductora que se borro estando confirmado: el consumo se habia aplicado y nunca se restituyo.',
             -- En esta tabla la FECHA DEL HECHO es `created_at` (no hay `fecha_movimiento`), y de ahí
             -- la toma el trigger del histórico como `fecha_operacion`. Se fecha en el DÍA DEL
             -- SEGUIMIENTO, no en el de hoy: es el criterio de DesvalidarAsync y evita que el saldo
             -- del galpón quede con un hueco entre el consumo y su devolución. `registrado_at` guarda
             -- el instante real, que es lo que hace `RegistrarIngresoAsync` con la fecha tipeada.
             r.fecha_seguimiento::timestamptz,
             timezone('utc', now()), false);

        -- 2) Los kilos vuelven al stock. La fila se ubica por la clave natural del índice único.
        UPDATE public.inventario_gestion_stock s
           SET quantity   = s.quantity + r.cantidad_kg,
               updated_at = timezone('utc', now())
         WHERE s.farm_id = r.farm_id
           AND s.item_inventario_ecuador_id = r.item
           AND COALESCE(s.nucleo_id, '') = COALESCE(r.nucleo_id, '')
           AND COALESCE(s.galpon_id, '') = COALESCE(r.galpon_id, '')
           AND COALESCE(s.silo_id, 0)    = COALESCE(r.silo_id, 0);

        IF NOT FOUND THEN
            RAISE NOTICE 'Devolucion reproductora: la reserva % (% kg, granja %, galpon %) no tiene fila de stock; el ingreso quedo registrado pero el saldo no se pudo sumar.',
                r.reserva_id, r.cantidad_kg, r.farm_id, COALESCE(r.galpon_id, '(sin galpon)');
        END IF;

        -- 3) La reserva deja de contar como separación viva.
        UPDATE public.seguimiento_reserva_alimento
           SET estado = 'LIBERADA', liberada_at = timezone('utc', now())
         WHERE id = r.reserva_id;

        v_n  := v_n + 1;
        v_kg := v_kg + r.cantidad_kg;
    END LOOP;

    -- Las reservas de AVES huérfanas solo se marcan LIBERADA: en reproductora las bajas las escribe
    -- el cruce que dispara la confirmación, y ese cruce se rehace solo al borrar el registro.
    -- Reponerlas a mano descuadraría el maestro por partida doble.
    UPDATE public.seguimiento_reserva_aves ra
       SET estado = 'LIBERADA', liberada_at = timezone('utc', now())
     WHERE ra.origen_modulo = 'REPRODUCTORA'
       AND ra.estado = 'APLICADA'
       AND NOT EXISTS (
           SELECT 1 FROM public.seguimiento_diario_lote_reproductora_aves_engorde s
           WHERE s.id = ra.origen_seguimiento_id);
    GET DIAGNOSTICS v_aves = ROW_COUNT;

    RAISE NOTICE 'Devolucion reproductora: % reservas de alimento devueltas (% kg), % reservas de aves liberadas.',
        v_n, v_kg, v_aves;
END $$;
";

        private const string DESHACER_SQL = @"
-- Deshace la devolución: borra los ingresos que sembró el Up (por su referencia exacta), vuelve a
-- restar los kilos y devuelve las reservas a APLICADA.
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT m.id, m.farm_id, m.nucleo_id, m.galpon_id, m.silo_id,
               m.item_inventario_ecuador_id AS item, m.quantity,
               substring(m.reference from 'reproductora #([0-9]+)')::bigint AS seg
        FROM public.inventario_gestion_movimiento m
        WHERE m.movement_type = 'Ingreso'
          AND m.reference LIKE 'Seguimiento reproductora #% (devolucion por eliminacion, remediacion 31-ago-2026)'
        ORDER BY m.id
    LOOP
        UPDATE public.inventario_gestion_stock s
           SET quantity   = s.quantity - r.quantity,
               updated_at = timezone('utc', now())
         WHERE s.farm_id = r.farm_id
           AND s.item_inventario_ecuador_id = r.item
           AND COALESCE(s.nucleo_id, '') = COALESCE(r.nucleo_id, '')
           AND COALESCE(s.galpon_id, '') = COALESCE(r.galpon_id, '')
           AND COALESCE(s.silo_id, 0)    = COALESCE(r.silo_id, 0);

        UPDATE public.seguimiento_reserva_alimento
           SET estado = 'APLICADA', liberada_at = NULL
         WHERE origen_modulo = 'REPRODUCTORA' AND origen_seguimiento_id = r.seg;

        UPDATE public.seguimiento_reserva_aves
           SET estado = 'APLICADA', liberada_at = NULL
         WHERE origen_modulo = 'REPRODUCTORA' AND origen_seguimiento_id = r.seg;

        -- El histórico se ANULA al borrar (trigger _del) y su (origen_tabla, origen_id) sigue
        -- ocupando el índice único, así que se limpia antes de que quede huérfano.
        DELETE FROM public.lote_registro_historico_unificado
         WHERE origen_tabla = 'inventario_gestion_movimiento' AND origen_id = r.id;

        DELETE FROM public.inventario_gestion_movimiento WHERE id = r.id;
    END LOOP;
END $$;
";
    }
}
