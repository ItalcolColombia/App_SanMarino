-- =============================================================================
-- ENSAYO del barrido de cuadre de ItalcolPanama, en transaccion que se REVIERTE.
--
-- Simula lo que escribiria "Cuadrar galpon" en los 6 galpones cuya causa es
-- "alguien corrigio el inventario a mano y la tabla diaria nunca se entero", y
-- verifica lo unico que importa antes de tocar produccion:
--
--   1) que los 6 queden en descuadre 0,0;
--   2) que NINGUN galpon GANE dias en rojo (el efecto colateral que este ensayo
--      ya cazo una vez: barrer G0495 lo dejaba en saldo -2.607,7).
--
-- Plan: fase_de_desarrollo/barrido_cuadre_panama_plan.md
--
-- ⚠️ ESCRIBE, pero todo va dentro de un BEGIN que termina en ROLLBACK. Es para la
-- copia local; correrlo contra produccion no tiene sentido y toma bloqueos reales.
--
-- Medido el 25-ago-2026 sobre la copia de produccion:
--   Panama 12 -> 6 descuadrados, 55.866,5 -> 15.289,1 kg (crudo), dias en rojo 16 -> 16.
-- =============================================================================
\set ON_ERROR_STOP on
\timing off

DROP TABLE IF EXISTS _barrido_antes;
CREATE TEMP TABLE _barrido_antes AS
SELECT galpon_id, descuadre_kg, filas_negativas FROM fn_cuadre_alimento_engorde(5);

BEGIN;

-- Los 6 SEGUROS. G0495 queda fuera a proposito (objetivo negativo: imposible),
-- G0461 y G0460 por ciclo recien arrancado / fecha, G0463 y G0492 por reservas.
INSERT INTO inventario_gestion_movimiento
    (company_id, pais_id, farm_id, nucleo_id, galpon_id, item_inventario_ecuador_id,
     quantity, unit, movement_type, estado, reference, reason, created_at, created_by_user_id)
SELECT c.company_id, s.pais_id, c.granja_id, c.nucleo_id, c.galpon_id, s.item_inventario_ecuador_id,
       ROUND(ABS(c.saldo_tabla_kg - (c.stock_kg - c.mov_post_kg))::numeric, 3), 'kg',
       CASE WHEN (c.stock_kg - c.mov_post_kg) - c.saldo_tabla_kg > 0
            THEN 'AjusteCuadreTablaEntrada' ELSE 'AjusteCuadreTablaSalida' END,
       'Ajuste de cuadre', 'Ajuste de cuadre',
       'Barrido Panama: el inventario ya corregido a mano es el que manda',
       (c.ultimo_seguimiento + TIME '12:00')::timestamptz, NULL
FROM fn_cuadre_alimento_engorde(5) c
JOIN LATERAL (SELECT s2.item_inventario_ecuador_id, s2.pais_id
              FROM inventario_gestion_stock s2
              WHERE s2.farm_id = c.granja_id
                AND TRIM(COALESCE(s2.nucleo_id,'')) = c.nucleo_id
                AND TRIM(COALESCE(s2.galpon_id,'')) = c.galpon_id
              ORDER BY s2.quantity DESC LIMIT 1) s ON TRUE
WHERE c.galpon_id IN ('G0475','G0483','G0491','G0477','G0476','G0496');

\echo ''
\echo '=== CAMBIOS (ningun galpon puede GANAR dias en rojo) ==='
SELECT a.galpon_id,
       ROUND(a.descuadre_kg::numeric,1) desc_antes,
       ROUND(d.descuadre_kg::numeric,1) desc_despues,
       a.filas_negativas rojos_antes, d.filas_negativas rojos_despues,
       CASE WHEN d.filas_negativas > a.filas_negativas
            THEN '*** GANO DIAS EN ROJO -- PARAR ***' ELSE 'ok' END alerta
FROM _barrido_antes a
JOIN fn_cuadre_alimento_engorde(5) d ON d.galpon_id = a.galpon_id
WHERE a.descuadre_kg IS DISTINCT FROM d.descuadre_kg
   OR a.filas_negativas IS DISTINCT FROM d.filas_negativas
ORDER BY a.galpon_id;

\echo ''
\echo '=== RESUMEN antes -> despues ==='
SELECT (SELECT COUNT(*) FROM _barrido_antes WHERE ABS(descuadre_kg)>1) desc_antes,
       (SELECT ROUND(SUM(ABS(descuadre_kg))::numeric,1) FROM _barrido_antes WHERE ABS(descuadre_kg)>1) kg_antes,
       (SELECT COUNT(*) FROM fn_cuadre_alimento_engorde(5) WHERE ABS(descuadre_kg)>1) desc_despues,
       (SELECT ROUND(SUM(ABS(descuadre_kg))::numeric,1) FROM fn_cuadre_alimento_engorde(5) WHERE ABS(descuadre_kg)>1) kg_despues,
       (SELECT COUNT(*) FROM _barrido_antes WHERE filas_negativas>0) rojos_antes,
       (SELECT COUNT(*) FROM fn_cuadre_alimento_engorde(5) WHERE filas_negativas>0) rojos_despues;

ROLLBACK;

\echo ''
\echo '=== CONTROL: por que G0495 NO entra (objetivo negativo = imposible) ==='
SELECT galpon_id,
       ROUND(stock_kg::numeric,1) stock,
       ROUND(mov_post_kg::numeric,1) mov_post,
       ROUND((stock_kg - mov_post_kg)::numeric,1) saldo_objetivo
FROM fn_cuadre_alimento_engorde(5)
WHERE galpon_id IN ('G0495','G0461','G0460');
