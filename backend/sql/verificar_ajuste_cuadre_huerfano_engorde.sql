-- =============================================================================
-- Ajustes de cuadre HUERFANOS de engorde: los que no tienen ciclo al que corregir.
--
-- SOLO LECTURA. No escribe nada. Se puede correr contra una copia de produccion.
--
-- QUE BUSCA. `EliminarStockAsync` y «Cuadrar galpon» escriben movimientos
-- `AjusteCuadreTablaEntrada`/`AjusteCuadreTablaSalida` para corregir la TABLA DIARIA sin tocar el
-- stock. El lote se lo pone el trigger via `fn_lote_ave_engorde_id_desde_ubicacion`, que exige
-- `deleted_at IS NULL` y `estado_operativo_lote <> 'Cerrado'`. Si el galpon se quedo sin lote vivo
-- —lo normal cuando alguien limpia el ciclo anterior y borra sus lotes— el movimiento nace con
-- `lote_ave_engorde_id IS NULL`: HUERFANO.
--
-- POR QUE IMPORTA (ticket de operacion 10-sep-2026, DOÑA MARIA / nucleo C / galpon 2, lote 257).
-- Hasta la v19 de `fn_seguimiento_diario_engorde`, la ventana de alimento previo al encaset se
-- cobraba esos huerfanos al ciclo SIGUIENTE. Medido: el 05-sep borraron los lotes 168/169 y, 55
-- segundos despues, el stock sobrante del galpon; el lote 257, encasetado al dia siguiente, abrio
-- en **-3.996,56 kg** y su tabla quedo exactamente esos kilos por debajo del stock.
--
--   Desde v19 los huerfanos son INERTES (ninguna de las 5 CTE los lee) y `EliminarStockAsync` ya no
--   los genera. Este verificador sirve para dos cosas:
--     1. Confirmar que no quedan huerfanos NUEVOS (la consulta 3 tiene que dar 0 en fechas
--        posteriores al despliegue de la v19).
--     2. Medir el pasivo historico: cuantos hay, de que galpones y cuantos kilos.
--
-- Consulta 4: el CONTROL de que la v19 funciona — para cada galpon con huerfanos, compara el saldo
-- de la tabla diaria del ciclo vivo contra su stock. Con la v19 aplicada la diferencia no puede
-- explicarse por los huerfanos.
-- =============================================================================
\set ON_ERROR_STOP on
\timing off

\echo '=== 1. Ajustes de cuadre por empresa: cuantos, cuantos huerfanos, cuantos kilos ==='
SELECT COALESCE(c.name, '(sin empresa)')                                  AS empresa,
       h.tipo_evento,
       COUNT(*)                                                            AS filas,
       COUNT(*) FILTER (WHERE h.lote_ave_engorde_id IS NULL)               AS huerfanos,
       ROUND(SUM(h.cantidad_kg)::numeric, 3)                               AS kg_total,
       ROUND(SUM(h.cantidad_kg) FILTER (WHERE h.lote_ave_engorde_id IS NULL)::numeric, 3)
                                                                           AS kg_huerfanos,
       MIN(h.fecha_operacion)::date                                        AS desde,
       MAX(h.fecha_operacion)::date                                        AS hasta
FROM lote_registro_historico_unificado h
LEFT JOIN companies c ON c.id = h.company_id
WHERE h.tipo_evento IN ('INV_AJUSTE_CUADRE_ENTRADA', 'INV_AJUSTE_CUADRE_SALIDA')
  AND NOT h.anulado
GROUP BY 1, 2
ORDER BY 1, 2;

\echo ''
\echo '=== 2. El detalle de los HUERFANOS, con el galpon y si ya hay un ciclo nuevo esperandolos ==='
SELECT c.name                                              AS empresa,
       f.name                                              AS granja,
       h.galpon_id,
       h.fecha_operacion::date                             AS fecha,
       h.tipo_evento,
       ROUND(h.cantidad_kg::numeric, 3)                    AS kg,
       -- El lote que HOY ocupa el galpon (el que se los habria comido antes de la v19).
       (SELECT l.lote_ave_engorde_id
          FROM lote_ave_engorde l
         WHERE l.granja_id = h.farm_id
           AND COALESCE(TRIM(l.nucleo_id), '') = COALESCE(TRIM(h.nucleo_id), '')
           AND COALESCE(TRIM(l.galpon_id), '') = COALESCE(TRIM(h.galpon_id), '')
           AND l.deleted_at IS NULL
         ORDER BY l.lote_ave_engorde_id DESC
         LIMIT 1)                                          AS lote_vivo_hoy,
       COALESCE(h.numero_documento, h.referencia, '')      AS documento
