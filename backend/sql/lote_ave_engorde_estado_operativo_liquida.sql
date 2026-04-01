-- Estado operativo del lote (liquidación / reapertura). Ejecutar en PostgreSQL si no aplica migraciones EF.
ALTER TABLE public.lote_ave_engorde
  ADD COLUMN IF NOT EXISTS estado_operativo_lote VARCHAR(20) NOT NULL DEFAULT 'Abierto',
  ADD COLUMN IF NOT EXISTS liquidado_at TIMESTAMPTZ NULL,
  ADD COLUMN IF NOT EXISTS liquidado_por_user_id VARCHAR(450) NULL,
  ADD COLUMN IF NOT EXISTS reabierto_at TIMESTAMPTZ NULL,
  ADD COLUMN IF NOT EXISTS reabierto_por_user_id VARCHAR(450) NULL,
  ADD COLUMN IF NOT EXISTS motivo_reapertura VARCHAR(2000) NULL;

UPDATE public.lote_ave_engorde SET estado_operativo_lote = 'Abierto' WHERE estado_operativo_lote IS NULL OR TRIM(estado_operativo_lote) = '';

COMMENT ON COLUMN public.lote_ave_engorde.estado_operativo_lote IS 'Abierto | Cerrado (liquidado).';
COMMENT ON COLUMN public.lote_ave_engorde.liquidado_por_user_id IS 'Id usuario (Guid) que cerró el lote.';
COMMENT ON COLUMN public.lote_ave_engorde.reabierto_por_user_id IS 'Id usuario (Guid) que reabrió el lote.';
