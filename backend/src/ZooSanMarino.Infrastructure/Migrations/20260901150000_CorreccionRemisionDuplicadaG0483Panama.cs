using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <b>ItalcolPanamá — DOÑA MARIA / G0483</b>: la remisión <b>190755</b> quedó cargada <b>dos
    /// veces</b> (12.500 kg cada una). Anula el ingreso duplicado y reescribe el saldo persistido
    /// desde la función, con lo que el cuadre del galpón pasa de <b>12.500 kg de descuadre a 0</b>.
    /// </summary>
    /// <remarks>
    /// <b>Qué pasó, con la hora exacta.</b> El 01-ago-2026, en 2 minutos y 15 segundos:
    /// <list type="number">
    /// <item><c>11:15:05</c> — ingreso de 12.500 kg de la remisión 190755, fechado <b>01-ago</b> (mov 10274).</item>
    /// <item><c>11:15:55</c> — «Eliminar registro de stock» por los mismos 12.500 kg (mov 10275).</item>
    /// <item><c>11:17:20</c> — el <b>mismo</b> ingreso otra vez, ahora fechado <b>28-jul</b>, que es el correcto (mov 10276).</item>
    /// </list>
    /// O sea: lo cargaron con la fecha equivocada, deshicieron el stock y lo volvieron a cargar bien.
    /// El stock quedó correcto; la tabla diaria no, porque «Eliminar registro de stock» bajaba el
    /// stock y no la tabla (el <c>EliminacionStock</c> se espeja como <c>INV_OTRO</c>, que
    /// <c>fn_seguimiento_diario_engorde</c> no lee). Es el mismo defecto de <b>TK-2026-000183</b>,
    /// parchado en <c>EliminarStockAsync</c>; esta migración corrige el dato que ya quedó mal.
    ///
    /// <b>El cuadre lo dice solo.</b> Antes: <c>saldo_tabla 23.636,5</c> contra <c>stock 11.136,5</c>,
    /// <c>mov_post 0</c> ⇒ <b>descuadre 12.500 exacto</b>. Después de anular el duplicado el
    /// invariante <b>cierra clavado</b>: 11.136,5 == 11.136,5, descuadre 0, y <b>ningún día en rojo</b>
    /// (el mínimo del galpón baja de 10.502 a 1.691 kg, sigue positivo).
    ///
    /// <b>Por qué acá no hay copia congelada que parchear.</b> Los dos lotes del galpón —<c>33 - 1</c>
    /// (187) y <c>33 - 2</c> (190), que conviven porque la bodega es del galpón— están <b>Abiertos</b>,
    /// así que la grilla recalcula en vivo. Basta con el histórico y con reescribir la columna
    /// persistida <b>desde la función</b>, que es el mismo SQL de
    /// <c>SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync</c>: una sola fórmula por número.
    /// Si para cuando esto corra algún lote del galpón estuviera <b>cerrado y congelado</b>, la
    /// migración lo <b>avisa y no lo toca</b>: ahí la fn devuelve la copia congelada y el camino es
    /// reabrir el lote, como se hizo en CAROLINA.
    ///
    /// <b>El recálculo hace algo más que restar 12.500, y está medido.</b> Reescribir la columna desde
    /// la fn también <b>resincroniza el desfase que ya existe</b>: hoy, de las 59 filas de los dos
    /// lotes, <b>7 no coinciden con la fn</b> (dos del 31-jul por +12.500, y cinco entre +1.905 y
    /// −499). Es la misma resincronización que haría el aplicador con el próximo movimiento de
    /// inventario del galpón, y por eso se deja: la fn es la dueña del número y la columna es su
    /// proyección. Se dice acá para que el cambio no sorprenda a nadie leyendo el diff del dato.
    ///
    /// <b>Cuál de los dos ingresos se anula.</b> El que tiene <b>su <c>EliminacionStock</c> pegado</b>
    /// (id consecutivo): ese es el que el operador quiso deshacer. El otro —el de la fecha correcta—
    /// no se toca. La migración lo localiza por esa firma, no por id.
    ///
    /// <b>Fail-safe.</b> Si no encuentra exactamente <b>un</b> ingreso con esa firma y con el histórico
    /// sin anular, <c>RAISE NOTICE</c> y no toca nada. Idempotente por el propio estado del histórico.
    ///
    /// Plan: <c>fase_de_desarrollo/eliminar_stock_no_bajaba_la_tabla_diaria_plan.md</c> (§ los otros pares).
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto.
    /// </remarks>
    public partial class CorreccionRemisionDuplicadaG0483Panama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    c_galpon   constant varchar(50) := 'G0483';
    c_remision constant varchar(100) := '190755';
    c_kg       constant numeric     := 12500;
    c_fecha    constant date        := DATE '2026-08-01';

    v_hist     bigint;
    v_mov      integer;
    v_lote     integer;
    v_filas    int;
    v_total    int := 0;
    v_congelados text;
