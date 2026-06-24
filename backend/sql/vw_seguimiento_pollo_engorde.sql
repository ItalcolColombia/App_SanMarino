-- =============================================================================
-- Vista: vw_seguimiento_pollo_engorde  (REBUILD 2026-06-24)
-- =============================================================================
-- Reimplementación SET-BASED de fn_seguimiento_diario_engorde (v7) para TODOS los
-- lotes con seguimiento, manteniendo el nombre de la vista (Power BI).
--
-- CAMBIOS vs versión anterior (corrección hacia la función):
--  • Incluye DÍAS MOVIMIENTO-ONLY (venta/ingreso sin seguimiento) → filas con
--    seguimiento_id NULL. Nueva columna `tipo_fila` ('seguimiento' | 'movimiento').
--  • saldo_alimento_kg_calculado = modelo M1 de la función (apertura piso-0,
--    corte por cierre efectivo, scope galpón). saldo_alimento_kg_bd = persistido.
--  • saldo_aves_vivas ahora resta también VENTAS (antes solo pérdidas).
--    saldo_aves_vivas_hembras/_machos restan pérdidas + ventas del género
--    (mixtas solo afectan el global; H+M puede exceder global por mixtas no asignables).
--  • ingreso/traslado de alimento y `documento_hist` por scope GALPÓN + rango_final
--    (igual que la función), no por lote_ave_engorde_id del histórico.
--  • pct_perdidas_dia sobre aves vivas al inicio del día (incluye ventas).
-- SE CONSERVAN: consumo_bodega_kg (INV_CONSUMO histórico por lote), todas las
--    columnas existentes y sus nombres. SE AGREGAN: uniformidad/cv/agua ph-orp-temp,
--    ciclo, historico_consumo_alimento, despacho_peso_neto/tara/promedio, created_by_user_id.
-- =============================================================================

DROP VIEW IF EXISTS public.vw_seguimiento_pollo_engorde;

