-- ============================================================
-- Agrega farm_id y erp_create a la tabla lote_postura_base
-- Ejecutar manualmente en la base de datos local
-- ============================================================

ALTER TABLE lote_postura_base
  ADD COLUMN IF NOT EXISTS farm_id    INTEGER NULL,
  ADD COLUMN IF NOT EXISTS erp_create DATE    NULL;

CREATE INDEX IF NOT EXISTS ix_lote_postura_base_farm_id ON lote_postura_base(farm_id);