BEGIN
    -- 1) El duplicado es el ingreso que tiene SU `EliminacionStock` pegado (id consecutivo): ese es
    --    el que el operador quiso deshacer. El gemelo con la fecha correcta (28-jul) no tiene pareja
    --    y no entra. Se exige ademas que exista OTRO ingreso con la MISMA remision, galpon, item y
    --    cantidad: sin eso no habria duplicado que anular.
    SELECT h.id, h.origen_id
      INTO v_hist, v_mov
      FROM lote_registro_historico_unificado h
      JOIN inventario_gestion_movimiento i ON i.id = h.origen_id
     WHERE h.origen_tabla = 'inventario_gestion_movimiento'
       AND h.tipo_evento  = 'INV_INGRESO'
       AND h.anulado      = FALSE
       AND TRIM(COALESCE(h.galpon_id, '')) = c_galpon
       AND h.fecha_operacion = c_fecha
       AND h.cantidad_kg     = c_kg
       AND TRIM(COALESCE(h.referencia, '')) = c_remision
       AND EXISTS (SELECT 1 FROM inventario_gestion_movimiento e
                    WHERE e.movement_type = 'EliminacionStock'
                      AND e.galpon_id = i.galpon_id
                      AND e.item_inventario_ecuador_id = i.item_inventario_ecuador_id
                      AND e.quantity = i.quantity
                      AND e.id - i.id BETWEEN 0 AND 3)
       AND EXISTS (SELECT 1 FROM inventario_gestion_movimiento g
                    WHERE g.id <> i.id
                      AND g.movement_type = 'Ingreso'
                      AND g.galpon_id = i.galpon_id
                      AND g.item_inventario_ecuador_id = i.item_inventario_ecuador_id
                      AND g.quantity = i.quantity
                      AND TRIM(COALESCE(g.reference, '')) = c_remision);

    IF v_hist IS NULL THEN
        RAISE NOTICE 'G0483: no se encontro el ingreso duplicado de % kg de la remision % sin anular. No se toca nada.', c_kg, c_remision;
        RETURN;
    END IF;

    -- 2) El hecho raiz. El stock NO se toca: esos kilos ya salieron con el `EliminacionStock`.
    UPDATE lote_registro_historico_unificado SET anulado = TRUE WHERE id = v_hist;
    RAISE NOTICE 'G0483: anulado el ingreso duplicado (historico %, movimiento %).', v_hist, v_mov;

    -- 3) La columna persistida, reescrita DESDE la fn para todos los lotes del galpon (la bodega es
    --    compartida: los dos lotes conviven y comparten la grilla). Mismo SQL que
    --    SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync: una sola formula por numero.
    FOR v_lote IN
        SELECT l.lote_ave_engorde_id
          FROM lote_ave_engorde l
         WHERE TRIM(COALESCE(l.galpon_id, '')) = c_galpon
           AND l.deleted_at IS NULL
           AND l.lote_ave_engorde_id IS NOT NULL
         ORDER BY l.lote_ave_engorde_id
    LOOP
        -- Un lote CONGELADO se saltea a proposito: ahi la fn devuelve la copia congelada, asi que
        -- recalcular escribiria el numero viejo y taparia el problema. Hay que reabrirlo.
        IF EXISTS (SELECT 1 FROM liquidacion_lote_engorde_congelada c
                    WHERE c.lote_ave_engorde_id = v_lote AND c.anulada_at IS NULL) THEN
            v_congelados := COALESCE(v_congelados || ', ', '') || v_lote::text;
            CONTINUE;
        END IF;

        WITH nuevos AS (
            SELECT f.seg_id, ROUND(f.saldo_alimento_kg::numeric, 3) AS saldo
              FROM fn_seguimiento_diario_engorde(v_lote) f
             WHERE f.seg_id IS NOT NULL
        )
        UPDATE seguimiento_diario_aves_engorde s
           SET saldo_alimento_kg = n.saldo
          FROM nuevos n
         WHERE s.id = n.seg_id
           AND s.saldo_alimento_kg IS DISTINCT FROM n.saldo;
        GET DIAGNOSTICS v_filas = ROW_COUNT;
        v_total := v_total + v_filas;
    END LOOP;

    RAISE NOTICE 'G0483: % filas de saldo persistido reescritas desde la fn.', v_total;

    IF v_congelados IS NOT NULL THEN
        RAISE WARNING 'G0483: los lotes % estan CERRADOS y congelados, asi que su grilla sale de la copia congelada y NO se corrigio. Hay que reabrirlos para que se recalcule (ver TK-2026-000183).', v_congelados;
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    c_galpon   constant varchar(50) := 'G0483';
    c_remision constant varchar(100) := '190755';
    c_kg       constant numeric     := 12500;
    c_fecha    constant date        := DATE '2026-08-01';

    v_hist     bigint;
    v_lote     integer;
    v_filas    int;
    v_total    int := 0;
