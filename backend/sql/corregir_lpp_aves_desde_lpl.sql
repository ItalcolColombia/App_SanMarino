-- =============================================================================
-- CORRECCIÓN: Ajustar aves en LPP para que coincidan con el cierre de levante (LPL)
--
-- Cuando la validación muestra REVISAR porque lpp_aves_h_inicial / lpp_aves_m_inicial
-- no coinciden con aves_h_cierre_levante / aves_m_cierre_levante, este script
-- actualiza los LPP (LPP-H y LPP-M) con las aves del cierre del LPL.
--
-- Así se cumple: "aves con que se abrió producción = aves disponibles al final
-- del lote de levante".
--
-- Afecta a lotes 13 y 14 (o los que indiques en _lotes_validar).
-- =============================================================================

CREATE TEMP TABLE IF NOT EXISTS _lotes_validar (lote_id INT PRIMARY KEY);
DELETE FROM _lotes_validar;
INSERT INTO _lotes_validar (lote_id) VALUES (13), (14);

-- Actualizar LPP-H: aves hembras = cierre levante; machos = 0
UPDATE public.lote_postura_produccion lpp
SET
  aves_h_inicial         = lpl.aves_h_actual,
  aves_m_inicial         = 0,
  aves_h_actual          = lpl.aves_h_actual,
  aves_m_actual          = 0,
  hembras_iniciales_prod = lpl.aves_h_actual,
  machos_iniciales_prod  = 0,
  updated_at              = (NOW() AT TIME ZONE 'utc')
FROM public.lote_postura_levante lpl
WHERE lpp.lote_postura_levante_id = lpl.lote_postura_levante_id
  AND lpp.deleted_at IS NULL
  AND lpl.deleted_at IS NULL
  AND lpl.lote_id IN (SELECT lote_id FROM _lotes_validar)
  AND lpp.lote_nombre LIKE '%-H';

-- Actualizar LPP-M: aves machos = cierre levante; hembras = 0
UPDATE public.lote_postura_produccion lpp
SET
  aves_h_inicial         = 0,
  aves_m_inicial         = lpl.aves_m_actual,
  aves_h_actual          = 0,
  aves_m_actual          = lpl.aves_m_actual,
  hembras_iniciales_prod = 0,
  machos_iniciales_prod  = lpl.aves_m_actual,
  updated_at              = (NOW() AT TIME ZONE 'utc')
FROM public.lote_postura_levante lpl
WHERE lpp.lote_postura_levante_id = lpl.lote_postura_levante_id
  AND lpp.deleted_at IS NULL
  AND lpl.deleted_at IS NULL
  AND lpl.lote_id IN (SELECT lote_id FROM _lotes_validar)
  AND lpp.lote_nombre LIKE '%-M';

-- Resumen: comprobar que quedó alineado
SELECT 'Después de corrección: LPP vs cierre LPL' AS mensaje;
SELECT
  lpl.lote_id,
  lpl.lote_nombre       AS nombre_levante,
  lpl.aves_h_actual     AS aves_h_cierre_levante,
  lpl.aves_m_actual     AS aves_m_cierre_levante,
  lpp_h.lote_nombre     AS lpp_hembras,
  lpp_h.aves_h_inicial  AS lpp_h_aves_inicial,
  lpp_h.aves_h_actual   AS lpp_h_aves_actual,
  lpp_m.lote_nombre     AS lpp_machos,
  lpp_m.aves_m_inicial  AS lpp_m_aves_inicial,
  lpp_m.aves_m_actual   AS lpp_m_aves_actual,
  CASE
    WHEN lpl.aves_h_actual = lpp_h.aves_h_inicial AND lpl.aves_m_actual = lpp_m.aves_m_inicial
    THEN 'OK'
    ELSE 'REVISAR'
  END AS resultado
FROM public.lote_postura_levante lpl
LEFT JOIN public.lote_postura_produccion lpp_h
  ON lpp_h.lote_postura_levante_id = lpl.lote_postura_levante_id
 AND lpp_h.deleted_at IS NULL AND lpp_h.lote_nombre LIKE '%-H'
LEFT JOIN public.lote_postura_produccion lpp_m
  ON lpp_m.lote_postura_levante_id = lpl.lote_postura_levante_id
 AND lpp_m.deleted_at IS NULL AND lpp_m.lote_nombre LIKE '%-M'
WHERE lpl.deleted_at IS NULL
  AND lpl.lote_id IN (SELECT lote_id FROM _lotes_validar)
ORDER BY lpl.lote_id;
