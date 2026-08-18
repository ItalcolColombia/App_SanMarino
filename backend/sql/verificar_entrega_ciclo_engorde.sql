-- =============================================================================
-- GATE G1 de la ENTREGA AL CICLO SIGUIENTE (alimento de engorde) — y el hallazgo que produjo.
--
-- 🔴 RESULTADO DE LA PRIMERA CORRIDA (18-ago-2026): el modelo de ENTREGA no puede dispararse
--    NUNCA en la topologia real. 0 de 53 pares secuenciales con hueco.
--
-- EL MECANISMO, EN CORTO. La entrega necesita escribir una salida sintetica en el ULTIMO DIA VISIBLE
-- del cedente, y necesita que el cedente TENGA saldo ese dia para poder entregarlo (el tope). Pero
-- `rango_final.fecha_max` se cierra en cuanto `saldo_close` encuentra la PRIMERA fecha >= ultimo
-- seguimiento con saldo <= 0,5. Y todo ciclo bien operado termina en 0: es la propia regla R2 («al
-- liquidar el lote trasladan el alimento sobrante fuera del galpon»).
--
-- Consecuencia medida sobre los 53 pares con hueco de la BD local:
--     cedentes cuya grilla LLEGA al dia de la entrega ...........  0 de 53
--     cedentes que terminan con saldo > 0 .......................  2 de 53
-- O sea: cuando el alimento llega al hueco, el cedente ya vacio su bodega. No hay kilos que entregar
-- ni dia donde escribir la entrega. El feature se dispara solo cuando la operacion dejo saldo
-- colgado — que es justamente la ANOMALIA que R2 manda senalar, no el caso sano que motivo el pedido.
--
-- QUE SIGNIFICA. El alimento que cae en el hueco NO es del ciclo anterior en ningun sentido contable:
-- llega despues de que ese ciclo cerro. No hay handoff que modelar. Lo que ese alimento necesita es
-- que la APERTURA DEL DESTINO alcance mas atras — o sea `dias_alimento_previo_encaset` (la ventana
-- D4), que el plan excluye explicitamente como «otro feature». Decision de producto.
--
-- El script se conserva porque es el instrumento que lo demostro, y porque cualquier rediseno futuro
-- tiene que volver a pasar por I1..I11 antes de tocar la fn.
--
-- COMO SE CORRE (exige la fn v16b instalada; con la v16a la parte de entrega da 0 por construccion):
--     psql ... -f backend/sql/verificar_entrega_ciclo_engorde.sql
-- Todo en UNA transaccion que termina en ROLLBACK; la ultima consulta verifica 0 rastro.
-- ⚠️ La fase de inyeccion NO bombea `inventario_gestion_stock`, asi que I5 (cuadre) sube por
-- construccion: son movimientos de historico sin su contraparte de stock. Antes de usar I5 como
-- veredicto hay que agregar ese INSERT.
-- Line endings LF a proposito (psql.exe duplica el CR).
-- =============================================================================

\set ON_ERROR_STOP on
\timing off

BEGIN;

-- ─────────────────────────────────────────────────────────────────────────────
-- 0. Topologia: pares secuenciales REALES con hueco, y con espacio antes de que la ventana previa al
--    encaset del destino alcance sola el movimiento (si lo alcanzara, el estado correcto es INERTE).
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TEMP TABLE _seg AS
SELECT l.lote_ave_engorde_id AS id, l.company_id, l.granja_id,
       COALESCE(TRIM(l.nucleo_id), '') AS nuc, COALESCE(TRIM(l.galpon_id), '') AS gal,
       l.fecha_encaset::date AS enc,
       MIN(s.fecha)::date AS smin, MAX(s.fecha)::date AS smax
FROM lote_ave_engorde l
JOIN seguimiento_diario_aves_engorde s ON s.lote_ave_engorde_id = l.lote_ave_engorde_id
WHERE l.deleted_at IS NULL
GROUP BY 1,2,3,4,5,6;

CREATE TEMP TABLE _pares AS
SELECT a.company_id, a.granja_id, a.nuc, a.gal,
       a.id AS cedente, b.id AS destino,
       a.smax AS ced_hasta, b.smin AS des_desde,
       -- el dia del ingreso inyectado: en el hueco y ANTES de que la ventana del destino lo alcance
       LEAST(a.smax + 1, (b.enc - 10) - 1) AS fecha_iny,
       b.smin - 1 AS fecha_entrega
