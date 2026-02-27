-- =============================================================================
-- ACTUALIZAR aves actuales en LPP desde seguimiento_diario (producción)
--
-- Recalcula aves_h_actual y aves_m_actual de cada LPP con la fórmula:
--   aves_h_actual = aves_h_inicial - SUM(mortalidad_hembras + sel_h + error_sexaje_hembras)
--   aves_m_actual = aves_m_inicial - SUM(mortalidad_machos + sel_m + error_sexaje_machos)
-- usando todos los registros de seguimiento_diario (tipo=produccion) del LPP.
--
-- Así las aves actuales quedan alineadas con cada registro de seguimiento
-- (mortalidad, descarte/selección, error de sexaje).
--
-- Opción: actualizar solo lotes 13 y 14; para TODOS los LPP, comenta el bloque
-- "AND lpp2.lote_postura_levante_id IN (...)" dentro del SELECT del sub.
-- =============================================================================

CREATE TEMP TABLE IF NOT EXISTS _lotes_validar (lote_id INT PRIMARY KEY);
DELETE FROM _lotes_validar;
INSERT INTO _lotes_validar (lote_id) VALUES (13), (14);

-- Actualizar aves_h_actual y aves_m_actual en cada LPP desde seguimiento_diario
-- (mortalidad + descarte/sel + error_sexaje por registro)
UPDATE public.lote_postura_produccion lpp
SET
  aves_h_actual = GREATEST(0, sub.aves_h_calc),
  aves_m_actual = GREATEST(0, sub.aves_m_calc),
  updated_at    = (NOW() AT TIME ZONE 'utc')
FROM (
  SELECT
    lpp2.lote_postura_produccion_id,
    (COALESCE(lpp2.aves_h_inicial, 0)
     - COALESCE(SUM(sd.mortalidad_hembras), 0)
     - COALESCE(SUM(sd.sel_h), 0)
     - COALESCE(SUM(sd.error_sexaje_hembras), 0)) AS aves_h_calc,
    (COALESCE(lpp2.aves_m_inicial, 0)
     - COALESCE(SUM(sd.mortalidad_machos), 0)
     - COALESCE(SUM(sd.sel_m), 0)
     - COALESCE(SUM(sd.error_sexaje_machos), 0)) AS aves_m_calc
  FROM public.lote_postura_produccion lpp2
  LEFT JOIN public.seguimiento_diario sd
    ON sd.tipo_seguimiento = 'produccion'
   AND sd.lote_postura_produccion_id = lpp2.lote_postura_produccion_id
  WHERE lpp2.deleted_at IS NULL
    -- Solo LPP de lotes en _lotes_validar; para todos los LPP, comenta las 4 líneas siguientes:
    AND lpp2.lote_postura_levante_id IN (
      SELECT lote_postura_levante_id FROM public.lote_postura_levante
      WHERE deleted_at IS NULL AND lote_id IN (SELECT lote_id FROM _lotes_validar)
    )
  GROUP BY lpp2.lote_postura_produccion_id,
           lpp2.aves_h_inicial, lpp2.aves_m_inicial
) sub
WHERE lpp.lote_postura_produccion_id = sub.lote_postura_produccion_id;

-- Resumen: aves actuales ya alineadas con seguimiento (mortalidad + descarte + error sexaje)
SELECT 'Producción: aves actuales alineadas con seguimiento_diario' AS mensaje;
SELECT
  lpp.lote_postura_produccion_id,
  lpp.lote_nombre,
  lpp.aves_h_inicial,
  lpp.aves_m_inicial,
  lpp.aves_h_actual,
  lpp.aves_m_actual,
  COALESCE(SUM(sd.mortalidad_hembras), 0) + COALESCE(SUM(sd.sel_h), 0) + COALESCE(SUM(sd.error_sexaje_hembras), 0) AS total_descuentos_h,
  COALESCE(SUM(sd.mortalidad_machos), 0) + COALESCE(SUM(sd.sel_m), 0) + COALESCE(SUM(sd.error_sexaje_machos), 0) AS total_descuentos_m,
  CASE
    WHEN lpp.aves_h_actual = GREATEST(0, COALESCE(lpp.aves_h_inicial, 0)
      - COALESCE(SUM(sd.mortalidad_hembras), 0) - COALESCE(SUM(sd.sel_h), 0) - COALESCE(SUM(sd.error_sexaje_hembras), 0))
     AND lpp.aves_m_actual = GREATEST(0, COALESCE(lpp.aves_m_inicial, 0)
      - COALESCE(SUM(sd.mortalidad_machos), 0) - COALESCE(SUM(sd.sel_m), 0) - COALESCE(SUM(sd.error_sexaje_machos), 0))
    THEN 'OK'
    ELSE 'REVISAR'
  END AS aves_actuales_alineadas
FROM public.lote_postura_produccion lpp
LEFT JOIN public.seguimiento_diario sd
  ON sd.tipo_seguimiento = 'produccion' AND sd.lote_postura_produccion_id = lpp.lote_postura_produccion_id
WHERE lpp.deleted_at IS NULL
  AND lpp.lote_postura_levante_id IN (
    SELECT lote_postura_levante_id FROM public.lote_postura_levante
    WHERE deleted_at IS NULL AND lote_id IN (SELECT lote_id FROM _lotes_validar)
  )
GROUP BY lpp.lote_postura_produccion_id, lpp.lote_nombre,
         lpp.aves_h_inicial, lpp.aves_m_inicial, lpp.aves_h_actual, lpp.aves_m_actual
ORDER BY lpp.lote_postura_levante_id, lpp.lote_nombre;
