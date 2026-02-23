-- Agregar estado_cierre a lote_postura_produccion (Abierta/Cerrada)
-- La columna Estado en la pestaña Producción mostrará Abierta o Cerrada, no "Produccion".

ALTER TABLE public.lote_postura_produccion
ADD COLUMN IF NOT EXISTS estado_cierre VARCHAR(20) NULL;

-- Valores por defecto: lotes existentes = Abierta
UPDATE public.lote_postura_produccion
SET estado_cierre = 'Abierta'
WHERE estado_cierre IS NULL;

ALTER TABLE public.lote_postura_produccion
DROP CONSTRAINT IF EXISTS ck_lpp_estado_cierre;

ALTER TABLE public.lote_postura_produccion
ADD CONSTRAINT ck_lpp_estado_cierre
CHECK (estado_cierre IN ('Abierta', 'Cerrada'));

COMMENT ON COLUMN public.lote_postura_produccion.estado_cierre IS 'Abierta: lote en producción activa. Cerrada: lote de producción finalizado.';
