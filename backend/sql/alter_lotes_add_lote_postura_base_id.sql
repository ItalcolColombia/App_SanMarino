-- Agrega referencia opcional a lote_postura_base desde lotes
-- Flujo: "Nueva asociación a seguimiento" -> crea lote (public.lotes) y guarda lote_postura_base_id

-- 1) Agregar columna (nullable)
ALTER TABLE IF EXISTS public.lotes
  ADD COLUMN IF NOT EXISTS lote_postura_base_id INTEGER NULL;

-- 2) Índice para búsquedas/joins
CREATE INDEX IF NOT EXISTS ix_lotes_lote_postura_base_id
  ON public.lotes(lote_postura_base_id);

-- 3) FK opcional (si la tabla public.lote_postura_base existe)
DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM information_schema.tables
    WHERE table_schema = 'public'
      AND table_name   = 'lote_postura_base'
  ) THEN
    IF NOT EXISTS (
      SELECT 1
      FROM pg_constraint
      WHERE conname = 'fk_lotes_lote_postura_base'
    ) THEN
      ALTER TABLE public.lotes
        ADD CONSTRAINT fk_lotes_lote_postura_base
        FOREIGN KEY (lote_postura_base_id)
        REFERENCES public.lote_postura_base(lote_postura_base_id)
        ON DELETE RESTRICT;
    END IF;
  END IF;
END $$;

