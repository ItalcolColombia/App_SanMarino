-- =============================================================================
-- verificar_atribucion_lote_engorde.sql  —  DETECTOR, SOLO LECTURA
-- =============================================================================
-- Mide cuántas filas de `lote_registro_historico_unificado` quedaron imputadas a
-- un lote de engorde distinto del que estaba vivo en ese galpón en la fecha de la
-- operación (item A9).
--
-- POR QUÉ HACE FALTA UN DETECTOR NUEVO
--
-- `fn_cuadre_alimento_engorde` —el detector que ya existe— compara el saldo del
-- CICLO ACTIVO contra el stock del galpón. Este defecto vive casi entero en los
-- ciclos ya CERRADOS (medición del 09-ago-2026: 1.705 de 1.707 filas), y además
-- una imputación equivocada ENTRE dos lotes del mismo galpón se cancela al
-- agregar por galpón. O sea: el cuadre puede dar 61 filas / 1 descuadrado —como
-- da hoy— con millones de kilos imputados al lote equivocado.
--
-- Esto es exactamente lo que advierte la compuerta de las rondas fallidas de la
-- marca «próximo ciclo»: *"identidad SIN marcas: NECESARIA, JAMÁS SUFICIENTE.
-- Las 3 rondas dieron 0/0 siempre, incluida la que producía negativos."* Un
-- detector que no puede ver el defecto no prueba nada cuando sale limpio.
--
-- EL DEFECTO
--
-- `fn_lote_ave_engorde_id_desde_ubicacion(granja, nucleo, galpon)` resuelve el
-- lote con `ORDER BY lote_ave_engorde_id DESC LIMIT 1` — el id MÁS ALTO del
-- galpón, sin mirar la fecha de la operación. El trigger que llena el histórico
-- la llama en el INSERT. En un galpón que encadena ciclos (en Ecuador, 34 de 35
-- galpones, hasta 4 lotes) todo movimiento fechado en un ciclo anterior se le
-- carga al lote más nuevo.
--
-- CÓMO SE USA
--
--   psql ... -f backend/sql/verificar_atribucion_lote_engorde.sql
--
-- Correrlo ANTES de cualquier cambio (congela la línea base) y DESPUÉS
-- (compara). Toda empresa que no sea el objetivo del cambio tiene que quedar
-- IGUAL — no en cero, sino igual: este script mide un defecto preexistente, no
-- una regresión.
-- =============================================================================

\echo '=== A) Topologia: galpones que encadenan ciclos (donde el defecto puede ocurrir) ==='

SELECT c.name                                        AS empresa,
       count(*) FILTER (WHERE t.lotes = 1)           AS galpones_un_solo_lote,
       count(*) FILTER (WHERE t.lotes > 1)           AS galpones_encadenados,
       max(t.lotes)                                  AS max_lotes_en_un_galpon
FROM (
    SELECT company_id, granja_id,
           COALESCE(nucleo_id, '') AS n, COALESCE(galpon_id, '') AS g,
           count(*) AS lotes
    FROM lote_ave_engorde
    WHERE deleted_at IS NULL
    GROUP BY 1, 2, 3, 4
) t
JOIN companies c ON c.id = t.company_id
GROUP BY 1
ORDER BY 3 DESC;


-- Ventana de vida de cada lote dentro de su galpón: desde su encaset hasta el
-- encaset del siguiente. Es la definición que el trigger debería haber usado.
CREATE TEMP VIEW _vida_lote AS
SELECT l.lote_ave_engorde_id,
       l.granja_id,
       COALESCE(l.nucleo_id, '') AS n,
       COALESCE(l.galpon_id, '') AS g,
       l.fecha_encaset::date     AS desde,
       LEAD(l.fecha_encaset::date) OVER (
           PARTITION BY l.granja_id, COALESCE(l.nucleo_id, ''), COALESCE(l.galpon_id, '')
           ORDER BY l.fecha_encaset, l.lote_ave_engorde_id)                 AS hasta,
       ROW_NUMBER() OVER (
           PARTITION BY l.granja_id, COALESCE(l.nucleo_id, ''), COALESCE(l.galpon_id, '')
           ORDER BY l.fecha_encaset, l.lote_ave_engorde_id)                 AS orden
FROM lote_ave_engorde l
WHERE l.deleted_at IS NULL AND l.fecha_encaset IS NOT NULL;

CREATE TEMP VIEW _primer_lote AS
SELECT granja_id, n, g, lote_ave_engorde_id AS primer_lote, desde AS primer_encaset
FROM _vida_lote WHERE orden = 1;

