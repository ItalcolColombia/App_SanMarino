using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <b>TK-2026-000183</b> — CAROLINA / GALPON 1 / lote 2602 (ItalcolEcuador): el día 1 mostraba
    /// <b>saldo 5.600 kg contra un ingreso de 2.880 kg</b>. Anula el ingreso duplicado y baja los
    /// 2.880 kg de las tres superficies donde ese número quedó escrito.
    /// </summary>
    /// <remarks>
    /// <b>Qué pasó.</b> En <c>G0057</c> hay dos <c>Ingreso</c> de 2.880 kg del mismo ítem: el real del
    /// 02-abr con la remisión <b>56114</b> (que entra como apertura, por caer en la ventana previa al
    /// encaset) y un <b>duplicado</b> del 07-abr sin remisión. Al duplicado le aplicaron «Eliminar
    /// registro de stock», y ese camino <b>bajaba el stock y no la tabla diaria</b>: el
    /// <c>EliminacionStock</c> se espeja como <c>INV_OTRO</c>, un <c>tipo_evento</c> que
    /// <c>fn_seguimiento_diario_engorde</c> no lee. Aritmética exacta del día 1:
    /// <c>5.600 = 2.880 (apertura) + 2.880 (duplicado) − 160 (consumo)</c>.
    /// El defecto de código lo cierra <c>EliminarStockAsync</c> en el mismo commit; esta migración
    /// corrige el dato que ya quedó mal.
    ///
    /// <b>Por qué son tres tablas.</b> <c>fn_seguimiento_diario_engorde</c> devuelve la <b>copia
    /// congelada</b> mientras exista sin anular (arranca con un <c>UNION ALL</c> contra
    /// <c>liquidacion_lote_engorde_congelada_fila</c>), y la columna persistida del maestro la escribe
    /// <c>SaldoAlimentoEngordeAplicador</c>. Corregir solo el histórico no cambiaría un solo número en
    /// pantalla.
    ///
    /// <b>Por qué quirúrgico y no reabrir para volver a congelar.</b> Medido en transacción revertida
    /// sobre la copia de producción: anulado el duplicado, el cálculo vivo da día 1 = <b>2.720</b> con
    /// ingreso <b>0</b> y cierra en <b>0</b> —idéntico al galpón gemelo—, y difiere de la congelada
    /// v13 en <b>52 filas solo por <c>saldo_alimento_kg</c>, todas por exactamente 2.880</b>, más el
    /// ingreso del día 1. Cero diferencias en consumo, aves, mortalidad, documento, tipo de alimento,
    /// despachos y pesos. Pero la fn de hoy <b>numera distinto el <c>edad_dia</c></b> (el 07-abr pasa
    /// de día 1 a día 2, por el arreglo de la hora de llegada): recongelar traería ese cambio ajeno al
    /// ticket. La corrección quirúrgica escribe exactamente los mismos kilos que el recálculo vivo,
    /// sin importar nada más.
    ///
    /// <b>Fail-safe.</b> Localiza el duplicado por <b>atributos, no por id</b> (galpón + 2.880 kg +
    /// fecha + <c>Ingreso</c> sin remisión + su <c>EliminacionStock</c> pareja de id casi consecutivo,
    /// con el histórico sin anular). Si no encuentra esa firma exacta, <c>RAISE NOTICE</c> y no toca
    /// nada; si el saldo mínimo del lote fuera menor que los kilos a restar, <c>RAISE EXCEPTION</c> en
    /// vez de escribir negativos. Es idempotente por el propio estado del histórico: en la segunda
    /// corrida ya está anulado y no encuentra nada que hacer.
    ///
    /// <b>Lo que NO toca, a propósito.</b> El <b>stock</b> (ya está bien: esos kilos salieron con la
    /// eliminación). El <c>metadata</c> de las filas congeladas (el <c>ingresoAlimentoKg: 3600</c> que
    /// vive ahí es la carga del seguimiento, no la columna que pinta la grilla). El <c>checksum</c>
    /// del header, que es el md5 de las filas <i>tal como las devolvió la fn al congelar</i> y no es
    /// reproducible sobre las filas guardadas: la corrección queda registrada en <c>metadata</c>, que
    /// es honesto, en vez de recalcularlo con otra fórmula y fingir integridad. Y los otros <b>12
    /// pares</b> con la misma firma que encuentra el detector: en particular <b>G0058</b>, donde el
    /// ingreso es real —es el único del día y el lote cierra en 0— y anularlo lo dejaría en −2.880.
    ///
    /// Plan: <c>fase_de_desarrollo/eliminar_stock_no_bajaba_la_tabla_diaria_plan.md</c>.
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto.
    /// </remarks>
    public partial class CorreccionSaldoAlimentoCarolinaG1Lote2602 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    c_galpon   constant varchar(50) := 'G0057';
    c_kg       constant numeric     := 2880;
    c_fecha    constant date        := DATE '2026-04-07';
    c_marca    constant text        := 'correccionTk2026000183';

    v_hist     bigint;
    v_mov      integer;
    v_lote     integer;
    v_liq      bigint;
    v_filas    integer;
    v_min      numeric;
