-- =====================================================================
-- 052 — Renombrar columnas de traslado con prefijo de fase
-- Feature 14 (refinamiento UX): las columnas de acumulado de traslado
-- ahora indican claramente la fase del lote en que ocurrió el movimiento.
--
--   lote_postura_levante.traslado_*    → lote_postura_levante.levante_traslado_*
--   lote_postura_produccion.traslado_* → lote_postura_produccion.produccion_traslado_*
-- =====================================================================

-- ── LOTE POSTURA LEVANTE ────────────────────────────────────────────
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
             WHERE table_name = 'lote_postura_levante'
               AND column_name = 'traslado_ingreso_hembras') THEN
    ALTER TABLE lote_postura_levante RENAME COLUMN traslado_ingreso_hembras TO levante_traslado_ingreso_hembras;
  END IF;

  IF EXISTS (SELECT 1 FROM information_schema.columns
             WHERE table_name = 'lote_postura_levante'
               AND column_name = 'traslado_ingreso_machos') THEN
    ALTER TABLE lote_postura_levante RENAME COLUMN traslado_ingreso_machos TO levante_traslado_ingreso_machos;
  END IF;

  IF EXISTS (SELECT 1 FROM information_schema.columns
             WHERE table_name = 'lote_postura_levante'
               AND column_name = 'traslado_salida_hembras') THEN
    ALTER TABLE lote_postura_levante RENAME COLUMN traslado_salida_hembras TO levante_traslado_salida_hembras;
  END IF;

  IF EXISTS (SELECT 1 FROM information_schema.columns
             WHERE table_name = 'lote_postura_levante'
               AND column_name = 'traslado_salida_machos') THEN
    ALTER TABLE lote_postura_levante RENAME COLUMN traslado_salida_machos TO levante_traslado_salida_machos;
  END IF;
END $$;

-- ── LOTE POSTURA PRODUCCION ─────────────────────────────────────────
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
             WHERE table_name = 'lote_postura_produccion'
               AND column_name = 'traslado_ingreso_hembras') THEN
    ALTER TABLE lote_postura_produccion RENAME COLUMN traslado_ingreso_hembras TO produccion_traslado_ingreso_hembras;
  END IF;

  IF EXISTS (SELECT 1 FROM information_schema.columns
             WHERE table_name = 'lote_postura_produccion'
               AND column_name = 'traslado_ingreso_machos') THEN
    ALTER TABLE lote_postura_produccion RENAME COLUMN traslado_ingreso_machos TO produccion_traslado_ingreso_machos;
  END IF;

  IF EXISTS (SELECT 1 FROM information_schema.columns
             WHERE table_name = 'lote_postura_produccion'
               AND column_name = 'traslado_salida_hembras') THEN
    ALTER TABLE lote_postura_produccion RENAME COLUMN traslado_salida_hembras TO produccion_traslado_salida_hembras;
  END IF;

  IF EXISTS (SELECT 1 FROM information_schema.columns
             WHERE table_name = 'lote_postura_produccion'
               AND column_name = 'traslado_salida_machos') THEN
    ALTER TABLE lote_postura_produccion RENAME COLUMN traslado_salida_machos TO produccion_traslado_salida_machos;
  END IF;
END $$;

COMMENT ON COLUMN lote_postura_levante.levante_traslado_ingreso_hembras   IS 'Hembras recibidas en fase LEVANTE.';
COMMENT ON COLUMN lote_postura_levante.levante_traslado_ingreso_machos    IS 'Machos recibidos en fase LEVANTE.';
COMMENT ON COLUMN lote_postura_levante.levante_traslado_salida_hembras    IS 'Hembras enviadas en fase LEVANTE.';
COMMENT ON COLUMN lote_postura_levante.levante_traslado_salida_machos     IS 'Machos enviados en fase LEVANTE.';

COMMENT ON COLUMN lote_postura_produccion.produccion_traslado_ingreso_hembras IS 'Hembras recibidas en fase PRODUCCIÓN.';
COMMENT ON COLUMN lote_postura_produccion.produccion_traslado_ingreso_machos  IS 'Machos recibidos en fase PRODUCCIÓN.';
COMMENT ON COLUMN lote_postura_produccion.produccion_traslado_salida_hembras  IS 'Hembras enviadas en fase PRODUCCIÓN.';
COMMENT ON COLUMN lote_postura_produccion.produccion_traslado_salida_machos   IS 'Machos enviados en fase PRODUCCIÓN.';
