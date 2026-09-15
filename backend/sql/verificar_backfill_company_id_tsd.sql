-- =============================================================================
-- verificar_backfill_company_id_tsd.sql — SOLO LECTURA (todo termina en ROLLBACK)
-- =============================================================================
-- Mide el efecto de la migración 20260914120000_BackfillCompanyIdMovimientosTsd antes de que
-- corra en un entorno: qué auditorías TSD-* estampa, cuáles deja en 0 y cómo cambia el saldo de
-- aves de producción (fn_seguimiento_diario_produccion suma movimiento_aves con
-- company_id = lpp.company_id, así que un TSD en 0 hoy NO descuenta ni acredita aves en producción).
--
-- Uso:  psql -h <host> -p <port> -U <user> -d <db> -v ON_ERROR_STOP=1 -f verificar_backfill_company_id_tsd.sql
--
-- Lectura del resultado:
--   [1] auditorías por prefijo y empresa ANTES.
--   [2] cada TSD en 0 con la empresa que recibiría (NULL = queda en 0) y si le falta lote destino.
--   [3] UPDATE de la migración y 2.ª pasada: la segunda tiene que decir UPDATE 0 (idempotencia).
--   [4] saldo de producción por LPP: filas comparadas y filas distintas por columna. Todo LPP que
--       no tenga un TSD en su lote debe salir con 0 en todas las columnas.
--   [5] auditorías por prefijo y empresa DESPUÉS (dentro de la transacción).
-- =============================================================================

BEGIN;

\echo '[1] Auditorías por prefijo y empresa (ANTES)'
SELECT left(numero_movimiento, 4) AS prefijo, company_id, estado,
       count(*) FILTER (WHERE deleted_at IS NULL) AS vivas,
       count(*) FILTER (WHERE deleted_at IS NOT NULL) AS borradas
  FROM movimiento_aves
 GROUP BY 1, 2, 3
 ORDER BY 1, 2, 3;

\echo '[2] TSD con company_id = 0 y la empresa que recibirían (NULL = quedan en 0)'
SELECT ma.id, ma.numero_movimiento, ma.fecha_movimiento::date AS fecha,
       ma.granja_origen_id, f.company_id AS empresa_granja_origen,
       CASE WHEN f.company_id > 0 THEN f.company_id END AS empresa_a_estampar,
       ma.lote_origen_id, ma.lote_destino_id,
       (ma.lote_destino_id IS NULL) AS sin_lote_destino,
       ma.cantidad_hembras, ma.cantidad_machos
  FROM movimiento_aves ma
  LEFT JOIN farms f ON f.id = ma.granja_origen_id
 WHERE ma.numero_movimiento LIKE 'TSD-%'
   AND ma.company_id = 0
 ORDER BY ma.id;

-- Saldo de producción ANTES, para TODOS los LPP vivos (fn LANGUAGE sql: sin temp tables propias).
CREATE TEMP TABLE _saldo_antes ON COMMIT DROP AS
SELECT lpp.lote_postura_produccion_id AS lpp_id, lpp.company_id,
       r.fecha, r.aves_h_inicio_dia, r.aves_m_inicio_dia, r.saldo_aves_h, r.saldo_aves_m,
       r.mov_traslado_in_h, r.mov_traslado_in_m, r.mov_traslado_out_h, r.mov_traslado_out_m
  FROM lote_postura_produccion lpp
 CROSS JOIN LATERAL fn_seguimiento_diario_produccion(lpp.lote_postura_produccion_id, NULL) r
 WHERE lpp.deleted_at IS NULL;

\echo '[3] Backfill (mismo SQL que la migración) y 2.ª pasada: debe decir UPDATE 0'
UPDATE movimiento_aves ma
   SET company_id = f.company_id
  FROM farms f
 WHERE ma.numero_movimiento LIKE 'TSD-%'
   AND ma.company_id = 0
   AND ma.granja_origen_id = f.id
   AND f.company_id > 0;

UPDATE movimiento_aves ma
   SET company_id = f.company_id
  FROM farms f
 WHERE ma.numero_movimiento LIKE 'TSD-%'
   AND ma.company_id = 0
   AND ma.granja_origen_id = f.id
   AND f.company_id > 0;

