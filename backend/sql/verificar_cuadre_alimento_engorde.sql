-- =============================================================================
-- CUADRE DE ALIMENTO DE ENGORDE — el reporte canónico, con la causa de cada descuadre.
--
-- POR QUÉ EXISTE
-- `fn_cuadre_alimento_engorde` devuelve el invariante por galpón, pero la receta con la que se
-- venía consultando —`WHERE abs(descuadre_kg) > 1 OR filas_negativas > 0`— **mezcla dos señales
-- distintas** y da un número que asusta sin decir nada:
--
--   · `descuadre_kg`     = el saldo guardado no coincide con el stock. Son KILOS que faltan o sobran.
--   · `filas_negativas`  = hubo DÍAS que cerraron en rojo. El total puede estar perfecto: lo que
--                          está mal es el ORDEN o la FECHA de los ingresos (el «Patrón B» de V8).
--
-- Medido el 20-ago-2026 en ItalcolPanamá: esa consulta devolvía **23 galpones**, pero sólo **8**
-- tenían kilos de verdad. Los otros 15 entraban por `filas_negativas > 0` con un `descuadre_kg` del
-- orden de 1e-11 — ruido de coma flotante, o sea CERO. Reportar «23 descuadrados» cuando son 8 hace
-- perder el tiempo en 15 galpones que no tienen ningún kilo perdido.
--
-- Y sin la causa al lado, un descuadre no se puede accionar. Este script la resuelve para cada uno.
--
-- USO — el mismo comando las dos veces, sin flags:
--
--     psql ... -f backend/sql/verificar_cuadre_alimento_engorde.sql     <- congela la línea base
--     ... pasa el tiempo / se aplica un cambio ...
--     psql ... -f backend/sql/verificar_cuadre_alimento_engorde.sql     <- compara contra la base
--
-- Para empezar de cero:  DROP TABLE _cuadre_engorde_base;
--
-- SOLO LECTURA sobre datos de negocio: lo único que escribe es su propia tabla de línea base.
-- =============================================================================

\set ON_ERROR_STOP on

-- Tolerancia en kg. Es la misma de `CuadreAlimentoEngordeCalculos.ToleranciaKg`: por debajo de un
-- kilo no es un descuadre, es aritmética de punto flotante.
\set TOL 1

DROP TABLE IF EXISTS _cuadre_engorde_hoy;

CREATE TEMP TABLE _cuadre_engorde_hoy AS
WITH todas AS (
    SELECT * FROM fn_cuadre_alimento_engorde(NULL)
),
-- Ajustes y eliminaciones de stock POSTERIORES al último seguimiento del lote.
--
-- Son la causa #1 y están excluidos del cuadre A PROPÓSITO: `INV_OTRO` no entra ni al saldo
-- (`TipoEventoInventarioCalculos.AfectaSaldoAlimentoEngorde`) ni a `mov_post`, porque `AjusteStock`
-- guarda la cantidad en VALOR ABSOLUTO, sin el signo del delta ⇒ la fn no puede compensarlo aunque
-- quiera. El efecto: el ajuste mueve el stock del galpón, la tabla diaria del lote no se entera, y
-- el saldo guardado —que sólo se reescribe al guardar un seguimiento— queda desalineado para siempre.
otro_post AS (
    SELECT t.company_id, t.granja_id, t.galpon_id, t.lote_ave_engorde_id,
           COALESCE(SUM(h.cantidad_kg), 0)::FLOAT8 AS kg,
           COUNT(h.*)                              AS n
    FROM todas t
    LEFT JOIN lote_registro_historico_unificado h
           ON h.farm_id = t.granja_id
          AND COALESCE(TRIM(h.nucleo_id), '') = COALESCE(TRIM(t.nucleo_id), '')
          AND COALESCE(TRIM(h.galpon_id), '') = COALESCE(TRIM(t.galpon_id), '')
          AND h.tipo_evento = 'INV_OTRO'
          AND NOT h.anulado
          AND DATE(h.fecha_operacion) > t.ultimo_seguimiento
    GROUP BY 1,2,3,4
),
-- ¿Cuántos ciclos pasaron por este galpón? Un descuadre NO se resuelve al cerrar el lote: el stock
-- es del GALPÓN y el saldo es del CICLO ACTIVO, así que lo que sobra o falta se HEREDA al ciclo
-- siguiente. Medido: G0483 arrastró 23.300,0 kg del lote 187 al 190 (mismos kilos que V8 anotó
-- cuatro días antes para ese galpón, cuando el lote era otro).
ciclos AS (
    SELECT l.company_id, l.galpon_id, COUNT(*) AS lotes_en_galpon
    FROM lote_ave_engorde l
    WHERE l.deleted_at IS NULL AND COALESCE(TRIM(l.galpon_id), '') <> ''
    GROUP BY 1,2
)
SELECT t.company_id, t.empresa, t.granja, t.galpon_id,
       t.lote_ave_engorde_id AS lote, t.lote_nombre, t.ultimo_seguimiento,
       ROUND(t.descuadre_kg::numeric, 1)  AS descuadre_kg,
       t.filas_negativas,
       ROUND(o.kg::numeric, 1)            AS ajuste_post_kg,
       o.n                                AS ajuste_post_n,
       ROUND((t.descuadre_kg - o.kg)::numeric, 1) AS sin_explicar_kg,
       COALESCE(c.lotes_en_galpon, 1)     AS lotes_en_galpon,
       CASE
           WHEN ABS(t.descuadre_kg) <= :TOL AND t.filas_negativas > 0
               THEN 'solo dias en rojo (total cuadra: es orden/fecha de ingresos)'
           WHEN ABS(t.descuadre_kg) <= :TOL
               THEN 'cuadra'
           WHEN ABS(t.descuadre_kg - o.kg) <= :TOL
               THEN 'ajuste de stock posterior al ultimo seguimiento'
           WHEN COALESCE(c.lotes_en_galpon, 1) > 1
               THEN 'sin explicar — el galpon encadena ciclos: revisar herencia del anterior'
           ELSE 'sin explicar'
       END AS causa
