-- =============================================================================
-- Migración masiva: recalcular `saldo_alimento_kg` para todos los lotes engorde
-- Plan: fase_de_desarrollo/13_migracion_masiva_saldo_alimento.md
-- Fecha: 2026-05-28
-- Dependencias:
--   * `fn_seguimiento_diario_engorde(int)` con fix #10 + #12 ya aplicado
--   * `lote_ave_engorde` y `seguimiento_diario_aves_engorde` existentes
--
-- Lógica:
--   1. Crea (si no existe) tabla persistente `_migracion_saldo_alimento_2026_05_28`
--      con snapshot del estado ANTES de la migración (para rollback de emergencia).
--   2. Calcula el saldo nuevo por seg_id usando `fn_seguimiento_diario_engorde` para
--      cada lote_ave_engorde activo (LATERAL JOIN).
--   3. UPDATE únicamente las filas donde el saldo persistido difiere del nuevo
--      (idempotente: re-ejecutar no produce cambios extra).
--   4. Reporta resumen de cambios.
--   5. Valida 0 discrepancias post-migración.
--
-- Para revertir (post-COMMIT):
--   UPDATE seguimiento_diario_aves_engorde p
--   SET saldo_alimento_kg = b.saldo_antes
--   FROM _migracion_saldo_alimento_2026_05_28 b
--   WHERE p.id = b.id;
-- =============================================================================

BEGIN;

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. Snapshot persistente (para rollback de emergencia)
-- ─────────────────────────────────────────────────────────────────────────────
DROP TABLE IF EXISTS _migracion_saldo_alimento_2026_05_28;
CREATE TABLE _migracion_saldo_alimento_2026_05_28 AS
SELECT
    s.id                                            AS seg_id,
    s.lote_ave_engorde_id                           AS lote_id,
    DATE(s.fecha)                                   AS fecha,
    s.saldo_alimento_kg                             AS saldo_antes,
    s.updated_at                                    AS updated_at_antes,
    (now() AT TIME ZONE 'utc')                      AS migrated_at
FROM seguimiento_diario_aves_engorde s
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
WHERE l.deleted_at IS NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. Calcular saldos nuevos (vía fn_seguimiento_diario_engorde) y aplicar UPDATE
-- ─────────────────────────────────────────────────────────────────────────────
WITH nuevos_saldos AS (
    SELECT
        l.lote_ave_engorde_id                       AS lote_id,
        fn.seg_id,
        fn.saldo_alimento_kg::numeric(18,3)         AS saldo_nuevo
    FROM lote_ave_engorde l
    CROSS JOIN LATERAL fn_seguimiento_diario_engorde(l.lote_ave_engorde_id) fn
    WHERE l.deleted_at IS NULL
)
UPDATE seguimiento_diario_aves_engorde p
SET
    saldo_alimento_kg = n.saldo_nuevo,
    updated_at        = (now() AT TIME ZONE 'utc')
FROM nuevos_saldos n
WHERE p.id = n.seg_id
  AND (
       p.saldo_alimento_kg IS NULL
    OR ABS(p.saldo_alimento_kg - n.saldo_nuevo) >= 0.001
  );

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. Resumen de cambios aplicados
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    'RESUMEN DE CAMBIOS'                            AS reporte,
    COUNT(*)                                        AS filas_actualizadas,
    COUNT(*) FILTER (WHERE s.saldo_antes IS NULL)   AS de_null_a_valor,
    COUNT(*) FILTER (WHERE s.saldo_antes IS NOT NULL) AS valor_corregido,
    COUNT(DISTINCT s.lote_id)                       AS lotes_afectados,
    ROUND(MAX(ABS(COALESCE(s.saldo_antes, 0) - p.saldo_alimento_kg))::numeric, 3) AS max_delta_kg,
    ROUND(SUM(COALESCE(s.saldo_antes, 0) - p.saldo_alimento_kg)::numeric, 3) AS suma_delta_kg
FROM _migracion_saldo_alimento_2026_05_28 s
JOIN seguimiento_diario_aves_engorde p ON p.id = s.seg_id
WHERE (
       (s.saldo_antes IS NULL AND p.saldo_alimento_kg IS NOT NULL)
    OR (s.saldo_antes IS NOT NULL AND ABS(s.saldo_antes - p.saldo_alimento_kg) >= 0.001)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. Validación post-migración: 0 discrepancias entre persistido y fn
-- ─────────────────────────────────────────────────────────────────────────────
WITH check_post AS (
    SELECT
        fn.seg_id,
        fn.saldo_alimento_kg                        AS fn_saldo,
        p.saldo_alimento_kg::FLOAT8                 AS p_saldo
    FROM lote_ave_engorde l
    CROSS JOIN LATERAL fn_seguimiento_diario_engorde(l.lote_ave_engorde_id) fn
    JOIN seguimiento_diario_aves_engorde p ON p.id = fn.seg_id
    WHERE l.deleted_at IS NULL
)
SELECT
    'VALIDACION POST-MIGRACION'                     AS reporte,
    COUNT(*)                                        AS total_filas,
    COUNT(*) FILTER (WHERE ABS(fn_saldo - p_saldo) < 0.001) AS coinciden,
    COUNT(*) FILTER (WHERE ABS(fn_saldo - p_saldo) >= 0.001) AS divergen,
    ROUND(MAX(ABS(fn_saldo - p_saldo))::numeric, 6) AS max_diff_kg,
    CASE
        WHEN COUNT(*) FILTER (WHERE ABS(fn_saldo - p_saldo) >= 0.001) = 0
        THEN 'OK ✅ — Todos los saldos coinciden con fn_seguimiento_diario_engorde'
        ELSE 'FAIL ❌ — Quedan discrepancias; considere ROLLBACK'
    END                                             AS resultado
FROM check_post;

-- ─────────────────────────────────────────────────────────────────────────────
-- 5. Confirmar (revisar los SELECT anteriores antes de cambiar a COMMIT).
-- ─────────────────────────────────────────────────────────────────────────────
COMMIT;
