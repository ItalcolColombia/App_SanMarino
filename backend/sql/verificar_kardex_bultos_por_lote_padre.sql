-- V25.7.6 — el número fino del kardex de bultos por lote padre (insumo de la decisión V19.2.1).
-- Reproduce la query del reporte Contable (sección BULTO) tal como la arman
-- ReporteContableService.ObtenerDatosBultosUnificadoAsync + ReporteContableBultosCalculos.AcumularSaldos.
-- SOLO LECTURA: no escribe ni una fila de negocio (SELECT + tablas TEMP de la propia sesión).
--
-- Uso:
--   psql ... -f backend/sql/verificar_kardex_bultos_por_lote_padre.sql
--
-- Parámetros: el CTE `par` de abajo (empresa, fecha de corte, kg por bulto). Cambiar `empresa` para
-- medir otro tenant; la rama unificada (`inventario_gestion_movimiento`) sólo aplica a las empresas
-- con `companies.reportes_alimento_desde_inventario_unificado = true`. Para las demás el reporte lee
-- `farm_inventory_movements` y este script NO las mide.
--
-- Devuelve tres saldos sobre el MISMO algoritmo, para poder compararlos:
--   saldo_hoy  lo que muestra el reporte: entradas de la GRANJA − consumo de ESTE lote padre
--   saldo_a    opción (a) de V19.2.1: entradas de la granja − consumo de TODOS los lotes de la granja
--   saldo_inv  sólo el kardex de inventario (entradas − traslados − retiros), sin restar el consumo
--              del seguimiento por segunda vez. Ver V40.6: los movimientos `Consumo` del inventario
--              los ESCRIBE el propio seguimiento diario, así que el saldo de hoy los cuenta dos veces.

\set ON_ERROR_STOP on

DROP TABLE IF EXISTS tmp_v2576_deltas;
DROP TABLE IF EXISTS tmp_v2576_res;