FROM _seg a
JOIN _seg b ON b.granja_id = a.granja_id AND b.nuc = a.nuc AND b.gal = a.gal AND b.enc > a.enc
WHERE b.smin > a.smax + 1
  AND (b.enc - 10) > a.smax + 1
  AND NOT EXISTS (SELECT 1 FROM _seg m
                   WHERE m.granja_id = a.granja_id AND m.nuc = a.nuc AND m.gal = a.gal
                     AND m.enc > a.enc AND m.enc < b.enc);

\echo ''
\echo '=== UNIVERSO DE PRUEBA (pares secuenciales reales con hueco) ==='
SELECT count(*) AS pares, count(DISTINCT granja_id) AS granjas FROM _pares;

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. Foto ANTES: la tabla diaria de todos los lotes involucrados, sin ninguna entrega.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TEMP TABLE _lotes_probe AS
SELECT DISTINCT unnest(ARRAY[cedente, destino]) AS lote_id FROM _pares;

CREATE TEMP TABLE _antes AS
SELECT p.lote_id, f.fecha, f.seg_id, f.saldo_alimento_kg, f.ingreso_alimento_kg,
       f.traslado_salida_kg, f.apertura_alimento_kg, f.documento
FROM _lotes_probe p CROSS JOIN LATERAL fn_seguimiento_diario_engorde(p.lote_id) f;

CREATE TEMP TABLE _cuadre_antes AS SELECT * FROM fn_cuadre_alimento_engorde(NULL);

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. INYECCION: un ingreso de 3.000 kg en el hueco de cada par, con su espejo en el historico y su
--    stock, tal como lo dejaria el alta real. Y el HECHO ya materializado en VIGENTE.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TEMP TABLE _iny AS
SELECT p.*, 3000.0::numeric AS kg,
       'GATE-' || p.cedente || '-' || p.destino AS doc
FROM _pares p;

INSERT INTO lote_registro_historico_unificado
    (company_id, lote_ave_engorde_id, farm_id, nucleo_id, galpon_id, fecha_operacion, tipo_evento,
     origen_tabla, origen_id, cantidad_kg, numero_documento, anulado, para_proximo_ciclo)
SELECT i.company_id, i.cedente, i.granja_id, i.nuc, i.gal, i.fecha_iny, 'INV_INGRESO',
       'gate_entrega', ROW_NUMBER() OVER (ORDER BY i.cedente), i.kg, i.doc, FALSE, TRUE
FROM _iny i;

INSERT INTO alimento_entrega_ciclo_engorde
    (company_id, farm_id, nucleo_id, galpon_id, origen_tabla, origen_id, fecha_movimiento,
     kg_movimiento, numero_documento, lote_cedente_id, lote_destino_id, fecha_entrega,
     kg_entregados, kg_no_diferible, estado, motivo, sellada)
SELECT i.company_id, i.granja_id, i.nuc, i.gal, 'gate_entrega',
       ROW_NUMBER() OVER (ORDER BY i.cedente), i.fecha_iny, i.kg, i.doc,
       i.cedente, i.destino, i.fecha_entrega, i.kg, 0, 'VIGENTE', 'gate', FALSE
FROM _iny i;

CREATE TEMP TABLE _despues AS
SELECT p.lote_id, f.fecha, f.seg_id, f.saldo_alimento_kg, f.ingreso_alimento_kg,
       f.traslado_salida_kg, f.apertura_alimento_kg, f.documento
FROM _lotes_probe p CROSS JOIN LATERAL fn_seguimiento_diario_engorde(p.lote_id) f;

-- ─────────────────────────────────────────────────────────────────────────────
-- INVARIANTES
-- ─────────────────────────────────────────────────────────────────────────────
\echo ''
\echo '=== I1 — filas de la diaria en NEGATIVO: no puede aparecer ninguna nueva ==='
SELECT (SELECT count(*) FROM _antes   WHERE saldo_alimento_kg < -0.001) AS negativas_antes,
       (SELECT count(*) FROM _despues WHERE saldo_alimento_kg < -0.001) AS negativas_despues;

\echo ''
\echo '=== I2 — CONSERVACION: el ingreso inyectado (3.000 kg) aparece EXACTAMENTE una vez por galpon ==='
\echo '(delta del saldo final del cedente + delta de la apertura del destino == 3.000 en cada par)'
SELECT count(*) AS pares,
       count(*) FILTER (WHERE abs(delta_total - 3000) <= 0.01) AS conservan,
       count(*) FILTER (WHERE abs(delta_total - 3000) >  0.01) AS ROTOS,
       round(max(abs(delta_total - 3000))::numeric, 3) AS peor_desvio
