-- Crea tabla para "lote base" de postura (creación rápida)
-- Nota: el esquema definitivo se gestiona vía EF migrations; este script es opcional para entornos manuales.

CREATE TABLE IF NOT EXISTS public.lote_postura_base (
  lote_postura_base_id SERIAL PRIMARY KEY,
  lote_nombre          VARCHAR(200) NOT NULL,
  codigo_erp           VARCHAR(80) NULL,
  cantidad_hembras     INTEGER NOT NULL DEFAULT 0,
  cantidad_machos      INTEGER NOT NULL DEFAULT 0,
  cantidad_mixtas      INTEGER NOT NULL DEFAULT 0,
  company_id           INTEGER NOT NULL,
  created_by_user_id   INTEGER NOT NULL,
  created_at           TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
  updated_by_user_id   INTEGER NULL,
  updated_at           TIMESTAMP WITHOUT TIME ZONE NULL,
  deleted_at           TIMESTAMP WITHOUT TIME ZONE NULL,
  pais_id              INTEGER NULL,
  CONSTRAINT ck_lpb_nonneg_counts CHECK (cantidad_hembras >= 0 AND cantidad_machos >= 0 AND cantidad_mixtas >= 0)
);

CREATE INDEX IF NOT EXISTS ix_lote_postura_base_company ON public.lote_postura_base(company_id);
CREATE INDEX IF NOT EXISTS ix_lote_postura_base_codigo_erp ON public.lote_postura_base(codigo_erp);

-- Relación opcional: guardar el lote base en el lote principal
ALTER TABLE IF EXISTS public.lotes
  ADD COLUMN IF NOT EXISTS lote_postura_base_id INTEGER NULL;
CREATE INDEX IF NOT EXISTS ix_lotes_lote_postura_base_id
  ON public.lotes(lote_postura_base_id);