BEGIN
    -- 1) La firma exacta del ingreso duplicado. Se busca por ATRIBUTOS, no por id: un ingreso de
    --    2.880 kg del galpon G0057 fechado el dia 1, SIN remision, con su `EliminacionStock` pareja
    --    del mismo galpon/item/cantidad e id casi consecutivo, cuyo historico sigue sin anular.
    --    El galpon gemelo G0058 tiene un par identico y NO entra: alli el ingreso es real (es el
    --    unico del dia y el lote cierra en 0), anularlo lo dejaria en -2.880.
    SELECT h.id, h.origen_id, h.lote_ave_engorde_id
      INTO v_hist, v_mov, v_lote
      FROM lote_registro_historico_unificado h
      JOIN inventario_gestion_movimiento i ON i.id = h.origen_id
     WHERE h.origen_tabla = 'inventario_gestion_movimiento'
       AND h.tipo_evento  = 'INV_INGRESO'
       AND h.anulado      = FALSE
       AND TRIM(COALESCE(h.galpon_id, '')) = c_galpon
       AND h.fecha_operacion = c_fecha
       AND h.cantidad_kg     = c_kg
       AND COALESCE(TRIM(h.referencia), '') = ''
       AND EXISTS (SELECT 1
                     FROM inventario_gestion_movimiento e
                    WHERE e.movement_type = 'EliminacionStock'
                      AND e.galpon_id = i.galpon_id
                      AND e.item_inventario_ecuador_id = i.item_inventario_ecuador_id
                      AND e.quantity = i.quantity
                      AND e.id - i.id BETWEEN 0 AND 3);

    IF v_hist IS NULL THEN
        RAISE NOTICE 'TK-2026-000183: no se encontro el ingreso duplicado de % kg en % del % sin anular. No se toca nada.', c_kg, c_galpon, c_fecha;
        RETURN;
    END IF;

    -- 2) El hecho raiz: el ingreso duplicado deja de contar. El stock NO se toca: esos kilos ya
    --    salieron con el `EliminacionStock` del 30-abr.
    UPDATE lote_registro_historico_unificado
       SET anulado = TRUE
     WHERE id = v_hist;

    -- 3) La columna persistida del maestro. Un saldo por debajo de los kilos a restar significaria
    --    que el diagnostico no es el que se midio: se corta en vez de escribir negativos.
    SELECT MIN(s.saldo_alimento_kg) INTO v_min
      FROM seguimiento_diario_aves_engorde s
     WHERE s.lote_ave_engorde_id = v_lote
       AND s.fecha::date >= c_fecha;

    IF v_min IS NOT NULL AND v_min < c_kg THEN
        RAISE EXCEPTION 'TK-2026-000183: el saldo minimo del lote % es % kg, menor que los % kg a restar. Abortado.', v_lote, v_min, c_kg;
    END IF;

    UPDATE seguimiento_diario_aves_engorde
       SET saldo_alimento_kg = saldo_alimento_kg - c_kg
     WHERE lote_ave_engorde_id = v_lote
       AND fecha::date >= c_fecha;
    GET DIAGNOSTICS v_filas = ROW_COUNT;
    RAISE NOTICE 'TK-2026-000183: % dias corregidos en seguimiento_diario_aves_engorde (lote %).', v_filas, v_lote;

    -- 4) La copia congelada, que es lo que la granja ve: la fn devuelve la congelada mientras exista
    --    sin anular, asi que sin este paso el usuario seguiria viendo 5.600.
    SELECT c.id INTO v_liq
      FROM liquidacion_lote_engorde_congelada c
     WHERE c.lote_ave_engorde_id = v_lote
       AND c.anulada_at IS NULL;

    IF v_liq IS NULL THEN
        RAISE NOTICE 'TK-2026-000183: el lote % no tiene copia congelada vigente (se reabrio). La grilla ya recalcula en vivo.', v_lote;
        RETURN;
    END IF;

    UPDATE liquidacion_lote_engorde_congelada_fila
       SET saldo_alimento_kg = saldo_alimento_kg - c_kg
     WHERE liquidacion_id = v_liq
       AND fecha >= c_fecha;
    GET DIAGNOSTICS v_filas = ROW_COUNT;

    UPDATE liquidacion_lote_engorde_congelada_fila
       SET ingreso_alimento_kg = ingreso_alimento_kg - c_kg
     WHERE liquidacion_id = v_liq
       AND fecha = c_fecha
       AND ingreso_alimento_kg >= c_kg;

    -- El `checksum` se conserva a proposito: es el md5 de las filas tal como las devolvio la fn al
    -- congelar, y no es reproducible sobre las filas guardadas. La correccion queda registrada en
    -- `metadata`, con los ids que necesita el Down().
    UPDATE liquidacion_lote_engorde_congelada
       SET saldo_alimento_kg = saldo_alimento_kg - c_kg,
           metadata = COALESCE(metadata, '{}'::jsonb) || jsonb_build_object(
                          c_marca, jsonb_build_object(
                              'migracion',    '20260901140000',
                              'ticket',       'TK-2026-000183',
                              'historicoId',  v_hist,
                              'movimientoId', v_mov,
                              'loteId',       v_lote,
                              'kg',           c_kg,
                              'desdeFecha',   c_fecha::text,
                              'motivo',       'Ingreso duplicado de la remision 56114 al que se le aplico Eliminar registro de stock: bajo el stock y no la tabla diaria.'))
     WHERE id = v_liq;

    RAISE NOTICE 'TK-2026-000183: corregidas % filas de la copia congelada % (lote %, historico %, movimiento %).', v_filas, v_liq, v_lote, v_hist, v_mov;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    c_marca    constant text := 'correccionTk2026000183';

    v_liq      bigint;
    v_marca    jsonb;
    v_hist     bigint;
    v_lote     integer;
    v_kg       numeric;
    v_fecha    date;
