-- ============================================================
-- Agregar estado_cierre a lote_postura_levante
-- Valores: 'Abierto' (antes semana 26) | 'Cerrado' (semana 26+)
-- ============================================================

ALTER TABLE public.lote_postura_levante
ADD COLUMN IF NOT EXISTS estado_cierre VARCHAR(20) NULL DEFAULT 'Abierto';

-- Actualizar existentes sin valor
UPDATE public.lote_postura_levante
SET estado_cierre = 'Abierto'
WHERE estado_cierre IS NULL;

-- Restricción de valores
ALTER TABLE public.lote_postura_levante
DROP CONSTRAINT IF EXISTS ck_lpl_estado_cierre;

ALTER TABLE public.lote_postura_levante
ADD CONSTRAINT ck_lpl_estado_cierre
CHECK (estado_cierre IN ('Abierto', 'Cerrado'));

COMMENT ON COLUMN public.lote_postura_levante.estado_cierre IS 'Abierto: antes semana 26. Cerrado: semana 26+ cuando se crean lotes producción.';