CREATE TEMP TABLE tmp_v2576_deltas AS
WITH par AS (
    SELECT 1 AS empresa, DATE '2026-08-18' AS hoy, 40.0::numeric AS factor
),
padres AS (
    SELECT l.lote_id AS padre_id, l.lote_nombre, l.granja_id
    FROM lotes l, par
    WHERE l.company_id = par.empresa AND l.deleted_at IS NULL AND l.lote_padre_id IS NULL
),
granja_padres AS (
    SELECT granja_id, count(*) AS n_padres FROM padres GROUP BY granja_id
),
-- Lotes que el reporte consolida bajo cada padre: el padre + sus sublotes.
lotes_de_padre AS (
    SELECT p.padre_id, p.granja_id, l.lote_id, l.fecha_encaset
    FROM padres p
    JOIN lotes l ON (l.lote_id = p.padre_id OR l.lote_padre_id = p.padre_id)
    CROSS JOIN par
    WHERE l.company_id = par.empresa AND l.deleted_at IS NULL
),
-- Todos los lotes de la GRANJA: el universo que mira la opción (a).
lotes_de_granja AS (
    SELECT DISTINCT lp.granja_id, lp.lote_id FROM lotes_de_padre lp
),
-- Ventana [primera llegada - dias_alimento_previo_encaset, hoy] (ReporteContableBultosCalculos.Ventana).
dias_previos AS (
    SELECT COALESCE(MIN(c.dias_alimento_previo_encaset), 10) AS dias
    FROM companies c, par WHERE c.id = par.empresa
),
ventana AS (
    SELECT lp.padre_id,
           lp.granja_id,
           COALESCE(MIN(lp.fecha_encaset)::date, par.hoy) - dp.dias AS desde,
           par.hoy AS hasta
    FROM lotes_de_padre lp CROSS JOIN par CROSS JOIN dias_previos dp
    GROUP BY lp.padre_id, lp.granja_id, par.hoy, dp.dias
),
-- Kardex de la GRANJA por día, en bultos: mismo filtro y misma clasificación que el service.
mov_granja AS (
    SELECT m.farm_id,
           (m.created_at AT TIME ZONE 'UTC')::date AS fecha,
           SUM(CASE WHEN m.movement_type IN ('Ingreso','TrasladoEntrada','TrasladoInterGranjaEntrada')
                    THEN (CASE WHEN lower(btrim(m.unit)) IN ('bultos','bulto') THEN m.quantity ELSE m.quantity / par.factor END)
                    ELSE 0 END) AS entradas,
           SUM(CASE WHEN m.movement_type IN ('TrasladoSalida','TrasladoInterGranjaSalida','TrasladoInterGranjaPendiente')
                    THEN (CASE WHEN lower(btrim(m.unit)) IN ('bultos','bulto') THEN m.quantity ELSE m.quantity / par.factor END)
                    ELSE 0 END) AS traslados,
           SUM(CASE WHEN m.movement_type = 'Consumo'
                    THEN (CASE WHEN lower(btrim(m.unit)) IN ('bultos','bulto') THEN m.quantity ELSE m.quantity / par.factor END)
                    ELSE 0 END) AS retiros
    FROM inventario_gestion_movimiento m
    JOIN item_inventario_ecuador i ON i.id = m.item_inventario_ecuador_id
    CROSS JOIN par
    WHERE m.company_id = par.empresa
      AND lower(btrim(i.tipo_item)) = 'alimento'
      AND i.activo
    GROUP BY m.farm_id, (m.created_at AT TIME ZONE 'UTC')::date
),
-- Consumo diario por LOTE, en bultos: las dos fuentes que lee el reporte.
consumo_lote AS (
    SELECT s.lote_id::int AS lote_id, s.fecha::date AS fecha,
           (COALESCE(s.consumo_kg_hembras,0) + COALESCE(s.consumo_kg_machos,0)) / par.factor AS bultos
    FROM seguimiento_diario_levante s CROSS JOIN par
    WHERE s.tipo_seguimiento = 'levante' AND s.lote_id ~ '^[0-9]+$'
    UNION ALL
    SELECT p.lote_id, p.fecha_registro::date,
           (COALESCE(p.cons_kg_h,0) + COALESCE(p.cons_kg_m,0))::numeric / par.factor
    FROM seguimiento_diario_produccion p CROSS JOIN par
),
-- Lo que el reporte imputa HOY: sólo los lotes de ESTE padre.
consumo_propio AS (
    SELECT lp.padre_id, cl.fecha, SUM(cl.bultos) AS bultos
    FROM lotes_de_padre lp JOIN consumo_lote cl ON cl.lote_id = lp.lote_id
    GROUP BY lp.padre_id, cl.fecha
),
-- Lo que restaría la opción (a): todos los lotes de la granja.
consumo_granja AS (
    SELECT lg.granja_id, cl.fecha, SUM(cl.bultos) AS bultos
    FROM lotes_de_granja lg JOIN consumo_lote cl ON cl.lote_id = lg.lote_id
    GROUP BY lg.granja_id, cl.fecha
),
-- Fechas con fila en el reporte HOY: las que traen dato de algún lote del padre, más las que sólo
-- traen movimiento de granja dentro de la ventana (las filas "solo bultos" de C1).
fechas_hoy AS (
    SELECT v.padre_id, v.granja_id, cp.fecha
    FROM ventana v JOIN consumo_propio cp ON cp.padre_id = v.padre_id
    WHERE cp.fecha BETWEEN v.desde AND v.hasta
    UNION
    SELECT v.padre_id, v.granja_id, mg.fecha
    FROM ventana v JOIN mov_granja mg ON mg.farm_id = v.granja_id
    WHERE mg.fecha BETWEEN v.desde AND v.hasta
      AND (mg.entradas <> 0 OR mg.traslados <> 0 OR mg.retiros <> 0)
),
-- Para la opción (a) el saldo tiene que restar TODO el consumo de la granja, también el de los días
-- en que este padre no tiene fila: si no, el número no cuadra contra el inventario, que es
-- justamente lo que la opción (a) promete.
fechas AS (
    SELECT padre_id, granja_id, fecha, true AS en_reporte_hoy FROM fechas_hoy
    UNION
    SELECT v.padre_id, v.granja_id, cg.fecha, false
    FROM ventana v JOIN consumo_granja cg ON cg.granja_id = v.granja_id
    WHERE cg.fecha BETWEEN v.desde AND v.hasta
      AND NOT EXISTS (SELECT 1 FROM fechas_hoy fh
                      WHERE fh.padre_id = v.padre_id AND fh.fecha = cg.fecha)
)
SELECT f.padre_id,
       p.lote_nombre,
       f.granja_id,
       fa.name AS granja,
       gp.n_padres,
       f.fecha,
       f.en_reporte_hoy,
       COALESCE(mg.entradas,0)::numeric  AS entradas,
       COALESCE(mg.traslados,0)::numeric AS traslados,
       COALESCE(mg.retiros,0)::numeric   AS retiros,
       COALESCE(cp.bultos,0)::numeric    AS consumo_propio,
       COALESCE(cg.bultos,0)::numeric    AS consumo_granja
FROM fechas f
JOIN padres p         ON p.padre_id = f.padre_id
JOIN farms fa         ON fa.id = f.granja_id
JOIN granja_padres gp ON gp.granja_id = f.granja_id
LEFT JOIN mov_granja mg     ON mg.farm_id  = f.granja_id AND mg.fecha = f.fecha
LEFT JOIN consumo_propio cp ON cp.padre_id = f.padre_id  AND cp.fecha = f.fecha
LEFT JOIN consumo_granja cg ON cg.granja_id = f.granja_id AND cg.fecha = f.fecha;

CREATE TEMP TABLE tmp_v2576_res(
    padre_id int, lote_nombre text, granja text, n_padres int,
    filas int, desde date, hasta date,
    entradas numeric, retiros numeric, consumo_propio numeric, consumo_ajeno numeric,
    saldo_hoy numeric, saldo_opcion_a numeric, saldo_sin_doble numeric);

