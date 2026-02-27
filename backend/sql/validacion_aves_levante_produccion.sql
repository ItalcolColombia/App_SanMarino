-- =============================================================================
-- VALIDACIÓN: Aves levante → producción y aves disponibles según seguimiento diario
--
-- Comprueba que:
--   1. Aves al cierre de LEVANTE = aves con que se abrió - descuentos (seguimiento_diario levante).
--   2. Aves con que se abrió PRODUCCIÓN (LPP) = aves disponibles al cierre de levante (LPL).
--   3. Aves actuales según seguimiento diario (inicial - descuentos) vs almacenadas.
--
-- Ejecutar tras las migraciones de levante y producción (lotes 13, 14 u otros).
-- =============================================================================

-- Lotes a validar (ajustar si migraste otros)
CREATE TEMP TABLE IF NOT EXISTS _lotes_validar (lote_id INT PRIMARY KEY);
DELETE FROM _lotes_validar;
INSERT INTO _lotes_validar (lote_id) VALUES (13), (14);

-- 1. VALIDACIÓN LEVANTE: aves actuales en LPL vs recalculadas desde seguimiento_diario
WITH aves_levante_calc AS (
  SELECT
    lpl.lote_postura_levante_id,
    lpl.lote_id,
    lpl.lote_nombre,
    lpl.aves_h_inicial,
    lpl.aves_m_inicial,
    lpl.aves_h_actual   AS aves_h_actual_lpl,
    lpl.aves_m_actual   AS aves_m_actual_lpl,
    (COALESCE(lpl.aves_h_inicial, lpl.hembras_l, 0)
     - COALESCE(SUM(sd.mortalidad_hembras), 0)
     - COALESCE(SUM(sd.sel_h), 0)
     - COALESCE(SUM(sd.error_sexaje_hembras), 0)) AS aves_h_calculado,
    (COALESCE(lpl.aves_m_inicial, lpl.machos_l, 0)
     - COALESCE(SUM(sd.mortalidad_machos), 0)
     - COALESCE(SUM(sd.sel_m), 0)
     - COALESCE(SUM(sd.error_sexaje_machos), 0)) AS aves_m_calculado
  FROM public.lote_postura_levante lpl
  LEFT JOIN public.seguimiento_diario sd
    ON sd.tipo_seguimiento = 'levante'
   AND sd.lote_postura_levante_id = lpl.lote_postura_levante_id
  WHERE lpl.deleted_at IS NULL
    AND lpl.lote_id IN (SELECT lote_id FROM _lotes_validar)
  GROUP BY
    lpl.lote_postura_levante_id, lpl.lote_id, lpl.lote_nombre,
    lpl.aves_h_inicial, lpl.aves_m_inicial, lpl.hembras_l, lpl.machos_l,
    lpl.aves_h_actual, lpl.aves_m_actual
)
SELECT
  '1. LEVANTE: LPL actual vs calculado desde seguimiento_diario' AS validacion,
  lote_id,
  lote_nombre,
  aves_h_inicial,
  aves_m_inicial,
  aves_h_actual_lpl     AS aves_h_en_lpl,
  aves_m_actual_lpl     AS aves_m_en_lpl,
  GREATEST(0, aves_h_calculado) AS aves_h_segun_seguimiento,
  GREATEST(0, aves_m_calculado) AS aves_m_segun_seguimiento,
  CASE
    WHEN GREATEST(0, aves_h_calculado) = COALESCE(aves_h_actual_lpl, 0)
     AND GREATEST(0, aves_m_calculado) = COALESCE(aves_m_actual_lpl, 0)
    THEN 'OK'
    ELSE 'REVISAR (diferencia)'
  END AS resultado
FROM aves_levante_calc
ORDER BY lote_id;