FROM lote_registro_historico_unificado h
JOIN farms     f ON f.id = h.farm_id
JOIN companies c ON c.id = h.company_id
WHERE h.tipo_evento IN ('INV_AJUSTE_CUADRE_ENTRADA', 'INV_AJUSTE_CUADRE_SALIDA')
  AND NOT h.anulado
  AND h.lote_ave_engorde_id IS NULL
ORDER BY c.name, f.name, h.galpon_id, h.fecha_operacion;

\echo ''
\echo '=== 3. GATE: huerfanos NUEVOS (posteriores al despliegue de la v19). Tiene que salir VACIA ==='
\echo '    Ajustar la fecha al dia del despliegue antes de usarla como gate.'
SELECT h.id, h.fecha_operacion::date, h.farm_id, h.galpon_id,
       ROUND(h.cantidad_kg::numeric, 3) AS kg, h.tipo_evento
FROM lote_registro_historico_unificado h
WHERE h.tipo_evento IN ('INV_AJUSTE_CUADRE_ENTRADA', 'INV_AJUSTE_CUADRE_SALIDA')
  AND NOT h.anulado
  AND h.lote_ave_engorde_id IS NULL
  AND h.fecha_operacion::date > DATE '2026-09-10'
ORDER BY h.fecha_operacion, h.id;

\echo ''
\echo '=== 4. CONTROL: en los galpones con huerfanos, el ciclo vivo cuadra contra su stock? ==='
\echo '    `dif_kg` deja de contener los kilos huerfanos desde la v19.'
WITH galpones_con_huerfanos AS (
    SELECT DISTINCT h.farm_id, COALESCE(TRIM(h.nucleo_id), '') AS nucleo_id,
                    COALESCE(TRIM(h.galpon_id), '') AS galpon_id,
                    SUM(h.cantidad_kg) OVER (PARTITION BY h.farm_id, h.nucleo_id, h.galpon_id) AS kg_huerfanos
    FROM lote_registro_historico_unificado h
    WHERE h.tipo_evento IN ('INV_AJUSTE_CUADRE_ENTRADA', 'INV_AJUSTE_CUADRE_SALIDA')
      AND NOT h.anulado
      AND h.lote_ave_engorde_id IS NULL
),
lote_vivo AS (
    SELECT g.*, (SELECT l.lote_ave_engorde_id
                   FROM lote_ave_engorde l
                  WHERE l.granja_id = g.farm_id
                    AND COALESCE(TRIM(l.nucleo_id), '') = g.nucleo_id
                    AND COALESCE(TRIM(l.galpon_id), '') = g.galpon_id
                    AND l.deleted_at IS NULL
                  ORDER BY l.lote_ave_engorde_id DESC
                  LIMIT 1) AS lote
    FROM galpones_con_huerfanos g
)
SELECT f.name AS granja, v.galpon_id, v.lote,
       ROUND(v.kg_huerfanos::numeric, 3)                            AS kg_huerfanos,
       ROUND(t.saldo::numeric, 3)                                   AS saldo_tabla,
       ROUND(s.stock::numeric, 3)                                   AS stock,
       ROUND((s.stock - t.saldo)::numeric, 3)                       AS dif_kg
FROM lote_vivo v
JOIN farms f ON f.id = v.farm_id
LEFT JOIN LATERAL (
    SELECT d.saldo_alimento_kg AS saldo
    FROM fn_seguimiento_diario_engorde(v.lote) d
    ORDER BY d.fecha DESC, COALESCE(d.seg_id, 0) DESC
    LIMIT 1
) t ON v.lote IS NOT NULL
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(st.quantity), 0) AS stock
    FROM inventario_gestion_stock st
    WHERE st.farm_id = v.farm_id
      AND COALESCE(TRIM(st.nucleo_id), '') = v.nucleo_id
      AND COALESCE(TRIM(st.galpon_id), '') = v.galpon_id
) s ON TRUE
ORDER BY f.name, v.galpon_id;
