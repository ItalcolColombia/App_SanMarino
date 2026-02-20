-- Copia Raza y AnoTablaGenetica del lote padre al lote hijo en producción
-- cuando el hijo los tiene NULL. Así la guía genética en indicadores tendrá datos.
-- Ejecutar una vez si ya existen lotes en fase Producción creados sin estos campos.

UPDATE lotes h
SET
  raza = p.raza,
  ano_tabla_genetica = p.ano_tabla_genetica,
  updated_at = NOW() AT TIME ZONE 'utc'
FROM lotes p
WHERE h.lote_padre_id = p.lote_id
  AND h.fase = 'Produccion'
  AND h.deleted_at IS NULL
  AND p.deleted_at IS NULL
  AND (h.raza IS NULL OR h.ano_tabla_genetica IS NULL)
  AND (p.raza IS NOT NULL OR p.ano_tabla_genetica IS NOT NULL);
