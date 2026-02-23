-- ============================================================
-- Agregar lote_postura_produccion_id a seguimiento_diario
-- Para vincular registros de seguimiento producción con lote_postura_produccion
-- ============================================================

ALTER TABLE public.seguimiento_diario
ADD COLUMN IF NOT EXISTS lote_postura_produccion_id INTEGER NULL;

COMMENT ON COLUMN public.seguimiento_diario.lote_postura_produccion_id IS 'FK a lote_postura_produccion. Solo aplica cuando tipo_seguimiento = ''produccion''.';

-- Índice para consultas por lote_postura_produccion
CREATE INDEX IF NOT EXISTS ix_seguimiento_diario_lote_postura_produccion_id
ON public.seguimiento_diario(lote_postura_produccion_id)
WHERE lote_postura_produccion_id IS NOT NULL;

-- ============================================================
-- Agregar lote_postura_produccion_id a produccion_diaria (legacy)
-- ============================================================

ALTER TABLE public.produccion_diaria
ADD COLUMN IF NOT EXISTS lote_postura_produccion_id INTEGER NULL;

CREATE INDEX IF NOT EXISTS ix_produccion_diaria_lote_postura_produccion_id
ON public.produccion_diaria(lote_postura_produccion_id)
WHERE lote_postura_produccion_id IS NOT NULL;