-- 2. VALIDACIÓN: Aves con que se abrió producción (LPP) = Aves disponibles al cierre de levante (LPL)
SELECT '2. PRODUCCIÓN: Aves inicio LPP vs aves cierre levante (LPL)' AS validacion;
SELECT
  lpl.lote_id,
  lpl.lote_nombre       AS nombre_levante,
  lpl.aves_h_actual     AS aves_h_cierre_levante,
  lpl.aves_m_actual     AS aves_m_cierre_levante,
  lpp_h.lote_nombre     AS lpp_hembras,
  lpp_h.aves_h_inicial  AS lpp_h_aves_inicial,
  lpp_m.lote_nombre     AS lpp_machos,
  lpp_m.aves_m_inicial  AS lpp_m_aves_inicial,
  CASE
    WHEN lpl.aves_h_actual = lpp_h.aves_h_inicial AND lpl.aves_m_actual = lpp_m.aves_m_inicial
    THEN 'OK'
    ELSE 'REVISAR (no coinciden)'
  END AS resultado
FROM public.lote_postura_levante lpl
LEFT JOIN public.lote_postura_produccion lpp_h
  ON lpp_h.lote_postura_levante_id = lpl.lote_postura_levante_id
 AND lpp_h.deleted_at IS NULL
 AND lpp_h.lote_nombre LIKE '%-H'
LEFT JOIN public.lote_postura_produccion lpp_m
  ON lpp_m.lote_postura_levante_id = lpl.lote_postura_levante_id
 AND lpp_m.deleted_at IS NULL
 AND lpp_m.lote_nombre LIKE '%-M'
WHERE lpl.deleted_at IS NULL
  AND lpl.lote_id IN (SELECT lote_id FROM _lotes_validar)
ORDER BY lpl.lote_id;

-- 3. AVES DISPONIBLES ACTUALES según seguimiento diario (LEVANTE): resumen por lote
SELECT '3. LEVANTE: Resumen aves con que se abrió y aves disponibles (según seguimiento)' AS validacion;
SELECT
  lpl.lote_id,
  lpl.lote_nombre,
  COALESCE(lpl.aves_h_inicial, lpl.hembras_l, 0)     AS aves_hembras_abrio,
  COALESCE(lpl.aves_m_inicial, lpl.machos_l, 0)     AS aves_machos_abrio,
  COALESCE(SUM(sd.mortalidad_hembras), 0)            AS total_mortalidad_h,
  COALESCE(SUM(sd.sel_h), 0)                         AS total_sel_h,
  COALESCE(SUM(sd.error_sexaje_hembras), 0)         AS total_error_sexaje_h,
  COALESCE(SUM(sd.mortalidad_machos), 0)            AS total_mortalidad_m,
  COALESCE(SUM(sd.sel_m), 0)                         AS total_sel_m,
  COALESCE(SUM(sd.error_sexaje_machos), 0)          AS total_error_sexaje_m,
  GREATEST(0,
    COALESCE(lpl.aves_h_inicial, lpl.hembras_l, 0)
    - COALESCE(SUM(sd.mortalidad_hembras), 0) - COALESCE(SUM(sd.sel_h), 0) - COALESCE(SUM(sd.error_sexaje_hembras), 0)
  ) AS aves_hembras_disponibles,
  GREATEST(0,
    COALESCE(lpl.aves_m_inicial, lpl.machos_l, 0)
    - COALESCE(SUM(sd.mortalidad_machos), 0) - COALESCE(SUM(sd.sel_m), 0) - COALESCE(SUM(sd.error_sexaje_machos), 0)
  ) AS aves_machos_disponibles,
  lpl.aves_h_actual AS aves_h_actual_en_lpl,
  lpl.aves_m_actual AS aves_m_actual_en_lpl
FROM public.lote_postura_levante lpl
LEFT JOIN public.seguimiento_diario sd
  ON sd.tipo_seguimiento = 'levante' AND sd.lote_postura_levante_id = lpl.lote_postura_levante_id
WHERE lpl.deleted_at IS NULL
  AND lpl.lote_id IN (SELECT lote_id FROM _lotes_validar)
