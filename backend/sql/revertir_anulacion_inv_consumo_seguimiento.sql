-- =============================================================================
-- REVERTIR anulación incorrecta de INV_CONSUMO causada por el script previo.
--
-- El script previo anuló registros INV_CONSUMO cuyo seguimiento ya no existe.
-- Para lotes que SÍ tenían su seguimiento activo, anuló registros válidos.
-- Este script restaura esos registros (anulado = FALSE) donde el seguimiento
-- aún existe en seguimiento_diario_aves_engorde.
--
-- SEGURO: Solo restaura registros cuyo seguimiento existe; no toca los que
-- fueron anulados correctamente (seguimiento fue eliminado).
-- =============================================================================

-- 1. Vista previa — cuántos registros se van a restaurar
SELECT COUNT(*) AS registros_a_restaurar
FROM public.lote_registro_historico_unificado h
WHERE h.tipo_evento = 'INV_CONSUMO'
  AND h.anulado = TRUE
  AND h.referencia ~ '^Seguimiento aves engorde #(\d+)'
  AND EXISTS (
    SELECT 1 FROM public.seguimiento_diario_aves_engorde s
    WHERE s.id = (regexp_match(h.referencia, '^Seguimiento aves engorde #(\d+)'))[1]::bigint
  );

-- 2. Restaurar los registros incorrectamente anulados
UPDATE public.lote_registro_historico_unificado h
SET anulado = FALSE
WHERE h.tipo_evento = 'INV_CONSUMO'
  AND h.anulado = TRUE
  AND h.referencia ~ '^Seguimiento aves engorde #(\d+)'
  AND EXISTS (
    SELECT 1 FROM public.seguimiento_diario_aves_engorde s
    WHERE s.id = (regexp_match(h.referencia, '^Seguimiento aves engorde #(\d+)'))[1]::bigint
  );

-- 3. Confirmar cuántos quedaron anulados (deben ser solo los orphans reales)
SELECT COUNT(*) AS consumos_aun_anulados_orphans
FROM public.lote_registro_historico_unificado h
WHERE h.tipo_evento = 'INV_CONSUMO'
  AND h.anulado = TRUE
  AND h.referencia ~ '^Seguimiento aves engorde #(\d+)'
  AND NOT EXISTS (
    SELECT 1 FROM public.seguimiento_diario_aves_engorde s
    WHERE s.id = (regexp_match(h.referencia, '^Seguimiento aves engorde #(\d+)'))[1]::bigint
  );
