using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo SQL de la reversión de consumos duplicados. Vive en su propio archivo
    /// (<c>partial</c>) por tamaño: la documentación está en
    /// <c>20260831140000_RevertirConsumosDuplicadosPorValidacion.cs</c>.
    /// </summary>
    public partial class RevertirConsumosDuplicadosPorValidacion
    {
        private const string REVERSION_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- Reversión de los consumos que la validación aplicó DOS veces.
--   1) respaldo del movimiento sobrante          (para que el Down pueda deshacerlo)
--   2) DELETE del sobrante                       (el trigger anula su fila del histórico)
--   3) devolución de los kilos al stock          (el DELETE NO lo hace; medido)
-- Idempotente: la 2a corrida no encuentra ningún grupo con count(*) > 1.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public._backup_consumos_duplicados_validacion_20260831
    (LIKE public.inventario_gestion_movimiento INCLUDING DEFAULTS);

DO $$
DECLARE
    r            record;
    v_revertidos integer := 0;
    v_kg         numeric(18,3) := 0;
    v_sin_stock  integer := 0;
BEGIN
    FOR r IN
        -- Sobrantes = todos los del grupo menos el de menor id. La firma incluye la UBICACIÓN y el
        -- SILO: dos galpones (o dos silos) que consumen el mismo día el mismo ítem son dos consumos
        -- legítimos, no una duplicación. Misma regla que DuplicadosValidacionCalculos.
        WITH grupos AS (
            SELECT reference, farm_id,
                   COALESCE(nucleo_id, '') AS nuc,
                   COALESCE(galpon_id, '') AS gal,
                   COALESCE(silo_id, 0)    AS silo,
                   item_inventario_ecuador_id AS item,
                   quantity,
                   MIN(id) AS conservar
            FROM public.inventario_gestion_movimiento
            WHERE movement_type = 'Consumo'
              AND reference LIKE '%(validado)%'
            GROUP BY 1,2,3,4,5,6,7
            HAVING COUNT(*) > 1
        )
        SELECT m.id, m.company_id, m.farm_id, m.nucleo_id, m.galpon_id, m.silo_id,
               m.item_inventario_ecuador_id AS item, m.quantity, m.reference
        FROM public.inventario_gestion_movimiento m
        JOIN grupos g
          ON  g.reference = m.reference
          AND g.farm_id   = m.farm_id
          AND g.nuc       = COALESCE(m.nucleo_id, '')
          AND g.gal       = COALESCE(m.galpon_id, '')
          AND g.silo      = COALESCE(m.silo_id, 0)
          AND g.item      = m.item_inventario_ecuador_id
          AND g.quantity  = m.quantity
        WHERE m.movement_type = 'Consumo'
          AND m.id <> g.conservar
        ORDER BY m.id
    LOOP
        -- 1) Respaldo íntegro antes de tocar nada (el Down lo reinserta con su id original).
        INSERT INTO public._backup_consumos_duplicados_validacion_20260831
        SELECT * FROM public.inventario_gestion_movimiento WHERE id = r.id
        ON CONFLICT DO NOTHING;

        -- 2) Borrar el sobrante. trg_inventario_gestion_movimiento_lote_hist_del deja su fila de
        --    lote_registro_historico_unificado en anulado = true: el histórico se ANULA, nunca se
        --    abandona, o el saldo la seguiría contando.
        DELETE FROM public.inventario_gestion_movimiento WHERE id = r.id;

        -- 3) Devolver los kilos. El DELETE de arriba NO toca el stock -- verificado en una
        --    transacción revertida sobre la copia de producción antes de escribir esta migración.
        --    La fila se ubica por la clave natural del índice único (con sus COALESCE).
        UPDATE public.inventario_gestion_stock s
           SET quantity   = s.quantity + r.quantity,
               updated_at = timezone('utc', now())
         WHERE s.farm_id = r.farm_id
           AND s.item_inventario_ecuador_id = r.item
           AND COALESCE(s.nucleo_id, '') = COALESCE(r.nucleo_id, '')
           AND COALESCE(s.galpon_id, '') = COALESCE(r.galpon_id, '')
           AND COALESCE(s.silo_id, 0)    = COALESCE(r.silo_id, 0);

        IF NOT FOUND THEN
            -- Sin fila de stock no se inventa una: significaría crear existencia donde el sistema
            -- nunca la registró. Se avisa para que quede en el log del deploy.
            v_sin_stock := v_sin_stock + 1;
            RAISE NOTICE 'Reversion duplicados: el movimiento % (% kg, granja %, galpon %) no tiene fila de stock; se borro el duplicado pero no se pudo devolver el saldo.',
                r.id, r.quantity, r.farm_id, COALESCE(r.galpon_id, '(sin galpon)');
        END IF;

        v_revertidos := v_revertidos + 1;
        v_kg := v_kg + r.quantity;
    END LOOP;

    RAISE NOTICE 'Reversion duplicados por validacion: % movimientos revertidos, % kg devueltos, % sin fila de stock.',
        v_revertidos, v_kg, v_sin_stock;
END $$;
";

        private const string RESTAURAR_SQL = @"
-- Deshace la reversión: reinserta los movimientos respaldados con su id original, deja el histórico
-- como estaba y vuelve a restar los kilos del stock.
DO $$
DECLARE
    r record;
BEGIN
    IF to_regclass('public._backup_consumos_duplicados_validacion_20260831') IS NULL THEN
        RETURN;
    END IF;

    FOR r IN
        SELECT b.* FROM public._backup_consumos_duplicados_validacion_20260831 b
        WHERE NOT EXISTS (
            SELECT 1 FROM public.inventario_gestion_movimiento m WHERE m.id = b.id)
        ORDER BY b.id
    LOOP
        -- 🔴 El histórico se ANULA, no se borra: el Up dejó la fila con anulado = true y su
        --    (origen_tabla, origen_id) SIGUE OCUPANDO el índice único uq_lote_hist_origen. Si se
        --    reinsertara el movimiento con esa fila todavía ahí, el trigger de alta intentaría crear
        --    la suya y reventaría con duplicate key. Por eso se borra ANTES: el trigger vuelve a
        --    crearla limpia y sin anular, que es exactamente como estaba antes del Up.
        DELETE FROM public.lote_registro_historico_unificado
         WHERE origen_tabla = 'inventario_gestion_movimiento'
           AND origen_id = r.id;

        INSERT INTO public.inventario_gestion_movimiento
        SELECT * FROM public._backup_consumos_duplicados_validacion_20260831 WHERE id = r.id;

        UPDATE public.inventario_gestion_stock s
           SET quantity   = s.quantity - r.quantity,
               updated_at = timezone('utc', now())
         WHERE s.farm_id = r.farm_id
           AND s.item_inventario_ecuador_id = r.item_inventario_ecuador_id
           AND COALESCE(s.nucleo_id, '') = COALESCE(r.nucleo_id, '')
           AND COALESCE(s.galpon_id, '') = COALESCE(r.galpon_id, '')
           AND COALESCE(s.silo_id, 0)    = COALESCE(r.silo_id, 0);
    END LOOP;

    -- La secuencia puede haber quedado detrás de los ids reinsertados.
    PERFORM setval('inventario_gestion_movimiento_id_seq',
                   GREATEST((SELECT COALESCE(MAX(id), 1) FROM public.inventario_gestion_movimiento),
                            (SELECT last_value FROM inventario_gestion_movimiento_id_seq)));

    DROP TABLE public._backup_consumos_duplicados_validacion_20260831;
END $$;
";
    }
}
