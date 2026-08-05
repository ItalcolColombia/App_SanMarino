-- =====================================================================================
-- Corrección del maestro de aves de pollo engorde: realinear lote_ave_engorde.hembras_l /
-- machos_l con la IDENTIDAD DE CONSERVACIÓN derivada de lo que está registrado.
--
--     maestro = inicio(historial) − ventas Completado − bajas BAJA_SEGUIMIENTO − ajustes fantasma
--
-- Origen: ticket 05-ago-2026 (diferencia de aves entre el seguimiento diario y la venta).
-- El fix de código (commit 3998aa2) resolvió la fórmula; esto corrige los DATOS que quedaron
-- desalineados en lotes puntuales. Este archivo es la copia trazable de la migración EF
-- 20260805150000_CorreccionMaestroAvesEngordeIdentidad.
--
-- ─── Por qué cada término de la identidad ────────────────────────────────────────────
--  · inicio   → historial_lote_pollo_engorde (tipo_registro='Inicio'): el encaset por SEXO.
--  · ventas   → movimiento_pollo_engorde Completado: lo único que descarga aves del maestro.
--  · bajas    → filas BAJA_SEGUIMIENTO vivas del histórico unificado: las bajas de seguimiento
--               que RetiroAvesEngordeAplicador YA descontó del maestro (las anteriores al
--               descuento automático no tienen fila y por eso NO entran aquí).
--  · ajustes  → historial_lote_pollo_engorde (tipo_registro='Ajuste'): descuentos DELIBERADOS
--               de aves fantasma. 🔴 Omitirlos revive aves ya dadas de baja a propósito.
--
-- ─── Guardas (por qué NO se toca todo lo que "no cuadra") ────────────────────────────
--  1. `inicio = aves_encasetadas`: si el historial Inicio no coincide con el encaset del lote,
--     la referencia no es confiable y el lote queda FUERA (4 lotes de Ecuador están así; su
--     pantalla ya coincide con la grilla, así que no hay nada que arreglar).
--  2. Resultado >= 0 por sexo: nunca se escribe un maestro negativo.
--  3. `IS DISTINCT FROM`: idempotente; re-ejecutar no toca ninguna fila.
--
-- ─── Efecto medido sobre el dump tipo-prod (simulado en transacción + ROLLBACK) ──────
--  ItalcolPanama ......... 0 lotes tocados (sus 60 lotes ya cumplen la identidad)
--  ItalcolEcuador ........ 2 lotes:
--    · id 107 · Kilometro 61 · Galpon-1 · lote 2604
--        10865/13386 → 10860/13374 (−17). Los 17 son exactamente la fila BAJA_SEGUIMIENTO
--        del 24-07 (origen_id 10595, 5 H + 12 M): la fila se escribió pero su descuento nunca
--        llegó al maestro. Tras la corrección la pantalla da 23.919 = la grilla.
--    · id 184 · SAN GUILLERMO · Galpon-1 · lote 2604
--        5793/5695 → 5743/5745 (total invariante). Solo corrige el reparto por SEXO; el
--        total mostrado no cambia (11.488 antes y después).
--  Los 8 lotes 2601 corregidos deliberadamente en su día (filas 'Ajuste' por 1.552 aves,
--  todos liquidados) quedan INTACTOS: con los ajustes en la identidad, ya cuadran.
--
-- ─── Reversión manual (el Down de la migración es no-op a propósito) ─────────────────
--  Valores previos exactos en el dump del 05-ago-2026, por si hiciera falta restaurarlos:
--    UPDATE lote_ave_engorde SET hembras_l=10865, machos_l=13386 WHERE lote_ave_engorde_id=107;
--    UPDATE lote_ave_engorde SET hembras_l=5793,  machos_l=5695  WHERE lote_ave_engorde_id=184;
-- =====================================================================================

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
      AND i.ih + i.im + i.ix = l.aves_encasetadas   -- guarda 1
)
UPDATE lote_ave_engorde l
SET hembras_l = o.nh, machos_l = o.nm
FROM objetivo o
WHERE l.lote_ave_engorde_id = o.id
  AND o.nh >= 0 AND o.nm >= 0                                                    -- guarda 2
  AND (l.hembras_l IS DISTINCT FROM o.nh OR l.machos_l IS DISTINCT FROM o.nm);   -- guarda 3