-- Acumulación cronológica, igual que AcumularSaldos: el acumulado se reinicia al saldo (ya recortado
-- a 0) del día calendario anterior SOLO si ese día tuvo filas; con hueco de fechas sigue corriendo
-- sin recortar. El recorte a 0 se aplica al saldo que sale, no al acumulado.
-- Tres escenarios sobre el MISMO algoritmo:
--   'hoy' = lo que muestra el reporte: entradas de la granja - consumo de ESTE padre
--   'a'   = opcion (a) de V19.2.1: entradas de la granja - consumo de TODOS los lotes de la granja
--   'inv' = sin doble conteo: solo el kardex de inventario (entradas - traslados - retiros)
CREATE OR REPLACE FUNCTION pg_temp.v2576_saldo(p_padre int, p_modo text)
RETURNS numeric LANGUAGE plpgsql AS $fn$
DECLARE
    d record;
    acum numeric := 0;
    fecha_prev date := NULL;
    saldos jsonb := '{}'::jsonb;
    consumo numeric;
BEGIN
    FOR d IN SELECT * FROM tmp_v2576_deltas
             WHERE padre_id = p_padre AND (p_modo = 'a' OR en_reporte_hoy)
             ORDER BY fecha
    LOOP
        IF fecha_prev IS NULL OR fecha_prev <> d.fecha THEN
            IF fecha_prev IS NOT NULL THEN
                saldos := jsonb_set(saldos, ARRAY[fecha_prev::text], to_jsonb(GREATEST(0, acum)));
            END IF;
            IF saldos ? (d.fecha - 1)::text THEN
                acum := (saldos ->> (d.fecha - 1)::text)::numeric;
            END IF;
            fecha_prev := d.fecha;
        END IF;

        consumo := CASE p_modo WHEN 'hoy' THEN d.consumo_propio
                               WHEN 'a'   THEN d.consumo_granja
                               ELSE 0 END;
        acum := acum + d.entradas - d.traslados - d.retiros - consumo;
    END LOOP;

    RETURN GREATEST(0, acum);
END
$fn$;

INSERT INTO tmp_v2576_res
SELECT d.padre_id, d.lote_nombre, d.granja, d.n_padres,
       count(*) FILTER (WHERE d.en_reporte_hoy)::int,
       min(d.fecha), max(d.fecha),
       round(sum(d.entradas) FILTER (WHERE d.en_reporte_hoy),1),
       round(sum(d.retiros)  FILTER (WHERE d.en_reporte_hoy),1),
       round(sum(d.consumo_propio),1),
       round(sum(d.consumo_granja) - sum(d.consumo_propio),1),
       round(pg_temp.v2576_saldo(d.padre_id, 'hoy'),1),
       round(pg_temp.v2576_saldo(d.padre_id, 'a'),1),
       round(pg_temp.v2576_saldo(d.padre_id, 'inv'),1)
FROM tmp_v2576_deltas d
GROUP BY d.padre_id, d.lote_nombre, d.granja, d.n_padres;

\echo ''
\echo '=== V25.7.6 - saldo de BULTOS por lote padre: hoy vs opcion (a) vs sin doble conteo ==='
SELECT granja, lote_nombre, padre_id AS id, n_padres AS padres, filas,
       entradas, retiros, consumo_propio AS cons_propio, consumo_ajeno AS cons_ajeno,
       saldo_hoy, saldo_opcion_a AS saldo_a, saldo_sin_doble AS saldo_inv,
       round(saldo_hoy - saldo_opcion_a,1)   AS delta_a,
       round(saldo_hoy - saldo_sin_doble,1)  AS delta_inv
FROM tmp_v2576_res ORDER BY granja, lote_nombre, padre_id;

\echo ''
\echo '=== Por granja: sumar los reportes es el dano concreto ==='
SELECT granja, n_padres AS padres,
       round(sum(saldo_hoy),1)       AS suma_saldos_hoy,
       round(max(saldo_opcion_a),1)  AS saldo_granja_a,
       round(max(saldo_sin_doble),1) AS saldo_granja_inv,
       round(sum(saldo_hoy - saldo_opcion_a),1)  AS delta_a_total,
       round(sum(saldo_hoy - saldo_sin_doble),1) AS delta_inv_total
FROM tmp_v2576_res GROUP BY granja, n_padres ORDER BY granja;

\echo ''
\echo '=== El doble conteo: el Consumo del inventario LO ESCRIBE el seguimiento ==='
SELECT f.name AS granja, count(*) AS movs_consumo,
       round(sum(m.quantity),1) AS kg_en_inventario,
       left(min(m.reference), 40) AS ejemplo_referencia
FROM inventario_gestion_movimiento m
JOIN item_inventario_ecuador i ON i.id = m.item_inventario_ecuador_id
JOIN farms f ON f.id = m.farm_id
WHERE m.company_id = 1 AND lower(btrim(i.tipo_item)) = 'alimento' AND i.activo
  AND m.movement_type = 'Consumo'
GROUP BY f.name ORDER BY f.name;
