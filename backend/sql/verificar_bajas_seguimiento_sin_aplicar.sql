-- ─────────────────────────────────────────────────────────────────────────────
-- Bajas de seguimiento que NUNCA se aplicaron al maestro (pollo engorde)
--
-- COMPLEMENTO de `fn_cuadre_aves_engorde`, no un segundo detector: el desfase del maestro se mira
-- SIEMPRE con esa función (una sola fórmula por número).
--
--     SELECT * FROM fn_cuadre_aves_engorde(NULL) WHERE NOT cuadra;   -- sano = 0 filas
--
-- Lo que se mira acá es el otro lado del invariante: la fila `BAJA_SEGUIMIENTO` del histórico
-- unificado es la ÚNICA prueba de que un día descontó aves. Un seguimiento sin fila nunca movió el
-- maestro — es la cohorte anterior al aplicador (< 2026-07-27 17:58) o un día cuyo descuento falló
-- y quedó en el log.
--
-- Antes del fix de `RetiroAvesEngordeAplicador.SincronizarAsync` (ago-2026), borrar o editar uno de
-- esos registros ACREDITABA al maestro aves que jamás se habían debitado, en silencio: sin fila
-- anulada, sin `updated_at` y sin auditoría. Hoy el baseline sale de la fila, así que esta cohorte
-- es inocua; el bloque A queda como termómetro de cuánto historial arrastra cada empresa.
-- Plan: `fase_de_desarrollo/fix_baseline_bajas_seguimiento_engorde_plan.md`
--
-- SOLO LECTURA. Sano = **0 filas en el bloque B**.
--
-- Uso:
--   psql ... -f backend/sql/verificar_bajas_seguimiento_sin_aplicar.sql
-- ─────────────────────────────────────────────────────────────────────────────

\echo '=== A) Informativo: seguimientos SIN fila BAJA_SEGUIMIENTO (nunca descontaron el maestro) ==='

SELECT c.name AS empresa,
       COUNT(*)                                   AS seguimientos_sin_fila,
       COUNT(DISTINCT s.lote_ave_engorde_id)      AS lotes,
       SUM(COALESCE(s.mortalidad_hembras,0)+COALESCE(s.sel_h,0)+COALESCE(s.error_sexaje_hembras,0)
          +COALESCE(s.mortalidad_machos,0)+COALESCE(s.sel_m,0)+COALESCE(s.error_sexaje_machos,0)) AS aves,
       MIN(s.fecha)::date                         AS desde,
       MAX(s.fecha)::date                         AS hasta
FROM seguimiento_diario_aves_engorde s
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
JOIN companies        c ON c.id = l.company_id
LEFT JOIN lote_registro_historico_unificado h
       ON h.origen_tabla = 'seguimiento_diario_aves_engorde' AND h.origen_id = s.id
WHERE h.id IS NULL AND l.deleted_at IS NULL
GROUP BY 1
ORDER BY 2 DESC;

\echo ''
\echo '=== B) Filas BAJA_SEGUIMIENTO huerfanas VIVAS — el seguimiento ya no existe (deben estar anuladas) ==='
-- El histórico unificado se ANULA, nunca se abandona: una fila viva cuyo origen desapareció sigue
-- restando aves que ya no tienen respaldo. `SincronizarCruceAsync` las revierte; si aparece alguna
-- por otro camino, es un bug de ese camino.

SELECT h.lote_ave_engorde_id AS lote, h.origen_id AS seg_id, h.fecha_operacion,
       h.cantidad_hembras, h.cantidad_machos, h.cantidad_mixtas, h.referencia
FROM lote_registro_historico_unificado h
LEFT JOIN seguimiento_diario_aves_engorde s ON s.id = h.origen_id
WHERE h.tipo_evento  = 'BAJA_SEGUIMIENTO'
  AND h.origen_tabla = 'seguimiento_diario_aves_engorde'
  AND s.id IS NULL
  AND NOT h.anulado
ORDER BY 1, 2;
