-- =====================================================================
-- 050 — Acumulados de traslado en lote_postura_produccion
-- Feature 14: paridad con Levante (Feature 13)
--
-- Mismas 4 columnas que añadimos en lote_postura_levante:
--   • traslado_ingreso_hembras / machos  → suma de todas las entradas
--   • traslado_salida_hembras / machos   → suma de todas las salidas
-- =====================================================================

ALTER TABLE lote_postura_produccion
  ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_ingreso_machos  INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_salida_hembras  INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_salida_machos   INTEGER NOT NULL DEFAULT 0;

COMMENT ON COLUMN lote_postura_produccion.traslado_ingreso_hembras IS 'Hembras recibidas vía traslados (acumulado).';
COMMENT ON COLUMN lote_postura_produccion.traslado_ingreso_machos  IS 'Machos recibidos vía traslados (acumulado).';
COMMENT ON COLUMN lote_postura_produccion.traslado_salida_hembras  IS 'Hembras enviadas vía traslados (acumulado).';
COMMENT ON COLUMN lote_postura_produccion.traslado_salida_machos   IS 'Machos enviados vía traslados (acumulado).';