-- Atribución esperada de cada fila del histórico.
--   1) Si cae dentro de la ventana de vida de un lote -> ese lote.
--   2) Si es ANTERIOR al primer encaset del galpón -> el PRIMER lote. No es un
--      error: es el alimento previo al encaset, que ya es una feature (fn v15).
--      Tratarlo como "sin lote" dejaría esas filas invisibles, que es como se
--      rompieron intentos anteriores en esta misma zona.
CREATE TEMP VIEW _atribucion AS
SELECT h.id,
       h.company_id,
       h.farm_id,
       h.nucleo_id,
       h.galpon_id,
       h.fecha_operacion,
       h.cantidad_kg,
       h.lote_ave_engorde_id AS grabado,
       COALESCE(
           (SELECT v.lote_ave_engorde_id FROM _vida_lote v
             WHERE v.granja_id = h.farm_id
               AND v.n = COALESCE(h.nucleo_id, '')
               AND v.g = COALESCE(h.galpon_id, '')
               AND h.fecha_operacion::date >= v.desde
               AND (v.hasta IS NULL OR h.fecha_operacion::date < v.hasta)),
           (SELECT p.primer_lote FROM _primer_lote p
             WHERE p.granja_id = h.farm_id
               AND p.n = COALESCE(h.nucleo_id, '')
               AND p.g = COALESCE(h.galpon_id, '')
               AND h.fecha_operacion::date < p.primer_encaset)
       ) AS esperado
FROM lote_registro_historico_unificado h
WHERE h.anulado = false;


\echo ''
\echo '=== B) Filas mal atribuidas, por empresa ==='

SELECT c.name AS empresa,
       count(*)                                                                       AS filas,
       count(*) FILTER (WHERE esperado IS NOT NULL AND grabado IS DISTINCT FROM esperado) AS mal_atribuidas,
       round(100.0 * count(*) FILTER (WHERE esperado IS NOT NULL AND grabado IS DISTINCT FROM esperado)
             / NULLIF(count(*), 0), 1)                                                AS pct,
       round(sum(cantidad_kg) FILTER (WHERE esperado IS NOT NULL AND grabado IS DISTINCT FROM esperado)::numeric, 0) AS kg_mal_atribuidos,
       -- Sin lote resoluble: el galpón no tiene ningún lote de engorde con encaset.
       -- Normal en empresas que no operan engorde en esa ubicación.
       count(*) FILTER (WHERE esperado IS NULL)                                       AS irresolubles
FROM _atribucion a
JOIN companies c ON c.id = a.company_id
GROUP BY 1
ORDER BY 3 DESC;


\echo ''
\echo '=== C) Donde vive el defecto: estado del lote al que SE GRABO ==='
\echo '    (si esta casi todo en Cerrado, el cuadre no puede verlo)'

SELECT c.name AS empresa,
       le.estado_operativo_lote AS estado_del_lote_grabado,
       count(*)                 AS filas,
       round(sum(a.cantidad_kg)::numeric, 0) AS kg
FROM _atribucion a
JOIN companies c          ON c.id = a.company_id
JOIN lote_ave_engorde le  ON le.lote_ave_engorde_id = a.grabado
WHERE a.esperado IS NOT NULL AND a.grabado IS DISTINCT FROM a.esperado
GROUP BY 1, 2
ORDER BY 1, 3 DESC;


\echo ''
\echo '=== D) Cuantos lotes LIQUIDADOS estan involucrados ==='
\echo '    (una correccion retroactiva los dejaria fuera de sintonia con su copia congelada)'

SELECT c.name AS empresa,
       count(DISTINCT le.lote_ave_engorde_id) FILTER (WHERE le.liquidado_at IS NOT NULL) AS lotes_liquidados_afectados,
       count(DISTINCT le.lote_ave_engorde_id)                                            AS lotes_afectados_total
FROM _atribucion a
JOIN companies c         ON c.id = a.company_id
JOIN lote_ave_engorde le ON le.lote_ave_engorde_id = a.grabado
WHERE a.esperado IS NOT NULL AND a.grabado IS DISTINCT FROM a.esperado
GROUP BY 1
ORDER BY 2 DESC;


\echo ''
\echo '=== E) Top 15 galpones por kilos mal atribuidos ==='

SELECT c.name AS empresa, a.farm_id, a.nucleo_id, a.galpon_id,
       count(*) AS filas, round(sum(a.cantidad_kg)::numeric, 0) AS kg
FROM _atribucion a
JOIN companies c ON c.id = a.company_id
WHERE a.esperado IS NOT NULL AND a.grabado IS DISTINCT FROM a.esperado
GROUP BY 1, 2, 3, 4
ORDER BY 6 DESC NULLS LAST
LIMIT 15;

DROP VIEW _atribucion;
DROP VIEW _primer_lote;
DROP VIEW _vida_lote;

\echo ''
\echo '=== FIN — este script no escribe nada ==='