GROUP BY lpl.lote_postura_levante_id, lpl.lote_id, lpl.lote_nombre,
         lpl.aves_h_inicial, lpl.aves_m_inicial, lpl.hembras_l, lpl.machos_l,
         lpl.aves_h_actual, lpl.aves_m_actual
ORDER BY lpl.lote_id;

-- 4. PRODUCCIÓN: Aves con que se abrió cada LPP y aves actuales según seguimiento_diario (producción)
SELECT '4. PRODUCCIÓN: Aves inicio y disponibles según seguimiento diario (por LPP)' AS validacion;
SELECT
  lpp.lote_postura_produccion_id,
  lpp.lote_nombre,
  lpp.aves_h_inicial,
  lpp.aves_m_inicial,
  lpp.aves_h_actual     AS aves_h_actual_en_lpp,
  lpp.aves_m_actual     AS aves_m_actual_en_lpp,
  COALESCE(SUM(sd.mortalidad_hembras), 0) + COALESCE(SUM(sd.sel_h), 0) + COALESCE(SUM(sd.error_sexaje_hembras), 0) AS descuentos_h,
  COALESCE(SUM(sd.mortalidad_machos), 0) + COALESCE(SUM(sd.sel_m), 0) + COALESCE(SUM(sd.error_sexaje_machos), 0) AS descuentos_m,
  GREATEST(0,
    COALESCE(lpp.aves_h_inicial, 0)
    - COALESCE(SUM(sd.mortalidad_hembras), 0) - COALESCE(SUM(sd.sel_h), 0) - COALESCE(SUM(sd.error_sexaje_hembras), 0)
  ) AS aves_h_segun_seguimiento,
  GREATEST(0,
    COALESCE(lpp.aves_m_inicial, 0)
    - COALESCE(SUM(sd.mortalidad_machos), 0) - COALESCE(SUM(sd.sel_m), 0) - COALESCE(SUM(sd.error_sexaje_machos), 0)
  ) AS aves_m_segun_seguimiento
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

-- 5. Resumen OK / REVISAR por lote (levante)
SELECT '5. RESUMEN: ¿Flujo correcto por lote? (levante: LPL actual = calculado; producción: inicio LPP = cierre LPL)' AS validacion;
WITH calc_levante AS (
  SELECT
    lpl.lote_id,
    lpl.aves_h_actual AS lpl_h,
    lpl.aves_m_actual AS lpl_m,
    GREATEST(0,
      COALESCE(lpl.aves_h_inicial, lpl.hembras_l, 0)
      - (SELECT COALESCE(SUM(sd.mortalidad_hembras), 0) + COALESCE(SUM(sd.sel_h), 0) + COALESCE(SUM(sd.error_sexaje_hembras), 0)
         FROM public.seguimiento_diario sd
         WHERE sd.tipo_seguimiento = 'levante' AND sd.lote_postura_levante_id = lpl.lote_postura_levante_id)
    ) AS calc_h,
    GREATEST(0,
      COALESCE(lpl.aves_m_inicial, lpl.machos_l, 0)
      - (SELECT COALESCE(SUM(sd.mortalidad_machos), 0) + COALESCE(SUM(sd.sel_m), 0) + COALESCE(SUM(sd.error_sexaje_machos), 0)
         FROM public.seguimiento_diario sd
         WHERE sd.tipo_seguimiento = 'levante' AND sd.lote_postura_levante_id = lpl.lote_postura_levante_id)
    ) AS calc_m
  FROM public.lote_postura_levante lpl
  WHERE lpl.deleted_at IS NULL AND lpl.lote_id IN (SELECT lote_id FROM _lotes_validar)
),
lpp_ini AS (
  SELECT
    lpl.lote_id,
    MAX(CASE WHEN lpp.lote_nombre LIKE '%-H' THEN lpp.aves_h_inicial END) AS lpp_h_ini,
    MAX(CASE WHEN lpp.lote_nombre LIKE '%-M' THEN lpp.aves_m_inicial END) AS lpp_m_ini
  FROM public.lote_postura_levante lpl
  JOIN public.lote_postura_produccion lpp ON lpp.lote_postura_levante_id = lpl.lote_postura_levante_id AND lpp.deleted_at IS NULL
  WHERE lpl.deleted_at IS NULL AND lpl.lote_id IN (SELECT lote_id FROM _lotes_validar)
  GROUP BY lpl.lote_id
)
SELECT
  c.lote_id,
  CASE
    WHEN c.lpl_h = c.calc_h AND c.lpl_m = c.calc_m
     AND (p.lpp_h_ini IS NULL OR p.lpp_h_ini = c.lpl_h)
     AND (p.lpp_m_ini IS NULL OR p.lpp_m_ini = c.lpl_m)
    THEN 'OK'
    ELSE 'REVISAR'
  END AS flujo_aves_correcto,
  c.lpl_h AS aves_h_cierre_levante,
  c.calc_h AS aves_h_calculado_levante,
  c.lpl_m AS aves_m_cierre_levante,
  c.calc_m AS aves_m_calculado_levante,
  p.lpp_h_ini AS lpp_aves_h_inicial,
  p.lpp_m_ini AS lpp_aves_m_inicial
