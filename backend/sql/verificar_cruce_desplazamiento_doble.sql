-- ============================================================================
-- VERIFICACION (solo lectura sobre el estado final: corre TODO dentro de una
-- transaccion que se revierte). Gate multipais del cambio de
-- fn_cruce_reproductora_a_engorde del 11-sep-2026: el desplazamiento por hora de
-- llegada dejaba de aplicarse dos veces cuando la reproductora ya arrancaba en la
-- edad 1.
--
-- Que hace:
--   1. Recalcula el cruce de TODOS los lotes con la fn VIGENTE  -> linea base
--      (aisla el efecto del cambio de la fn de cualquier dato que haya cambiado
--       desde la ultima vez que corrio el trigger).
--   2. Reemplaza la fn por la del repo (backend/sql/fn_cruce_reproductora_a_engorde.sql)
--      y vuelve a recalcular  -> resultado nuevo.
--   3. EXCEPT en los dos sentidos, todas las columnas de negocio, TODAS las empresas.
--
-- Uso:
--   psql ... -v ON_ERROR_STOP=1 -f backend/sql/verificar_cruce_desplazamiento_doble.sql
--
-- Lo que tiene que salir: en la seccion 3, cero filas para toda empresa que no sea
-- ItalcolPanama, y en Panama SOLO los lotes 239, 255, 256 y 257 (los unicos con
-- hora >= 13:00 cuya reproductora arranca en la edad 1).
--
-- SIN-MIGRACION: diagnostico de solo lectura; no crea ningun objeto (la fn que
-- carga la revierte el ROLLBACK final).
-- ============================================================================

BEGIN;

-- ─────────────────────────────────────────────────────────────────────────────
-- 1) Linea base: el cruce como lo escribiria la fn VIGENTE, hoy.
-- ─────────────────────────────────────────────────────────────────────────────
DO $verif$
DECLARE l int;
BEGIN
    FOR l IN SELECT DISTINCT lote_ave_engorde_id FROM lote_reproductora_ave_engorde LOOP
        PERFORM fn_cruce_reproductora_a_engorde(l);
    END LOOP;
END
$verif$;

CREATE TEMP TABLE cruce_base AS
SELECT s.lote_ave_engorde_id,
       (s.fecha AT TIME ZONE 'UTC')::date        AS dia,
       (s.metadata->>'edad')::int                AS edad,
       (s.metadata->>'desplazamientoHora')::int  AS desp,
       s.mortalidad_machos, s.mortalidad_hembras, s.sel_m, s.sel_h,
       s.error_sexaje_machos, s.error_sexaje_hembras,
       s.consumo_kg_machos, s.consumo_kg_hembras,
       s.peso_prom_machos, s.peso_prom_hembras
  FROM seguimiento_diario_aves_engorde s
 WHERE s.origen_cruce;

\echo '--- 1) filas de cruce en la linea base ---'
SELECT count(*) AS filas_cruce_base FROM cruce_base;

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) La fn del repo + recalculo.
-- ─────────────────────────────────────────────────────────────────────────────
\i fn_cruce_reproductora_a_engorde.sql

DO $verif$
DECLARE l int;
BEGIN
    FOR l IN SELECT DISTINCT lote_ave_engorde_id FROM lote_reproductora_ave_engorde LOOP
        PERFORM fn_cruce_reproductora_a_engorde(l);
    END LOOP;
END
$verif$;

CREATE TEMP TABLE cruce_nuevo AS
SELECT s.lote_ave_engorde_id,
       (s.fecha AT TIME ZONE 'UTC')::date        AS dia,
       (s.metadata->>'edad')::int                AS edad,
       (s.metadata->>'desplazamientoHora')::int  AS desp,
       s.mortalidad_machos, s.mortalidad_hembras, s.sel_m, s.sel_h,
       s.error_sexaje_machos, s.error_sexaje_hembras,
       s.consumo_kg_machos, s.consumo_kg_hembras,
       s.peso_prom_machos, s.peso_prom_hembras
  FROM seguimiento_diario_aves_engorde s
 WHERE s.origen_cruce;