FROM (
    SELECT p.cedente, p.destino,
           COALESCE((SELECT sum(d.ingreso_alimento_kg) - sum(a.ingreso_alimento_kg)
                       FROM _despues d, _antes a
                      WHERE d.lote_id = p.cedente AND a.lote_id = p.cedente
                        AND d.fecha = a.fecha AND d.seg_id IS NOT DISTINCT FROM a.seg_id), 0)
         + COALESCE((SELECT sum(d.ingreso_alimento_kg) FROM _despues d
                      WHERE d.lote_id = p.cedente
                        AND NOT EXISTS (SELECT 1 FROM _antes a WHERE a.lote_id = p.cedente
                                          AND a.fecha = d.fecha
                                          AND a.seg_id IS NOT DISTINCT FROM d.seg_id)), 0)
           AS delta_total
    FROM _pares p
) x;

\echo ''
\echo '=== I2b — SUMA CERO del handoff: lo que sale del cedente entra al destino, kg a kg ==='
SELECT count(*) AS pares,
       count(*) FILTER (WHERE abs(salida_cedente - credito_destino) <= 0.01) AS cuadran,
       count(*) FILTER (WHERE abs(salida_cedente - credito_destino) >  0.01) AS ROTOS
FROM (
    SELECT p.cedente, p.destino,
           COALESCE((SELECT sum(d.traslado_salida_kg) FROM _despues d WHERE d.lote_id = p.cedente), 0)
         - COALESCE((SELECT sum(a.traslado_salida_kg) FROM _antes   a WHERE a.lote_id = p.cedente), 0)
             AS salida_cedente,
           COALESCE((SELECT sum(d.apertura_alimento_kg) FROM _despues d WHERE d.lote_id = p.destino), 0)
         - COALESCE((SELECT sum(a.apertura_alimento_kg) FROM _antes   a WHERE a.lote_id = p.destino), 0)
             AS credito_destino
    FROM _pares p
) x;

\echo ''
\echo '=== I3 — VISIBILIDAD (R3): el ingreso inyectado se ve en la grilla del CEDENTE ==='
SELECT count(*) AS pares,
       count(*) FILTER (WHERE visible) AS visibles,
       count(*) FILTER (WHERE NOT visible) AS INVISIBLES
FROM (
    SELECT i.cedente,
           EXISTS (SELECT 1 FROM _despues d
                    WHERE d.lote_id = i.cedente AND d.fecha = i.fecha_iny
                      AND d.ingreso_alimento_kg >= 2999.99) AS visible
    FROM _iny i
) x;

\echo ''
\echo '=== I3b — la ENTREGA tiene su propia linea, con documento que la explica ==='
SELECT count(*) AS pares,
       count(*) FILTER (WHERE con_linea) AS con_linea_de_entrega,
       count(*) FILTER (WHERE NOT con_linea) AS SIN_LINEA
FROM (
    SELECT i.cedente,
           EXISTS (SELECT 1 FROM _despues d
                    WHERE d.lote_id = i.cedente AND d.fecha = i.fecha_entrega
                      AND d.traslado_salida_kg >= 2999.99
                      AND d.documento LIKE '%Entrega al ciclo siguiente%') AS con_linea
    FROM _iny i
) x;

\echo ''
\echo '=== I4 — NO MULTIPLICACION: el ingreso se cuenta en UN solo lote ==='
SELECT count(*) AS pares,
       count(*) FILTER (WHERE lotes_que_lo_ven = 1) AS ok,
       count(*) FILTER (WHERE lotes_que_lo_ven <> 1) AS MULTIPLICADOS
FROM (
    SELECT i.cedente,
           (SELECT count(DISTINCT d.lote_id) FROM _despues d
             WHERE d.fecha = i.fecha_iny AND d.ingreso_alimento_kg >= 2999.99
               AND d.lote_id IN (i.cedente, i.destino)) AS lotes_que_lo_ven
    FROM _iny i
) x;

\echo ''
\echo '=== I5 — CUADRE: no puede alejarse de 0 en ningun galpon ==='
SELECT (SELECT count(*) FILTER (WHERE abs(COALESCE(descuadre_kg,0)) > 0.001) FROM _cuadre_antes) AS descuadrados_antes,
       (SELECT count(*) FILTER (WHERE abs(COALESCE(descuadre_kg,0)) > 0.001) FROM fn_cuadre_alimento_engorde(NULL)) AS descuadrados_despues;