FROM calc_levante c
LEFT JOIN lpp_ini p ON p.lote_id = c.lote_id
ORDER BY c.lote_id;

-- 6. PRODUCCIÓN: ¿Aves actuales en LPP alineadas con seguimiento_diario (mortalidad + descarte + error sexaje)?
SELECT '6. PRODUCCIÓN: Aves actuales (LPP) vs calculadas desde seguimiento_diario' AS validacion;
SELECT
  lpp.lote_postura_produccion_id,
  lpp.lote_nombre,
  lpp.aves_h_inicial,
  lpp.aves_m_inicial,
  lpp.aves_h_actual     AS aves_h_actual_en_lpp,
  lpp.aves_m_actual     AS aves_m_actual_en_lpp,
  COALESCE(SUM(sd.mortalidad_hembras), 0) + COALESCE(SUM(sd.sel_h), 0) + COALESCE(SUM(sd.error_sexaje_hembras), 0) AS descuentos_h,
  COALESCE(SUM(sd.mortalidad_machos), 0) + COALESCE(SUM(sd.sel_m), 0) + COALESCE(SUM(sd.error_sexaje_machos), 0) AS descuentos_m,
  GREATEST(0,
    COALESCE(lpp.aves_h_inicial, 0)
    - COALESCE(SUM(sd.mortalidad_hembras), 0) - COALESCE(SUM(sd.sel_h), 0) - COALESCE(SUM(sd.error_sexaje_hembras), 0)
  ) AS aves_h_segun_seguimiento,
  GREATEST(0,
    COALESCE(lpp.aves_m_inicial, 0)
    - COALESCE(SUM(sd.mortalidad_machos), 0) - COALESCE(SUM(sd.sel_m), 0) - COALESCE(SUM(sd.error_sexaje_machos), 0)
  ) AS aves_m_segun_seguimiento,
  CASE
    WHEN lpp.aves_h_actual = GREATEST(0,
          COALESCE(lpp.aves_h_inicial, 0)
          - COALESCE(SUM(sd.mortalidad_hembras), 0) - COALESCE(SUM(sd.sel_h), 0) - COALESCE(SUM(sd.error_sexaje_hembras), 0))
     AND lpp.aves_m_actual = GREATEST(0,
          COALESCE(lpp.aves_m_inicial, 0)
          - COALESCE(SUM(sd.mortalidad_machos), 0) - COALESCE(SUM(sd.sel_m), 0) - COALESCE(SUM(sd.error_sexaje_machos), 0))
    THEN 'OK'
    ELSE 'REVISAR (ejecutar actualizar_lpp_aves_actuales_desde_seguimiento.sql)'
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