BEGIN
    -- Misma firma que el Up(), pero buscando la fila que el Up() dejo anulada.
    SELECT h.id INTO v_hist
      FROM lote_registro_historico_unificado h
      JOIN inventario_gestion_movimiento i ON i.id = h.origen_id
     WHERE h.origen_tabla = 'inventario_gestion_movimiento'
       AND h.tipo_evento  = 'INV_INGRESO'
       AND h.anulado      = TRUE
       AND TRIM(COALESCE(h.galpon_id, '')) = c_galpon
       AND h.fecha_operacion = c_fecha
       AND h.cantidad_kg     = c_kg
       AND TRIM(COALESCE(h.referencia, '')) = c_remision
       AND EXISTS (SELECT 1 FROM inventario_gestion_movimiento e
                    WHERE e.movement_type = 'EliminacionStock'
                      AND e.galpon_id = i.galpon_id
                      AND e.item_inventario_ecuador_id = i.item_inventario_ecuador_id
                      AND e.quantity = i.quantity
                      AND e.id - i.id BETWEEN 0 AND 3);

    IF v_hist IS NULL THEN
        RAISE NOTICE 'G0483 (Down): no hay ingreso duplicado anulado con esa firma. No se toca nada.';
        RETURN;
    END IF;

    UPDATE lote_registro_historico_unificado SET anulado = FALSE WHERE id = v_hist;

    FOR v_lote IN
        SELECT l.lote_ave_engorde_id
          FROM lote_ave_engorde l
         WHERE TRIM(COALESCE(l.galpon_id, '')) = c_galpon
           AND l.deleted_at IS NULL
           AND l.lote_ave_engorde_id IS NOT NULL
         ORDER BY l.lote_ave_engorde_id
    LOOP
        IF EXISTS (SELECT 1 FROM liquidacion_lote_engorde_congelada c
                    WHERE c.lote_ave_engorde_id = v_lote AND c.anulada_at IS NULL) THEN
            CONTINUE;
        END IF;

        WITH nuevos AS (
            SELECT f.seg_id, ROUND(f.saldo_alimento_kg::numeric, 3) AS saldo
              FROM fn_seguimiento_diario_engorde(v_lote) f
             WHERE f.seg_id IS NOT NULL
        )
        UPDATE seguimiento_diario_aves_engorde s
           SET saldo_alimento_kg = n.saldo
          FROM nuevos n
         WHERE s.id = n.seg_id
           AND s.saldo_alimento_kg IS DISTINCT FROM n.saldo;
        GET DIAGNOSTICS v_filas = ROW_COUNT;
        v_total := v_total + v_filas;
    END LOOP;

    RAISE NOTICE 'G0483 (Down): revertido el historico % y reescritas % filas de saldo.', v_hist, v_total;
END $$;
");
        }
    }
}