CREATE VIEW public.vw_seguimiento_pollo_engorde AS
WITH
lote_info AS (
    SELECT
        l.lote_ave_engorde_id,
        l.lote_nombre,
        l.fecha_encaset,
        l.granja_id,
        fa.name                                   AS granja_nombre,
        fa.company_id                             AS company_id,
        cp.name                                   AS company_nombre,
        l.galpon_id,
        gp.galpon_nombre,
        l.nucleo_id,
        nu.nucleo_nombre,
        COALESCE(TRIM(l.nucleo_id), '')           AS nucleo_id_t,
        COALESCE(TRIM(l.galpon_id), '')           AS galpon_id_t,
        COALESCE(l.aves_encasetadas, 0)           AS aves_encasetadas,
        COALESCE(l.hembras_l,0)+COALESCE(l.machos_l,0)+COALESCE(l.mixtas,0) AS suma_hm,
        COALESCE(l.hembras_l, 0)::bigint          AS aves_iniciales_hembras,
        COALESCE(l.machos_l,  0)::bigint          AS aves_iniciales_machos,
        GREATEST(0,
            CASE WHEN COALESCE(l.hembras_l,0)+COALESCE(l.machos_l,0) > 0
                 THEN COALESCE(l.hembras_l,0)+COALESCE(l.machos_l,0)
                 ELSE COALESCE(l.aves_encasetadas,0) END
        )::bigint                                 AS aves_iniciales,
        LOWER(COALESCE(l.estado_operativo_lote,'')) AS estado_operativo_lote
    FROM lote_ave_engorde l
    LEFT JOIN farms     fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
    LEFT JOIN companies cp ON cp.id = fa.company_id
    LEFT JOIN nucleos   nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
    LEFT JOIN galpones  gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
    WHERE l.deleted_at IS NULL
),
rango_seg AS (
    SELECT s.lote_ave_engorde_id, MIN(s.fecha)::date AS fecha_min, MAX(s.fecha)::date AS last_seg
    FROM seguimiento_diario_aves_engorde s
    GROUP BY s.lote_ave_engorde_id
),
-- Apertura (Lindley forma cerrada): P_final = SUM(delta); apertura = P_final − LEAST(0, MIN(P_run))
apert_mov AS (
    SELECT li.lote_ave_engorde_id, DATE(h.fecha_operacion) AS f, h.created_at AS ts,
        CASE h.tipo_evento
            WHEN 'INV_INGRESO'          THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_ENTRADA' THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
            ELSE 0 END AS delta
    FROM lote_info li
    JOIN rango_seg rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id AND rs.fecha_min IS NOT NULL
    JOIN lote_registro_historico_unificado h
      ON h.farm_id = li.granja_id
     AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id_t
     AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id_t
    WHERE NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
      AND NOT (h.tipo_evento='INV_INGRESO' AND h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
      AND NOT (h.referencia IS NOT NULL AND (h.referencia LIKE '%devolución por eliminación%' OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND DATE(h.fecha_operacion) < rs.fecha_min
      AND (li.fecha_encaset IS NULL OR DATE(h.fecha_operacion) >= li.fecha_encaset::date)
),
apert_run AS (
    SELECT lote_ave_engorde_id, delta,
        SUM(delta) OVER (PARTITION BY lote_ave_engorde_id ORDER BY f, ts ROWS UNBOUNDED PRECEDING) AS p
    FROM apert_mov
),
apertura_alimento AS (
    SELECT lote_ave_engorde_id, (SUM(delta) - LEAST(0, MIN(p)))::float8 AS apertura_kg
    FROM apert_run GROUP BY lote_ave_engorde_id
),
-- Detección de cierre por alimento (saldo a 0) — sin tope superior
hist_full AS (
    SELECT li.lote_ave_engorde_id, DATE(h.fecha_operacion) AS fecha,
        SUM(CASE
            WHEN h.tipo_evento='INV_INGRESO' AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%') THEN COALESCE(h.cantidad_kg,0)
            WHEN h.tipo_evento='INV_TRASLADO_ENTRADA' THEN COALESCE(h.cantidad_kg,0)
            WHEN h.tipo_evento='INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg,0))
            ELSE 0 END)::float8 AS neto_kg
    FROM lote_info li
    JOIN rango_seg rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
    JOIN lote_registro_historico_unificado h
      ON h.farm_id = li.granja_id
     AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id_t
     AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id_t
    WHERE NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
      AND NOT (h.referencia IS NOT NULL AND (h.referencia LIKE '%devolución por eliminación%' OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
    GROUP BY li.lote_ave_engorde_id, DATE(h.fecha_operacion)
),
consumo_por_fecha AS (
    SELECT s.lote_ave_engorde_id, DATE(s.fecha) AS fecha,
        SUM(COALESCE(s.consumo_kg_hembras,0)+COALESCE(s.consumo_kg_machos,0))::float8 AS cons_kg
    FROM seguimiento_diario_aves_engorde s
    GROUP BY s.lote_ave_engorde_id, DATE(s.fecha)
),
saldo_dates AS (
    SELECT lote_ave_engorde_id, fecha FROM hist_full
    UNION
    SELECT lote_ave_engorde_id, fecha FROM consumo_por_fecha
),
saldo_running AS (
    SELECT sd.lote_ave_engorde_id, sd.fecha,
        GREATEST(0,
            COALESCE(aa.apertura_kg,0)
            + COALESCE(SUM(hf.neto_kg) OVER (PARTITION BY sd.lote_ave_engorde_id ORDER BY sd.fecha ROWS UNBOUNDED PRECEDING),0)
            - COALESCE(SUM(cf.cons_kg) OVER (PARTITION BY sd.lote_ave_engorde_id ORDER BY sd.fecha ROWS UNBOUNDED PRECEDING),0)
        ) AS saldo
    FROM saldo_dates sd
    LEFT JOIN hist_full         hf ON hf.lote_ave_engorde_id=sd.lote_ave_engorde_id AND hf.fecha=sd.fecha
    LEFT JOIN consumo_por_fecha cf ON cf.lote_ave_engorde_id=sd.lote_ave_engorde_id AND cf.fecha=sd.fecha
    LEFT JOIN apertura_alimento aa ON aa.lote_ave_engorde_id=sd.lote_ave_engorde_id
),
saldo_close AS (
    SELECT sr.lote_ave_engorde_id, MIN(sr.fecha) AS close_date
    FROM saldo_running sr
    JOIN rango_seg rs ON rs.lote_ave_engorde_id = sr.lote_ave_engorde_id
    WHERE rs.last_seg IS NOT NULL AND sr.fecha >= rs.last_seg AND sr.saldo <= 0.5
    GROUP BY sr.lote_ave_engorde_id
),
rango_final AS (
    SELECT rs.lote_ave_engorde_id, rs.fecha_min,
        COALESCE(sc.close_date, CASE WHEN li.estado_operativo_lote='cerrado' THEN rs.last_seg ELSE NULL END) AS fecha_max
    FROM rango_seg rs
    JOIN lote_info li ON li.lote_ave_engorde_id = rs.lote_ave_engorde_id
    LEFT JOIN saldo_close sc ON sc.lote_ave_engorde_id = rs.lote_ave_engorde_id
),
salidas_totales AS (
    SELECT s.lote_ave_engorde_id, COALESCE(SUM(
        COALESCE(s.mortalidad_hembras,0)+COALESCE(s.mortalidad_machos,0)
        +COALESCE(s.sel_h,0)+COALESCE(s.sel_m,0)
        +COALESCE(s.error_sexaje_hembras,0)+COALESCE(s.error_sexaje_machos,0)),0) AS bajas_seguimiento
    FROM seguimiento_diario_aves_engorde s GROUP BY s.lote_ave_engorde_id
),
ventas_totales AS (
    SELECT h.lote_ave_engorde_id, COALESCE(SUM(
        COALESCE(h.cantidad_hembras,0)+COALESCE(h.cantidad_machos,0)+COALESCE(h.cantidad_mixtas,0)),0) AS total_ventas
    FROM lote_registro_historico_unificado h
    WHERE h.tipo_evento='VENTA_AVES' AND NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
    GROUP BY h.lote_ave_engorde_id
),
aves_iniciales AS (
    SELECT li.lote_ave_engorde_id,
        CASE
            WHEN li.estado_operativo_lote='cerrado' THEN GREATEST(1, COALESCE(st.bajas_seguimiento,0)+COALESCE(vt.total_ventas,0))
            WHEN li.aves_encasetadas > 0 AND li.suma_hm = 0 THEN li.aves_encasetadas
            WHEN li.suma_hm > 0 AND li.aves_encasetadas = 0 THEN li.suma_hm
            WHEN li.aves_encasetadas = li.suma_hm THEN li.aves_encasetadas
            ELSE li.aves_encasetadas
        END AS inicial
    FROM lote_info li
    LEFT JOIN salidas_totales st ON st.lote_ave_engorde_id = li.lote_ave_engorde_id
    LEFT JOIN ventas_totales  vt ON vt.lote_ave_engorde_id = li.lote_ave_engorde_id
),
ventas_por_fecha AS (
    SELECT h.lote_ave_engorde_id, DATE(h.fecha_operacion) AS fecha,
        COALESCE(SUM(COALESCE(h.cantidad_hembras,0)+COALESCE(h.cantidad_machos,0)+COALESCE(h.cantidad_mixtas,0)),0) AS ventas_dia,
        COALESCE(SUM(COALESCE(h.cantidad_hembras,0)),0) AS despacho_h,
        COALESCE(SUM(COALESCE(h.cantidad_machos, 0)),0) AS despacho_m,
        COALESCE(SUM(COALESCE(h.cantidad_mixtas, 0)),0) AS despacho_x,
        COALESCE(SUM(COALESCE(h.peso_neto,      0)),0)::float8 AS despacho_peso_neto,
        COALESCE(SUM(COALESCE(h.peso_tara_real, 0)),0)::float8 AS despacho_peso_tara
    FROM lote_registro_historico_unificado h
    WHERE h.tipo_evento='VENTA_AVES' AND NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
    GROUP BY h.lote_ave_engorde_id, DATE(h.fecha_operacion)
),
-- Consumo de bodega (INV_CONSUMO) por lote/fecha — se PRESERVA el significado existente
consumo_bodega_por_fecha AS (
    SELECT h.lote_ave_engorde_id, DATE(h.fecha_operacion) AS fecha,
        SUM(CASE WHEN h.tipo_evento='INV_CONSUMO' AND NOT h.anulado THEN COALESCE(h.cantidad_kg,0) ELSE 0 END)::float8 AS consumo_bodega_kg
    FROM lote_registro_historico_unificado h
    WHERE NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
    GROUP BY h.lote_ave_engorde_id, DATE(h.fecha_operacion)
),
hist_alimento AS (
    SELECT li.lote_ave_engorde_id, DATE(h.fecha_operacion) AS fecha,
        COALESCE(SUM(CASE WHEN h.tipo_evento='INV_INGRESO' AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%') THEN COALESCE(h.cantidad_kg,0) ELSE 0 END),0)::float8 AS ingreso_kg,
        COALESCE(SUM(CASE WHEN h.tipo_evento='INV_TRASLADO_ENTRADA' THEN COALESCE(h.cantidad_kg,0) ELSE 0 END),0)::float8 AS traslado_entrada_kg,
        COALESCE(SUM(CASE WHEN h.tipo_evento='INV_TRASLADO_SALIDA' THEN ABS(COALESCE(h.cantidad_kg,0)) ELSE 0 END),0)::float8 AS traslado_salida_kg
    FROM lote_info li
    JOIN rango_final rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
    JOIN lote_registro_historico_unificado h
      ON h.farm_id = li.granja_id
     AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id_t
     AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id_t
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (h.referencia LIKE '%devolución por eliminación%' OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
      AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max)
    GROUP BY li.lote_ave_engorde_id, DATE(h.fecha_operacion)
),
docs_por_fecha AS (
    SELECT li.lote_ave_engorde_id, DATE(h.fecha_operacion) AS fecha,
        STRING_AGG(DISTINCT NULLIF(TRIM(COALESCE(h.numero_documento, h.referencia, '')), ''), ', ') AS documento
    FROM lote_info li
    JOIN rango_final rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
    JOIN lote_registro_historico_unificado h ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (h.referencia LIKE '%devolución por eliminación%' OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (
          (h.tipo_evento='INV_INGRESO'
           AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id),'') = li.nucleo_id_t
           AND COALESCE(TRIM(h.galpon_id),'') = li.galpon_id_t
           AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
           AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max))
          OR
          (h.tipo_evento='VENTA_AVES' AND h.lote_ave_engorde_id = li.lote_ave_engorde_id)
      )
    GROUP BY li.lote_ave_engorde_id, DATE(h.fecha_operacion)
),
-- Universo de fechas = seguimiento ∪ movimientos (acotado a rango_final)
fechas_universo AS (
    SELECT s.lote_ave_engorde_id, DATE(s.fecha) AS fecha, s.id AS seg_id
    FROM seguimiento_diario_aves_engorde s
    UNION ALL
    SELECT li.lote_ave_engorde_id, DATE(h.fecha_operacion) AS fecha, NULL::bigint AS seg_id
    FROM lote_info li
    JOIN rango_final rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
    JOIN lote_registro_historico_unificado h ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (h.referencia LIKE '%devolución por eliminación%' OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (
          (h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
           AND NOT (h.tipo_evento='INV_INGRESO' AND h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id),'') = li.nucleo_id_t
           AND COALESCE(TRIM(h.galpon_id),'') = li.galpon_id_t
           AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
           AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max))
          OR
          (h.tipo_evento='VENTA_AVES' AND h.lote_ave_engorde_id = li.lote_ave_engorde_id)
      )
      AND (li.fecha_encaset IS NULL OR DATE(h.fecha_operacion) >= li.fecha_encaset::date)
      AND NOT EXISTS (
          SELECT 1 FROM seguimiento_diario_aves_engorde s2
          WHERE s2.lote_ave_engorde_id = li.lote_ave_engorde_id
            AND DATE(s2.fecha) = DATE(h.fecha_operacion)
      )
    GROUP BY li.lote_ave_engorde_id, DATE(h.fecha_operacion)
),
seg_enriquecido AS (
    SELECT
        fu.lote_ave_engorde_id,
        s.id AS seg_id,
        fu.fecha,
        CASE WHEN li.fecha_encaset IS NOT NULL THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset)) ELSE 0 END AS edad_dia,
        LEAST(8, GREATEST(1, CEIL((CASE WHEN li.fecha_encaset IS NOT NULL THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset)) ELSE 0 END + 1)/7.0)))::smallint AS semana,
        COALESCE(s.mortalidad_hembras,0)   AS mortalidad_hembras,
        COALESCE(s.mortalidad_machos, 0)   AS mortalidad_machos,
        COALESCE(s.sel_h,0)                AS sel_h,
        COALESCE(s.sel_m,0)                AS sel_m,
        COALESCE(s.error_sexaje_hembras,0) AS error_sexaje_hembras,
        COALESCE(s.error_sexaje_machos, 0) AS error_sexaje_machos,
        COALESCE(s.mortalidad_hembras,0)+COALESCE(s.mortalidad_machos,0)+COALESCE(s.sel_h,0)+COALESCE(s.sel_m,0) AS total_mort_sel_dia,
        COALESCE(s.mortalidad_hembras,0)+COALESCE(s.mortalidad_machos,0)+COALESCE(s.sel_h,0)+COALESCE(s.sel_m,0)
            +COALESCE(s.error_sexaje_hembras,0)+COALESCE(s.error_sexaje_machos,0) AS perdidas_totales_dia,
        COALESCE(s.mortalidad_hembras,0)+COALESCE(s.sel_h,0)+COALESCE(s.error_sexaje_hembras,0) AS perdidas_hembras_dia,
        COALESCE(s.mortalidad_machos,0)+COALESCE(s.sel_m,0)+COALESCE(s.error_sexaje_machos,0)   AS perdidas_machos_dia,
        COALESCE(s.consumo_kg_hembras,0)::float8 AS consumo_kg_hembras,
        COALESCE(s.consumo_kg_machos, 0)::float8 AS consumo_kg_machos,
        (COALESCE(s.consumo_kg_hembras,0)+COALESCE(s.consumo_kg_machos,0))::float8 AS consumo_dia_kg,
        s.saldo_alimento_kg::float8 AS saldo_alimento_kg_bd,
        s.tipo_alimento,
        s.peso_prom_hembras::float8 AS peso_prom_hembras,
        s.peso_prom_machos::float8  AS peso_prom_machos,
        s.uniformidad_hembras::float8 AS uniformidad_hembras,
        s.uniformidad_machos::float8  AS uniformidad_machos,
        s.cv_hembras::float8 AS cv_hembras,
        s.cv_machos::float8  AS cv_machos,
        s.consumo_agua_diario::float8 AS consumo_agua_diario,
        s.consumo_agua_ph::float8 AS consumo_agua_ph,
        s.consumo_agua_orp::float8 AS consumo_agua_orp,
        s.consumo_agua_temperatura::float8 AS consumo_agua_temperatura,
        s.observaciones,
        s.ciclo,
        s.metadata,
        s.items_adicionales,
        s.historico_consumo_alimento,
        s.created_by_user_id,
        COALESCE(vpf.ventas_dia,0)         AS ventas_dia,
        COALESCE(vpf.despacho_h,0)         AS despacho_h,
        COALESCE(vpf.despacho_m,0)         AS despacho_m,
        COALESCE(vpf.despacho_x,0)         AS despacho_x,
        COALESCE(vpf.despacho_peso_neto,0) AS despacho_peso_neto,
        COALESCE(vpf.despacho_peso_tara,0) AS despacho_peso_tara,
        COALESCE(ha.ingreso_kg,0)          AS ingreso_alimento_kg,
        COALESCE(ha.traslado_entrada_kg,0) AS traslado_entrada_kg,
        COALESCE(ha.traslado_salida_kg,0)  AS traslado_salida_kg,
        COALESCE(cb.consumo_bodega_kg,0)   AS consumo_bodega_kg,
        dpf.documento
    FROM fechas_universo fu
    JOIN lote_info li ON li.lote_ave_engorde_id = fu.lote_ave_engorde_id
    LEFT JOIN seguimiento_diario_aves_engorde s ON s.id = fu.seg_id
    LEFT JOIN ventas_por_fecha         vpf ON vpf.lote_ave_engorde_id = fu.lote_ave_engorde_id AND vpf.fecha = fu.fecha
    LEFT JOIN hist_alimento            ha  ON ha.lote_ave_engorde_id  = fu.lote_ave_engorde_id AND ha.fecha  = fu.fecha
    LEFT JOIN consumo_bodega_por_fecha cb  ON cb.lote_ave_engorde_id  = fu.lote_ave_engorde_id AND cb.fecha  = fu.fecha
    LEFT JOIN docs_por_fecha           dpf ON dpf.lote_ave_engorde_id = fu.lote_ave_engorde_id AND dpf.fecha = fu.fecha
),
-- Cumulativo de alimento (scope galpón) hasta cada fecha del universo (por lote)
universo_fechas_distinct AS (
    SELECT DISTINCT lote_ave_engorde_id, fecha FROM fechas_universo
),
alim_cum AS (
    SELECT u.lote_ave_engorde_id, u.fecha,
        SUM(COALESCE(ha.ingreso_kg + ha.traslado_entrada_kg - ha.traslado_salida_kg, 0))
            OVER (PARTITION BY u.lote_ave_engorde_id ORDER BY u.fecha ROWS UNBOUNDED PRECEDING)::float8 AS alim_cum_kg
    FROM universo_fechas_distinct u
    LEFT JOIN hist_alimento ha ON ha.lote_ave_engorde_id = u.lote_ave_engorde_id AND ha.fecha = u.fecha
),
pt_calc AS (
    SELECT se.*,
        ( COALESCE(aa.apertura_kg,0)
          + COALESCE(ac.alim_cum_kg,0)
          - SUM(se.consumo_dia_kg) OVER (PARTITION BY se.lote_ave_engorde_id ORDER BY se.fecha, COALESCE(se.seg_id,0) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)
        )::float8 AS pt
    FROM seg_enriquecido se
    LEFT JOIN apertura_alimento aa ON aa.lote_ave_engorde_id = se.lote_ave_engorde_id
    LEFT JOIN alim_cum ac ON ac.lote_ave_engorde_id = se.lote_ave_engorde_id AND ac.fecha = se.fecha
),
calc AS (
    SELECT se.*,
        ai.inicial,
        li.lote_nombre, li.fecha_encaset, li.granja_id, li.granja_nombre, li.company_id, li.company_nombre,
        li.galpon_id, li.galpon_nombre, li.nucleo_id, li.nucleo_nombre,
        li.aves_iniciales, li.aves_iniciales_hembras, li.aves_iniciales_machos,
        -- acumulados / saldos (windows particionadas por lote, orden fecha+seg_id)
        SUM(se.consumo_dia_kg) OVER w_ord AS acum_consumo_kg,
        GREATEST(0, ai.inicial - SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_ord)::int AS saldo_aves_vivas,
        GREATEST(0, li.aves_iniciales_hembras - SUM(se.perdidas_hembras_dia + se.despacho_h) OVER w_ord)::bigint AS saldo_aves_vivas_hembras,
        GREATEST(0, li.aves_iniciales_machos  - SUM(se.perdidas_machos_dia + se.despacho_m) OVER w_ord)::bigint AS saldo_aves_vivas_machos,
        CASE
            WHEN GREATEST(0, ai.inicial - COALESCE(SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev,0)) > 0
            THEN round((100.0 * se.total_mort_sel_dia / GREATEST(0, ai.inicial - COALESCE(SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev,0)))::numeric, 2)
            WHEN se.total_mort_sel_dia > 0 THEN 100::numeric
            ELSE NULL::numeric
        END AS pct_perdidas_dia,
        (se.pt - LEAST(0, MIN(se.pt) OVER w_ord))::float8 AS saldo_alimento_kg_calc
    FROM pt_calc se
    JOIN lote_info li ON li.lote_ave_engorde_id = se.lote_ave_engorde_id
    JOIN aves_iniciales ai ON ai.lote_ave_engorde_id = se.lote_ave_engorde_id
    WINDOW
        w_ord  AS (PARTITION BY se.lote_ave_engorde_id ORDER BY se.fecha, COALESCE(se.seg_id,0) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
        w_prev AS (PARTITION BY se.lote_ave_engorde_id ORDER BY se.fecha, COALESCE(se.seg_id,0) ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
)
SELECT
    -- Identificación y contexto (orden y nombres conservados)
    seg_id                                              AS seguimiento_id,
    lote_ave_engorde_id,
    lote_nombre,
    company_id,
    company_nombre,
    granja_id,
    granja_nombre,
    galpon_id,
    galpon_nombre,
    nucleo_id,
    nucleo_nombre,
    to_char(fecha::timestamptz, 'DD/MM/YYYY')           AS fecha_dmy,
    fecha                                               AS fecha_registro,
    semana,
    edad_dia                                            AS edad_dias_vida,
    to_char(fecha::timestamptz, 'Dy, DD Mon')           AS dia_calendario_corto,
    mortalidad_hembras,
    mortalidad_machos,
    sel_h                                               AS seleccion_hembras,
    sel_m                                               AS seleccion_machos,
    total_mort_sel_dia                                  AS total_mort_mas_sel_dia,
    error_sexaje_hembras,
    error_sexaje_machos,
    despacho_h                                          AS despacho_hembras_hist,
    despacho_m                                          AS despacho_machos_hist,
    despacho_x                                          AS despacho_mixtas_hist,
    trim_scale(saldo_alimento_kg_bd::numeric)           AS saldo_alimento_kg_bd,
    trim_scale(saldo_alimento_kg_calc::numeric)         AS saldo_alimento_kg_calculado,
    saldo_aves_vivas,
    saldo_aves_vivas_hembras,
    saldo_aves_vivas_machos,
    tipo_alimento,
    CASE
        WHEN upper(COALESCE(tipo_alimento,'')) LIKE '%PRE%' THEN 'PRE'
        WHEN upper(COALESCE(tipo_alimento,'')) LIKE '%INI%' THEN 'INI'
        WHEN upper(COALESCE(tipo_alimento,'')) LIKE '%ENG%' THEN 'ENG'
        WHEN upper(COALESCE(tipo_alimento,'')) LIKE '%FIN%' THEN 'FIN-D'
        WHEN COALESCE(tipo_alimento,'') = '' THEN '—'
        ELSE left(tipo_alimento, 8)
    END                                                 AS tipo_alimento_corto,
    CASE WHEN COALESCE(ingreso_alimento_kg,0) > 0 THEN to_char(ingreso_alimento_kg::numeric,'FM9999999999990.999') || ' kg' ELSE NULL END AS ingreso_alimento_texto_hist,
    CASE
        WHEN COALESCE(traslado_entrada_kg,0)=0 AND COALESCE(traslado_salida_kg,0)=0 THEN NULL
        ELSE concat_ws(' · ',
            CASE WHEN COALESCE(traslado_entrada_kg,0)>0 THEN 'Entrada ' || to_char(traslado_entrada_kg::numeric,'FM9999999999990.999') || ' kg' ELSE NULL END,
            CASE WHEN COALESCE(traslado_salida_kg, 0)>0 THEN 'Salida '  || to_char(traslado_salida_kg::numeric, 'FM9999999999990.999') || ' kg' ELSE NULL END)
    END                                                 AS traslado_texto_hist,
    COALESCE(documento,'')                              AS documento_hist,
    metadata ->> 'ingresoAlimento'                      AS metadata_ingreso_alimento,
    metadata ->> 'traslado'                             AS metadata_traslado,
    metadata ->> 'documento'                            AS metadata_documento,
    trim_scale(consumo_kg_hembras::numeric)             AS consumo_kg_hembras,
    trim_scale(consumo_kg_machos::numeric)              AS consumo_kg_machos,
    trim_scale(consumo_dia_kg::numeric)                 AS consumo_real_dia_kg,
    trim_scale(acum_consumo_kg::numeric)                AS consumo_acumulado_kg,
    trim_scale(consumo_bodega_kg::numeric)              AS consumo_bodega_kg,
    trim_scale(consumo_agua_diario::numeric)            AS consumo_agua_diario,
    trim_scale(pct_perdidas_dia)                        AS pct_perdidas_dia,
    trim_scale(peso_prom_hembras::numeric)              AS peso_prom_hembras,
    trim_scale(peso_prom_machos::numeric)               AS peso_prom_machos,
    observaciones,
    metadata,
    items_adicionales,
    -- ── NUEVAS columnas (al final) ──
    CASE WHEN seg_id IS NULL THEN 'movimiento' ELSE 'seguimiento' END AS tipo_fila,
    trim_scale(uniformidad_hembras::numeric)            AS uniformidad_hembras,
    trim_scale(uniformidad_machos::numeric)             AS uniformidad_machos,
    trim_scale(cv_hembras::numeric)                     AS cv_hembras,
    trim_scale(cv_machos::numeric)                      AS cv_machos,
    trim_scale(consumo_agua_ph::numeric)                AS consumo_agua_ph,
    trim_scale(consumo_agua_orp::numeric)               AS consumo_agua_orp,
    trim_scale(consumo_agua_temperatura::numeric)       AS consumo_agua_temperatura,
    ciclo,
    historico_consumo_alimento,
    trim_scale(despacho_peso_neto::numeric)             AS despacho_peso_neto,
    trim_scale(despacho_peso_tara::numeric)             AS despacho_peso_tara,
    trim_scale(CASE WHEN (despacho_h+despacho_m+despacho_x) > 0 THEN despacho_peso_neto/(despacho_h+despacho_m+despacho_x) ELSE 0 END::numeric) AS despacho_promedio_peso_ave,
    created_by_user_id
FROM calc
ORDER BY lote_ave_engorde_id, fecha, COALESCE(seg_id, 0);

ALTER VIEW public.vw_seguimiento_pollo_engorde OWNER TO repropesa01;
GRANT SELECT ON TABLE public.vw_seguimiento_pollo_engorde TO "usrDWH";

COMMENT ON VIEW public.vw_seguimiento_pollo_engorde IS
  'Seguimiento diario engorde (espejo set-based de fn_seguimiento_diario_engorde v7). Incluye días movimiento-only (tipo_fila). saldo_alimento_kg_calculado = M1; saldo_aves_vivas resta ventas. Nombre y columnas previas conservados; columnas nuevas al final.';
