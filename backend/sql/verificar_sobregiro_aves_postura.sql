-- ============================================================================
-- verificar_sobregiro_aves_postura.sql — DETECTOR, solo lectura.
--
-- Responde: ¿hay filas de seguimiento diario que carguen MÁS bajas que aves
-- disponibles? Hoy nada lo impide: el soft-check REQ-011b de
-- SeguimientoLoteLevanteService solo hace LogWarning, va envuelto en try/catch
-- y compara `saldo == 0` EXACTO — o sea que salta en el cierre legítimo y NO
-- salta en el sobregiro real. El maestro (aves_h_actual) tampoco delata nada
-- porque escribe con GREATEST(0, …) y esconde el negativo.
--
-- Correr ANTES de convertir esa validación en bloqueo duro: dice exactamente
-- cuántas escrituras históricas quedarían rechazadas, en qué empresas.
--
-- Aritmética (NO inventada acá):
--   * Base por sexo LEVANTE  = misma cadena de fallback que
--     fn_indicadores_levante_postura (hembras_l -> traslado_ingreso de filas
--     puro-traslado > sem 25 -> 0), y mismo predicado de exclusión de filas.
--   * Bajas LEVANTE = SaldoAvesLevanteCalculos.BajasNetas
--     (mort + sel + err + traslado_salida + venta - traslado_ingreso).
--   * PRODUCCIÓN se apoya en la fn diaria canónica
--     fn_seguimiento_diario_produccion; su saldo se contrasta contra el
--     saldo_aves_h/m que la propia fn expone (deben coincidir).
--   * SIN clamp a 0: el clamp es justo lo que oculta el sobregiro.
--
-- ⚠️ Al interpretar: margen = 0 es LEGÍTIMO (lote agotado / cerrado). La regla
--    correcta es «bajas <= disponibles», NO «saldo > 0»: exigir > 0 rompería
--    el cierre normal de todos los lotes que terminan en cero.
--
-- Uso:  psql ... -f backend/sql/verificar_sobregiro_aves_postura.sql
-- ============================================================================

DROP TABLE IF EXISTS _barrido_lev;
CREATE TEMP TABLE _barrido_lev AS
WITH lote AS (
    SELECT l.lote_id, l.company_id, l.lote_nombre, l.granja_id,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date AS enc_date,
           COALESCE(l.hembras_l, 0) AS hl, COALESCE(l.machos_l, 0) AS ml
      FROM lotes l
     WHERE l.deleted_at IS NULL
),
-- filas de PURO traslado > sem 25: la fn las descarta de la serie y las usa como base
ing_fb AS (
    SELECT lo.lote_id,
           COALESCE(SUM(COALESCE(sl.traslado_ingreso_hembras,0)),0) AS ing_h,
           COALESCE(SUM(COALESCE(sl.traslado_ingreso_machos ,0)),0) AS ing_m
      FROM lote lo
      JOIN seguimiento_diario_levante sl
        ON sl.tipo_seguimiento = 'levante' AND sl.lote_id = lo.lote_id::text
     WHERE lo.enc_date IS NOT NULL
       AND (floor((((sl.fecha AT TIME ZONE 'America/Bogota')::date - lo.enc_date) / 7.0))::int) + 1 > 25
       AND COALESCE(sl.mortalidad_hembras,0)=0 AND COALESCE(sl.mortalidad_machos,0)=0
       AND COALESCE(sl.sel_h,0)=0 AND COALESCE(sl.sel_m,0)=0
       AND COALESCE(sl.error_sexaje_hembras,0)=0 AND COALESCE(sl.error_sexaje_machos,0)=0
       AND COALESCE(sl.consumo_kg_hembras,0)=0 AND COALESCE(sl.consumo_kg_machos,0)=0
       AND COALESCE(sl.peso_prom_hembras,0)=0 AND COALESCE(sl.peso_prom_machos,0)=0
       AND (COALESCE(sl.traslado_salida_hembras,0)+COALESCE(sl.traslado_salida_machos,0)
          + COALESCE(sl.traslado_ingreso_hembras,0)+COALESCE(sl.traslado_ingreso_machos,0)) > 0
     GROUP BY lo.lote_id
),
base AS (
    SELECT lo.*,
           COALESCE(NULLIF(lo.hl,0), NULLIF(f.ing_h,0), 0) AS base_h,
           COALESCE(NULLIF(lo.ml,0), NULLIF(f.ing_m,0), 0) AS base_m
      FROM lote lo LEFT JOIN ing_fb f ON f.lote_id = lo.lote_id
),
dia AS (   -- filas que la fn SÍ procesa (mismo WHERE NOT (...) de exclusión)
    SELECT b.lote_id, b.company_id, b.lote_nombre, b.granja_id, b.base_h, b.base_m,
           sl.id,
           (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date,
           COALESCE(sl.mortalidad_hembras,0) + COALESCE(sl.sel_h,0) + COALESCE(sl.error_sexaje_hembras,0)
             + COALESCE(sl.traslado_salida_hembras,0) + COALESCE(sl.venta_aves_hembras,0)
             - COALESCE(sl.traslado_ingreso_hembras,0) AS bajas_h,
           COALESCE(sl.mortalidad_machos,0) + COALESCE(sl.sel_m,0) + COALESCE(sl.error_sexaje_machos,0)
             + COALESCE(sl.traslado_salida_machos,0) + COALESCE(sl.venta_aves_machos,0)
             - COALESCE(sl.traslado_ingreso_machos,0) AS bajas_m
      FROM base b
      JOIN seguimiento_diario_levante sl
        ON sl.tipo_seguimiento = 'levante' AND sl.lote_id = b.lote_id::text
     WHERE b.enc_date IS NOT NULL
       AND NOT (
              (floor((((sl.fecha AT TIME ZONE 'America/Bogota')::date - b.enc_date) / 7.0))::int) + 1 > 25
          AND COALESCE(sl.mortalidad_hembras,0)=0 AND COALESCE(sl.mortalidad_machos,0)=0
          AND COALESCE(sl.sel_h,0)=0 AND COALESCE(sl.sel_m,0)=0
          AND COALESCE(sl.error_sexaje_hembras,0)=0 AND COALESCE(sl.error_sexaje_machos,0)=0
          AND COALESCE(sl.consumo_kg_hembras,0)=0 AND COALESCE(sl.consumo_kg_machos,0)=0
          AND COALESCE(sl.peso_prom_hembras,0)=0 AND COALESCE(sl.peso_prom_machos,0)=0
          AND (COALESCE(sl.traslado_salida_hembras,0)+COALESCE(sl.traslado_salida_machos,0)
             + COALESCE(sl.traslado_ingreso_hembras,0)+COALESCE(sl.traslado_ingreso_machos,0)) > 0
       )
)
SELECT d.*,
       d.base_h - SUM(d.bajas_h) OVER w AS saldo_h,
       d.base_m - SUM(d.bajas_m) OVER w AS saldo_m,
       d.base_h - (SUM(d.bajas_h) OVER w - d.bajas_h) AS disp_h_antes,
       d.base_m - (SUM(d.bajas_m) OVER w - d.bajas_m) AS disp_m_antes
  FROM dia d
WINDOW w AS (PARTITION BY d.lote_id ORDER BY d.reg_date, d.id
             ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW);

\echo
\echo ============ 1) FILAS QUE UN BLOQUEO DURO HABRIA RECHAZADO (por empresa) ============
SELECT c.id AS empresa_id, c.name AS empresa,
       count(*)                                                    AS filas_totales,
       count(*) FILTER (WHERE saldo_h < 0 OR saldo_m < 0)          AS filas_que_sobregiran,
       count(DISTINCT lote_id)                                     AS lotes_con_datos,
       count(DISTINCT lote_id) FILTER (WHERE saldo_h < 0 OR saldo_m < 0) AS lotes_afectados
  FROM _barrido_lev b JOIN companies c ON c.id = b.company_id
 GROUP BY 1,2 ORDER BY 1;

