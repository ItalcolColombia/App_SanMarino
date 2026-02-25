-- =============================================================================
-- MIGRACIÓN 02: Vincular seguimiento_diario (tipo=levante) con lote_postura_levante_id
-- Si lote_id en seguimiento_diario es numérico (lotes.lote_id) o coincide con lote_nombre,
-- actualizar lote_postura_levante_id desde la relación lote_id -> LPL.
-- =============================================================================

-- Caso 1: lote_id en seguimiento_diario es el lote_id numérico (como string)
UPDATE public.seguimiento_diario sd
SET lote_postura_levante_id = lpl.lote_postura_levante_id
FROM public.lote_postura_levante lpl
WHERE sd.tipo_seguimiento = 'levante'
  AND sd.lote_postura_levante_id IS NULL
  AND lpl.deleted_at IS NULL
  AND (
    sd.lote_id = lpl.lote_id::text
    OR (sd.lote_id ~ '^\d+$' AND lpl.lote_id::text = sd.lote_id)
  );

-- Caso 2: lote_id en seguimiento_diario es lote_nombre (string)
UPDATE public.seguimiento_diario sd
SET lote_postura_levante_id = lpl.lote_postura_levante_id
FROM public.lote_postura_levante lpl
WHERE sd.tipo_seguimiento = 'levante'
  AND sd.lote_postura_levante_id IS NULL
  AND lpl.deleted_at IS NULL
  AND TRIM(lpl.lote_nombre) = TRIM(sd.lote_id);

-- Si existe seguimiento_lote_levante (tabla legacy) y seguimiento_diario fue poblado desde ahí:
-- el lote_id en SD podría venir de SeguimientoLoteLevante.LoteId (int) -> habría que migrar primero
-- o ya estar en seguimiento_diario. Este script asume seguimiento_diario ya existe con lote_id.

SELECT 'Seguimiento levante vinculado a LPL' AS paso, 
       COUNT(*) AS registros_actualizados
FROM public.seguimiento_diario 
WHERE tipo_seguimiento = 'levante' AND lote_postura_levante_id IS NOT NULL;
