-- =============================================================================
-- Prueba end-to-end del AJUSTE DE CUADRE, en una transaccion que se REVIERTE.
--
-- Simula exactamente lo que escribe CuadrarGalponAsync y verifica que
-- fn_cuadre_alimento_engorde deje el galpon en 0, en las DOS direcciones:
--
--   A) G0044 (ItalcolEcuador) — SOBRA STOCK. La tabla tiene razon.
--      Se corrige el inventario; la tabla no se toca.
--   B) G0475 (ItalcolPanama) — SOBRA TABLA. El stock tiene razon.
--      Se corrige la tabla con el tipo NUEVO; el inventario no se toca.
--
-- El caso B es el que hasta hoy NO TENIA ARREGLO POSIBLE desde ninguna pantalla.
--
-- ⚠️ A diferencia de los otros `verificar_*.sql`, este ESCRIBE — pero todo va dentro de un BEGIN
-- que termina en ROLLBACK, asi que no deja nada. Aun asi: correrlo contra PRODUCCION no tiene
-- sentido y toma bloqueos sobre filas reales mientras dura. Es para la copia local.
--
-- Resultado medido el 25-ago-2026 sobre la copia de produccion:
--   G0044  descuadre -5.000,0 -> 0,0   (ItalcolEcuador pasa de 1 galpon descuadrado a 0)
--   G0475  descuadre 18.650,4 -> 0,0   (ItalcolPanama baja de 12 a 11, y de 55.866,5 a 37.216,1 kg)
-- =============================================================================
\set ON_ERROR_STOP on
\timing off

BEGIN;

-- ── ANTES ────────────────────────────────────────────────────────────────────
\echo '=== ANTES ==='
SELECT granja, galpon_id, lote_ave_engorde_id,
       ROUND(saldo_tabla_kg::numeric,1) saldo, ROUND(mov_post_kg::numeric,1) mov_post,
       ROUND(stock_kg::numeric,1) stock, ROUND(descuadre_kg::numeric,1) descuadre
FROM fn_cuadre_alimento_engorde(NULL)
WHERE lote_ave_engorde_id IN (207, 165)
ORDER BY granja;

-- ═════════════════════════════════════════════════════════════════════════════
-- A) G0044 · granja 41 · nucleo 685062 · item 5 · delta stock = -5.000
--    Es lo que hace ActualizarStockAsync: baja la fila de stock y deja el
--    movimiento `AjusteStock` (que la tabla diaria NO ve, y esta bien).
-- ═════════════════════════════════════════════════════════════════════════════
UPDATE inventario_gestion_stock
   SET quantity = quantity - 5000, updated_at = now()
 WHERE farm_id = 41 AND TRIM(COALESCE(nucleo_id,'')) = '685062'
   AND TRIM(COALESCE(galpon_id,'')) = 'G0044' AND item_inventario_ecuador_id = 5;

INSERT INTO inventario_gestion_movimiento
    (company_id, pais_id, farm_id, nucleo_id, galpon_id, item_inventario_ecuador_id,
     quantity, unit, movement_type, estado, reference, reason, created_at, created_by_user_id)
SELECT s.company_id, s.pais_id, s.farm_id, s.nucleo_id, s.galpon_id, s.item_inventario_ecuador_id,
       5000, s.unit, 'AjusteStock', 'Ajuste manual', NULL,
       'Ajuste manual. Cuadre de galpon. remision 63705 duplicada, eliminada el 19-ago',
       now(), NULL
FROM inventario_gestion_stock s
WHERE s.farm_id = 41 AND TRIM(COALESCE(s.nucleo_id,'')) = '685062'
  AND TRIM(COALESCE(s.galpon_id,'')) = 'G0044' AND s.item_inventario_ecuador_id = 5;

-- ═════════════════════════════════════════════════════════════════════════════
-- B) G0475 · granja de Panama · el tipo NUEVO. Solo mueve la TABLA DIARIA.
--    Fechado en el ULTIMO SEGUIMIENTO del ciclo: un movimiento posterior a
--    seg_max seria "movimiento posterior" y no entraria en la tabla.
-- ═════════════════════════════════════════════════════════════════════════════
INSERT INTO inventario_gestion_movimiento
    (company_id, pais_id, farm_id, nucleo_id, galpon_id, item_inventario_ecuador_id,
     quantity, unit, movement_type, estado, reference, reason, created_at, created_by_user_id)
SELECT c.company_id, s.pais_id, c.granja_id, c.nucleo_id, c.galpon_id, s.item_inventario_ecuador_id,
       ROUND(ABS(c.saldo_tabla_kg - (c.stock_kg - c.mov_post_kg))::numeric, 3),
       'kg', 'AjusteCuadreTablaSalida', 'Ajuste de cuadre', 'Ajuste de cuadre',
       'Cuadre de galpon: alinear la tabla diaria con el inventario ya corregido a mano',
       (c.ultimo_seguimiento + TIME '12:00')::timestamptz, NULL
FROM fn_cuadre_alimento_engorde(NULL) c
JOIN LATERAL (
    SELECT s2.item_inventario_ecuador_id, s2.pais_id
    FROM inventario_gestion_stock s2
    WHERE s2.farm_id = c.granja_id
      AND TRIM(COALESCE(s2.nucleo_id,'')) = c.nucleo_id
      AND TRIM(COALESCE(s2.galpon_id,'')) = c.galpon_id
    ORDER BY s2.quantity DESC
    LIMIT 1) s ON TRUE
WHERE c.lote_ave_engorde_id = 165;

-- ── El trigger tiene que haber espejado el tipo NUEVO en el historico ────────
\echo ''
\echo '=== El trigger espejo el tipo nuevo? (tiene que decir INV_AJUSTE_CUADRE_SALIDA) ==='
SELECT h.tipo_evento, h.cantidad_kg, h.fecha_operacion, h.referencia
FROM lote_registro_historico_unificado h
WHERE h.movement_type_original = 'AjusteCuadreTablaSalida';

-- ── DESPUES ──────────────────────────────────────────────────────────────────
\echo ''
\echo '=== DESPUES (las dos filas tienen que quedar en descuadre 0,0) ==='
SELECT granja, galpon_id, lote_ave_engorde_id,
       ROUND(saldo_tabla_kg::numeric,1) saldo, ROUND(mov_post_kg::numeric,1) mov_post,
       ROUND(stock_kg::numeric,1) stock, ROUND(descuadre_kg::numeric,1) descuadre,
       filas_negativas
FROM fn_cuadre_alimento_engorde(NULL)
WHERE lote_ave_engorde_id IN (207, 165)
ORDER BY granja;

-- ── Y NINGUN otro galpon se puede haber movido ───────────────────────────────
\echo ''
\echo '=== Resumen global: Ecuador tiene que bajar a 0 descuadrados; Panama a 11 ==='
SELECT empresa, COUNT(*) total,
       COUNT(*) FILTER (WHERE ABS(descuadre_kg) > 1) descuadrados,
       ROUND(SUM(ABS(descuadre_kg)) FILTER (WHERE ABS(descuadre_kg) > 1)::numeric,1) kg_abs
FROM fn_cuadre_alimento_engorde(NULL)
GROUP BY 1 ORDER BY 1;

ROLLBACK;
