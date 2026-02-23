-- ============================================================
-- Agregar lote_postura_levante_id a seguimiento_diario
-- Para vincular registros de seguimiento con lote_postura_levante
-- ============================================================

ALTER TABLE public.seguimiento_diario
ADD COLUMN IF NOT EXISTS lote_postura_levante_id INTEGER NULL;

COMMENT ON COLUMN public.seguimiento_diario.lote_postura_levante_id IS 'FK a lote_postura_levante. Solo aplica cuando tipo_seguimiento = ''levante''.';

-- Índice para consultas por lote_postura_levante
CREATE INDEX IF NOT EXISTS ix_seguimiento_diario_lote_postura_levante_id
ON public.seguimiento_diario(lote_postura_levante_id)
WHERE lote_postura_levante_id IS NOT NULL;