\echo '--- 2) filas de cruce con la fn nueva (tiene que dar el mismo total) ---'
SELECT count(*) AS filas_cruce_nuevo FROM cruce_nuevo;

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) GATE MULTIPAIS: diferencias por empresa, en los dos sentidos.
-- ─────────────────────────────────────────────────────────────────────────────
\echo '--- 3) diferencias por EMPRESA (toda empresa distinta de ItalcolPanama debe dar 0) ---'
WITH dif AS (
    SELECT 'solo_base'  AS lado, d.* FROM (SELECT * FROM cruce_base  EXCEPT SELECT * FROM cruce_nuevo) d
    UNION ALL
    SELECT 'solo_nuevo' AS lado, d.* FROM (SELECT * FROM cruce_nuevo EXCEPT SELECT * FROM cruce_base ) d
)
SELECT c.name AS empresa,
       count(*) FILTER (WHERE dif.lado = 'solo_base')  AS filas_solo_base,
       count(*) FILTER (WHERE dif.lado = 'solo_nuevo') AS filas_solo_nuevo,
       count(DISTINCT dif.lote_ave_engorde_id)         AS lotes_afectados,
       string_agg(DISTINCT dif.lote_ave_engorde_id::text, ',' ORDER BY dif.lote_ave_engorde_id::text) AS ids
  FROM dif
  JOIN lote_ave_engorde lae ON lae.lote_ave_engorde_id = dif.lote_ave_engorde_id
  JOIN companies c          ON c.id = lae.company_id
 GROUP BY c.name
 ORDER BY c.name;

\echo '--- 3b) detalle fila a fila de los lotes que se mueven ---'
WITH dif AS (
    SELECT 'solo_base'  AS lado, d.* FROM (SELECT * FROM cruce_base  EXCEPT SELECT * FROM cruce_nuevo) d
    UNION ALL
    SELECT 'solo_nuevo' AS lado, d.* FROM (SELECT * FROM cruce_nuevo EXCEPT SELECT * FROM cruce_base ) d
)
SELECT lado, lote_ave_engorde_id AS lote, dia, edad, desp,
       mortalidad_machos + mortalidad_hembras AS mort,
       consumo_kg_machos + consumo_kg_hembras AS kg
  FROM dif
 ORDER BY lote_ave_engorde_id, edad, lado;

\echo '--- 3c) el cruce sigue teniendo las MISMAS cifras por lote y edad (solo cambia el dia) ---'
WITH b AS (SELECT lote_ave_engorde_id, edad, mortalidad_machos, mortalidad_hembras, sel_m, sel_h,
                  error_sexaje_machos, error_sexaje_hembras, consumo_kg_machos, consumo_kg_hembras,
                  peso_prom_machos, peso_prom_hembras FROM cruce_base),
     n AS (SELECT lote_ave_engorde_id, edad, mortalidad_machos, mortalidad_hembras, sel_m, sel_h,
                  error_sexaje_machos, error_sexaje_hembras, consumo_kg_machos, consumo_kg_hembras,
                  peso_prom_machos, peso_prom_hembras FROM cruce_nuevo)
SELECT (SELECT count(*) FROM (SELECT * FROM b EXCEPT SELECT * FROM n) x) AS solo_base_sin_fecha,
       (SELECT count(*) FROM (SELECT * FROM n EXCEPT SELECT * FROM b) x) AS solo_nuevo_sin_fecha;

\echo '--- 3d) primera fila del cruce por lote afectado (antes -> despues) ---'
SELECT b.lote_ave_engorde_id AS lote,
       min(b.dia) AS primer_dia_antes,
       (SELECT min(dia) FROM cruce_nuevo n WHERE n.lote_ave_engorde_id = b.lote_ave_engorde_id) AS primer_dia_despues
  FROM cruce_base b
 GROUP BY b.lote_ave_engorde_id
HAVING min(b.dia) IS DISTINCT FROM
       (SELECT min(dia) FROM cruce_nuevo n WHERE n.lote_ave_engorde_id = b.lote_ave_engorde_id)
 ORDER BY 1;

ROLLBACK;
