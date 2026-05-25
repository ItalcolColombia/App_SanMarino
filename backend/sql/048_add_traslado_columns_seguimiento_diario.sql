-- =====================================================================
-- 048 — Columnas dedicadas de traslado en seguimiento_diario_levante_reproductoras
-- Feature 13 (refinamiento): separar traslado de mortalidad/selección
--
-- Hasta ahora el TrasladoAvesDesdeSegService guardaba los splits H/M del
-- traslado en las columnas mortalidad_hembras / mortalidad_machos. Eso
-- causaba que el traslado se viera como mortalidad en la tabla de
-- seguimiento diario. Ahora cada lado del traslado tiene sus propias
-- columnas:
--   • traslado_ingreso_hembras / machos  → fila INGRESO en el lote destino
--   • traslado_salida_hembras / machos   → fila SALIDA  en el lote origen
--
-- Las columnas mortalidad_* quedan exclusivamente para mortalidades manuales.
-- =====================================================================

ALTER TABLE seguimiento_diario_levante_reproductoras
  ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_ingreso_machos  INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_salida_hembras  INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_salida_machos   INTEGER NOT NULL DEFAULT 0;

COMMENT ON COLUMN seguimiento_diario_levante_reproductoras.traslado_ingreso_hembras IS 'Hembras recibidas por traslado en esta fila (dirección INGRESO).';
COMMENT ON COLUMN seguimiento_diario_levante_reproductoras.traslado_ingreso_machos  IS 'Machos recibidos por traslado en esta fila (dirección INGRESO).';
COMMENT ON COLUMN seguimiento_diario_levante_reproductoras.traslado_salida_hembras  IS 'Hembras enviadas por traslado en esta fila (dirección SALIDA).';
COMMENT ON COLUMN seguimiento_diario_levante_reproductoras.traslado_salida_machos   IS 'Machos enviados por traslado en esta fila (dirección SALIDA).';