CREATE TEMP TABLE _saldo_despues ON COMMIT DROP AS
SELECT lpp.lote_postura_produccion_id AS lpp_id, lpp.company_id,
       r.fecha, r.aves_h_inicio_dia, r.aves_m_inicio_dia, r.saldo_aves_h, r.saldo_aves_m,
       r.mov_traslado_in_h, r.mov_traslado_in_m, r.mov_traslado_out_h, r.mov_traslado_out_m
  FROM lote_postura_produccion lpp
 CROSS JOIN LATERAL fn_seguimiento_diario_produccion(lpp.lote_postura_produccion_id, NULL) r
 WHERE lpp.deleted_at IS NULL;

\echo '[4] Saldo de producción: filas y diferencias por columna, por empresa y LPP (solo LPP con alguna diferencia + total)'
WITH cmp AS (
    SELECT COALESCE(a.lpp_id, d.lpp_id)         AS lpp_id,
           COALESCE(a.company_id, d.company_id) AS company_id,
           a.lpp_id IS NULL AS solo_despues,
           d.lpp_id IS NULL AS solo_antes,
           a.saldo_aves_h       IS DISTINCT FROM d.saldo_aves_h       AS dif_saldo_h,
           a.saldo_aves_m       IS DISTINCT FROM d.saldo_aves_m       AS dif_saldo_m,
           a.aves_h_inicio_dia  IS DISTINCT FROM d.aves_h_inicio_dia  AS dif_inicio_h,
           a.mov_traslado_in_h  IS DISTINCT FROM d.mov_traslado_in_h  AS dif_in_h,
           a.mov_traslado_in_m  IS DISTINCT FROM d.mov_traslado_in_m  AS dif_in_m,
           a.mov_traslado_out_h IS DISTINCT FROM d.mov_traslado_out_h AS dif_out_h,
           a.mov_traslado_out_m IS DISTINCT FROM d.mov_traslado_out_m AS dif_out_m
      FROM _saldo_antes a
      FULL JOIN _saldo_despues d
        ON d.lpp_id = a.lpp_id AND d.fecha IS NOT DISTINCT FROM a.fecha
),
por_lpp AS (
    SELECT company_id, lpp_id,
           count(*)                                AS filas,
           count(*) FILTER (WHERE solo_antes)      AS solo_antes,
           count(*) FILTER (WHERE solo_despues)    AS solo_despues,
           count(*) FILTER (WHERE dif_saldo_h)     AS saldo_h,
           count(*) FILTER (WHERE dif_saldo_m)     AS saldo_m,
           count(*) FILTER (WHERE dif_inicio_h)    AS inicio_h,
           count(*) FILTER (WHERE dif_in_h)        AS tras_in_h,
           count(*) FILTER (WHERE dif_in_m)        AS tras_in_m,
           count(*) FILTER (WHERE dif_out_h)       AS tras_out_h,
           count(*) FILTER (WHERE dif_out_m)       AS tras_out_m
      FROM cmp
     GROUP BY company_id, lpp_id
)
SELECT company_id::text, lpp_id::text, filas, solo_antes, solo_despues,
       saldo_h, saldo_m, inicio_h, tras_in_h, tras_in_m, tras_out_h, tras_out_m
  FROM por_lpp
 WHERE solo_antes + solo_despues + saldo_h + saldo_m + inicio_h
       + tras_in_h + tras_in_m + tras_out_h + tras_out_m > 0
UNION ALL
SELECT 'TOTAL', count(*)::text || ' LPP', sum(filas), sum(solo_antes), sum(solo_despues),
       sum(saldo_h), sum(saldo_m), sum(inicio_h), sum(tras_in_h), sum(tras_in_m),
       sum(tras_out_h), sum(tras_out_m)
  FROM por_lpp;

\echo '[5] Auditorías por prefijo y empresa (DESPUÉS, dentro de la transacción)'
SELECT left(numero_movimiento, 4) AS prefijo, company_id, count(*) AS filas
  FROM movimiento_aves
 GROUP BY 1, 2
 ORDER BY 1, 2;

ROLLBACK;