\echo
\echo ============ 2) LOTES QUE QUEDAN EN FALTA (saldo final negativo) ============
SELECT c.name AS empresa, f.lote_id, f.lote_nombre, f.granja_id,
       f.base_h, f.base_m, f.saldo_h AS saldo_final_h, f.saldo_m AS saldo_final_m
  FROM (SELECT DISTINCT ON (lote_id) * FROM _barrido_lev
         ORDER BY lote_id, reg_date DESC, id DESC) f
  JOIN companies c ON c.id = f.company_id
 WHERE f.saldo_h < 0 OR f.saldo_m < 0
 ORDER BY LEAST(f.saldo_h, f.saldo_m);

\echo
\echo ============ 3) LA PRIMERA FILA QUE SOBREGIRA CADA LOTE (la que el bloqueo rechazaria) ============
SELECT c.name AS empresa, p.lote_id, p.lote_nombre, p.reg_date,
       p.disp_h_antes AS disponibles_h, p.bajas_h AS bajas_h_cargadas,
       p.disp_m_antes AS disponibles_m, p.bajas_m AS bajas_m_cargadas
  FROM (SELECT DISTINCT ON (lote_id) * FROM _barrido_lev
         WHERE saldo_h < 0 OR saldo_m < 0
         ORDER BY lote_id, reg_date, id) p
  JOIN companies c ON c.id = p.company_id
 ORDER BY p.reg_date;

