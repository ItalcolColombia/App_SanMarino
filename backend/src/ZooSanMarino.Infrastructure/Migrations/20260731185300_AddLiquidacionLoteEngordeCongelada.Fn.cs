// Partial de la migración AddLiquidacionLoteEngordeCongelada: SOLO las dos constantes SQL de
// fn_seguimiento_diario_engorde (v13 para Up, v12 para Down). Viven separadas para que el archivo
// principal se pueda leer; el cuerpo v12 está copiado VERBATIM de backend/sql (ni una línea tocada).

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    public partial class AddLiquidacionLoteEngordeCongelada
    {
        /// <summary>
        /// v13 — SIGUE siendo LANGUAGE sql (medido: la fn SÍ se inlinea en los LATERAL de los
        /// reportes; una variante plpgsql la convertía en Function Scan sin inline y multiplicaba
        /// ×2.8 el Reporte de Costos). La conmutación es un UNION ALL con quals excluyentes:
        ///   - rama congelada: lee liquidacion_lote_engorde_congelada_fila si hay copia VIGENTE;
        ///   - rama viva: el cuerpo v12 VERBATIM como subconsulta, gateado por NOT EXISTS —
        ///     el planner lo ejecuta con One-Time Filter (verificado en EXPLAIN), así que con
        ///     copia vigente el cuerpo vivo NO se ejecuta.
        /// El ORDER BY exterior (fecha, COALESCE(seg_id,0)) reproduce exactamente el orden v12 y,
        /// por construcción de `orden` (row_number sobre ese mismo ORDER BY), también el de la copia.
        /// </summary>
        private const string FnSeguimientoDiarioEngordeV13 = """
-- =============================================================================
-- v13 (2026-07-31) — Liquidación CONGELADA: si el lote tiene copia vigente en
--   liquidacion_lote_engorde_congelada, la fn devuelve ESA copia (tabla
--   liquidacion_lote_engorde_congelada_fila) en vez de recalcular. Un lote
--   liquidado deja de moverse cuando cambia la fórmula o llegan movimientos
--   tardíos; reabrirlo anula la copia y vuelve el cálculo en vivo.
--   Se conserva LANGUAGE sql a propósito (inlining en los LATERAL de los
--   reportes); el cuerpo v12 queda VERBATIM como rama del UNION ALL.
-- =============================================================================
CREATE OR REPLACE FUNCTION fn_seguimiento_diario_engorde(p_lote_id INT)
RETURNS TABLE (
    -- Identificación
    seg_id                      BIGINT,
    fecha                       DATE,
    -- Tiempo
    edad_dia                    INT,
    semana                      SMALLINT,
    -- Seguimiento crudo
    mortalidad_hembras          INT,
    mortalidad_machos           INT,
    sel_h                       INT,
    sel_m                       INT,
    error_sexaje_hembras        INT,
    error_sexaje_machos         INT,
    -- Calculados simples
    total_mort_sel_dia          INT,
    perdidas_totales_dia        INT,
    consumo_kg_hembras          DOUBLE PRECISION,
    consumo_kg_machos           DOUBLE PRECISION,
    consumo_dia_kg              DOUBLE PRECISION,
    -- Acumulados corrientes (window functions)
    acum_consumo_kg             DOUBLE PRECISION,
    saldo_aves                  INT,
    pct_perdidas_dia            DOUBLE PRECISION,
    -- Saldo alimento persistido por RecalcularSaldoAlimentoPorLoteAsync
    saldo_alimento_kg           DOUBLE PRECISION,
    -- Histórico agregado por fecha
    ingreso_alimento_kg         DOUBLE PRECISION,
    traslado_entrada_kg         DOUBLE PRECISION,
    traslado_salida_kg          DOUBLE PRECISION,
    consumo_bodega_kg           DOUBLE PRECISION,
    -- Documento: numeroDocumento || referencia de INV_INGRESO + VENTA_AVES
    documento                   TEXT,
    despacho_hembras            INT,
    despacho_machos             INT,
    despacho_mixtas             INT,
    -- Peso INDIVIDUAL real de la venta de ESTE lote en la fecha (R3.5), no el global de factura
    despacho_peso_neto          DOUBLE PRECISION,
    despacho_peso_tara          DOUBLE PRECISION,
    despacho_promedio_peso_ave  DOUBLE PRECISION,
    -- Mediciones del seguimiento
    tipo_alimento               TEXT,
    peso_prom_hembras           DOUBLE PRECISION,
    peso_prom_machos            DOUBLE PRECISION,
    uniformidad_hembras         DOUBLE PRECISION,
    uniformidad_machos          DOUBLE PRECISION,
    cv_hembras                  DOUBLE PRECISION,
    cv_machos                   DOUBLE PRECISION,
    consumo_agua_diario         DOUBLE PRECISION,
    consumo_agua_ph             DOUBLE PRECISION,
    consumo_agua_orp            DOUBLE PRECISION,
    consumo_agua_temperatura    DOUBLE PRECISION,
    observaciones               TEXT,
    ciclo                       TEXT,
    metadata                    JSONB,
    items_adicionales           JSONB,
    historico_consumo_alimento  JSONB,
    created_by_user_id          TEXT
) LANGUAGE sql STABLE AS $$
SELECT u.seg_id, u.fecha, u.edad_dia, u.semana,
       u.mortalidad_hembras, u.mortalidad_machos, u.sel_h, u.sel_m,
       u.error_sexaje_hembras, u.error_sexaje_machos,
       u.total_mort_sel_dia, u.perdidas_totales_dia,
       u.consumo_kg_hembras, u.consumo_kg_machos, u.consumo_dia_kg,
       u.acum_consumo_kg, u.saldo_aves, u.pct_perdidas_dia, u.saldo_alimento_kg,
       u.ingreso_alimento_kg, u.traslado_entrada_kg, u.traslado_salida_kg, u.consumo_bodega_kg,
       u.documento, u.despacho_hembras, u.despacho_machos, u.despacho_mixtas,
       u.despacho_peso_neto, u.despacho_peso_tara, u.despacho_promedio_peso_ave,
       u.tipo_alimento, u.peso_prom_hembras, u.peso_prom_machos,
       u.uniformidad_hembras, u.uniformidad_machos, u.cv_hembras, u.cv_machos,
       u.consumo_agua_diario, u.consumo_agua_ph, u.consumo_agua_orp, u.consumo_agua_temperatura,
       u.observaciones, u.ciclo, u.metadata, u.items_adicionales, u.historico_consumo_alimento,
       u.created_by_user_id
FROM (
    SELECT f.seg_id, f.fecha, f.edad_dia, f.semana,
           f.mortalidad_hembras, f.mortalidad_machos, f.sel_h, f.sel_m,
           f.error_sexaje_hembras, f.error_sexaje_machos,
           f.total_mort_sel_dia, f.perdidas_totales_dia,
           f.consumo_kg_hembras, f.consumo_kg_machos, f.consumo_dia_kg,
           f.acum_consumo_kg, f.saldo_aves, f.pct_perdidas_dia, f.saldo_alimento_kg,
           f.ingreso_alimento_kg, f.traslado_entrada_kg, f.traslado_salida_kg, f.consumo_bodega_kg,
           f.documento, f.despacho_hembras, f.despacho_machos, f.despacho_mixtas,
           f.despacho_peso_neto, f.despacho_peso_tara, f.despacho_promedio_peso_ave,
           f.tipo_alimento, f.peso_prom_hembras, f.peso_prom_machos,
           f.uniformidad_hembras, f.uniformidad_machos, f.cv_hembras, f.cv_machos,
           f.consumo_agua_diario, f.consumo_agua_ph, f.consumo_agua_orp, f.consumo_agua_temperatura,
           f.observaciones, f.ciclo, f.metadata, f.items_adicionales, f.historico_consumo_alimento,
           f.created_by_user_id
      FROM liquidacion_lote_engorde_congelada_fila f
     WHERE f.liquidacion_id = (SELECT c.id
                                 FROM liquidacion_lote_engorde_congelada c
                                WHERE c.lote_ave_engorde_id = p_lote_id
                                  AND c.anulada_at IS NULL)
    UNION ALL
    SELECT t.*
      FROM (

WITH

-- 1. Datos clave del lote
lote_info AS (
    SELECT
        l.granja_id,
        COALESCE(TRIM(l.nucleo_id), '')  AS nucleo_id,
        COALESCE(TRIM(l.galpon_id), '')  AS galpon_id,
        l.fecha_encaset,
        COALESCE(l.aves_encasetadas, 0)  AS aves_encasetadas,
        COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0) AS suma_hm,
        LOWER(COALESCE(l.estado_operativo_lote, '')) AS estado_operativo_lote,
        -- ⭐ v8: mortalidad en caja (llegada), capturada fuera del seguimiento diario.
        COALESCE(l.mort_caja_h, 0) + COALESCE(l.mort_caja_m, 0) AS mort_caja_total,
        -- ⭐ v9: el alimento cuenta desde esta fecha, no desde el encaset. En engorde el preiniciador
        -- llega antes que los pollitos; cortar en el encaset dejaba fuera alimento propio del lote.
        -- La ventana la configura la empresa (default 10 días, tope 30 = VentanaAlimentoPrevioCalculos).
        (l.fecha_encaset::DATE - LEAST(30, GREATEST(0, COALESCE(c.dias_alimento_previo_encaset, 10)))) AS fecha_corte_alimento
    FROM lote_ave_engorde l
    LEFT JOIN companies c ON c.id = l.company_id
    WHERE l.lote_ave_engorde_id = p_lote_id
      AND l.deleted_at IS NULL
),

-- 2. Rango base del ciclo: primer y último seguimiento + estado
rango_seg AS (
    SELECT
        MIN(s.fecha)::DATE AS fecha_min,
        MAX(s.fecha)::DATE AS last_seg,
        (SELECT LOWER(COALESCE(estado_operativo_lote, ''))
         FROM lote_ave_engorde
         WHERE lote_ave_engorde_id = p_lote_id AND deleted_at IS NULL) AS estado
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 2d. ⭐ v12: día en que arranca de verdad la ventana de apertura.
--     La ventana previa al encaset (v9) no puede meterse en el ciclo anterior: nada anterior al
--     último día de seguimiento del lote que ocupaba el galpón antes que yo es alimento mío.
--     Complementa a `lotes_ajenos` (v11), que solo caza la limpieza etiquetada con el lote VIEJO;
--     ésta caza la que quedó etiquetada con el lote NUEVO (ver cabecera v12).
corte_apertura AS (
    SELECT GREATEST(
               li.fecha_corte_alimento,
               COALESCE(
                   (SELECT MAX(DATE(s2.fecha)) + 1
                      FROM seguimiento_diario_aves_engorde s2
                      JOIN lote_ave_engorde l2 ON l2.lote_ave_engorde_id = s2.lote_ave_engorde_id
                                              AND l2.deleted_at IS NULL
                     WHERE l2.granja_id = li.granja_id
                       AND COALESCE(TRIM(l2.nucleo_id), '') = li.nucleo_id
                       AND COALESCE(TRIM(l2.galpon_id), '') = li.galpon_id
                       AND l2.lote_ave_engorde_id <> p_lote_id
                       -- solo ciclos que YA habían cerrado cuando este empezó
                       AND (SELECT MAX(DATE(s3.fecha))
                              FROM seguimiento_diario_aves_engorde s3
                             WHERE s3.lote_ave_engorde_id = l2.lote_ave_engorde_id) < rs.fecha_min),
                   li.fecha_corte_alimento)
           ) AS desde
    FROM lote_info li, rango_seg rs
    WHERE rs.fecha_min IS NOT NULL
),

-- 2c. ⭐ v11: lotes AJENOS = otros lotes del mismo galpón cuyo ciclo NO se solapa con el mío.
--     Es el COMPLEMENTO exacto del predicado de `consumo_galpon_por_fecha` (v10): si a un lote no le
--     cuento el consumo porque no convive conmigo, tampoco puedo contarle los ingresos ni los
--     traslados. Sin esto la ventana de alimento previo al encaset (v9) se comía la limpieza de
--     cierre del ciclo anterior y la apertura salía negativa (ver cabecera v11).
--     Un lote sin seguimiento cae acá por construcción (igual que en v10): todavía no tiene ciclo.
lotes_ajenos AS (
    SELECT l2.lote_ave_engorde_id AS id
    FROM lote_ave_engorde l2
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON rs.fecha_min IS NOT NULL
    WHERE l2.deleted_at IS NULL
      AND l2.lote_ave_engorde_id <> p_lote_id
      AND l2.granja_id = li.granja_id
      AND COALESCE(TRIM(l2.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(l2.galpon_id), '') = li.galpon_id
      AND NOT EXISTS (
            SELECT 1
              FROM seguimiento_diario_aves_engorde s2
             WHERE s2.lote_ave_engorde_id = l2.lote_ave_engorde_id
            HAVING MIN(DATE(s2.fecha)) <= rs.last_seg
               AND MAX(DATE(s2.fecha)) >= rs.fecha_min)
),

-- 2b. ⭐ v10: consumo diario de TODOS los lotes del galpón (inventario COMPARTIDO).
--     El alimento vive en la bodega del galpón, no del lote: los ingresos siempre se leyeron con
--     scope galpón, pero el consumo se restaba solo del lote consultado. Con dos lotes solapados en
--     el mismo galpón cada uno veía el 100 % de los ingresos y únicamente su propio consumo, así que
--     LOS DOS inflaban el saldo. Caso G0490 (DOÑA MARIA, jul-2026): ingresos 97.729,6 kg; el lote 168
--     mostraba 82.806,4 (= todo − sus 14.923,3) y el 169 mostraba 34.316,9 (= todo − sus 63.412,8),
--     cuando el saldo real compartido era 19.393,5 contra 18.939,9 de inventario.
--     Acotado por fecha_corte_alimento igual que los ingresos, para no arrastrar ciclos anteriores.
consumo_galpon_por_fecha AS (
    SELECT DATE(s.fecha) AS fecha,
           SUM(COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS cons_kg
    FROM seguimiento_diario_aves_engorde s
    JOIN lote_ave_engorde l2 ON l2.lote_ave_engorde_id = s.lote_ave_engorde_id
                            AND l2.deleted_at IS NULL
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON TRUE
    WHERE l2.granja_id = li.granja_id
      AND COALESCE(TRIM(l2.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(l2.galpon_id), '') = li.galpon_id
      AND (li.fecha_corte_alimento IS NULL OR DATE(s.fecha) >= li.fecha_corte_alimento)
      -- Solo los lotes que CONVIVEN en el galpón comparten bodega. Un galpón que encadena ciclos
      -- sucesivos (Ecuador: 3-4 lotes por galpón, uno detrás de otro) NO comparte nada: cada ciclo
      -- gasta su propio alimento. Sin este filtro el saldo de un lote se movía con el consumo del
      -- ciclo siguiente — y como los lotes viejos quedan en 'Abierto' (fecha_max NULL), la fn les
      -- sigue mostrando fechas posteriores, así que el efecto era grande y equivocado.
      AND (
            l2.lote_ave_engorde_id = p_lote_id
         OR EXISTS (
              SELECT 1
                FROM seguimiento_diario_aves_engorde s2
               WHERE s2.lote_ave_engorde_id = l2.lote_ave_engorde_id
               HAVING MIN(DATE(s2.fecha)) <= rs.last_seg
                  AND MAX(DATE(s2.fecha)) >= rs.fecha_min)
      )
    GROUP BY DATE(s.fecha)
),

-- 3. Saldo de apertura del galpón ANTES del primer seguimiento (v2/v3).
--    ⭐ v6: PISO 0 POR PASO (Lindley), alineado con el frontend/C# (la apertura nunca abre
--    en negativo: una salida sin ingreso previo suficiente se clampa a 0). Antes era un SUM
--    plano que podía dar apertura negativa y desalinear el saldo vs la pantalla (ej. lote 7).
apert_mov AS (
    SELECT DATE(h.fecha_operacion) AS f, h.created_at AS ts,
        CASE h.tipo_evento
            WHEN 'INV_INGRESO'          THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_ENTRADA' THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
            ELSE 0
        END AS delta
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON rs.fecha_min IS NOT NULL
    WHERE NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND NOT (h.tipo_evento = 'INV_INGRESO'
               AND h.referencia IS NOT NULL
               AND h.referencia LIKE 'Seguimiento aves engorde #%')
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND DATE(h.fecha_operacion) < rs.fecha_min
      -- ⭐ v12: la ventana arranca en el corte de v9 o el día siguiente al fin del ciclo anterior,
      -- el que sea más tarde. Caza la limpieza que quedó etiquetada con el lote NUEVO.
      AND (li.fecha_corte_alimento IS NULL
           OR DATE(h.fecha_operacion) >= (SELECT desde FROM corte_apertura))
      -- ⭐ v11: nada del ciclo anterior. Sin este filtro la ventana previa al encaset se comía
      -- la limpieza de cierre del lote que ocupaba el galpón y la apertura salía negativa.
      -- Caza la limpieza etiquetada con el lote VIEJO; la del NUEVO la caza el corte de arriba.
      AND (h.lote_ave_engorde_id IS NULL
           OR NOT EXISTS (SELECT 1 FROM lotes_ajenos la WHERE la.id = h.lote_ave_engorde_id))
),
apert_run AS (
    SELECT
        SUM(delta) OVER (ORDER BY f, ts ROWS UNBOUNDED PRECEDING) AS p,
        ROW_NUMBER()    OVER (ORDER BY f DESC, ts DESC)           AS rn_desc
    FROM apert_mov
),
apertura_alimento AS (
    -- v9: saldo de apertura CRUDO (P final), sin el reseteo de base de Lindley.
    -- ⭐ v10: menos el consumo que OTROS lotes del galpón ya hicieron antes del primer seguimiento
    -- de éste. Sin ese término el segundo lote de un galpón abría con todo el alimento que el
    -- primero ya se había comido. Junto con el consumo de galpón de pt_calc, la suma telescópica
    -- deja saldo(f) = ingresos(≤f) − consumo_del_galpón(≤f) — el mismo valor para los dos lotes.
    SELECT (
        COALESCE((SELECT p FROM apert_run WHERE rn_desc = 1), 0)
      - COALESCE((SELECT SUM(cg.cons_kg)
                    FROM consumo_galpon_por_fecha cg, rango_seg rs
                   WHERE rs.fecha_min IS NOT NULL
                     AND cg.fecha < rs.fecha_min), 0)
    )::FLOAT8 AS apertura_kg
),

-- 3b. ⭐ v5: movimientos de alimento del galpón por fecha, SIN tope superior
--     (para detectar la fecha de cierre = saldo a 0). Neto firmado igual que el saldo.
hist_full AS (
    SELECT
        DATE(h.fecha_operacion) AS fecha,
        SUM(CASE
            WHEN h.tipo_evento = 'INV_INGRESO'
             AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
                 THEN COALESCE(h.cantidad_kg, 0)
            WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA' THEN COALESCE(h.cantidad_kg, 0)
            WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
            ELSE 0
        END)::FLOAT8 AS neto_kg
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON TRUE
    WHERE NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
    GROUP BY DATE(h.fecha_operacion)
),

-- 3c. Consumo del seguimiento por fecha (scope LOTE, sin cambios desde v9).
--     Alimenta saldo_running → saldo_close, que detecta el cierre del ciclo. Se deja por lote a
--     propósito: es lo que fija `fecha_max`, y usar aquí el consumo del galpón sería circular
--     (el consumo compartido de pt_calc ya se acota CON `fecha_max`).
consumo_por_fecha AS (
    SELECT DATE(s.fecha) AS fecha,
           SUM(COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS cons_kg
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
    GROUP BY DATE(s.fecha)
),

-- 3d. ⭐ v5: saldo de alimento corriente (misma fórmula que la columna saldo_alimento_kg)
--     evaluado sobre TODO el histórico (sin tope) para detectar el cierre.
saldo_running AS (
    SELECT sf.fecha,
        GREATEST(0,
            (SELECT apertura_kg FROM apertura_alimento)
            + COALESCE(SUM(hf.neto_kg) OVER (ORDER BY sf.fecha ROWS UNBOUNDED PRECEDING), 0)
            - COALESCE(SUM(cf.cons_kg) OVER (ORDER BY sf.fecha ROWS UNBOUNDED PRECEDING), 0)
        ) AS saldo
    FROM (SELECT fecha FROM hist_full UNION SELECT fecha FROM consumo_por_fecha) sf
    LEFT JOIN hist_full          hf ON hf.fecha = sf.fecha
    LEFT JOIN consumo_por_fecha  cf ON cf.fecha = sf.fecha
),

-- 3e. ⭐ v5: fecha de cierre = primera fecha >= último seguimiento con saldo en 0
--     (lote vaciado de alimento). NULL si el saldo nunca llega a 0 (lote aún activo).
saldo_close AS (
    SELECT MIN(sr.fecha) AS close_date
    FROM saldo_running sr, rango_seg rs
    WHERE rs.last_seg IS NOT NULL
      AND sr.fecha >= rs.last_seg
      AND sr.saldo <= 0.5
),

-- 4. ⭐ v5: rango final. fecha_max = cierre efectivo (saldo 0) o, si no lo hay,
--    MAX(seg) para lotes 'cerrado' (fallback) o NULL para 'abierto' aún activo.
rango_final AS (
    SELECT
        rs.fecha_min,
        COALESCE(
            sc.close_date,
            CASE WHEN rs.estado = 'cerrado' THEN rs.last_seg ELSE NULL END
        ) AS fecha_max
    FROM rango_seg rs, saldo_close sc
),

-- 5. Bajas totales en seguimiento
salidas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) +
        COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) +
        COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0)
    ), 0) AS bajas_seguimiento
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 6. Ventas totales de aves (VENTA_AVES)
ventas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)
    ), 0) AS total_ventas
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
),

-- 7. Aves iniciales (espejo de avesInicialesLote() del frontend)
--    ⭐ v8: ramas que parten de aves_encasetadas/suma_hm restan mort_caja_total (piso 0). La
--    rama 'cerrado' no cambia: ya fuerza el cierre en 0 por construcción propia (bajas+ventas).
aves_iniciales AS (
    SELECT
        CASE
            WHEN li.estado_operativo_lote = 'cerrado'
                THEN GREATEST(1, st.bajas_seguimiento + vt.total_ventas)
            WHEN li.aves_encasetadas > 0 AND li.suma_hm = 0 THEN GREATEST(0, li.aves_encasetadas - li.mort_caja_total)
            WHEN li.suma_hm > 0 AND li.aves_encasetadas = 0 THEN GREATEST(0, li.suma_hm - li.mort_caja_total)
            WHEN li.aves_encasetadas = li.suma_hm              THEN GREATEST(0, li.aves_encasetadas - li.mort_caja_total)
            ELSE GREATEST(0, li.aves_encasetadas - li.mort_caja_total)
        END AS inicial
    FROM lote_info li
    CROSS JOIN salidas_totales st
    CROSS JOIN ventas_totales vt
),

-- 8. Ventas VENTA_AVES por fecha (despachos y saldo aves)
ventas_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        COALESCE(SUM(
            COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)
        ), 0)                                                                          AS ventas_dia,
        COALESCE(SUM(COALESCE(h.cantidad_hembras, 0)), 0)                             AS despacho_h,
        COALESCE(SUM(COALESCE(h.cantidad_machos,  0)), 0)                             AS despacho_m,
        COALESCE(SUM(COALESCE(h.cantidad_mixtas,  0)), 0)                             AS despacho_x,
        COALESCE(SUM(COALESCE(h.peso_neto,      0)), 0)::FLOAT8                        AS despacho_peso_neto,
        COALESCE(SUM(COALESCE(h.peso_tara_real, 0)), 0)::FLOAT8                        AS despacho_peso_tara
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
    GROUP BY DATE(h.fecha_operacion)
),

-- 9. kg de alimento por fecha (scope: granja + nucleo + galpon), acotado a rango_final
hist_alimento AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_INGRESO'
             AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
            THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END), 0)::FLOAT8                  AS ingreso_kg,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA'
            THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END), 0)::FLOAT8                  AS traslado_entrada_kg,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'
            THEN ABS(COALESCE(h.cantidad_kg, 0)) ELSE 0 END), 0)::FLOAT8             AS traslado_salida_kg
    FROM lote_registro_historico_unificado h
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
      AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max)
    GROUP BY DATE(h.fecha_operacion)
),

-- 10. Documento por fecha (INV_INGRESO scope galpón + VENTA_AVES scope lote), acotado a rango_final
docs_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        STRING_AGG(
            DISTINCT NULLIF(TRIM(COALESCE(h.numero_documento, h.referencia, '')), ''),
            ', '
        )                                                                              AS documento
    FROM lote_registro_historico_unificado h
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (
          (h.tipo_evento = 'INV_INGRESO'
           AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
           AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
           AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max))
          OR
          -- ⭐ v7: VENTA_AVES del lote SIN tope fecha_min/fecha_max (ver cabecera).
          (h.tipo_evento = 'VENTA_AVES'
           AND h.lote_ave_engorde_id = p_lote_id)
      )
    GROUP BY DATE(h.fecha_operacion)
),

-- 11. UNIVERSO DE FECHAS = fechas con seguimiento ∪ fechas con movimientos (acotado a rango_final)
fechas_universo AS (
    SELECT DATE(s.fecha) AS fecha, s.id AS seg_id
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
    UNION ALL
    SELECT DATE(h.fecha_operacion) AS fecha, NULL::BIGINT AS seg_id
    FROM lote_registro_historico_unificado h
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (
          (h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
           AND NOT (h.tipo_evento = 'INV_INGRESO'
                    AND h.referencia IS NOT NULL
                    AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
           AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
           AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max))
          OR
          -- ⭐ v7: VENTA_AVES del lote SIN tope fecha_min/fecha_max → toda venta (incluso
          -- posterior al cierre por alimento) genera su fila y el saldo cierra en 0.
          (h.tipo_evento = 'VENTA_AVES' AND h.lote_ave_engorde_id = p_lote_id)
      )
      AND (li.fecha_corte_alimento IS NULL OR DATE(h.fecha_operacion) >= li.fecha_corte_alimento)
      AND NOT EXISTS (
          SELECT 1 FROM seguimiento_diario_aves_engorde s2
          WHERE s2.lote_ave_engorde_id = p_lote_id
            AND DATE(s2.fecha) = DATE(h.fecha_operacion)
      )
    GROUP BY DATE(h.fecha_operacion)
),

-- 12. Seguimiento enriquecido
seg_enriquecido AS (
    SELECT
        s.id                                                                           AS seg_id,
        fu.fecha                                                                       AS fecha,
        CASE WHEN li.fecha_encaset IS NOT NULL
             THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
             ELSE 0 END                                                                AS edad_dia,
        LEAST(8, GREATEST(1,
            CEIL((CASE WHEN li.fecha_encaset IS NOT NULL
                       THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
                       ELSE 0 END + 1) / 7.0)
        ))::SMALLINT                                                                   AS semana,
        COALESCE(s.mortalidad_hembras,   0)                                            AS mortalidad_hembras,
        COALESCE(s.mortalidad_machos,    0)                                            AS mortalidad_machos,
        COALESCE(s.sel_h,                0)                                            AS sel_h,
        COALESCE(s.sel_m,                0)                                            AS sel_m,
        COALESCE(s.error_sexaje_hembras, 0)                                            AS error_sexaje_hembras,
        COALESCE(s.error_sexaje_machos,  0)                                            AS error_sexaje_machos,
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
            + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)                              AS total_mort_sel_dia,
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
            + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)
            + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_totales_dia,
        COALESCE(s.consumo_kg_hembras, 0)::FLOAT8                                      AS consumo_kg_hembras,
        COALESCE(s.consumo_kg_machos,  0)::FLOAT8                                      AS consumo_kg_machos,
        (COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS consumo_dia_kg,
        s.saldo_alimento_kg::FLOAT8                                                    AS saldo_alimento_kg,
        s.tipo_alimento,
        s.peso_prom_hembras::FLOAT8                                                    AS peso_prom_hembras,
        s.peso_prom_machos::FLOAT8                                                     AS peso_prom_machos,
        s.uniformidad_hembras::FLOAT8                                                  AS uniformidad_hembras,
        s.uniformidad_machos::FLOAT8                                                   AS uniformidad_machos,
        s.cv_hembras::FLOAT8                                                           AS cv_hembras,
        s.cv_machos::FLOAT8                                                            AS cv_machos,
        s.consumo_agua_diario::FLOAT8                                                  AS consumo_agua_diario,
        s.consumo_agua_ph::FLOAT8                                                      AS consumo_agua_ph,
        s.consumo_agua_orp::FLOAT8                                                     AS consumo_agua_orp,
        s.consumo_agua_temperatura::FLOAT8                                             AS consumo_agua_temperatura,
        s.observaciones,
        s.ciclo,
        s.metadata,
        s.items_adicionales,
        s.historico_consumo_alimento,
        s.created_by_user_id,
        COALESCE(vpf.ventas_dia, 0)                                                    AS ventas_dia,
        COALESCE(vpf.despacho_h, 0)                                                    AS despacho_h,
        COALESCE(vpf.despacho_m, 0)                                                    AS despacho_m,
        COALESCE(vpf.despacho_x, 0)                                                    AS despacho_x,
        COALESCE(vpf.despacho_peso_neto, 0)                                            AS despacho_peso_neto,
        COALESCE(vpf.despacho_peso_tara, 0)                                            AS despacho_peso_tara,
        COALESCE(ha.ingreso_kg,          0)                                            AS ingreso_alimento_kg,
        COALESCE(ha.traslado_entrada_kg, 0)                                            AS traslado_entrada_kg,
        COALESCE(ha.traslado_salida_kg,  0)                                            AS traslado_salida_kg,
        dpf.documento
    FROM fechas_universo fu
    CROSS JOIN lote_info li
    LEFT JOIN seguimiento_diario_aves_engorde s ON s.id = fu.seg_id
    LEFT JOIN ventas_por_fecha vpf ON vpf.fecha = fu.fecha
    LEFT JOIN hist_alimento    ha  ON ha.fecha  = fu.fecha
    LEFT JOIN docs_por_fecha   dpf ON dpf.fecha = fu.fecha
),

-- 12b. ⭐ v6 (M1): P_t = saldo SIN piso (apertura + ingresos acumulados − consumo acumulado).
--      Se materializa aquí para poder tomar MIN(P_t) acumulado en el SELECT final (la
--      forma cerrada de Lindley necesita una ventana sobre otra ventana).
pt_calc AS (
    SELECT se.*,
        (
            (SELECT apertura_kg FROM apertura_alimento)
            + COALESCE((SELECT SUM(ha2.ingreso_kg + ha2.traslado_entrada_kg - ha2.traslado_salida_kg)
                        FROM hist_alimento ha2
                        WHERE ha2.fecha <= se.fecha), 0)
            -- ⭐ v10: consumo de TODO el galpón, no solo el del lote consultado. Acotado a la MISMA
            -- ventana que los ingresos (fecha_min..fecha_max de rango_final): desde fecha_min porque
            -- lo anterior ya está en apertura_alimento, y hasta fecha_max para no traerse el consumo
            -- del ciclo SIGUIENTE del galpón — en Ecuador cada galpón encadena 3-4 lotes y sin ese
            -- tope el saldo de un lote cerrado se movía con el consumo del que vino después.
            - COALESCE((SELECT SUM(cg.cons_kg)
                          FROM consumo_galpon_por_fecha cg, rango_final rf
                         WHERE cg.fecha <= se.fecha
                           AND (rf.fecha_min IS NULL OR cg.fecha >= rf.fecha_min)
                           AND (rf.fecha_max IS NULL OR cg.fecha <= rf.fecha_max)), 0)
        )::FLOAT8 AS pt
    FROM seg_enriquecido se
)

-- 13. Query final
SELECT
    se.seg_id,
    se.fecha,
    se.edad_dia,
    se.semana,
    se.mortalidad_hembras,
    se.mortalidad_machos,
    se.sel_h,
    se.sel_m,
    se.error_sexaje_hembras,
    se.error_sexaje_machos,
    se.total_mort_sel_dia,
    se.perdidas_totales_dia,
    se.consumo_kg_hembras,
    se.consumo_kg_machos,
    se.consumo_dia_kg,
    SUM(se.consumo_dia_kg) OVER w_ord                                                 AS acum_consumo_kg,
    GREATEST(0,
        ai.inicial - SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_ord
    )::INT                                                                             AS saldo_aves,
    CASE
        WHEN GREATEST(0,
            ai.inicial - COALESCE(SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0)
        ) > 0
        THEN (100.0 * se.total_mort_sel_dia /
            GREATEST(0,
                ai.inicial - COALESCE(SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0)
            ))::FLOAT8
        WHEN se.total_mort_sel_dia > 0 THEN 100.0::FLOAT8
        ELSE NULL
    END                                                                                AS pct_perdidas_dia,
    -- ⭐ v9 (M2): saldo CRUDO, sin piso ni reseteo de base.
    -- El reseteo de Lindley "olvidaba" el déficit transitorio para no mostrar negativos, pero cada
    -- olvido regalaba alimento inexistente y el acumulado terminaba por encima del inventario: en el
    -- galpón 6 de DAYLAND (jul-2026) el reporte cerraba en 12.869,46 kg contra 2.235,33 reales,
    -- inflado exactamente en el peor negativo (−10.634,13 del 05/07). Un saldo negativo no es un
    -- error de cálculo: dice que el alimento se consumió antes de que su llegada quedara registrada.
    -- Espeja a RecalcularSaldoAlimentoPorLoteAsync / SeguimientoAvesEngordeCalculos, que ya no pisan.
    se.pt::FLOAT8                                                                      AS saldo_alimento_kg,
    se.ingreso_alimento_kg,
    se.traslado_entrada_kg,
    se.traslado_salida_kg,
    se.consumo_dia_kg                                                                  AS consumo_bodega_kg,
    se.documento,
    se.despacho_h  AS despacho_hembras,
    se.despacho_m  AS despacho_machos,
    se.despacho_x  AS despacho_mixtas,
    se.despacho_peso_neto                                                             AS despacho_peso_neto,
    se.despacho_peso_tara                                                             AS despacho_peso_tara,
    CASE WHEN (se.despacho_h + se.despacho_m + se.despacho_x) > 0
         THEN se.despacho_peso_neto / (se.despacho_h + se.despacho_m + se.despacho_x)
         ELSE 0 END                                                                   AS despacho_promedio_peso_ave,
    se.tipo_alimento,
    se.peso_prom_hembras,
    se.peso_prom_machos,
    se.uniformidad_hembras,
    se.uniformidad_machos,
    se.cv_hembras,
    se.cv_machos,
    se.consumo_agua_diario,
    se.consumo_agua_ph,
    se.consumo_agua_orp,
    se.consumo_agua_temperatura,
    se.observaciones,
    se.ciclo,
    se.metadata,
    se.items_adicionales,
    se.historico_consumo_alimento,
    se.created_by_user_id
FROM pt_calc se
CROSS JOIN aves_iniciales ai
WINDOW
    w_ord  AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    w_prev AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
-- (orden final: lo aplica el SELECT exterior de la union)
      ) t
     WHERE NOT EXISTS (SELECT 1
                         FROM liquidacion_lote_engorde_congelada c
                        WHERE c.lote_ave_engorde_id = p_lote_id
                          AND c.anulada_at IS NULL)
) u
ORDER BY u.fecha, COALESCE(u.seg_id, 0);
$$;
""";

        /// <summary>Cuerpo v12 original (LANGUAGE sql) — restaurado por Down().</summary>
        private const string FnSeguimientoDiarioEngordeV12 = """
CREATE OR REPLACE FUNCTION fn_seguimiento_diario_engorde(p_lote_id INT)
RETURNS TABLE (
    -- Identificación
    seg_id                      BIGINT,
    fecha                       DATE,
    -- Tiempo
    edad_dia                    INT,
    semana                      SMALLINT,
    -- Seguimiento crudo
    mortalidad_hembras          INT,
    mortalidad_machos           INT,
    sel_h                       INT,
    sel_m                       INT,
    error_sexaje_hembras        INT,
    error_sexaje_machos         INT,
    -- Calculados simples
    total_mort_sel_dia          INT,
    perdidas_totales_dia        INT,
    consumo_kg_hembras          DOUBLE PRECISION,
    consumo_kg_machos           DOUBLE PRECISION,
    consumo_dia_kg              DOUBLE PRECISION,
    -- Acumulados corrientes (window functions)
    acum_consumo_kg             DOUBLE PRECISION,
    saldo_aves                  INT,
    pct_perdidas_dia            DOUBLE PRECISION,
    -- Saldo alimento persistido por RecalcularSaldoAlimentoPorLoteAsync
    saldo_alimento_kg           DOUBLE PRECISION,
    -- Histórico agregado por fecha
    ingreso_alimento_kg         DOUBLE PRECISION,
    traslado_entrada_kg         DOUBLE PRECISION,
    traslado_salida_kg          DOUBLE PRECISION,
    consumo_bodega_kg           DOUBLE PRECISION,
    -- Documento: numeroDocumento || referencia de INV_INGRESO + VENTA_AVES
    documento                   TEXT,
    despacho_hembras            INT,
    despacho_machos             INT,
    despacho_mixtas             INT,
    -- Peso INDIVIDUAL real de la venta de ESTE lote en la fecha (R3.5), no el global de factura
    despacho_peso_neto          DOUBLE PRECISION,
    despacho_peso_tara          DOUBLE PRECISION,
    despacho_promedio_peso_ave  DOUBLE PRECISION,
    -- Mediciones del seguimiento
    tipo_alimento               TEXT,
    peso_prom_hembras           DOUBLE PRECISION,
    peso_prom_machos            DOUBLE PRECISION,
    uniformidad_hembras         DOUBLE PRECISION,
    uniformidad_machos          DOUBLE PRECISION,
    cv_hembras                  DOUBLE PRECISION,
    cv_machos                   DOUBLE PRECISION,
    consumo_agua_diario         DOUBLE PRECISION,
    consumo_agua_ph             DOUBLE PRECISION,
    consumo_agua_orp            DOUBLE PRECISION,
    consumo_agua_temperatura    DOUBLE PRECISION,
    observaciones               TEXT,
    ciclo                       TEXT,
    metadata                    JSONB,
    items_adicionales           JSONB,
    historico_consumo_alimento  JSONB,
    created_by_user_id          TEXT
) LANGUAGE sql STABLE AS $$

WITH

-- 1. Datos clave del lote
lote_info AS (
    SELECT
        l.granja_id,
        COALESCE(TRIM(l.nucleo_id), '')  AS nucleo_id,
        COALESCE(TRIM(l.galpon_id), '')  AS galpon_id,
        l.fecha_encaset,
        COALESCE(l.aves_encasetadas, 0)  AS aves_encasetadas,
        COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0) AS suma_hm,
        LOWER(COALESCE(l.estado_operativo_lote, '')) AS estado_operativo_lote,
        -- ⭐ v8: mortalidad en caja (llegada), capturada fuera del seguimiento diario.
        COALESCE(l.mort_caja_h, 0) + COALESCE(l.mort_caja_m, 0) AS mort_caja_total,
        -- ⭐ v9: el alimento cuenta desde esta fecha, no desde el encaset. En engorde el preiniciador
        -- llega antes que los pollitos; cortar en el encaset dejaba fuera alimento propio del lote.
        -- La ventana la configura la empresa (default 10 días, tope 30 = VentanaAlimentoPrevioCalculos).
        (l.fecha_encaset::DATE - LEAST(30, GREATEST(0, COALESCE(c.dias_alimento_previo_encaset, 10)))) AS fecha_corte_alimento
    FROM lote_ave_engorde l
    LEFT JOIN companies c ON c.id = l.company_id
    WHERE l.lote_ave_engorde_id = p_lote_id
      AND l.deleted_at IS NULL
),

-- 2. Rango base del ciclo: primer y último seguimiento + estado
rango_seg AS (
    SELECT
        MIN(s.fecha)::DATE AS fecha_min,
        MAX(s.fecha)::DATE AS last_seg,
        (SELECT LOWER(COALESCE(estado_operativo_lote, ''))
         FROM lote_ave_engorde
         WHERE lote_ave_engorde_id = p_lote_id AND deleted_at IS NULL) AS estado
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 2d. ⭐ v12: día en que arranca de verdad la ventana de apertura.
--     La ventana previa al encaset (v9) no puede meterse en el ciclo anterior: nada anterior al
--     último día de seguimiento del lote que ocupaba el galpón antes que yo es alimento mío.
--     Complementa a `lotes_ajenos` (v11), que solo caza la limpieza etiquetada con el lote VIEJO;
--     ésta caza la que quedó etiquetada con el lote NUEVO (ver cabecera v12).
corte_apertura AS (
    SELECT GREATEST(
               li.fecha_corte_alimento,
               COALESCE(
                   (SELECT MAX(DATE(s2.fecha)) + 1
                      FROM seguimiento_diario_aves_engorde s2
                      JOIN lote_ave_engorde l2 ON l2.lote_ave_engorde_id = s2.lote_ave_engorde_id
                                              AND l2.deleted_at IS NULL
                     WHERE l2.granja_id = li.granja_id
                       AND COALESCE(TRIM(l2.nucleo_id), '') = li.nucleo_id
                       AND COALESCE(TRIM(l2.galpon_id), '') = li.galpon_id
                       AND l2.lote_ave_engorde_id <> p_lote_id
                       -- solo ciclos que YA habían cerrado cuando este empezó
                       AND (SELECT MAX(DATE(s3.fecha))
                              FROM seguimiento_diario_aves_engorde s3
                             WHERE s3.lote_ave_engorde_id = l2.lote_ave_engorde_id) < rs.fecha_min),
                   li.fecha_corte_alimento)
           ) AS desde
    FROM lote_info li, rango_seg rs
    WHERE rs.fecha_min IS NOT NULL
),

-- 2c. ⭐ v11: lotes AJENOS = otros lotes del mismo galpón cuyo ciclo NO se solapa con el mío.
--     Es el COMPLEMENTO exacto del predicado de `consumo_galpon_por_fecha` (v10): si a un lote no le
--     cuento el consumo porque no convive conmigo, tampoco puedo contarle los ingresos ni los
--     traslados. Sin esto la ventana de alimento previo al encaset (v9) se comía la limpieza de
--     cierre del ciclo anterior y la apertura salía negativa (ver cabecera v11).
--     Un lote sin seguimiento cae acá por construcción (igual que en v10): todavía no tiene ciclo.
lotes_ajenos AS (
    SELECT l2.lote_ave_engorde_id AS id
    FROM lote_ave_engorde l2
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON rs.fecha_min IS NOT NULL
    WHERE l2.deleted_at IS NULL
      AND l2.lote_ave_engorde_id <> p_lote_id
      AND l2.granja_id = li.granja_id
      AND COALESCE(TRIM(l2.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(l2.galpon_id), '') = li.galpon_id
      AND NOT EXISTS (
            SELECT 1
              FROM seguimiento_diario_aves_engorde s2
             WHERE s2.lote_ave_engorde_id = l2.lote_ave_engorde_id
            HAVING MIN(DATE(s2.fecha)) <= rs.last_seg
               AND MAX(DATE(s2.fecha)) >= rs.fecha_min)
),

-- 2b. ⭐ v10: consumo diario de TODOS los lotes del galpón (inventario COMPARTIDO).
--     El alimento vive en la bodega del galpón, no del lote: los ingresos siempre se leyeron con
--     scope galpón, pero el consumo se restaba solo del lote consultado. Con dos lotes solapados en
--     el mismo galpón cada uno veía el 100 % de los ingresos y únicamente su propio consumo, así que
--     LOS DOS inflaban el saldo. Caso G0490 (DOÑA MARIA, jul-2026): ingresos 97.729,6 kg; el lote 168
--     mostraba 82.806,4 (= todo − sus 14.923,3) y el 169 mostraba 34.316,9 (= todo − sus 63.412,8),
--     cuando el saldo real compartido era 19.393,5 contra 18.939,9 de inventario.
--     Acotado por fecha_corte_alimento igual que los ingresos, para no arrastrar ciclos anteriores.
consumo_galpon_por_fecha AS (
    SELECT DATE(s.fecha) AS fecha,
           SUM(COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS cons_kg
    FROM seguimiento_diario_aves_engorde s
    JOIN lote_ave_engorde l2 ON l2.lote_ave_engorde_id = s.lote_ave_engorde_id
                            AND l2.deleted_at IS NULL
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON TRUE
    WHERE l2.granja_id = li.granja_id
      AND COALESCE(TRIM(l2.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(l2.galpon_id), '') = li.galpon_id
      AND (li.fecha_corte_alimento IS NULL OR DATE(s.fecha) >= li.fecha_corte_alimento)
      -- Solo los lotes que CONVIVEN en el galpón comparten bodega. Un galpón que encadena ciclos
      -- sucesivos (Ecuador: 3-4 lotes por galpón, uno detrás de otro) NO comparte nada: cada ciclo
      -- gasta su propio alimento. Sin este filtro el saldo de un lote se movía con el consumo del
      -- ciclo siguiente — y como los lotes viejos quedan en 'Abierto' (fecha_max NULL), la fn les
      -- sigue mostrando fechas posteriores, así que el efecto era grande y equivocado.
      AND (
            l2.lote_ave_engorde_id = p_lote_id
         OR EXISTS (
              SELECT 1
                FROM seguimiento_diario_aves_engorde s2
               WHERE s2.lote_ave_engorde_id = l2.lote_ave_engorde_id
               HAVING MIN(DATE(s2.fecha)) <= rs.last_seg
                  AND MAX(DATE(s2.fecha)) >= rs.fecha_min)
      )
    GROUP BY DATE(s.fecha)
),

-- 3. Saldo de apertura del galpón ANTES del primer seguimiento (v2/v3).
--    ⭐ v6: PISO 0 POR PASO (Lindley), alineado con el frontend/C# (la apertura nunca abre
--    en negativo: una salida sin ingreso previo suficiente se clampa a 0). Antes era un SUM
--    plano que podía dar apertura negativa y desalinear el saldo vs la pantalla (ej. lote 7).
apert_mov AS (
    SELECT DATE(h.fecha_operacion) AS f, h.created_at AS ts,
        CASE h.tipo_evento
            WHEN 'INV_INGRESO'          THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_ENTRADA' THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
            ELSE 0
        END AS delta
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON rs.fecha_min IS NOT NULL
    WHERE NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND NOT (h.tipo_evento = 'INV_INGRESO'
               AND h.referencia IS NOT NULL
               AND h.referencia LIKE 'Seguimiento aves engorde #%')
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND DATE(h.fecha_operacion) < rs.fecha_min
      -- ⭐ v12: la ventana arranca en el corte de v9 o el día siguiente al fin del ciclo anterior,
      -- el que sea más tarde. Caza la limpieza que quedó etiquetada con el lote NUEVO.
      AND (li.fecha_corte_alimento IS NULL
           OR DATE(h.fecha_operacion) >= (SELECT desde FROM corte_apertura))
      -- ⭐ v11: nada del ciclo anterior. Sin este filtro la ventana previa al encaset se comía
      -- la limpieza de cierre del lote que ocupaba el galpón y la apertura salía negativa.
      -- Caza la limpieza etiquetada con el lote VIEJO; la del NUEVO la caza el corte de arriba.
      AND (h.lote_ave_engorde_id IS NULL
           OR NOT EXISTS (SELECT 1 FROM lotes_ajenos la WHERE la.id = h.lote_ave_engorde_id))
),
apert_run AS (
    SELECT
        SUM(delta) OVER (ORDER BY f, ts ROWS UNBOUNDED PRECEDING) AS p,
        ROW_NUMBER()    OVER (ORDER BY f DESC, ts DESC)           AS rn_desc
    FROM apert_mov
),
apertura_alimento AS (
    -- v9: saldo de apertura CRUDO (P final), sin el reseteo de base de Lindley.
    -- ⭐ v10: menos el consumo que OTROS lotes del galpón ya hicieron antes del primer seguimiento
    -- de éste. Sin ese término el segundo lote de un galpón abría con todo el alimento que el
    -- primero ya se había comido. Junto con el consumo de galpón de pt_calc, la suma telescópica
    -- deja saldo(f) = ingresos(≤f) − consumo_del_galpón(≤f) — el mismo valor para los dos lotes.
    SELECT (
        COALESCE((SELECT p FROM apert_run WHERE rn_desc = 1), 0)
      - COALESCE((SELECT SUM(cg.cons_kg)
                    FROM consumo_galpon_por_fecha cg, rango_seg rs
                   WHERE rs.fecha_min IS NOT NULL
                     AND cg.fecha < rs.fecha_min), 0)
    )::FLOAT8 AS apertura_kg
),

-- 3b. ⭐ v5: movimientos de alimento del galpón por fecha, SIN tope superior
--     (para detectar la fecha de cierre = saldo a 0). Neto firmado igual que el saldo.
hist_full AS (
    SELECT
        DATE(h.fecha_operacion) AS fecha,
        SUM(CASE
            WHEN h.tipo_evento = 'INV_INGRESO'
             AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
                 THEN COALESCE(h.cantidad_kg, 0)
            WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA' THEN COALESCE(h.cantidad_kg, 0)
            WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
            ELSE 0
        END)::FLOAT8 AS neto_kg
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON TRUE
    WHERE NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
    GROUP BY DATE(h.fecha_operacion)
),

-- 3c. Consumo del seguimiento por fecha (scope LOTE, sin cambios desde v9).
--     Alimenta saldo_running → saldo_close, que detecta el cierre del ciclo. Se deja por lote a
--     propósito: es lo que fija `fecha_max`, y usar aquí el consumo del galpón sería circular
--     (el consumo compartido de pt_calc ya se acota CON `fecha_max`).
consumo_por_fecha AS (
    SELECT DATE(s.fecha) AS fecha,
           SUM(COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS cons_kg
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
    GROUP BY DATE(s.fecha)
),

-- 3d. ⭐ v5: saldo de alimento corriente (misma fórmula que la columna saldo_alimento_kg)
--     evaluado sobre TODO el histórico (sin tope) para detectar el cierre.
saldo_running AS (
    SELECT sf.fecha,
        GREATEST(0,
            (SELECT apertura_kg FROM apertura_alimento)
            + COALESCE(SUM(hf.neto_kg) OVER (ORDER BY sf.fecha ROWS UNBOUNDED PRECEDING), 0)
            - COALESCE(SUM(cf.cons_kg) OVER (ORDER BY sf.fecha ROWS UNBOUNDED PRECEDING), 0)
        ) AS saldo
    FROM (SELECT fecha FROM hist_full UNION SELECT fecha FROM consumo_por_fecha) sf
    LEFT JOIN hist_full          hf ON hf.fecha = sf.fecha
    LEFT JOIN consumo_por_fecha  cf ON cf.fecha = sf.fecha
),

-- 3e. ⭐ v5: fecha de cierre = primera fecha >= último seguimiento con saldo en 0
--     (lote vaciado de alimento). NULL si el saldo nunca llega a 0 (lote aún activo).
saldo_close AS (
    SELECT MIN(sr.fecha) AS close_date
    FROM saldo_running sr, rango_seg rs
    WHERE rs.last_seg IS NOT NULL
      AND sr.fecha >= rs.last_seg
      AND sr.saldo <= 0.5
),

-- 4. ⭐ v5: rango final. fecha_max = cierre efectivo (saldo 0) o, si no lo hay,
--    MAX(seg) para lotes 'cerrado' (fallback) o NULL para 'abierto' aún activo.
rango_final AS (
    SELECT
        rs.fecha_min,
        COALESCE(
            sc.close_date,
            CASE WHEN rs.estado = 'cerrado' THEN rs.last_seg ELSE NULL END
        ) AS fecha_max
    FROM rango_seg rs, saldo_close sc
),

-- 5. Bajas totales en seguimiento
salidas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) +
        COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) +
        COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0)
    ), 0) AS bajas_seguimiento
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 6. Ventas totales de aves (VENTA_AVES)
ventas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)
    ), 0) AS total_ventas
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
),

-- 7. Aves iniciales (espejo de avesInicialesLote() del frontend)
--    ⭐ v8: ramas que parten de aves_encasetadas/suma_hm restan mort_caja_total (piso 0). La
--    rama 'cerrado' no cambia: ya fuerza el cierre en 0 por construcción propia (bajas+ventas).
aves_iniciales AS (
    SELECT
        CASE
            WHEN li.estado_operativo_lote = 'cerrado'
                THEN GREATEST(1, st.bajas_seguimiento + vt.total_ventas)
            WHEN li.aves_encasetadas > 0 AND li.suma_hm = 0 THEN GREATEST(0, li.aves_encasetadas - li.mort_caja_total)
            WHEN li.suma_hm > 0 AND li.aves_encasetadas = 0 THEN GREATEST(0, li.suma_hm - li.mort_caja_total)
            WHEN li.aves_encasetadas = li.suma_hm              THEN GREATEST(0, li.aves_encasetadas - li.mort_caja_total)
            ELSE GREATEST(0, li.aves_encasetadas - li.mort_caja_total)
        END AS inicial
    FROM lote_info li
    CROSS JOIN salidas_totales st
    CROSS JOIN ventas_totales vt
),

-- 8. Ventas VENTA_AVES por fecha (despachos y saldo aves)
ventas_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        COALESCE(SUM(
            COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)
        ), 0)                                                                          AS ventas_dia,
        COALESCE(SUM(COALESCE(h.cantidad_hembras, 0)), 0)                             AS despacho_h,
        COALESCE(SUM(COALESCE(h.cantidad_machos,  0)), 0)                             AS despacho_m,
        COALESCE(SUM(COALESCE(h.cantidad_mixtas,  0)), 0)                             AS despacho_x,
        COALESCE(SUM(COALESCE(h.peso_neto,      0)), 0)::FLOAT8                        AS despacho_peso_neto,
        COALESCE(SUM(COALESCE(h.peso_tara_real, 0)), 0)::FLOAT8                        AS despacho_peso_tara
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
    GROUP BY DATE(h.fecha_operacion)
),

-- 9. kg de alimento por fecha (scope: granja + nucleo + galpon), acotado a rango_final
hist_alimento AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_INGRESO'
             AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
            THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END), 0)::FLOAT8                  AS ingreso_kg,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA'
            THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END), 0)::FLOAT8                  AS traslado_entrada_kg,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'
            THEN ABS(COALESCE(h.cantidad_kg, 0)) ELSE 0 END), 0)::FLOAT8             AS traslado_salida_kg
    FROM lote_registro_historico_unificado h
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
      AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max)
    GROUP BY DATE(h.fecha_operacion)
),

-- 10. Documento por fecha (INV_INGRESO scope galpón + VENTA_AVES scope lote), acotado a rango_final
docs_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        STRING_AGG(
            DISTINCT NULLIF(TRIM(COALESCE(h.numero_documento, h.referencia, '')), ''),
            ', '
        )                                                                              AS documento
    FROM lote_registro_historico_unificado h
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (
          (h.tipo_evento = 'INV_INGRESO'
           AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
           AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
           AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max))
          OR
          -- ⭐ v7: VENTA_AVES del lote SIN tope fecha_min/fecha_max (ver cabecera).
          (h.tipo_evento = 'VENTA_AVES'
           AND h.lote_ave_engorde_id = p_lote_id)
      )
    GROUP BY DATE(h.fecha_operacion)
),

-- 11. UNIVERSO DE FECHAS = fechas con seguimiento ∪ fechas con movimientos (acotado a rango_final)
fechas_universo AS (
    SELECT DATE(s.fecha) AS fecha, s.id AS seg_id
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
    UNION ALL
    SELECT DATE(h.fecha_operacion) AS fecha, NULL::BIGINT AS seg_id
    FROM lote_registro_historico_unificado h
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (
          (h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
           AND NOT (h.tipo_evento = 'INV_INGRESO'
                    AND h.referencia IS NOT NULL
                    AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
           AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
           AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max))
          OR
          -- ⭐ v7: VENTA_AVES del lote SIN tope fecha_min/fecha_max → toda venta (incluso
          -- posterior al cierre por alimento) genera su fila y el saldo cierra en 0.
          (h.tipo_evento = 'VENTA_AVES' AND h.lote_ave_engorde_id = p_lote_id)
      )
      AND (li.fecha_corte_alimento IS NULL OR DATE(h.fecha_operacion) >= li.fecha_corte_alimento)
      AND NOT EXISTS (
          SELECT 1 FROM seguimiento_diario_aves_engorde s2
          WHERE s2.lote_ave_engorde_id = p_lote_id
            AND DATE(s2.fecha) = DATE(h.fecha_operacion)
      )
    GROUP BY DATE(h.fecha_operacion)
),

-- 12. Seguimiento enriquecido
seg_enriquecido AS (
    SELECT
        s.id                                                                           AS seg_id,
        fu.fecha                                                                       AS fecha,
        CASE WHEN li.fecha_encaset IS NOT NULL
             THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
             ELSE 0 END                                                                AS edad_dia,
        LEAST(8, GREATEST(1,
            CEIL((CASE WHEN li.fecha_encaset IS NOT NULL
                       THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
                       ELSE 0 END + 1) / 7.0)
        ))::SMALLINT                                                                   AS semana,
        COALESCE(s.mortalidad_hembras,   0)                                            AS mortalidad_hembras,
        COALESCE(s.mortalidad_machos,    0)                                            AS mortalidad_machos,
        COALESCE(s.sel_h,                0)                                            AS sel_h,
        COALESCE(s.sel_m,                0)                                            AS sel_m,
        COALESCE(s.error_sexaje_hembras, 0)                                            AS error_sexaje_hembras,
        COALESCE(s.error_sexaje_machos,  0)                                            AS error_sexaje_machos,
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
            + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)                              AS total_mort_sel_dia,
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
            + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)
            + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_totales_dia,
        COALESCE(s.consumo_kg_hembras, 0)::FLOAT8                                      AS consumo_kg_hembras,
        COALESCE(s.consumo_kg_machos,  0)::FLOAT8                                      AS consumo_kg_machos,
        (COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS consumo_dia_kg,
        s.saldo_alimento_kg::FLOAT8                                                    AS saldo_alimento_kg,
        s.tipo_alimento,
        s.peso_prom_hembras::FLOAT8                                                    AS peso_prom_hembras,
        s.peso_prom_machos::FLOAT8                                                     AS peso_prom_machos,
        s.uniformidad_hembras::FLOAT8                                                  AS uniformidad_hembras,
        s.uniformidad_machos::FLOAT8                                                   AS uniformidad_machos,
        s.cv_hembras::FLOAT8                                                           AS cv_hembras,
        s.cv_machos::FLOAT8                                                            AS cv_machos,
        s.consumo_agua_diario::FLOAT8                                                  AS consumo_agua_diario,
        s.consumo_agua_ph::FLOAT8                                                      AS consumo_agua_ph,
        s.consumo_agua_orp::FLOAT8                                                     AS consumo_agua_orp,
        s.consumo_agua_temperatura::FLOAT8                                             AS consumo_agua_temperatura,
        s.observaciones,
        s.ciclo,
        s.metadata,
        s.items_adicionales,
        s.historico_consumo_alimento,
        s.created_by_user_id,
        COALESCE(vpf.ventas_dia, 0)                                                    AS ventas_dia,
        COALESCE(vpf.despacho_h, 0)                                                    AS despacho_h,
        COALESCE(vpf.despacho_m, 0)                                                    AS despacho_m,
        COALESCE(vpf.despacho_x, 0)                                                    AS despacho_x,
        COALESCE(vpf.despacho_peso_neto, 0)                                            AS despacho_peso_neto,
        COALESCE(vpf.despacho_peso_tara, 0)                                            AS despacho_peso_tara,
        COALESCE(ha.ingreso_kg,          0)                                            AS ingreso_alimento_kg,
        COALESCE(ha.traslado_entrada_kg, 0)                                            AS traslado_entrada_kg,
        COALESCE(ha.traslado_salida_kg,  0)                                            AS traslado_salida_kg,
        dpf.documento
    FROM fechas_universo fu
    CROSS JOIN lote_info li
    LEFT JOIN seguimiento_diario_aves_engorde s ON s.id = fu.seg_id
    LEFT JOIN ventas_por_fecha vpf ON vpf.fecha = fu.fecha
    LEFT JOIN hist_alimento    ha  ON ha.fecha  = fu.fecha
    LEFT JOIN docs_por_fecha   dpf ON dpf.fecha = fu.fecha
),

-- 12b. ⭐ v6 (M1): P_t = saldo SIN piso (apertura + ingresos acumulados − consumo acumulado).
--      Se materializa aquí para poder tomar MIN(P_t) acumulado en el SELECT final (la
--      forma cerrada de Lindley necesita una ventana sobre otra ventana).
pt_calc AS (
    SELECT se.*,
        (
            (SELECT apertura_kg FROM apertura_alimento)
            + COALESCE((SELECT SUM(ha2.ingreso_kg + ha2.traslado_entrada_kg - ha2.traslado_salida_kg)
                        FROM hist_alimento ha2
                        WHERE ha2.fecha <= se.fecha), 0)
            -- ⭐ v10: consumo de TODO el galpón, no solo el del lote consultado. Acotado a la MISMA
            -- ventana que los ingresos (fecha_min..fecha_max de rango_final): desde fecha_min porque
            -- lo anterior ya está en apertura_alimento, y hasta fecha_max para no traerse el consumo
            -- del ciclo SIGUIENTE del galpón — en Ecuador cada galpón encadena 3-4 lotes y sin ese
            -- tope el saldo de un lote cerrado se movía con el consumo del que vino después.
            - COALESCE((SELECT SUM(cg.cons_kg)
                          FROM consumo_galpon_por_fecha cg, rango_final rf
                         WHERE cg.fecha <= se.fecha
                           AND (rf.fecha_min IS NULL OR cg.fecha >= rf.fecha_min)
                           AND (rf.fecha_max IS NULL OR cg.fecha <= rf.fecha_max)), 0)
        )::FLOAT8 AS pt
    FROM seg_enriquecido se
)

-- 13. Query final
SELECT
    se.seg_id,
    se.fecha,
    se.edad_dia,
    se.semana,
    se.mortalidad_hembras,
    se.mortalidad_machos,
    se.sel_h,
    se.sel_m,
    se.error_sexaje_hembras,
    se.error_sexaje_machos,
    se.total_mort_sel_dia,
    se.perdidas_totales_dia,
    se.consumo_kg_hembras,
    se.consumo_kg_machos,
    se.consumo_dia_kg,
    SUM(se.consumo_dia_kg) OVER w_ord                                                 AS acum_consumo_kg,
    GREATEST(0,
        ai.inicial - SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_ord
    )::INT                                                                             AS saldo_aves,
    CASE
        WHEN GREATEST(0,
            ai.inicial - COALESCE(SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0)
        ) > 0
        THEN (100.0 * se.total_mort_sel_dia /
            GREATEST(0,
                ai.inicial - COALESCE(SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0)
            ))::FLOAT8
        WHEN se.total_mort_sel_dia > 0 THEN 100.0::FLOAT8
        ELSE NULL
    END                                                                                AS pct_perdidas_dia,
    -- ⭐ v9 (M2): saldo CRUDO, sin piso ni reseteo de base.
    -- El reseteo de Lindley "olvidaba" el déficit transitorio para no mostrar negativos, pero cada
    -- olvido regalaba alimento inexistente y el acumulado terminaba por encima del inventario: en el
    -- galpón 6 de DAYLAND (jul-2026) el reporte cerraba en 12.869,46 kg contra 2.235,33 reales,
    -- inflado exactamente en el peor negativo (−10.634,13 del 05/07). Un saldo negativo no es un
    -- error de cálculo: dice que el alimento se consumió antes de que su llegada quedara registrada.
    -- Espeja a RecalcularSaldoAlimentoPorLoteAsync / SeguimientoAvesEngordeCalculos, que ya no pisan.
    se.pt::FLOAT8                                                                      AS saldo_alimento_kg,
    se.ingreso_alimento_kg,
    se.traslado_entrada_kg,
    se.traslado_salida_kg,
    se.consumo_dia_kg                                                                  AS consumo_bodega_kg,
    se.documento,
    se.despacho_h  AS despacho_hembras,
    se.despacho_m  AS despacho_machos,
    se.despacho_x  AS despacho_mixtas,
    se.despacho_peso_neto                                                             AS despacho_peso_neto,
    se.despacho_peso_tara                                                             AS despacho_peso_tara,
    CASE WHEN (se.despacho_h + se.despacho_m + se.despacho_x) > 0
         THEN se.despacho_peso_neto / (se.despacho_h + se.despacho_m + se.despacho_x)
         ELSE 0 END                                                                   AS despacho_promedio_peso_ave,
    se.tipo_alimento,
    se.peso_prom_hembras,
    se.peso_prom_machos,
    se.uniformidad_hembras,
    se.uniformidad_machos,
    se.cv_hembras,
    se.cv_machos,
    se.consumo_agua_diario,
    se.consumo_agua_ph,
    se.consumo_agua_orp,
    se.consumo_agua_temperatura,
    se.observaciones,
    se.ciclo,
    se.metadata,
    se.items_adicionales,
    se.historico_consumo_alimento,
    se.created_by_user_id
FROM pt_calc se
CROSS JOIN aves_iniciales ai
WINDOW
    w_ord  AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    w_prev AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
ORDER BY se.fecha, COALESCE(se.seg_id, 0);
$$;
""";
    }
}
