-- =====================================================================
-- 046 — Acumulados de traslado en lote_postura_levante
-- Feature 13: Traslado de Aves Mejorado (Levante)
-- Fecha: 2026-05-24
--
-- Agrega 4 columnas para sumar todos los traslados que ingresan o salen
-- del lote a lo largo de su vida. Se calculan en
-- TrasladoAvesDesdeSegService.EjecutarTrasladoDesdeSegAsync y se decrementan
-- en SeguimientoLoteLevanteService.DeleteAsync al revertir un traslado.
-- =====================================================================

ALTER TABLE lote_postura_levante
  ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_ingreso_machos  INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_salida_hembras  INTEGER NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS traslado_salida_machos   INTEGER NOT NULL DEFAULT 0;

COMMENT ON COLUMN lote_postura_levante.traslado_ingreso_hembras IS 'Aves hembras recibidas vía traslados (acumulado)';
COMMENT ON COLUMN lote_postura_levante.traslado_ingreso_machos  IS 'Aves machos recibidos vía traslados (acumulado)';
COMMENT ON COLUMN lote_postura_levante.traslado_salida_hembras  IS 'Aves hembras enviadas vía traslados (acumulado)';
COMMENT ON COLUMN lote_postura_levante.traslado_salida_machos   IS 'Aves machos enviados vía traslados (acumulado)';
