-- =============================================================================
-- MIGRACIÓN 00: Cálculo de aves actuales para lote 13 (SOLO CONSULTAS - no modifica datos)
-- Ejecutar ANTES de la migración para validar:
--   1. Datos del lote (aves con que se abrió: hembras_l, machos_l)
--   2. Sumas de mortalidad, selección y error sexaje desde seguimiento_lote_levante
--   3. Aves actuales al cierre (semana 25): aves_h_actual, aves_m_actual
-- =============================================================================

-- 1) Datos del lote 13 (origen)
SELECT '1. Lote 13 (tabla lotes)' AS paso;
SELECT lote_id, lote_nombre, granja_id, nucleo_id, galpon_id,
       fecha_encaset, hembras_l AS aves_hembras_inicial, machos_l AS aves_machos_inicial,
       fase, edad_inicial, company_id
FROM lotes
WHERE lote_id = 13;

-- 2) Aves iniciales (desde lote, mismo resultado que hembras_l/machos_l)
SELECT '2. Aves con que se abrió el lote (desde lote)' AS paso;
SELECT l.hembras_l AS cantidad_hembras_inicial,
       l.machos_l  AS cantidad_machos_inicial
FROM lotes l
WHERE l.lote_id = 13;

-- 3) Registros de seguimiento levante (tabla anterior)
SELECT '3. Registros en seguimiento_lote_levante para lote 13' AS paso;
SELECT sl.id, sl.lote_id, sl.fecha_registro,
       sl.mortalidad_hembras, sl.mortalidad_machos,
       sl.sel_h, sl.sel_m,
       sl.error_sexaje_hembras, sl.error_sexaje_machos,
       sl.consumo_kg_hembras, sl.tipo_alimento
FROM seguimiento_lote_levante sl
WHERE sl.lote_id = 13
ORDER BY sl.fecha_registro;

-- 4) Sumas de descuentos (mortalidad + selección + error sexaje) para cálculo de aves actuales
SELECT '4. Sumas de descuentos desde seguimiento_lote_levante (lote 13)' AS paso;
SELECT
  COALESCE(SUM(sl.mortalidad_hembras), 0)   AS total_mortalidad_hembras,
  COALESCE(SUM(sl.mortalidad_machos), 0)    AS total_mortalidad_machos,
  COALESCE(SUM(sl.sel_h), 0)                AS total_sel_h,
  COALESCE(SUM(sl.sel_m), 0)                AS total_sel_m,
  COALESCE(SUM(sl.error_sexaje_hembras), 0) AS total_error_sexaje_hembras,
  COALESCE(SUM(sl.error_sexaje_machos), 0)  AS total_error_sexaje_machos
FROM seguimiento_lote_levante sl
WHERE sl.lote_id = 13;

-- 5) Cálculo de aves actuales al cierre (semana 25) – fórmula que se usará en LPL
--    aves_h_actual = hembras_l - SUM(mortalidad_hembras) - SUM(sel_h) - SUM(error_sexaje_hembras)
--    aves_m_actual = machos_l   - SUM(mortalidad_machos)  - SUM(sel_m)  - SUM(error_sexaje_machos)
SELECT '5. Aves actuales al cierre (para actualizar LPL)' AS paso;
SELECT
  l.hembras_l AS aves_hembras_inicial,
  l.machos_l  AS aves_machos_inicial,
  (COALESCE(l.hembras_l, 0)
   - COALESCE(SUM(sl.mortalidad_hembras), 0)
   - COALESCE(SUM(sl.sel_h), 0)
   - COALESCE(SUM(sl.error_sexaje_hembras), 0)) AS aves_h_calculado,
  (COALESCE(l.machos_l, 0)
   - COALESCE(SUM(sl.mortalidad_machos), 0)
   - COALESCE(SUM(sl.sel_m), 0)
   - COALESCE(SUM(sl.error_sexaje_machos), 0))  AS aves_m_calculado,
  GREATEST(0,
    COALESCE(l.hembras_l, 0)
    - COALESCE(SUM(sl.mortalidad_hembras), 0)
    - COALESCE(SUM(sl.sel_h), 0)
    - COALESCE(SUM(sl.error_sexaje_hembras), 0)) AS aves_h_actual,
  GREATEST(0,
    COALESCE(l.machos_l, 0)
    - COALESCE(SUM(sl.mortalidad_machos), 0)
    - COALESCE(SUM(sl.sel_m), 0)
    - COALESCE(SUM(sl.error_sexaje_machos), 0))  AS aves_m_actual
FROM lotes l
LEFT JOIN seguimiento_lote_levante sl ON sl.lote_id = l.lote_id
WHERE l.lote_id = 13
GROUP BY l.lote_id, l.hembras_l, l.machos_l;

-- 6) Semana aproximada del último registro (referencia para cierre en semana 25)
SELECT '6. Último registro y semana aproximada (referencia)' AS paso;
SELECT sl.fecha_registro,
       l.fecha_encaset,
       CASE
         WHEN l.fecha_encaset IS NOT NULL THEN
           GREATEST(0, ((sl.fecha_registro::date - (l.fecha_encaset AT TIME ZONE 'utc')::date) / 7))
         ELSE NULL
       END AS semana_aproximada
FROM seguimiento_lote_levante sl
JOIN lotes l ON l.lote_id = sl.lote_id
WHERE sl.lote_id = 13
ORDER BY sl.fecha_registro DESC
LIMIT 1;
