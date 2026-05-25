-- =====================================================================
-- 051 — Columnas dedicadas de traslado + auditoría en produccion_seguimiento
-- Feature 14: paridad con seguimiento_diario_levante_reproductoras
--
-- Añade splits H/M dedicados para traslados, flags es_traslado +
-- referencia a contraparte, y updated_by_user_id para auditoría.
-- También añade sel_h / sel_m / error_sexaje_h / error_sexaje_m
-- si no existen, para alinear con Levante.
-- =====================================================================

ALTER TABLE produccion_seguimiento
  ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_ingreso_machos  INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_salida_hembras  INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_salida_machos   INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS es_traslado                    BOOLEAN     NOT NULL DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS traslado_lote_contraparte_id   INTEGER     NULL,
  ADD COLUMN IF NOT EXISTS traslado_granja_contraparte_id INTEGER     NULL,
  ADD COLUMN IF NOT EXISTS traslado_direccion             VARCHAR(10) NULL,
  ADD COLUMN IF NOT EXISTS sel_h                INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS sel_m                INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS error_sexaje_hembras INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS error_sexaje_machos  INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS updated_by_user_id   INTEGER NULL;

-- Índice parcial para filtrar registros de traslado rápidamente
CREATE INDEX IF NOT EXISTS idx_produccion_seguimiento_es_traslado
  ON produccion_seguimiento(es_traslado)
  WHERE es_traslado = TRUE;

-- CHECK constraint para asegurar dirección válida
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.table_constraints
    WHERE constraint_name = 'chk_produccion_seguimiento_traslado_direccion'
  ) THEN
    ALTER TABLE produccion_seguimiento
      ADD CONSTRAINT chk_produccion_seguimiento_traslado_direccion
      CHECK (traslado_direccion IS NULL OR traslado_direccion IN ('SALIDA', 'INGRESO'));
  END IF;
END $$;

COMMENT ON COLUMN produccion_seguimiento.traslado_ingreso_hembras IS 'Hembras recibidas por traslado en esta fila (dirección INGRESO).';
COMMENT ON COLUMN produccion_seguimiento.traslado_ingreso_machos  IS 'Machos recibidos por traslado en esta fila (dirección INGRESO).';
COMMENT ON COLUMN produccion_seguimiento.traslado_salida_hembras  IS 'Hembras enviadas por traslado en esta fila (dirección SALIDA).';
COMMENT ON COLUMN produccion_seguimiento.traslado_salida_machos   IS 'Machos enviados por traslado en esta fila (dirección SALIDA).';
COMMENT ON COLUMN produccion_seguimiento.es_traslado IS 'TRUE si la fila contiene datos de traslado.';
COMMENT ON COLUMN produccion_seguimiento.traslado_direccion IS 'SALIDA = aves enviadas; INGRESO = aves recibidas.';
