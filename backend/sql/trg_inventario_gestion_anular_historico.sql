-- =============================================================================
-- Coherencia del histórico unificado cuando un movimiento de inventario se DESHACE.
--
-- EL PROBLEMA (jul-2026)
-- `lote_registro_historico_unificado` la llena el trigger
-- `trg_inventario_gestion_movimiento_lote_hist`, que es **AFTER INSERT y nada más**: ningún UPDATE ni
-- DELETE del movimiento se propagaba. Mantener la coherencia dependía de que cada camino del C# se
-- acordara de marcar `anulado = true` a mano. Había cuatro y dos se olvidaban:
--   * `AnularMovimientoHistoricoAsync` borraba el movimiento y dejaba la fila HUÉRFANA, así que el
--     saldo de alimento seguía contando un ingreso que ya había salido del stock.
--   * `RechazarTransitoPendienteAsync` cambiaba el `movement_type`, pero la fila conservaba su
--     `tipo_evento` (`TrasladoInterGranjaPendiente` mapea a INV_TRASLADO_SALIDA), así que el galpón de
--     origen seguía descontando una salida que nunca ocurrió.
--
-- LA SOLUCIÓN
-- Que el invariante lo garantice la BASE DE DATOS, no la disciplina del código. Es exactamente lo que
-- ya hace el módulo gemelo `movimiento_pollo_engorde` con `trg_..._lote_hist_anula`.
-- Con estos dos triggers ningún camino futuro —ni un DELETE manual por SQL— puede dejar el histórico
-- desalineado. El C# que anula a mano queda redundante pero se conserva: es idempotente y explícito.
--
-- POR QUÉ NO SE PASÓ A BORRADO LÓGICO
-- Dejar de borrar físicamente obligaría a auditar TODAS las lecturas de la tabla (GetStockAsync,
-- GetMovimientosAsync, GetTrasladosAsync, GetIngresosAsync, GetTransitosPendientesAsync…) y una sola
-- omitida resucita movimientos anulados. El AFTER DELETE cierra el agujero igual de bien para la
-- correctitud del saldo, con una fracción del riesgo. El borrado lógico aporta trazabilidad, no
-- correctitud, y queda como trabajo aparte.
-- =============================================================================

CREATE OR REPLACE FUNCTION trg_lote_hist_anular_desde_inventario_gestion()
RETURNS TRIGGER
LANGUAGE plpgsql AS $$
DECLARE
    v_mov_id INTEGER;
BEGIN
    v_mov_id := CASE TG_OP WHEN 'DELETE' THEN OLD.id ELSE NEW.id END;

    UPDATE public.lote_registro_historico_unificado
       SET anulado = TRUE
     WHERE origen_tabla = 'inventario_gestion_movimiento'
       AND origen_id    = v_mov_id
       AND NOT anulado;

    RETURN CASE TG_OP WHEN 'DELETE' THEN OLD ELSE NEW END;
END;
$$;

COMMENT ON FUNCTION trg_lote_hist_anular_desde_inventario_gestion() IS
'Marca anulada la fila del histórico unificado cuando su movimiento de inventario se borra o se cancela. '
'El trigger de alta es solo AFTER INSERT, así que sin esto el saldo de alimento contaría movimientos deshechos.';

-- 1) Cualquier DELETE, venga del C# o de un script manual.
DROP TRIGGER IF EXISTS trg_inventario_gestion_movimiento_lote_hist_del
    ON public.inventario_gestion_movimiento;
CREATE TRIGGER trg_inventario_gestion_movimiento_lote_hist_del
AFTER DELETE ON public.inventario_gestion_movimiento
FOR EACH ROW
EXECUTE FUNCTION trg_lote_hist_anular_desde_inventario_gestion();

-- 2) Cancelación por cambio de tipo (hoy: rechazo de un tránsito entre granjas).
--    `fn_tipo_evento_inventario` manda los tipos cancelados a INV_OTRO, que el saldo ya ignora, pero
--    la fila vieja conserva su tipo_evento original: hay que anularla explícitamente.
DROP TRIGGER IF EXISTS trg_inventario_gestion_movimiento_lote_hist_cancel
    ON public.inventario_gestion_movimiento;
CREATE TRIGGER trg_inventario_gestion_movimiento_lote_hist_cancel
AFTER UPDATE OF movement_type ON public.inventario_gestion_movimiento
FOR EACH ROW
WHEN (NEW.movement_type = 'TrasladoInterGranjaRechazado')
EXECUTE FUNCTION trg_lote_hist_anular_desde_inventario_gestion();