DROP TABLE IF EXISTS _barrido_prod;
CREATE TEMP TABLE _barrido_prod AS
WITH lpp AS (
    SELECT p.lote_postura_produccion_id AS lpp_id, p.company_id, p.lote_id,
           COALESCE(p.aves_h_inicial, p.hembras_iniciales_prod, 0) AS base_h,
           COALESCE(p.aves_m_inicial, p.machos_iniciales_prod, 0) AS base_m
      FROM lote_postura_produccion p
),
dia AS (
    SELECT l.lpp_id, l.company_id, l.lote_id, l.base_h, l.base_m,
           f.seg_id, (f.fecha_ts AT TIME ZONE 'America/Bogota')::date AS reg_date,
           COALESCE(f.mortalidad_hembras,0) + COALESCE(f.sel_h,0) + COALESCE(f.error_sexaje_hembras,0)
             + COALESCE(f.mov_venta_h,0) + COALESCE(f.mov_retiro_h,0)
             + COALESCE(f.mov_traslado_out_h,0) - COALESCE(f.mov_traslado_in_h,0) AS bajas_h,
           COALESCE(f.mortalidad_machos,0) + COALESCE(f.sel_m,0) + COALESCE(f.error_sexaje_machos,0)
             + COALESCE(f.mov_venta_m,0) + COALESCE(f.mov_retiro_m,0)
             + COALESCE(f.mov_traslado_out_m,0) - COALESCE(f.mov_traslado_in_m,0) AS bajas_m
      FROM lpp l
      CROSS JOIN LATERAL fn_seguimiento_diario_produccion(l.lpp_id, NULL) f
     WHERE f.seg_id IS NOT NULL AND NOT f.fila_sin_lpp
)
SELECT d.*,
       d.base_h - SUM(d.bajas_h) OVER w AS saldo_h,
       d.base_m - SUM(d.bajas_m) OVER w AS saldo_m,
       d.base_h - (SUM(d.bajas_h) OVER w - d.bajas_h) AS disp_h_antes,
       d.base_m - (SUM(d.bajas_m) OVER w - d.bajas_m) AS disp_m_antes
  FROM dia d
WINDOW w AS (PARTITION BY d.lpp_id ORDER BY d.reg_date, d.seg_id
             ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW);

\echo
\echo ============ PRODUCCION: filas que un bloqueo duro habria rechazado ============
SELECT c.id AS empresa_id, c.name AS empresa,
       count(*) AS filas_totales,
       count(*) FILTER (WHERE saldo_h < 0 OR saldo_m < 0) AS filas_que_sobregiran,
       count(DISTINCT lpp_id) AS lpp_con_datos,
       count(DISTINCT lpp_id) FILTER (WHERE saldo_h < 0 OR saldo_m < 0) AS lpp_afectados
  FROM _barrido_prod b JOIN companies c ON c.id = b.company_id
 GROUP BY 1,2 ORDER BY 1;

\echo
\echo ============ PRODUCCION: LPP con saldo final negativo ============
SELECT c.name AS empresa, f.lpp_id, f.lote_id, f.base_h, f.base_m,
       f.saldo_h AS saldo_final_h, f.saldo_m AS saldo_final_m
  FROM (SELECT DISTINCT ON (lpp_id) * FROM _barrido_prod
         ORDER BY lpp_id, reg_date DESC, seg_id DESC) f
  JOIN companies c ON c.id = f.company_id
 WHERE f.saldo_h < 0 OR f.saldo_m < 0
 ORDER BY LEAST(f.saldo_h, f.saldo_m);

\echo
\echo ============ CONTRASTE: saldo del barrido vs el que ya expone la fn (ultimo dia) ============
SELECT c.name AS empresa, f.lpp_id, f.saldo_h AS barrido_h, s.saldo_aves_h AS fn_h,
       f.saldo_m AS barrido_m, s.saldo_aves_m AS fn_m
  FROM (SELECT DISTINCT ON (lpp_id) * FROM _barrido_prod
         ORDER BY lpp_id, reg_date DESC, seg_id DESC) f
  JOIN companies c ON c.id = f.company_id
  CROSS JOIN LATERAL (SELECT saldo_aves_h, saldo_aves_m
                        FROM fn_seguimiento_diario_produccion(f.lpp_id, NULL)
                       WHERE seg_id = f.seg_id LIMIT 1) s
 ORDER BY f.lpp_id;

\echo
\echo ============ MARGEN: saldo minimo alcanzado por lote (cuan cerca del borde opera cada uno) ============
WITH m AS (
  SELECT 'levante' AS fase, company_id, lote_id::text AS clave, MIN(LEAST(saldo_h, saldo_m)) AS margen
    FROM _barrido_lev GROUP BY 1,2,3
  UNION ALL
  SELECT 'produccion', company_id, lpp_id::text, MIN(LEAST(saldo_h, saldo_m))
    FROM _barrido_prod GROUP BY 1,2,3
)
SELECT fase,
       count(*)                                   AS lotes,
       count(*) FILTER (WHERE margen <  0)        AS negativo,
       count(*) FILTER (WHERE margen =  0)        AS justo_en_cero,
       count(*) FILTER (WHERE margen BETWEEN 1 AND 50)   AS margen_1_50,
       count(*) FILTER (WHERE margen > 50)        AS margen_holgado
  FROM m GROUP BY 1 ORDER BY 1;

\echo
\echo ============ ALCANCE DEL BARRIDO: que empresas tienen datos de postura ============
SELECT c.id, c.name,
       (SELECT count(*) FROM _barrido_lev  b WHERE b.company_id = c.id) AS filas_levante,
       (SELECT count(*) FROM _barrido_prod p WHERE p.company_id = c.id) AS filas_produccion
  FROM companies c ORDER BY c.id;
