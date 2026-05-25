-- =====================================================================
-- 047 — Bandera y referencia de traslado en seguimiento_diario_levante_reproductoras
-- Feature 13: Traslado de Aves Mejorado (Levante)
-- Fecha: 2026-05-24
--
-- Nota: la entidad SeguimientoDiario está mapeada a
-- public.seguimiento_diario_levante_reproductoras (tabla unificada
-- usada por SeguimientoDiarioConfiguration).
--
-- Permite que un registro sea identificado como traslado (entrada o
-- salida) y mantenga la referencia al lote contraparte para revertir
-- la operación al eliminarlo.
-- =====================================================================

ALTER TABLE seguimiento_diario_levante_reproductoras
  ADD COLUMN IF NOT EXISTS es_traslado                    BOOLEAN     NOT NULL DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS traslado_lote_contraparte_id   INTEGER     NULL,
  ADD COLUMN IF NOT EXISTS traslado_granja_contraparte_id INTEGER     NULL,
  ADD COLUMN IF NOT EXISTS traslado_direccion             VARCHAR(10) NULL;

-- Índice parcial: solo registros de traslado (los más relevantes para revertir)
CREATE INDEX IF NOT EXISTS idx_seguimiento_diario_lev_es_traslado
  ON seguimiento_diario_levante_reproductoras(es_traslado)
  WHERE es_traslado = TRUE;

-- Constraint para asegurar que el valor de dirección sea válido
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.table_constraints
    WHERE constraint_name = 'chk_seg_diario_lev_traslado_direccion'
  ) THEN
    ALTER TABLE seguimiento_diario_levante_reproductoras
      ADD CONSTRAINT chk_seg_diario_lev_traslado_direccion
      CHECK (traslado_direccion IS NULL OR traslado_direccion IN ('SALIDA', 'INGRESO'));
  END IF;
END $$;

COMMENT ON COLUMN seguimiento_diario_levante_reproductoras.es_traslado IS 'TRUE si este registro fue creado por un traslado de aves (no por seguimiento manual).';
COMMENT ON COLUMN seguimiento_diario_levante_reproductoras.traslado_lote_contraparte_id IS 'lote_postura_levante_id (o produccion_id) del lote contraparte del traslado.';
COMMENT ON COLUMN seguimiento_diario_levante_reproductoras.traslado_granja_contraparte_id IS 'granja_id del lote contraparte (para auditoría rápida).';
COMMENT ON COLUMN seguimiento_diario_levante_reproductoras.traslado_direccion IS 'SALIDA = se enviaron aves desde este lote; INGRESO = se recibieron aves en este lote.';