BEGIN
    -- La marca que dejo el Up() es la unica fuente: trae los ids exactos que hay que revertir.
    SELECT c.id, c.metadata -> c_marca
      INTO v_liq, v_marca
      FROM liquidacion_lote_engorde_congelada c
     WHERE c.metadata ? c_marca
       AND c.anulada_at IS NULL;

    IF v_marca IS NULL THEN
        RAISE NOTICE 'TK-2026-000183 (Down): no hay copia congelada con la marca de la correccion. No se toca nada.';
        RETURN;
    END IF;

    v_hist  := (v_marca ->> 'historicoId')::bigint;
    v_lote  := (v_marca ->> 'loteId')::integer;
    v_kg    := (v_marca ->> 'kg')::numeric;
    v_fecha := (v_marca ->> 'desdeFecha')::date;

    UPDATE liquidacion_lote_engorde_congelada_fila
       SET ingreso_alimento_kg = ingreso_alimento_kg + v_kg
     WHERE liquidacion_id = v_liq
       AND fecha = v_fecha;

    UPDATE liquidacion_lote_engorde_congelada_fila
       SET saldo_alimento_kg = saldo_alimento_kg + v_kg
     WHERE liquidacion_id = v_liq
       AND fecha >= v_fecha;

    UPDATE liquidacion_lote_engorde_congelada
       SET saldo_alimento_kg = saldo_alimento_kg + v_kg,
           metadata = metadata - c_marca
     WHERE id = v_liq;

    UPDATE seguimiento_diario_aves_engorde
       SET saldo_alimento_kg = saldo_alimento_kg + v_kg
     WHERE lote_ave_engorde_id = v_lote
       AND fecha::date >= v_fecha;

    UPDATE lote_registro_historico_unificado
       SET anulado = FALSE
     WHERE id = v_hist;

    RAISE NOTICE 'TK-2026-000183 (Down): revertidos % kg en el lote % (historico %).', v_kg, v_lote, v_hist;
END $$;
");
        }
    }
}