-- ─────────────────────────────────────────────────────────────────────────────
-- I8 / I9 — LOS DOS BLOQUEANTES DEL NO-GO.
-- Se congela un extremo y se vuelve a leer. Con la atribucion PERSISTIDA nada se recalcula, asi que
-- el otro extremo no puede cambiar de opinion.
-- ─────────────────────────────────────────────────────────────────────────────
\echo ''
\echo '=== I8 — liquidar el CEDENTE no puede esconder kilos ==='
SAVEPOINT sp_i8;
-- Congelar de verdad: cabecera + las filas de la foto, que es lo que lee la rama CONGELADA de la fn.
INSERT INTO liquidacion_lote_engorde_congelada
    (lote_ave_engorde_id, company_id, granja_id, liquidado_at, liquidado_por_user_id, congelada_at,
     origen, fn_version, filas, checksum, lote_nombre, estado_operativo_lote, created_at)
SELECT DISTINCT p.cedente, p.company_id, p.granja_id, now(), 'gate', now(),
       'gate', 'v16b', 0, 'gate', 'gate', 'cerrado', now()
FROM _pares p;

SELECT count(*) AS pares,
       count(*) FILTER (WHERE abs(ap_ahora - ap_antes) <= 0.01) AS apertura_INTACTA,
       count(*) FILTER (WHERE abs(ap_ahora - ap_antes) >  0.01) AS APERTURA_QUE_CAMBIO,
       round(max(abs(ap_ahora - ap_antes))::numeric, 3) AS peor_delta
FROM (
    SELECT p.destino,
           COALESCE((SELECT sum(d.apertura_alimento_kg) FROM _despues d WHERE d.lote_id = p.destino), 0) AS ap_antes,
           COALESCE((SELECT sum(f.apertura_alimento_kg)
                       FROM fn_seguimiento_diario_engorde(p.destino) f), 0) AS ap_ahora
    FROM _pares p
) x;
ROLLBACK TO SAVEPOINT sp_i8;

\echo ''
\echo '=== I9 — liquidar el DESTINO no puede duplicar kilos ==='
SAVEPOINT sp_i9;
INSERT INTO liquidacion_lote_engorde_congelada
    (lote_ave_engorde_id, company_id, granja_id, liquidado_at, liquidado_por_user_id, congelada_at,
     origen, fn_version, filas, checksum, lote_nombre, estado_operativo_lote, created_at)
SELECT DISTINCT p.destino, p.company_id, p.granja_id, now(), 'gate', now(),
       'gate', 'v16b', 0, 'gate', 'gate', 'cerrado', now()
FROM _pares p;

SELECT count(*) AS pares,
       count(*) FILTER (WHERE abs(sal_ahora - sal_antes) <= 0.01) AS cedente_INTACTO,
       count(*) FILTER (WHERE abs(sal_ahora - sal_antes) >  0.01) AS CEDENTE_QUE_CAMBIO
FROM (
    SELECT p.cedente,
           COALESCE((SELECT sum(d.traslado_salida_kg) FROM _despues d WHERE d.lote_id = p.cedente), 0) AS sal_antes,
           COALESCE((SELECT sum(f.traslado_salida_kg)
                       FROM fn_seguimiento_diario_engorde(p.cedente) f), 0) AS sal_ahora
    FROM _pares p
) x;
ROLLBACK TO SAVEPOINT sp_i9;

\echo ''
\echo '=== I11 — anular el movimiento origen deja la entrega ANULADA, nunca borrada ==='
SAVEPOINT sp_i11;
UPDATE alimento_entrega_ciclo_engorde SET estado = 'ANULADA', kg_entregados = 0,
       anulada_motivo = 'gate I11' WHERE origen_tabla = 'gate_entrega';
SELECT count(*) AS filas_que_QUEDAN,
       count(*) FILTER (WHERE estado = 'ANULADA') AS anuladas,
       (SELECT count(*) FILTER (WHERE abs(COALESCE(descuadre_kg,0)) > 0.001)
          FROM fn_cuadre_alimento_engorde(NULL)) AS descuadrados_tras_anular
FROM alimento_entrega_ciclo_engorde WHERE origen_tabla = 'gate_entrega';
ROLLBACK TO SAVEPOINT sp_i11;

ROLLBACK;

\echo ''
\echo '=== RASTRO: tiene que quedar en 0 ==='
SELECT (SELECT count(*) FROM alimento_entrega_ciclo_engorde)                                   AS entregas,
       (SELECT count(*) FROM lote_registro_historico_unificado WHERE origen_tabla='gate_entrega') AS hist_inyectado,
       (SELECT count(*) FROM lote_registro_historico_unificado WHERE para_proximo_ciclo)        AS marcas;