FROM todas t
LEFT JOIN otro_post o
       ON o.company_id = t.company_id AND o.granja_id = t.granja_id
      AND o.galpon_id = t.galpon_id AND o.lote_ave_engorde_id = t.lote_ave_engorde_id
LEFT JOIN ciclos c
       ON c.company_id = t.company_id AND c.galpon_id = t.galpon_id;

\echo ''
\echo '=== 1) LO QUE IMPORTA: descuadres REALES (kilos), por empresa ==='
\echo '    Ojo: NO se mezcla con `filas_negativas`. Son dos problemas distintos.'
SELECT empresa,
       COUNT(*) FILTER (WHERE ABS(descuadre_kg) > :TOL)                        AS descuadrados,
       ROUND(SUM(ABS(descuadre_kg)) FILTER (WHERE ABS(descuadre_kg) > :TOL), 1) AS kg,
       COUNT(*) FILTER (WHERE ABS(descuadre_kg) <= :TOL AND filas_negativas > 0) AS solo_dias_en_rojo,
       COUNT(*)                                                                AS galpones
FROM _cuadre_engorde_hoy
GROUP BY empresa ORDER BY 2 DESC, empresa;

\echo ''
\echo '=== 2) CADA DESCUADRE REAL, CON SU CAUSA ==='
SELECT empresa, granja, galpon_id, lote, lote_nombre, ultimo_seguimiento,
       descuadre_kg, ajuste_post_kg, sin_explicar_kg, lotes_en_galpon, filas_negativas, causa
FROM _cuadre_engorde_hoy
WHERE ABS(descuadre_kg) > :TOL
ORDER BY ABS(descuadre_kg) DESC;

-- ── Línea base y comparación ─────────────────────────────────────────────────
DO $$
BEGIN
    IF to_regclass('public._cuadre_engorde_base') IS NULL THEN
        CREATE TABLE public._cuadre_engorde_base AS SELECT now() AS tomada_el, * FROM _cuadre_engorde_hoy;
        RAISE NOTICE 'Linea base creada (_cuadre_engorde_base). Volve a correr el script despues del cambio para ver el diff.';
    END IF;
END $$;

\echo ''
\echo '=== 3) DIFF CONTRA LA LINEA BASE — todo en 0 es lo esperado ==='
\echo '    Un galpon que APARECE o cuyo descuadre CRECE es una regresion, salvo justificacion escrita.'
SELECT COALESCE(h.empresa, b.empresa) AS empresa,
       COALESCE(h.galpon_id, b.galpon_id) AS galpon_id,
       COALESCE(h.lote, b.lote) AS lote,
       COALESCE(b.descuadre_kg, 0) AS antes,
       COALESCE(h.descuadre_kg, 0) AS ahora,
       ROUND((COALESCE(h.descuadre_kg,0) - COALESCE(b.descuadre_kg,0))::numeric, 1) AS delta,
       CASE WHEN b.galpon_id IS NULL THEN 'NUEVO'
            WHEN h.galpon_id IS NULL THEN 'desaparecio'
            WHEN COALESCE(b.lote,0) <> COALESCE(h.lote,0) THEN 'roto de ciclo (lote distinto)'
            ELSE 'cambio' END AS que_paso
FROM _cuadre_engorde_hoy h
FULL JOIN public._cuadre_engorde_base b
       ON b.company_id = h.company_id AND b.galpon_id = h.galpon_id
WHERE ABS(COALESCE(h.descuadre_kg,0) - COALESCE(b.descuadre_kg,0)) > :TOL
   OR (ABS(COALESCE(h.descuadre_kg,0)) > :TOL) <> (ABS(COALESCE(b.descuadre_kg,0)) > :TOL)
ORDER BY ABS(COALESCE(h.descuadre_kg,0) - COALESCE(b.descuadre_kg,0)) DESC;
