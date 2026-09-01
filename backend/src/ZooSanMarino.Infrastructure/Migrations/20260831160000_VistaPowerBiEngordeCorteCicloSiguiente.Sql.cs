using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo SQL de la vista de Power BI con el corte por ciclo siguiente. La documentacion esta en
    /// <c>20260831160000_VistaPowerBiEngordeCorteCicloSiguiente.cs</c>.
    /// </summary>
    public partial class VistaPowerBiEngordeCorteCicloSiguiente
    {
        private const string VISTA_CON_CORTE = @"CREATE OR REPLACE VIEW public.vw_seguimiento_pollo_engorde AS
SELECT seguimiento_id,
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
    fecha_dmy,
    fecha_registro,
    semana,
    edad_dias_vida,
    dia_calendario_corto,
    mortalidad_hembras,
    mortalidad_machos,
    seleccion_hembras,
    seleccion_machos,
    total_mort_mas_sel_dia,
    error_sexaje_hembras,
    error_sexaje_machos,
    despacho_hembras_hist,
    despacho_machos_hist,
    despacho_mixtas_hist,
    saldo_alimento_kg_bd,
    saldo_alimento_kg_calculado,
    saldo_aves_vivas,
    saldo_aves_vivas_hembras,
    saldo_aves_vivas_machos,
    tipo_alimento,
    tipo_alimento_corto,
    ingreso_alimento_texto_hist,
    traslado_texto_hist,
    documento_hist,
    metadata_ingreso_alimento,
    metadata_traslado,
    metadata_documento,
    consumo_kg_hembras,
    consumo_kg_machos,
    consumo_real_dia_kg,
    consumo_acumulado_kg,
    consumo_bodega_kg,
    consumo_agua_diario,
    pct_perdidas_dia,
    peso_prom_hembras,
    peso_prom_machos,
    observaciones,
    metadata,
    items_adicionales,
    tipo_fila,
    uniformidad_hembras,
    uniformidad_machos,
    cv_hembras,
    cv_machos,
    consumo_agua_ph,
    consumo_agua_orp,
    consumo_agua_temperatura,
    ciclo,
    historico_consumo_alimento,
    despacho_peso_neto,
    despacho_peso_tara,
    despacho_promedio_peso_ave,
    created_by_user_id,
        CASE
            WHEN COALESCE(created_by_user_id, ''::character varying)::text = 'SYSTEM_CRUCE'::text OR COALESCE(consumo_kg_machos, 0::numeric) > 0::numeric THEN NULL::numeric
            ELSE consumo_real_dia_kg
        END AS consumo_kg_mixto,
    NOT (COALESCE(created_by_user_id, ''::character varying)::text = 'SYSTEM_CRUCE'::text OR COALESCE(consumo_kg_machos, 0::numeric) > 0::numeric) AS consumo_es_mixto
   FROM ( SELECT v_1.seguimiento_id,
            v_1.lote_ave_engorde_id,
            v_1.lote_nombre,
            v_1.company_id,
            v_1.company_nombre,
            v_1.granja_id,
            v_1.granja_nombre,
            v_1.galpon_id,
            v_1.galpon_nombre,
            v_1.nucleo_id,
            v_1.nucleo_nombre,
            v_1.fecha_dmy,
            v_1.fecha_registro,
            v_1.semana,
            v_1.edad_dias_vida,
            v_1.dia_calendario_corto,
            v_1.mortalidad_hembras,
            v_1.mortalidad_machos,
            v_1.seleccion_hembras,
            v_1.seleccion_machos,
            v_1.total_mort_mas_sel_dia,
            v_1.error_sexaje_hembras,
            v_1.error_sexaje_machos,
            v_1.despacho_hembras_hist,
            v_1.despacho_machos_hist,
            v_1.despacho_mixtas_hist,
            v_1.saldo_alimento_kg_bd,
            v_1.saldo_alimento_kg_calculado,
            v_1.saldo_aves_vivas,
            v_1.saldo_aves_vivas_hembras,
            v_1.saldo_aves_vivas_machos,
            v_1.tipo_alimento,
            v_1.tipo_alimento_corto,
            v_1.ingreso_alimento_texto_hist,
            v_1.traslado_texto_hist,
            v_1.documento_hist,
            v_1.metadata_ingreso_alimento,
            v_1.metadata_traslado,
            v_1.metadata_documento,
            v_1.consumo_kg_hembras,
            v_1.consumo_kg_machos,
            v_1.consumo_real_dia_kg,
            v_1.consumo_acumulado_kg,
            v_1.consumo_bodega_kg,
            v_1.consumo_agua_diario,
            v_1.pct_perdidas_dia,
            v_1.peso_prom_hembras,
            v_1.peso_prom_machos,
            v_1.observaciones,
            v_1.metadata,
            v_1.items_adicionales,
            v_1.tipo_fila,
            v_1.uniformidad_hembras,
            v_1.uniformidad_machos,
            v_1.cv_hembras,
            v_1.cv_machos,
            v_1.consumo_agua_ph,
            v_1.consumo_agua_orp,
            v_1.consumo_agua_temperatura,
            v_1.ciclo,
            v_1.historico_consumo_alimento,
            v_1.despacho_peso_neto,
            v_1.despacho_peso_tara,
            v_1.despacho_promedio_peso_ave,
            v_1.created_by_user_id
           FROM ( SELECT v_1_1.seguimiento_id,
                    v_1_1.lote_ave_engorde_id,
                    v_1_1.lote_nombre,
                    v_1_1.company_id,
                    v_1_1.company_nombre,
                    v_1_1.granja_id,
                    v_1_1.granja_nombre,
                    v_1_1.galpon_id,
                    v_1_1.galpon_nombre,
                    v_1_1.nucleo_id,
                    v_1_1.nucleo_nombre,
                    v_1_1.fecha_dmy,
                    v_1_1.fecha_registro,
                    v_1_1.semana,
                    v_1_1.edad_dias_vida,
                    v_1_1.dia_calendario_corto,
                    v_1_1.mortalidad_hembras,
                    v_1_1.mortalidad_machos,
                    v_1_1.seleccion_hembras,
                    v_1_1.seleccion_machos,
                    v_1_1.total_mort_mas_sel_dia,
                    v_1_1.error_sexaje_hembras,
                    v_1_1.error_sexaje_machos,
                    v_1_1.despacho_hembras_hist,
                    v_1_1.despacho_machos_hist,
                    v_1_1.despacho_mixtas_hist,
                    v_1_1.saldo_alimento_kg_bd,
                    v_1_1.saldo_alimento_kg_calculado,
                    v_1_1.saldo_aves_vivas,
                    v_1_1.saldo_aves_vivas_hembras,
                    v_1_1.saldo_aves_vivas_machos,
                    v_1_1.tipo_alimento,
                    v_1_1.tipo_alimento_corto,
                    v_1_1.ingreso_alimento_texto_hist,
                    v_1_1.traslado_texto_hist,
                    v_1_1.documento_hist,
                    v_1_1.metadata_ingreso_alimento,
                    v_1_1.metadata_traslado,
                    v_1_1.metadata_documento,
                    v_1_1.consumo_kg_hembras,
                    v_1_1.consumo_kg_machos,
                    v_1_1.consumo_real_dia_kg,
                    v_1_1.consumo_acumulado_kg,
                    v_1_1.consumo_bodega_kg,
                    v_1_1.consumo_agua_diario,
                    v_1_1.pct_perdidas_dia,
                    v_1_1.peso_prom_hembras,
                    v_1_1.peso_prom_machos,
                    v_1_1.observaciones,
                    v_1_1.metadata,
                    v_1_1.items_adicionales,
                    v_1_1.tipo_fila,
                    v_1_1.uniformidad_hembras,
                    v_1_1.uniformidad_machos,
                    v_1_1.cv_hembras,
                    v_1_1.cv_machos,
                    v_1_1.consumo_agua_ph,
                    v_1_1.consumo_agua_orp,
                    v_1_1.consumo_agua_temperatura,
                    v_1_1.ciclo,
                    v_1_1.historico_consumo_alimento,
                    v_1_1.despacho_peso_neto,
                    v_1_1.despacho_peso_tara,
                    v_1_1.despacho_promedio_peso_ave,
                    v_1_1.created_by_user_id,
                        CASE
                            WHEN COALESCE(v_1_1.created_by_user_id, ''::character varying)::text = 'SYSTEM_CRUCE'::text OR COALESCE(v_1_1.consumo_kg_machos, 0::numeric) > 0::numeric THEN NULL::numeric
                            ELSE v_1_1.consumo_real_dia_kg
                        END AS consumo_kg_mixto,
                    NOT (COALESCE(v_1_1.created_by_user_id, ''::character varying)::text = 'SYSTEM_CRUCE'::text OR COALESCE(v_1_1.consumo_kg_machos, 0::numeric) > 0::numeric) AS consumo_es_mixto
                   FROM ( WITH lote_info AS (
                                 SELECT l.lote_ave_engorde_id,
                                    l.lote_nombre,
                                    l.fecha_encaset,
                                    l.granja_id,
                                    fa.name AS granja_nombre,
                                    fa.company_id,
                                    cp.name AS company_nombre,
                                    l.galpon_id,
                                    gp.galpon_nombre,
                                    l.nucleo_id,
                                    nu.nucleo_nombre,
                                    COALESCE(TRIM(BOTH FROM l.nucleo_id), ''::text) AS nucleo_id_t,
                                    COALESCE(TRIM(BOTH FROM l.galpon_id), ''::text) AS galpon_id_t,
                                    COALESCE(l.aves_encasetadas, 0) AS aves_encasetadas,
                                    COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0) AS suma_hm,
                                    COALESCE(l.hembras_l, 0)::bigint AS aves_iniciales_hembras,
                                    COALESCE(l.machos_l, 0)::bigint AS aves_iniciales_machos,
                                    GREATEST(0,
CASE
 WHEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0)) > 0 THEN COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0)
 ELSE COALESCE(l.aves_encasetadas, 0)
END)::bigint AS aves_iniciales,
                                    lower(COALESCE(l.estado_operativo_lote, ''::character varying)::text) AS estado_operativo_lote
                                   FROM lote_ave_engorde l
                                     LEFT JOIN farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
                                     LEFT JOIN companies cp ON cp.id = fa.company_id
                                     LEFT JOIN nucleos nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
                                     LEFT JOIN galpones gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
                                  WHERE l.deleted_at IS NULL
                                ), rango_seg AS (
                                 SELECT s.lote_ave_engorde_id,
                                    min(s.fecha)::date AS fecha_min,
                                    max(s.fecha)::date AS last_seg
                                   FROM seguimiento_diario_aves_engorde s
                                  GROUP BY s.lote_ave_engorde_id
                                ), apert_mov AS (
                                 SELECT li.lote_ave_engorde_id,
                                    h.fecha_operacion AS f,
                                    h.created_at AS ts,
CASE h.tipo_evento
 WHEN 'INV_INGRESO'::text THEN COALESCE(h.cantidad_kg, 0::numeric)
 WHEN 'INV_TRASLADO_ENTRADA'::text THEN COALESCE(h.cantidad_kg, 0::numeric)
 WHEN 'INV_TRASLADO_SALIDA'::text THEN - abs(COALESCE(h.cantidad_kg, 0::numeric))
 ELSE 0::numeric
END AS delta
                                   FROM lote_info li
                                     JOIN rango_seg rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id AND rs.fecha_min IS NOT NULL
                                     JOIN lote_registro_historico_unificado h ON h.farm_id = li.granja_id AND COALESCE(TRIM(BOTH FROM h.nucleo_id), ''::text) = li.nucleo_id_t AND COALESCE(TRIM(BOTH FROM h.galpon_id), ''::text) = li.galpon_id_t
                                  WHERE NOT h.anulado AND (h.tipo_evento::text = ANY (ARRAY['INV_INGRESO'::character varying::text, 'INV_TRASLADO_ENTRADA'::character varying::text, 'INV_TRASLADO_SALIDA'::character varying::text])) AND NOT (h.tipo_evento::text = 'INV_INGRESO'::text AND h.referencia IS NOT NULL AND h.referencia::text ~~ 'Seguimiento aves engorde #%'::text) AND NOT (h.referencia IS NOT NULL AND (h.referencia::text ~~ '%devolución por eliminación%'::text OR h.referencia::text ~~ '%devolucion por eliminacion%'::text)) AND h.fecha_operacion < rs.fecha_min AND (li.fecha_encaset IS NULL OR h.fecha_operacion >= li.fecha_encaset::date)
                                ), apert_run AS (
                                 SELECT apert_mov.lote_ave_engorde_id,
                                    apert_mov.delta,
                                    sum(apert_mov.delta) OVER (PARTITION BY apert_mov.lote_ave_engorde_id ORDER BY apert_mov.f, apert_mov.ts ROWS UNBOUNDED PRECEDING) AS p
                                   FROM apert_mov
                                ), apertura_alimento AS (
                                 SELECT apert_run.lote_ave_engorde_id,
                                    (sum(apert_run.delta) - LEAST(0::numeric, min(apert_run.p)))::double precision AS apertura_kg
                                   FROM apert_run
                                  GROUP BY apert_run.lote_ave_engorde_id
                                ), hist_full AS (
                                 SELECT li.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    sum(
CASE
 WHEN h.tipo_evento::text = 'INV_INGRESO'::text AND NOT (h.referencia IS NOT NULL AND h.referencia::text ~~ 'Seguimiento aves engorde #%'::text) THEN COALESCE(h.cantidad_kg, 0::numeric)
 WHEN h.tipo_evento::text = 'INV_TRASLADO_ENTRADA'::text THEN COALESCE(h.cantidad_kg, 0::numeric)
 WHEN h.tipo_evento::text = 'INV_TRASLADO_SALIDA'::text THEN - abs(COALESCE(h.cantidad_kg, 0::numeric))
 ELSE 0::numeric
END)::double precision AS neto_kg
                                   FROM lote_info li
                                     JOIN rango_seg rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
                                     JOIN lote_registro_historico_unificado h ON h.farm_id = li.granja_id AND COALESCE(TRIM(BOTH FROM h.nucleo_id), ''::text) = li.nucleo_id_t AND COALESCE(TRIM(BOTH FROM h.galpon_id), ''::text) = li.galpon_id_t
                                  WHERE NOT h.anulado AND (h.tipo_evento::text = ANY (ARRAY['INV_INGRESO'::character varying::text, 'INV_TRASLADO_ENTRADA'::character varying::text, 'INV_TRASLADO_SALIDA'::character varying::text])) AND NOT (h.referencia IS NOT NULL AND (h.referencia::text ~~ '%devolución por eliminación%'::text OR h.referencia::text ~~ '%devolucion por eliminacion%'::text)) AND (rs.fecha_min IS NULL OR h.fecha_operacion >= rs.fecha_min)
                                  GROUP BY li.lote_ave_engorde_id, h.fecha_operacion
                                ), consumo_por_fecha AS (
                                 SELECT s.lote_ave_engorde_id,
                                    date(s.fecha) AS fecha,
                                    sum(COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric))::double precision AS cons_kg
                                   FROM seguimiento_diario_aves_engorde s
                                  GROUP BY s.lote_ave_engorde_id, (date(s.fecha))
                                ), saldo_dates AS (
                                 SELECT hist_full.lote_ave_engorde_id,
                                    hist_full.fecha
                                   FROM hist_full
                                UNION
                                 SELECT consumo_por_fecha.lote_ave_engorde_id,
                                    consumo_por_fecha.fecha
                                   FROM consumo_por_fecha
                                ), saldo_running AS (
                                 SELECT sd.lote_ave_engorde_id,
                                    sd.fecha,
                                    GREATEST(0::double precision, COALESCE(aa.apertura_kg, 0::double precision) + COALESCE(sum(hf.neto_kg) OVER (PARTITION BY sd.lote_ave_engorde_id ORDER BY sd.fecha ROWS UNBOUNDED PRECEDING), 0::double precision) - COALESCE(sum(cf.cons_kg) OVER (PARTITION BY sd.lote_ave_engorde_id ORDER BY sd.fecha ROWS UNBOUNDED PRECEDING), 0::double precision)) AS saldo
                                   FROM saldo_dates sd
                                     LEFT JOIN hist_full hf ON hf.lote_ave_engorde_id = sd.lote_ave_engorde_id AND hf.fecha = sd.fecha
                                     LEFT JOIN consumo_por_fecha cf ON cf.lote_ave_engorde_id = sd.lote_ave_engorde_id AND cf.fecha = sd.fecha
                                     LEFT JOIN apertura_alimento aa ON aa.lote_ave_engorde_id = sd.lote_ave_engorde_id
                                ), saldo_close AS (
                                 SELECT sr.lote_ave_engorde_id,
                                    min(sr.fecha) AS close_date
                                   FROM saldo_running sr
                                     JOIN rango_seg rs ON rs.lote_ave_engorde_id = sr.lote_ave_engorde_id
                                  WHERE rs.last_seg IS NOT NULL AND sr.fecha >= rs.last_seg AND sr.saldo <= 0.5::double precision
                                  GROUP BY sr.lote_ave_engorde_id
                                ), corte_ciclo_siguiente AS (
                                 -- v14 en la vista (31-ago-2026). Espejo set-based del CTE homónimo de
                                 -- fn_seguimiento_diario_engorde, que la vista declara reproducir y que
                                 -- nunca recibió: el galpón deja de ser mío el día que OTRO lote empieza
                                 -- a registrar seguimiento en él. Sin esto, un lote sin cierre por saldo
                                 -- y sin estado 'cerrado' queda con fecha_max NULL, o sea sin tope, y se
                                 -- come el alimento del ciclo siguiente.
                                 -- Estrictamente POSTERIOR (primer_seg > last_seg): los lotes que
                                 -- CONVIVEN conmigo no cortan nada.
                                 SELECT rs.lote_ave_engorde_id,
                                    min(prim.primer_seg) - 1 AS hasta
                                   FROM rango_seg rs
                                     JOIN lote_info li ON li.lote_ave_engorde_id = rs.lote_ave_engorde_id
                                     JOIN ( SELECT l2.lote_ave_engorde_id,
                                              l2.granja_id,
                                              COALESCE(TRIM(BOTH FROM l2.nucleo_id), ''::text) AS nucleo_id_t,
                                              COALESCE(TRIM(BOTH FROM l2.galpon_id), ''::text) AS galpon_id_t,
                                              min(s2.fecha::date) AS primer_seg
                                             FROM seguimiento_diario_aves_engorde s2
                                               JOIN lote_ave_engorde l2 ON l2.lote_ave_engorde_id = s2.lote_ave_engorde_id AND l2.deleted_at IS NULL
                                            GROUP BY l2.lote_ave_engorde_id, l2.granja_id, COALESCE(TRIM(BOTH FROM l2.nucleo_id), ''::text), COALESCE(TRIM(BOTH FROM l2.galpon_id), ''::text)
                                          ) prim ON prim.granja_id = li.granja_id
                                            AND prim.nucleo_id_t = li.nucleo_id_t
                                            AND prim.galpon_id_t = li.galpon_id_t
                                            AND prim.lote_ave_engorde_id <> rs.lote_ave_engorde_id
                                            AND prim.primer_seg > rs.last_seg
                                  WHERE rs.last_seg IS NOT NULL
                                  GROUP BY rs.lote_ave_engorde_id
                                ), rango_final AS (
                                 SELECT rs.lote_ave_engorde_id,
                                    rs.fecha_min,
                                    -- LEAST ignora los NULL: un lote SIN ciclo posterior conserva
                                    -- exactamente el fecha_max de antes. Por eso el LEFT JOIN de abajo
                                    -- hace que esto no sea un cambio de comportamiento.
                                    LEAST(COALESCE(sc.close_date,
CASE
 WHEN li.estado_operativo_lote = 'cerrado'::text THEN rs.last_seg
 ELSE NULL::date
END), cc.hasta) AS fecha_max
                                   FROM rango_seg rs
                                     JOIN lote_info li ON li.lote_ave_engorde_id = rs.lote_ave_engorde_id
                                     LEFT JOIN saldo_close sc ON sc.lote_ave_engorde_id = rs.lote_ave_engorde_id
                                     LEFT JOIN corte_ciclo_siguiente cc ON cc.lote_ave_engorde_id = rs.lote_ave_engorde_id
                                ), salidas_totales AS (
                                 SELECT s.lote_ave_engorde_id,
                                    COALESCE(sum(COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0)), 0::bigint) AS bajas_seguimiento
                                   FROM seguimiento_diario_aves_engorde s
                                  GROUP BY s.lote_ave_engorde_id
                                ), ventas_totales AS (
                                 SELECT h.lote_ave_engorde_id,
                                    COALESCE(sum(COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)), 0::bigint) AS total_ventas
                                   FROM lote_registro_historico_unificado h
                                  WHERE h.tipo_evento::text = 'VENTA_AVES'::text AND NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
                                  GROUP BY h.lote_ave_engorde_id
                                ), aves_iniciales AS (
                                 SELECT li.lote_ave_engorde_id,
CASE
 WHEN li.estado_operativo_lote = 'cerrado'::text THEN GREATEST(1::bigint, COALESCE(st.bajas_seguimiento, 0::bigint) + COALESCE(vt.total_ventas, 0::bigint))
 WHEN li.aves_encasetadas > 0 AND li.suma_hm = 0 THEN li.aves_encasetadas::bigint
 WHEN li.suma_hm > 0 AND li.aves_encasetadas = 0 THEN li.suma_hm::bigint
 WHEN li.aves_encasetadas = li.suma_hm THEN li.aves_encasetadas::bigint
 ELSE li.aves_encasetadas::bigint
END AS inicial
                                   FROM lote_info li
                                     LEFT JOIN salidas_totales st ON st.lote_ave_engorde_id = li.lote_ave_engorde_id
                                     LEFT JOIN ventas_totales vt ON vt.lote_ave_engorde_id = li.lote_ave_engorde_id
                                ), ventas_por_fecha AS (
                                 SELECT h.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    COALESCE(sum(COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)), 0::bigint) AS ventas_dia,
                                    COALESCE(sum(COALESCE(h.cantidad_hembras, 0)), 0::bigint) AS despacho_h,
                                    COALESCE(sum(COALESCE(h.cantidad_machos, 0)), 0::bigint) AS despacho_m,
                                    COALESCE(sum(COALESCE(h.cantidad_mixtas, 0)), 0::bigint) AS despacho_x,
                                    COALESCE(sum(COALESCE(h.peso_neto, 0::numeric)), 0::numeric)::double precision AS despacho_peso_neto,
                                    COALESCE(sum(COALESCE(h.peso_tara_real, 0::numeric)), 0::numeric)::double precision AS despacho_peso_tara
                                   FROM lote_registro_historico_unificado h
                                  WHERE h.tipo_evento::text = 'VENTA_AVES'::text AND NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
                                  GROUP BY h.lote_ave_engorde_id, h.fecha_operacion
                                ), consumo_bodega_por_fecha AS (
                                 SELECT h.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    sum(
CASE
 WHEN h.tipo_evento::text = 'INV_CONSUMO'::text AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0::numeric)
 ELSE 0::numeric
END)::double precision AS consumo_bodega_kg
                                   FROM lote_registro_historico_unificado h
                                  WHERE NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
                                  GROUP BY h.lote_ave_engorde_id, h.fecha_operacion
                                ), hist_alimento AS (
                                 SELECT li.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    COALESCE(sum(
CASE
 WHEN h.tipo_evento::text = 'INV_INGRESO'::text AND NOT (h.referencia IS NOT NULL AND h.referencia::text ~~ 'Seguimiento aves engorde #%'::text) THEN COALESCE(h.cantidad_kg, 0::numeric)
 ELSE 0::numeric
END), 0::numeric)::double precision AS ingreso_kg,
                                    COALESCE(sum(
CASE
 WHEN h.tipo_evento::text = 'INV_TRASLADO_ENTRADA'::text THEN COALESCE(h.cantidad_kg, 0::numeric)
 ELSE 0::numeric
END), 0::numeric)::double precision AS traslado_entrada_kg,
                                    COALESCE(sum(
CASE
 WHEN h.tipo_evento::text = 'INV_TRASLADO_SALIDA'::text THEN abs(COALESCE(h.cantidad_kg, 0::numeric))
 ELSE 0::numeric
END), 0::numeric)::double precision AS traslado_salida_kg
                                   FROM lote_info li
                                     JOIN rango_final rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
                                     JOIN lote_registro_historico_unificado h ON h.farm_id = li.granja_id AND COALESCE(TRIM(BOTH FROM h.nucleo_id), ''::text) = li.nucleo_id_t AND COALESCE(TRIM(BOTH FROM h.galpon_id), ''::text) = li.galpon_id_t
                                  WHERE NOT h.anulado AND NOT (h.referencia IS NOT NULL AND (h.referencia::text ~~ '%devolución por eliminación%'::text OR h.referencia::text ~~ '%devolucion por eliminacion%'::text)) AND (h.tipo_evento::text = ANY (ARRAY['INV_INGRESO'::character varying::text, 'INV_TRASLADO_ENTRADA'::character varying::text, 'INV_TRASLADO_SALIDA'::character varying::text])) AND (rs.fecha_min IS NULL OR h.fecha_operacion >= rs.fecha_min) AND (rs.fecha_max IS NULL OR h.fecha_operacion <= rs.fecha_max)
                                  GROUP BY li.lote_ave_engorde_id, h.fecha_operacion
                                ), docs_por_fecha AS (
                                 SELECT li.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    string_agg(DISTINCT NULLIF(TRIM(BOTH FROM COALESCE(h.numero_documento, h.referencia, ''::character varying)), ''::text), ', '::text) AS documento
                                   FROM lote_info li
                                     JOIN rango_final rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
                                     JOIN lote_registro_historico_unificado h ON true
                                  WHERE NOT h.anulado AND NOT (h.referencia IS NOT NULL AND (h.referencia::text ~~ '%devolución por eliminación%'::text OR h.referencia::text ~~ '%devolucion por eliminacion%'::text)) AND (h.tipo_evento::text = 'INV_INGRESO'::text AND NOT (h.referencia IS NOT NULL AND h.referencia::text ~~ 'Seguimiento aves engorde #%'::text) AND h.farm_id = li.granja_id AND COALESCE(TRIM(BOTH FROM h.nucleo_id), ''::text) = li.nucleo_id_t AND COALESCE(TRIM(BOTH FROM h.galpon_id), ''::text) = li.galpon_id_t AND (rs.fecha_min IS NULL OR h.fecha_operacion >= rs.fecha_min) AND (rs.fecha_max IS NULL OR h.fecha_operacion <= rs.fecha_max) OR h.tipo_evento::text = 'VENTA_AVES'::text AND h.lote_ave_engorde_id = li.lote_ave_engorde_id)
                                  GROUP BY li.lote_ave_engorde_id, h.fecha_operacion
                                ), fechas_universo AS (
                                 SELECT s.lote_ave_engorde_id,
                                    date(s.fecha) AS fecha,
                                    s.id AS seg_id
                                   FROM seguimiento_diario_aves_engorde s
                                UNION ALL
                                 SELECT li.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    NULL::bigint AS seg_id
                                   FROM lote_info li
                                     JOIN rango_final rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
                                     JOIN lote_registro_historico_unificado h ON true
                                  WHERE NOT h.anulado AND NOT (h.referencia IS NOT NULL AND (h.referencia::text ~~ '%devolución por eliminación%'::text OR h.referencia::text ~~ '%devolucion por eliminacion%'::text)) AND ((h.tipo_evento::text = ANY (ARRAY['INV_INGRESO'::character varying::text, 'INV_TRASLADO_ENTRADA'::character varying::text, 'INV_TRASLADO_SALIDA'::character varying::text])) AND NOT (h.tipo_evento::text = 'INV_INGRESO'::text AND h.referencia IS NOT NULL AND h.referencia::text ~~ 'Seguimiento aves engorde #%'::text) AND h.farm_id = li.granja_id AND COALESCE(TRIM(BOTH FROM h.nucleo_id), ''::text) = li.nucleo_id_t AND COALESCE(TRIM(BOTH FROM h.galpon_id), ''::text) = li.galpon_id_t AND (rs.fecha_min IS NULL OR h.fecha_operacion >= rs.fecha_min) AND (rs.fecha_max IS NULL OR h.fecha_operacion <= rs.fecha_max) OR h.tipo_evento::text = 'VENTA_AVES'::text AND h.lote_ave_engorde_id = li.lote_ave_engorde_id) AND (li.fecha_encaset IS NULL OR h.fecha_operacion >= li.fecha_encaset::date) AND NOT (EXISTS ( SELECT 1
   FROM seguimiento_diario_aves_engorde s2
  WHERE s2.lote_ave_engorde_id = li.lote_ave_engorde_id AND date(s2.fecha) = h.fecha_operacion))
                                  GROUP BY li.lote_ave_engorde_id, h.fecha_operacion
                                ), seg_enriquecido AS (
                                 SELECT fu.lote_ave_engorde_id,
                                    s.id AS seg_id,
                                    fu.fecha,
CASE
 WHEN li.fecha_encaset IS NOT NULL THEN GREATEST(0, fu.fecha - date(li.fecha_encaset))
 ELSE 0
END AS edad_dia,
                                    LEAST(8::numeric, GREATEST(1::numeric, ceil((
CASE
 WHEN li.fecha_encaset IS NOT NULL THEN GREATEST(0, fu.fecha - date(li.fecha_encaset))
 ELSE 0
END + 1)::numeric / 7.0)))::smallint AS semana,
                                    COALESCE(s.mortalidad_hembras, 0) AS mortalidad_hembras,
                                    COALESCE(s.mortalidad_machos, 0) AS mortalidad_machos,
                                    COALESCE(s.sel_h, 0) AS sel_h,
                                    COALESCE(s.sel_m, 0) AS sel_m,
                                    COALESCE(s.error_sexaje_hembras, 0) AS error_sexaje_hembras,
                                    COALESCE(s.error_sexaje_machos, 0) AS error_sexaje_machos,
                                    COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) AS total_mort_sel_dia,
                                    COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_totales_dia,
                                    COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.error_sexaje_hembras, 0) AS perdidas_hembras_dia,
                                    COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_m, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_machos_dia,
                                    COALESCE(s.consumo_kg_hembras, 0::numeric)::double precision AS consumo_kg_hembras,
                                    COALESCE(s.consumo_kg_machos, 0::numeric)::double precision AS consumo_kg_machos,
                                    (COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric))::double precision AS consumo_dia_kg,
                                    s.saldo_alimento_kg::double precision AS saldo_alimento_kg_bd,
                                    s.tipo_alimento,
                                    s.peso_prom_hembras,
                                    s.peso_prom_machos,
                                    s.uniformidad_hembras,
                                    s.uniformidad_machos,
                                    s.cv_hembras,
                                    s.cv_machos,
                                    s.consumo_agua_diario,
                                    s.consumo_agua_ph,
                                    s.consumo_agua_orp,
                                    s.consumo_agua_temperatura,
                                    s.observaciones,
                                    s.ciclo,
                                    s.metadata,
                                    s.items_adicionales,
                                    s.historico_consumo_alimento,
                                    s.created_by_user_id,
                                    COALESCE(vpf.ventas_dia, 0::bigint) AS ventas_dia,
                                    COALESCE(vpf.despacho_h, 0::bigint) AS despacho_h,
                                    COALESCE(vpf.despacho_m, 0::bigint) AS despacho_m,
                                    COALESCE(vpf.despacho_x, 0::bigint) AS despacho_x,
                                    COALESCE(vpf.despacho_peso_neto, 0::double precision) AS despacho_peso_neto,
                                    COALESCE(vpf.despacho_peso_tara, 0::double precision) AS despacho_peso_tara,
                                    COALESCE(ha.ingreso_kg, 0::double precision) AS ingreso_alimento_kg,
                                    COALESCE(ha.traslado_entrada_kg, 0::double precision) AS traslado_entrada_kg,
                                    COALESCE(ha.traslado_salida_kg, 0::double precision) AS traslado_salida_kg,
                                    COALESCE(cb.consumo_bodega_kg, 0::double precision) AS consumo_bodega_kg,
                                    dpf.documento
                                   FROM fechas_universo fu
                                     JOIN lote_info li ON li.lote_ave_engorde_id = fu.lote_ave_engorde_id
                                     LEFT JOIN seguimiento_diario_aves_engorde s ON s.id = fu.seg_id
                                     LEFT JOIN ventas_por_fecha vpf ON vpf.lote_ave_engorde_id = fu.lote_ave_engorde_id AND vpf.fecha = fu.fecha
                                     LEFT JOIN hist_alimento ha ON ha.lote_ave_engorde_id = fu.lote_ave_engorde_id AND ha.fecha = fu.fecha
                                     LEFT JOIN consumo_bodega_por_fecha cb ON cb.lote_ave_engorde_id = fu.lote_ave_engorde_id AND cb.fecha = fu.fecha
                                     LEFT JOIN docs_por_fecha dpf ON dpf.lote_ave_engorde_id = fu.lote_ave_engorde_id AND dpf.fecha = fu.fecha
                                ), universo_fechas_distinct AS (
                                 SELECT DISTINCT fechas_universo.lote_ave_engorde_id,
                                    fechas_universo.fecha
                                   FROM fechas_universo
                                ), alim_cum AS (
                                 SELECT u.lote_ave_engorde_id,
                                    u.fecha,
                                    sum(COALESCE(ha.ingreso_kg + ha.traslado_entrada_kg - ha.traslado_salida_kg, 0::double precision)) OVER (PARTITION BY u.lote_ave_engorde_id ORDER BY u.fecha ROWS UNBOUNDED PRECEDING) AS alim_cum_kg
                                   FROM universo_fechas_distinct u
                                     LEFT JOIN hist_alimento ha ON ha.lote_ave_engorde_id = u.lote_ave_engorde_id AND ha.fecha = u.fecha
                                ), pt_calc AS (
                                 SELECT se.lote_ave_engorde_id,
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
                                    se.perdidas_hembras_dia,
                                    se.perdidas_machos_dia,
                                    se.consumo_kg_hembras,
                                    se.consumo_kg_machos,
                                    se.consumo_dia_kg,
                                    se.saldo_alimento_kg_bd,
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
                                    se.created_by_user_id,
                                    se.ventas_dia,
                                    se.despacho_h,
                                    se.despacho_m,
                                    se.despacho_x,
                                    se.despacho_peso_neto,
                                    se.despacho_peso_tara,
                                    se.ingreso_alimento_kg,
                                    se.traslado_entrada_kg,
                                    se.traslado_salida_kg,
                                    se.consumo_bodega_kg,
                                    se.documento,
                                    COALESCE(aa.apertura_kg, 0::double precision) + COALESCE(ac.alim_cum_kg, 0::double precision) - sum(se.consumo_dia_kg) OVER (PARTITION BY se.lote_ave_engorde_id ORDER BY se.fecha, (COALESCE(se.seg_id, 0::bigint)) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS pt
                                   FROM seg_enriquecido se
                                     LEFT JOIN apertura_alimento aa ON aa.lote_ave_engorde_id = se.lote_ave_engorde_id
                                     LEFT JOIN alim_cum ac ON ac.lote_ave_engorde_id = se.lote_ave_engorde_id AND ac.fecha = se.fecha
                                ), calc AS (
                                 SELECT se.lote_ave_engorde_id,
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
                                    se.perdidas_hembras_dia,
                                    se.perdidas_machos_dia,
                                    se.consumo_kg_hembras,
                                    se.consumo_kg_machos,
                                    se.consumo_dia_kg,
                                    se.saldo_alimento_kg_bd,
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
                                    se.created_by_user_id,
                                    se.ventas_dia,
                                    se.despacho_h,
                                    se.despacho_m,
                                    se.despacho_x,
                                    se.despacho_peso_neto,
                                    se.despacho_peso_tara,
                                    se.ingreso_alimento_kg,
                                    se.traslado_entrada_kg,
                                    se.traslado_salida_kg,
                                    se.consumo_bodega_kg,
                                    se.documento,
                                    se.pt,
                                    ai.inicial,
                                    li.lote_nombre,
                                    li.fecha_encaset,
                                    li.granja_id,
                                    li.granja_nombre,
                                    li.company_id,
                                    li.company_nombre,
                                    li.galpon_id,
                                    li.galpon_nombre,
                                    li.nucleo_id,
                                    li.nucleo_nombre,
                                    li.aves_iniciales,
                                    li.aves_iniciales_hembras,
                                    li.aves_iniciales_machos,
                                    sum(se.consumo_dia_kg) OVER w_ord AS acum_consumo_kg,
                                    GREATEST(0::numeric, ai.inicial::numeric - sum(se.perdidas_totales_dia + se.ventas_dia) OVER w_ord)::integer AS saldo_aves_vivas,
                                    GREATEST(0::numeric, li.aves_iniciales_hembras::numeric - sum(se.perdidas_hembras_dia + se.despacho_h) OVER w_ord)::bigint AS saldo_aves_vivas_hembras,
                                    GREATEST(0::numeric, li.aves_iniciales_machos::numeric - sum(se.perdidas_machos_dia + se.despacho_m) OVER w_ord)::bigint AS saldo_aves_vivas_machos,
CASE
 WHEN GREATEST(0::numeric, ai.inicial::numeric - COALESCE(sum(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0::numeric)) > 0::numeric THEN round(100.0 * se.total_mort_sel_dia::numeric / GREATEST(0::numeric, ai.inicial::numeric - COALESCE(sum(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0::numeric)), 2)
 WHEN se.total_mort_sel_dia > 0 THEN 100::numeric
 ELSE NULL::numeric
END AS pct_perdidas_dia,
                                    se.pt - LEAST(0::double precision, min(se.pt) OVER w_ord) AS saldo_alimento_kg_calc
                                   FROM pt_calc se
                                     JOIN lote_info li ON li.lote_ave_engorde_id = se.lote_ave_engorde_id
                                     JOIN aves_iniciales ai ON ai.lote_ave_engorde_id = se.lote_ave_engorde_id
                                  WINDOW w_ord AS (PARTITION BY se.lote_ave_engorde_id ORDER BY se.fecha, (COALESCE(se.seg_id, 0::bigint)) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW), w_prev AS (PARTITION BY se.lote_ave_engorde_id ORDER BY se.fecha, (COALESCE(se.seg_id, 0::bigint)) ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
                                )
                         SELECT calc.seg_id AS seguimiento_id,
                            calc.lote_ave_engorde_id,
                            calc.lote_nombre,
                            calc.company_id,
                            calc.company_nombre,
                            calc.granja_id,
                            calc.granja_nombre,
                            calc.galpon_id,
                            calc.galpon_nombre,
                            calc.nucleo_id,
                            calc.nucleo_nombre,
                            to_char(calc.fecha::timestamp with time zone, 'DD/MM/YYYY'::text) AS fecha_dmy,
                            calc.fecha AS fecha_registro,
                            calc.semana,
                            calc.edad_dia AS edad_dias_vida,
                            to_char(calc.fecha::timestamp with time zone, 'Dy, DD Mon'::text) AS dia_calendario_corto,
                            calc.mortalidad_hembras,
                            calc.mortalidad_machos,
                            calc.sel_h AS seleccion_hembras,
                            calc.sel_m AS seleccion_machos,
                            calc.total_mort_sel_dia AS total_mort_mas_sel_dia,
                            calc.error_sexaje_hembras,
                            calc.error_sexaje_machos,
                            calc.despacho_h AS despacho_hembras_hist,
                            calc.despacho_m AS despacho_machos_hist,
                            calc.despacho_x AS despacho_mixtas_hist,
                            trim_scale(calc.saldo_alimento_kg_bd::numeric) AS saldo_alimento_kg_bd,
                            trim_scale(calc.saldo_alimento_kg_calc::numeric) AS saldo_alimento_kg_calculado,
                            calc.saldo_aves_vivas,
                            calc.saldo_aves_vivas_hembras,
                            calc.saldo_aves_vivas_machos,
                            calc.tipo_alimento,
                                CASE
                                    WHEN upper(COALESCE(calc.tipo_alimento, ''::character varying)::text) ~~ '%PRE%'::text THEN 'PRE'::text
                                    WHEN upper(COALESCE(calc.tipo_alimento, ''::character varying)::text) ~~ '%INI%'::text THEN 'INI'::text
                                    WHEN upper(COALESCE(calc.tipo_alimento, ''::character varying)::text) ~~ '%ENG%'::text THEN 'ENG'::text
                                    WHEN upper(COALESCE(calc.tipo_alimento, ''::character varying)::text) ~~ '%FIN%'::text THEN 'FIN-D'::text
                                    WHEN COALESCE(calc.tipo_alimento, ''::character varying)::text = ''::text THEN '—'::text
                                    ELSE ""left""(calc.tipo_alimento::text, 8)
                                END AS tipo_alimento_corto,
                                CASE
                                    WHEN COALESCE(calc.ingreso_alimento_kg, 0::double precision) > 0::double precision THEN to_char(calc.ingreso_alimento_kg::numeric, 'FM9999999999990.999'::text) || ' kg'::text
                                    ELSE NULL::text
                                END AS ingreso_alimento_texto_hist,
                                CASE
                                    WHEN COALESCE(calc.traslado_entrada_kg, 0::double precision) = 0::double precision AND COALESCE(calc.traslado_salida_kg, 0::double precision) = 0::double precision THEN NULL::text
                                    ELSE concat_ws(' · '::text,
                                    CASE
WHEN COALESCE(calc.traslado_entrada_kg, 0::double precision) > 0::double precision THEN ('Entrada '::text || to_char(calc.traslado_entrada_kg::numeric, 'FM9999999999990.999'::text)) || ' kg'::text
ELSE NULL::text
                                    END,
                                    CASE
WHEN COALESCE(calc.traslado_salida_kg, 0::double precision) > 0::double precision THEN ('Salida '::text || to_char(calc.traslado_salida_kg::numeric, 'FM9999999999990.999'::text)) || ' kg'::text
ELSE NULL::text
                                    END)
                                END AS traslado_texto_hist,
                            COALESCE(calc.documento, ''::text) AS documento_hist,
                            calc.metadata ->> 'ingresoAlimento'::text AS metadata_ingreso_alimento,
                            calc.metadata ->> 'traslado'::text AS metadata_traslado,
                            calc.metadata ->> 'documento'::text AS metadata_documento,
                            trim_scale(calc.consumo_kg_hembras::numeric) AS consumo_kg_hembras,
                            trim_scale(calc.consumo_kg_machos::numeric) AS consumo_kg_machos,
                            trim_scale(calc.consumo_dia_kg::numeric) AS consumo_real_dia_kg,
                            trim_scale(calc.acum_consumo_kg::numeric) AS consumo_acumulado_kg,
                            trim_scale(calc.consumo_bodega_kg::numeric) AS consumo_bodega_kg,
                            trim_scale(calc.consumo_agua_diario::numeric) AS consumo_agua_diario,
                            trim_scale(calc.pct_perdidas_dia) AS pct_perdidas_dia,
                            trim_scale(calc.peso_prom_hembras::numeric) AS peso_prom_hembras,
                            trim_scale(calc.peso_prom_machos::numeric) AS peso_prom_machos,
                            calc.observaciones,
                            calc.metadata,
                            calc.items_adicionales,
                                CASE
                                    WHEN calc.seg_id IS NULL THEN 'movimiento'::text
                                    ELSE 'seguimiento'::text
                                END AS tipo_fila,
                            trim_scale(calc.uniformidad_hembras::numeric) AS uniformidad_hembras,
                            trim_scale(calc.uniformidad_machos::numeric) AS uniformidad_machos,
                            trim_scale(calc.cv_hembras::numeric) AS cv_hembras,
                            trim_scale(calc.cv_machos::numeric) AS cv_machos,
                            trim_scale(calc.consumo_agua_ph::numeric) AS consumo_agua_ph,
                            trim_scale(calc.consumo_agua_orp::numeric) AS consumo_agua_orp,
                            trim_scale(calc.consumo_agua_temperatura::numeric) AS consumo_agua_temperatura,
                            calc.ciclo,
                            calc.historico_consumo_alimento,
                            trim_scale(calc.despacho_peso_neto::numeric) AS despacho_peso_neto,
                            trim_scale(calc.despacho_peso_tara::numeric) AS despacho_peso_tara,
                            trim_scale(
                                CASE
                                    WHEN (calc.despacho_h + calc.despacho_m + calc.despacho_x) > 0 THEN calc.despacho_peso_neto / (calc.despacho_h + calc.despacho_m + calc.despacho_x)::double precision
                                    ELSE 0::double precision
                                END::numeric) AS despacho_promedio_peso_ave,
                            calc.created_by_user_id
                           FROM calc
                          ORDER BY calc.lote_ave_engorde_id, calc.fecha, (COALESCE(calc.seg_id, 0::bigint))) v_1_1) v_1) v;";

        private const string VISTA_SIN_CORTE = @"CREATE OR REPLACE VIEW public.vw_seguimiento_pollo_engorde AS
SELECT seguimiento_id,
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
    fecha_dmy,
    fecha_registro,
    semana,
    edad_dias_vida,
    dia_calendario_corto,
    mortalidad_hembras,
    mortalidad_machos,
    seleccion_hembras,
    seleccion_machos,
    total_mort_mas_sel_dia,
    error_sexaje_hembras,
    error_sexaje_machos,
    despacho_hembras_hist,
    despacho_machos_hist,
    despacho_mixtas_hist,
    saldo_alimento_kg_bd,
    saldo_alimento_kg_calculado,
    saldo_aves_vivas,
    saldo_aves_vivas_hembras,
    saldo_aves_vivas_machos,
    tipo_alimento,
    tipo_alimento_corto,
    ingreso_alimento_texto_hist,
    traslado_texto_hist,
    documento_hist,
    metadata_ingreso_alimento,
    metadata_traslado,
    metadata_documento,
    consumo_kg_hembras,
    consumo_kg_machos,
    consumo_real_dia_kg,
    consumo_acumulado_kg,
    consumo_bodega_kg,
    consumo_agua_diario,
    pct_perdidas_dia,
    peso_prom_hembras,
    peso_prom_machos,
    observaciones,
    metadata,
    items_adicionales,
    tipo_fila,
    uniformidad_hembras,
    uniformidad_machos,
    cv_hembras,
    cv_machos,
    consumo_agua_ph,
    consumo_agua_orp,
    consumo_agua_temperatura,
    ciclo,
    historico_consumo_alimento,
    despacho_peso_neto,
    despacho_peso_tara,
    despacho_promedio_peso_ave,
    created_by_user_id,
        CASE
            WHEN COALESCE(created_by_user_id, ''::character varying)::text = 'SYSTEM_CRUCE'::text OR COALESCE(consumo_kg_machos, 0::numeric) > 0::numeric THEN NULL::numeric
            ELSE consumo_real_dia_kg
        END AS consumo_kg_mixto,
    NOT (COALESCE(created_by_user_id, ''::character varying)::text = 'SYSTEM_CRUCE'::text OR COALESCE(consumo_kg_machos, 0::numeric) > 0::numeric) AS consumo_es_mixto
   FROM ( SELECT v_1.seguimiento_id,
            v_1.lote_ave_engorde_id,
            v_1.lote_nombre,
            v_1.company_id,
            v_1.company_nombre,
            v_1.granja_id,
            v_1.granja_nombre,
            v_1.galpon_id,
            v_1.galpon_nombre,
            v_1.nucleo_id,
            v_1.nucleo_nombre,
            v_1.fecha_dmy,
            v_1.fecha_registro,
            v_1.semana,
            v_1.edad_dias_vida,
            v_1.dia_calendario_corto,
            v_1.mortalidad_hembras,
            v_1.mortalidad_machos,
            v_1.seleccion_hembras,
            v_1.seleccion_machos,
            v_1.total_mort_mas_sel_dia,
            v_1.error_sexaje_hembras,
            v_1.error_sexaje_machos,
            v_1.despacho_hembras_hist,
            v_1.despacho_machos_hist,
            v_1.despacho_mixtas_hist,
            v_1.saldo_alimento_kg_bd,
            v_1.saldo_alimento_kg_calculado,
            v_1.saldo_aves_vivas,
            v_1.saldo_aves_vivas_hembras,
            v_1.saldo_aves_vivas_machos,
            v_1.tipo_alimento,
            v_1.tipo_alimento_corto,
            v_1.ingreso_alimento_texto_hist,
            v_1.traslado_texto_hist,
            v_1.documento_hist,
            v_1.metadata_ingreso_alimento,
            v_1.metadata_traslado,
            v_1.metadata_documento,
            v_1.consumo_kg_hembras,
            v_1.consumo_kg_machos,
            v_1.consumo_real_dia_kg,
            v_1.consumo_acumulado_kg,
            v_1.consumo_bodega_kg,
            v_1.consumo_agua_diario,
            v_1.pct_perdidas_dia,
            v_1.peso_prom_hembras,
            v_1.peso_prom_machos,
            v_1.observaciones,
            v_1.metadata,
            v_1.items_adicionales,
            v_1.tipo_fila,
            v_1.uniformidad_hembras,
            v_1.uniformidad_machos,
            v_1.cv_hembras,
            v_1.cv_machos,
            v_1.consumo_agua_ph,
            v_1.consumo_agua_orp,
            v_1.consumo_agua_temperatura,
            v_1.ciclo,
            v_1.historico_consumo_alimento,
            v_1.despacho_peso_neto,
            v_1.despacho_peso_tara,
            v_1.despacho_promedio_peso_ave,
            v_1.created_by_user_id
           FROM ( SELECT v_1_1.seguimiento_id,
                    v_1_1.lote_ave_engorde_id,
                    v_1_1.lote_nombre,
                    v_1_1.company_id,
                    v_1_1.company_nombre,
                    v_1_1.granja_id,
                    v_1_1.granja_nombre,
                    v_1_1.galpon_id,
                    v_1_1.galpon_nombre,
                    v_1_1.nucleo_id,
                    v_1_1.nucleo_nombre,
                    v_1_1.fecha_dmy,
                    v_1_1.fecha_registro,
                    v_1_1.semana,
                    v_1_1.edad_dias_vida,
                    v_1_1.dia_calendario_corto,
                    v_1_1.mortalidad_hembras,
                    v_1_1.mortalidad_machos,
                    v_1_1.seleccion_hembras,
                    v_1_1.seleccion_machos,
                    v_1_1.total_mort_mas_sel_dia,
                    v_1_1.error_sexaje_hembras,
                    v_1_1.error_sexaje_machos,
                    v_1_1.despacho_hembras_hist,
                    v_1_1.despacho_machos_hist,
                    v_1_1.despacho_mixtas_hist,
                    v_1_1.saldo_alimento_kg_bd,
                    v_1_1.saldo_alimento_kg_calculado,
                    v_1_1.saldo_aves_vivas,
                    v_1_1.saldo_aves_vivas_hembras,
                    v_1_1.saldo_aves_vivas_machos,
                    v_1_1.tipo_alimento,
                    v_1_1.tipo_alimento_corto,
                    v_1_1.ingreso_alimento_texto_hist,
                    v_1_1.traslado_texto_hist,
                    v_1_1.documento_hist,
                    v_1_1.metadata_ingreso_alimento,
                    v_1_1.metadata_traslado,
                    v_1_1.metadata_documento,
                    v_1_1.consumo_kg_hembras,
                    v_1_1.consumo_kg_machos,
                    v_1_1.consumo_real_dia_kg,
                    v_1_1.consumo_acumulado_kg,
                    v_1_1.consumo_bodega_kg,
                    v_1_1.consumo_agua_diario,
                    v_1_1.pct_perdidas_dia,
                    v_1_1.peso_prom_hembras,
                    v_1_1.peso_prom_machos,
                    v_1_1.observaciones,
                    v_1_1.metadata,
                    v_1_1.items_adicionales,
                    v_1_1.tipo_fila,
                    v_1_1.uniformidad_hembras,
                    v_1_1.uniformidad_machos,
                    v_1_1.cv_hembras,
                    v_1_1.cv_machos,
                    v_1_1.consumo_agua_ph,
                    v_1_1.consumo_agua_orp,
                    v_1_1.consumo_agua_temperatura,
                    v_1_1.ciclo,
                    v_1_1.historico_consumo_alimento,
                    v_1_1.despacho_peso_neto,
                    v_1_1.despacho_peso_tara,
                    v_1_1.despacho_promedio_peso_ave,
                    v_1_1.created_by_user_id,
                        CASE
                            WHEN COALESCE(v_1_1.created_by_user_id, ''::character varying)::text = 'SYSTEM_CRUCE'::text OR COALESCE(v_1_1.consumo_kg_machos, 0::numeric) > 0::numeric THEN NULL::numeric
                            ELSE v_1_1.consumo_real_dia_kg
                        END AS consumo_kg_mixto,
                    NOT (COALESCE(v_1_1.created_by_user_id, ''::character varying)::text = 'SYSTEM_CRUCE'::text OR COALESCE(v_1_1.consumo_kg_machos, 0::numeric) > 0::numeric) AS consumo_es_mixto
                   FROM ( WITH lote_info AS (
                                 SELECT l.lote_ave_engorde_id,
                                    l.lote_nombre,
                                    l.fecha_encaset,
                                    l.granja_id,
                                    fa.name AS granja_nombre,
                                    fa.company_id,
                                    cp.name AS company_nombre,
                                    l.galpon_id,
                                    gp.galpon_nombre,
                                    l.nucleo_id,
                                    nu.nucleo_nombre,
                                    COALESCE(TRIM(BOTH FROM l.nucleo_id), ''::text) AS nucleo_id_t,
                                    COALESCE(TRIM(BOTH FROM l.galpon_id), ''::text) AS galpon_id_t,
                                    COALESCE(l.aves_encasetadas, 0) AS aves_encasetadas,
                                    COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0) AS suma_hm,
                                    COALESCE(l.hembras_l, 0)::bigint AS aves_iniciales_hembras,
                                    COALESCE(l.machos_l, 0)::bigint AS aves_iniciales_machos,
                                    GREATEST(0,
CASE
 WHEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0)) > 0 THEN COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0)
 ELSE COALESCE(l.aves_encasetadas, 0)
END)::bigint AS aves_iniciales,
                                    lower(COALESCE(l.estado_operativo_lote, ''::character varying)::text) AS estado_operativo_lote
                                   FROM lote_ave_engorde l
                                     LEFT JOIN farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
                                     LEFT JOIN companies cp ON cp.id = fa.company_id
                                     LEFT JOIN nucleos nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
                                     LEFT JOIN galpones gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
                                  WHERE l.deleted_at IS NULL
                                ), rango_seg AS (
                                 SELECT s.lote_ave_engorde_id,
                                    min(s.fecha)::date AS fecha_min,
                                    max(s.fecha)::date AS last_seg
                                   FROM seguimiento_diario_aves_engorde s
                                  GROUP BY s.lote_ave_engorde_id
                                ), apert_mov AS (
                                 SELECT li.lote_ave_engorde_id,
                                    h.fecha_operacion AS f,
                                    h.created_at AS ts,
CASE h.tipo_evento
 WHEN 'INV_INGRESO'::text THEN COALESCE(h.cantidad_kg, 0::numeric)
 WHEN 'INV_TRASLADO_ENTRADA'::text THEN COALESCE(h.cantidad_kg, 0::numeric)
 WHEN 'INV_TRASLADO_SALIDA'::text THEN - abs(COALESCE(h.cantidad_kg, 0::numeric))
 ELSE 0::numeric
END AS delta
                                   FROM lote_info li
                                     JOIN rango_seg rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id AND rs.fecha_min IS NOT NULL
                                     JOIN lote_registro_historico_unificado h ON h.farm_id = li.granja_id AND COALESCE(TRIM(BOTH FROM h.nucleo_id), ''::text) = li.nucleo_id_t AND COALESCE(TRIM(BOTH FROM h.galpon_id), ''::text) = li.galpon_id_t
                                  WHERE NOT h.anulado AND (h.tipo_evento::text = ANY (ARRAY['INV_INGRESO'::character varying::text, 'INV_TRASLADO_ENTRADA'::character varying::text, 'INV_TRASLADO_SALIDA'::character varying::text])) AND NOT (h.tipo_evento::text = 'INV_INGRESO'::text AND h.referencia IS NOT NULL AND h.referencia::text ~~ 'Seguimiento aves engorde #%'::text) AND NOT (h.referencia IS NOT NULL AND (h.referencia::text ~~ '%devolución por eliminación%'::text OR h.referencia::text ~~ '%devolucion por eliminacion%'::text)) AND h.fecha_operacion < rs.fecha_min AND (li.fecha_encaset IS NULL OR h.fecha_operacion >= li.fecha_encaset::date)
                                ), apert_run AS (
                                 SELECT apert_mov.lote_ave_engorde_id,
                                    apert_mov.delta,
                                    sum(apert_mov.delta) OVER (PARTITION BY apert_mov.lote_ave_engorde_id ORDER BY apert_mov.f, apert_mov.ts ROWS UNBOUNDED PRECEDING) AS p
                                   FROM apert_mov
                                ), apertura_alimento AS (
                                 SELECT apert_run.lote_ave_engorde_id,
                                    (sum(apert_run.delta) - LEAST(0::numeric, min(apert_run.p)))::double precision AS apertura_kg
                                   FROM apert_run
                                  GROUP BY apert_run.lote_ave_engorde_id
                                ), hist_full AS (
                                 SELECT li.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    sum(
CASE
 WHEN h.tipo_evento::text = 'INV_INGRESO'::text AND NOT (h.referencia IS NOT NULL AND h.referencia::text ~~ 'Seguimiento aves engorde #%'::text) THEN COALESCE(h.cantidad_kg, 0::numeric)
 WHEN h.tipo_evento::text = 'INV_TRASLADO_ENTRADA'::text THEN COALESCE(h.cantidad_kg, 0::numeric)
 WHEN h.tipo_evento::text = 'INV_TRASLADO_SALIDA'::text THEN - abs(COALESCE(h.cantidad_kg, 0::numeric))
 ELSE 0::numeric
END)::double precision AS neto_kg
                                   FROM lote_info li
                                     JOIN rango_seg rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
                                     JOIN lote_registro_historico_unificado h ON h.farm_id = li.granja_id AND COALESCE(TRIM(BOTH FROM h.nucleo_id), ''::text) = li.nucleo_id_t AND COALESCE(TRIM(BOTH FROM h.galpon_id), ''::text) = li.galpon_id_t
                                  WHERE NOT h.anulado AND (h.tipo_evento::text = ANY (ARRAY['INV_INGRESO'::character varying::text, 'INV_TRASLADO_ENTRADA'::character varying::text, 'INV_TRASLADO_SALIDA'::character varying::text])) AND NOT (h.referencia IS NOT NULL AND (h.referencia::text ~~ '%devolución por eliminación%'::text OR h.referencia::text ~~ '%devolucion por eliminacion%'::text)) AND (rs.fecha_min IS NULL OR h.fecha_operacion >= rs.fecha_min)
                                  GROUP BY li.lote_ave_engorde_id, h.fecha_operacion
                                ), consumo_por_fecha AS (
                                 SELECT s.lote_ave_engorde_id,
                                    date(s.fecha) AS fecha,
                                    sum(COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric))::double precision AS cons_kg
                                   FROM seguimiento_diario_aves_engorde s
                                  GROUP BY s.lote_ave_engorde_id, (date(s.fecha))
                                ), saldo_dates AS (
                                 SELECT hist_full.lote_ave_engorde_id,
                                    hist_full.fecha
                                   FROM hist_full
                                UNION
                                 SELECT consumo_por_fecha.lote_ave_engorde_id,
                                    consumo_por_fecha.fecha
                                   FROM consumo_por_fecha
                                ), saldo_running AS (
                                 SELECT sd.lote_ave_engorde_id,
                                    sd.fecha,
                                    GREATEST(0::double precision, COALESCE(aa.apertura_kg, 0::double precision) + COALESCE(sum(hf.neto_kg) OVER (PARTITION BY sd.lote_ave_engorde_id ORDER BY sd.fecha ROWS UNBOUNDED PRECEDING), 0::double precision) - COALESCE(sum(cf.cons_kg) OVER (PARTITION BY sd.lote_ave_engorde_id ORDER BY sd.fecha ROWS UNBOUNDED PRECEDING), 0::double precision)) AS saldo
                                   FROM saldo_dates sd
                                     LEFT JOIN hist_full hf ON hf.lote_ave_engorde_id = sd.lote_ave_engorde_id AND hf.fecha = sd.fecha
                                     LEFT JOIN consumo_por_fecha cf ON cf.lote_ave_engorde_id = sd.lote_ave_engorde_id AND cf.fecha = sd.fecha
                                     LEFT JOIN apertura_alimento aa ON aa.lote_ave_engorde_id = sd.lote_ave_engorde_id
                                ), saldo_close AS (
                                 SELECT sr.lote_ave_engorde_id,
                                    min(sr.fecha) AS close_date
                                   FROM saldo_running sr
                                     JOIN rango_seg rs ON rs.lote_ave_engorde_id = sr.lote_ave_engorde_id
                                  WHERE rs.last_seg IS NOT NULL AND sr.fecha >= rs.last_seg AND sr.saldo <= 0.5::double precision
                                  GROUP BY sr.lote_ave_engorde_id
                                ), rango_final AS (
                                 SELECT rs.lote_ave_engorde_id,
                                    rs.fecha_min,
                                    COALESCE(sc.close_date,
CASE
 WHEN li.estado_operativo_lote = 'cerrado'::text THEN rs.last_seg
 ELSE NULL::date
END) AS fecha_max
                                   FROM rango_seg rs
                                     JOIN lote_info li ON li.lote_ave_engorde_id = rs.lote_ave_engorde_id
                                     LEFT JOIN saldo_close sc ON sc.lote_ave_engorde_id = rs.lote_ave_engorde_id
                                ), salidas_totales AS (
                                 SELECT s.lote_ave_engorde_id,
                                    COALESCE(sum(COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0)), 0::bigint) AS bajas_seguimiento
                                   FROM seguimiento_diario_aves_engorde s
                                  GROUP BY s.lote_ave_engorde_id
                                ), ventas_totales AS (
                                 SELECT h.lote_ave_engorde_id,
                                    COALESCE(sum(COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)), 0::bigint) AS total_ventas
                                   FROM lote_registro_historico_unificado h
                                  WHERE h.tipo_evento::text = 'VENTA_AVES'::text AND NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
                                  GROUP BY h.lote_ave_engorde_id
                                ), aves_iniciales AS (
                                 SELECT li.lote_ave_engorde_id,
CASE
 WHEN li.estado_operativo_lote = 'cerrado'::text THEN GREATEST(1::bigint, COALESCE(st.bajas_seguimiento, 0::bigint) + COALESCE(vt.total_ventas, 0::bigint))
 WHEN li.aves_encasetadas > 0 AND li.suma_hm = 0 THEN li.aves_encasetadas::bigint
 WHEN li.suma_hm > 0 AND li.aves_encasetadas = 0 THEN li.suma_hm::bigint
 WHEN li.aves_encasetadas = li.suma_hm THEN li.aves_encasetadas::bigint
 ELSE li.aves_encasetadas::bigint
END AS inicial
                                   FROM lote_info li
                                     LEFT JOIN salidas_totales st ON st.lote_ave_engorde_id = li.lote_ave_engorde_id
                                     LEFT JOIN ventas_totales vt ON vt.lote_ave_engorde_id = li.lote_ave_engorde_id
                                ), ventas_por_fecha AS (
                                 SELECT h.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    COALESCE(sum(COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)), 0::bigint) AS ventas_dia,
                                    COALESCE(sum(COALESCE(h.cantidad_hembras, 0)), 0::bigint) AS despacho_h,
                                    COALESCE(sum(COALESCE(h.cantidad_machos, 0)), 0::bigint) AS despacho_m,
                                    COALESCE(sum(COALESCE(h.cantidad_mixtas, 0)), 0::bigint) AS despacho_x,
                                    COALESCE(sum(COALESCE(h.peso_neto, 0::numeric)), 0::numeric)::double precision AS despacho_peso_neto,
                                    COALESCE(sum(COALESCE(h.peso_tara_real, 0::numeric)), 0::numeric)::double precision AS despacho_peso_tara
                                   FROM lote_registro_historico_unificado h
                                  WHERE h.tipo_evento::text = 'VENTA_AVES'::text AND NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
                                  GROUP BY h.lote_ave_engorde_id, h.fecha_operacion
                                ), consumo_bodega_por_fecha AS (
                                 SELECT h.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    sum(
CASE
 WHEN h.tipo_evento::text = 'INV_CONSUMO'::text AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0::numeric)
 ELSE 0::numeric
END)::double precision AS consumo_bodega_kg
                                   FROM lote_registro_historico_unificado h
                                  WHERE NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
                                  GROUP BY h.lote_ave_engorde_id, h.fecha_operacion
                                ), hist_alimento AS (
                                 SELECT li.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    COALESCE(sum(
CASE
 WHEN h.tipo_evento::text = 'INV_INGRESO'::text AND NOT (h.referencia IS NOT NULL AND h.referencia::text ~~ 'Seguimiento aves engorde #%'::text) THEN COALESCE(h.cantidad_kg, 0::numeric)
 ELSE 0::numeric
END), 0::numeric)::double precision AS ingreso_kg,
                                    COALESCE(sum(
CASE
 WHEN h.tipo_evento::text = 'INV_TRASLADO_ENTRADA'::text THEN COALESCE(h.cantidad_kg, 0::numeric)
 ELSE 0::numeric
END), 0::numeric)::double precision AS traslado_entrada_kg,
                                    COALESCE(sum(
CASE
 WHEN h.tipo_evento::text = 'INV_TRASLADO_SALIDA'::text THEN abs(COALESCE(h.cantidad_kg, 0::numeric))
 ELSE 0::numeric
END), 0::numeric)::double precision AS traslado_salida_kg
                                   FROM lote_info li
                                     JOIN rango_final rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
                                     JOIN lote_registro_historico_unificado h ON h.farm_id = li.granja_id AND COALESCE(TRIM(BOTH FROM h.nucleo_id), ''::text) = li.nucleo_id_t AND COALESCE(TRIM(BOTH FROM h.galpon_id), ''::text) = li.galpon_id_t
                                  WHERE NOT h.anulado AND NOT (h.referencia IS NOT NULL AND (h.referencia::text ~~ '%devolución por eliminación%'::text OR h.referencia::text ~~ '%devolucion por eliminacion%'::text)) AND (h.tipo_evento::text = ANY (ARRAY['INV_INGRESO'::character varying::text, 'INV_TRASLADO_ENTRADA'::character varying::text, 'INV_TRASLADO_SALIDA'::character varying::text])) AND (rs.fecha_min IS NULL OR h.fecha_operacion >= rs.fecha_min) AND (rs.fecha_max IS NULL OR h.fecha_operacion <= rs.fecha_max)
                                  GROUP BY li.lote_ave_engorde_id, h.fecha_operacion
                                ), docs_por_fecha AS (
                                 SELECT li.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    string_agg(DISTINCT NULLIF(TRIM(BOTH FROM COALESCE(h.numero_documento, h.referencia, ''::character varying)), ''::text), ', '::text) AS documento
                                   FROM lote_info li
                                     JOIN rango_final rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
                                     JOIN lote_registro_historico_unificado h ON true
                                  WHERE NOT h.anulado AND NOT (h.referencia IS NOT NULL AND (h.referencia::text ~~ '%devolución por eliminación%'::text OR h.referencia::text ~~ '%devolucion por eliminacion%'::text)) AND (h.tipo_evento::text = 'INV_INGRESO'::text AND NOT (h.referencia IS NOT NULL AND h.referencia::text ~~ 'Seguimiento aves engorde #%'::text) AND h.farm_id = li.granja_id AND COALESCE(TRIM(BOTH FROM h.nucleo_id), ''::text) = li.nucleo_id_t AND COALESCE(TRIM(BOTH FROM h.galpon_id), ''::text) = li.galpon_id_t AND (rs.fecha_min IS NULL OR h.fecha_operacion >= rs.fecha_min) AND (rs.fecha_max IS NULL OR h.fecha_operacion <= rs.fecha_max) OR h.tipo_evento::text = 'VENTA_AVES'::text AND h.lote_ave_engorde_id = li.lote_ave_engorde_id)
                                  GROUP BY li.lote_ave_engorde_id, h.fecha_operacion
                                ), fechas_universo AS (
                                 SELECT s.lote_ave_engorde_id,
                                    date(s.fecha) AS fecha,
                                    s.id AS seg_id
                                   FROM seguimiento_diario_aves_engorde s
                                UNION ALL
                                 SELECT li.lote_ave_engorde_id,
                                    h.fecha_operacion AS fecha,
                                    NULL::bigint AS seg_id
                                   FROM lote_info li
                                     JOIN rango_final rs ON rs.lote_ave_engorde_id = li.lote_ave_engorde_id
                                     JOIN lote_registro_historico_unificado h ON true
                                  WHERE NOT h.anulado AND NOT (h.referencia IS NOT NULL AND (h.referencia::text ~~ '%devolución por eliminación%'::text OR h.referencia::text ~~ '%devolucion por eliminacion%'::text)) AND ((h.tipo_evento::text = ANY (ARRAY['INV_INGRESO'::character varying::text, 'INV_TRASLADO_ENTRADA'::character varying::text, 'INV_TRASLADO_SALIDA'::character varying::text])) AND NOT (h.tipo_evento::text = 'INV_INGRESO'::text AND h.referencia IS NOT NULL AND h.referencia::text ~~ 'Seguimiento aves engorde #%'::text) AND h.farm_id = li.granja_id AND COALESCE(TRIM(BOTH FROM h.nucleo_id), ''::text) = li.nucleo_id_t AND COALESCE(TRIM(BOTH FROM h.galpon_id), ''::text) = li.galpon_id_t AND (rs.fecha_min IS NULL OR h.fecha_operacion >= rs.fecha_min) AND (rs.fecha_max IS NULL OR h.fecha_operacion <= rs.fecha_max) OR h.tipo_evento::text = 'VENTA_AVES'::text AND h.lote_ave_engorde_id = li.lote_ave_engorde_id) AND (li.fecha_encaset IS NULL OR h.fecha_operacion >= li.fecha_encaset::date) AND NOT (EXISTS ( SELECT 1
   FROM seguimiento_diario_aves_engorde s2
  WHERE s2.lote_ave_engorde_id = li.lote_ave_engorde_id AND date(s2.fecha) = h.fecha_operacion))
                                  GROUP BY li.lote_ave_engorde_id, h.fecha_operacion
                                ), seg_enriquecido AS (
                                 SELECT fu.lote_ave_engorde_id,
                                    s.id AS seg_id,
                                    fu.fecha,
CASE
 WHEN li.fecha_encaset IS NOT NULL THEN GREATEST(0, fu.fecha - date(li.fecha_encaset))
 ELSE 0
END AS edad_dia,
                                    LEAST(8::numeric, GREATEST(1::numeric, ceil((
CASE
 WHEN li.fecha_encaset IS NOT NULL THEN GREATEST(0, fu.fecha - date(li.fecha_encaset))
 ELSE 0
END + 1)::numeric / 7.0)))::smallint AS semana,
                                    COALESCE(s.mortalidad_hembras, 0) AS mortalidad_hembras,
                                    COALESCE(s.mortalidad_machos, 0) AS mortalidad_machos,
                                    COALESCE(s.sel_h, 0) AS sel_h,
                                    COALESCE(s.sel_m, 0) AS sel_m,
                                    COALESCE(s.error_sexaje_hembras, 0) AS error_sexaje_hembras,
                                    COALESCE(s.error_sexaje_machos, 0) AS error_sexaje_machos,
                                    COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) AS total_mort_sel_dia,
                                    COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_totales_dia,
                                    COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.error_sexaje_hembras, 0) AS perdidas_hembras_dia,
                                    COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_m, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_machos_dia,
                                    COALESCE(s.consumo_kg_hembras, 0::numeric)::double precision AS consumo_kg_hembras,
                                    COALESCE(s.consumo_kg_machos, 0::numeric)::double precision AS consumo_kg_machos,
                                    (COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric))::double precision AS consumo_dia_kg,
                                    s.saldo_alimento_kg::double precision AS saldo_alimento_kg_bd,
                                    s.tipo_alimento,
                                    s.peso_prom_hembras,
                                    s.peso_prom_machos,
                                    s.uniformidad_hembras,
                                    s.uniformidad_machos,
                                    s.cv_hembras,
                                    s.cv_machos,
                                    s.consumo_agua_diario,
                                    s.consumo_agua_ph,
                                    s.consumo_agua_orp,
                                    s.consumo_agua_temperatura,
                                    s.observaciones,
                                    s.ciclo,
                                    s.metadata,
                                    s.items_adicionales,
                                    s.historico_consumo_alimento,
                                    s.created_by_user_id,
                                    COALESCE(vpf.ventas_dia, 0::bigint) AS ventas_dia,
                                    COALESCE(vpf.despacho_h, 0::bigint) AS despacho_h,
                                    COALESCE(vpf.despacho_m, 0::bigint) AS despacho_m,
                                    COALESCE(vpf.despacho_x, 0::bigint) AS despacho_x,
                                    COALESCE(vpf.despacho_peso_neto, 0::double precision) AS despacho_peso_neto,
                                    COALESCE(vpf.despacho_peso_tara, 0::double precision) AS despacho_peso_tara,
                                    COALESCE(ha.ingreso_kg, 0::double precision) AS ingreso_alimento_kg,
                                    COALESCE(ha.traslado_entrada_kg, 0::double precision) AS traslado_entrada_kg,
                                    COALESCE(ha.traslado_salida_kg, 0::double precision) AS traslado_salida_kg,
                                    COALESCE(cb.consumo_bodega_kg, 0::double precision) AS consumo_bodega_kg,
                                    dpf.documento
                                   FROM fechas_universo fu
                                     JOIN lote_info li ON li.lote_ave_engorde_id = fu.lote_ave_engorde_id
                                     LEFT JOIN seguimiento_diario_aves_engorde s ON s.id = fu.seg_id
                                     LEFT JOIN ventas_por_fecha vpf ON vpf.lote_ave_engorde_id = fu.lote_ave_engorde_id AND vpf.fecha = fu.fecha
                                     LEFT JOIN hist_alimento ha ON ha.lote_ave_engorde_id = fu.lote_ave_engorde_id AND ha.fecha = fu.fecha
                                     LEFT JOIN consumo_bodega_por_fecha cb ON cb.lote_ave_engorde_id = fu.lote_ave_engorde_id AND cb.fecha = fu.fecha
                                     LEFT JOIN docs_por_fecha dpf ON dpf.lote_ave_engorde_id = fu.lote_ave_engorde_id AND dpf.fecha = fu.fecha
                                ), universo_fechas_distinct AS (
                                 SELECT DISTINCT fechas_universo.lote_ave_engorde_id,
                                    fechas_universo.fecha
                                   FROM fechas_universo
                                ), alim_cum AS (
                                 SELECT u.lote_ave_engorde_id,
                                    u.fecha,
                                    sum(COALESCE(ha.ingreso_kg + ha.traslado_entrada_kg - ha.traslado_salida_kg, 0::double precision)) OVER (PARTITION BY u.lote_ave_engorde_id ORDER BY u.fecha ROWS UNBOUNDED PRECEDING) AS alim_cum_kg
                                   FROM universo_fechas_distinct u
                                     LEFT JOIN hist_alimento ha ON ha.lote_ave_engorde_id = u.lote_ave_engorde_id AND ha.fecha = u.fecha
                                ), pt_calc AS (
                                 SELECT se.lote_ave_engorde_id,
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
                                    se.perdidas_hembras_dia,
                                    se.perdidas_machos_dia,
                                    se.consumo_kg_hembras,
                                    se.consumo_kg_machos,
                                    se.consumo_dia_kg,
                                    se.saldo_alimento_kg_bd,
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
                                    se.created_by_user_id,
                                    se.ventas_dia,
                                    se.despacho_h,
                                    se.despacho_m,
                                    se.despacho_x,
                                    se.despacho_peso_neto,
                                    se.despacho_peso_tara,
                                    se.ingreso_alimento_kg,
                                    se.traslado_entrada_kg,
                                    se.traslado_salida_kg,
                                    se.consumo_bodega_kg,
                                    se.documento,
                                    COALESCE(aa.apertura_kg, 0::double precision) + COALESCE(ac.alim_cum_kg, 0::double precision) - sum(se.consumo_dia_kg) OVER (PARTITION BY se.lote_ave_engorde_id ORDER BY se.fecha, (COALESCE(se.seg_id, 0::bigint)) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS pt
                                   FROM seg_enriquecido se
                                     LEFT JOIN apertura_alimento aa ON aa.lote_ave_engorde_id = se.lote_ave_engorde_id
                                     LEFT JOIN alim_cum ac ON ac.lote_ave_engorde_id = se.lote_ave_engorde_id AND ac.fecha = se.fecha
                                ), calc AS (
                                 SELECT se.lote_ave_engorde_id,
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
                                    se.perdidas_hembras_dia,
                                    se.perdidas_machos_dia,
                                    se.consumo_kg_hembras,
                                    se.consumo_kg_machos,
                                    se.consumo_dia_kg,
                                    se.saldo_alimento_kg_bd,
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
                                    se.created_by_user_id,
                                    se.ventas_dia,
                                    se.despacho_h,
                                    se.despacho_m,
                                    se.despacho_x,
                                    se.despacho_peso_neto,
                                    se.despacho_peso_tara,
                                    se.ingreso_alimento_kg,
                                    se.traslado_entrada_kg,
                                    se.traslado_salida_kg,
                                    se.consumo_bodega_kg,
                                    se.documento,
                                    se.pt,
                                    ai.inicial,
                                    li.lote_nombre,
                                    li.fecha_encaset,
                                    li.granja_id,
                                    li.granja_nombre,
                                    li.company_id,
                                    li.company_nombre,
                                    li.galpon_id,
                                    li.galpon_nombre,
                                    li.nucleo_id,
                                    li.nucleo_nombre,
                                    li.aves_iniciales,
                                    li.aves_iniciales_hembras,
                                    li.aves_iniciales_machos,
                                    sum(se.consumo_dia_kg) OVER w_ord AS acum_consumo_kg,
                                    GREATEST(0::numeric, ai.inicial::numeric - sum(se.perdidas_totales_dia + se.ventas_dia) OVER w_ord)::integer AS saldo_aves_vivas,
                                    GREATEST(0::numeric, li.aves_iniciales_hembras::numeric - sum(se.perdidas_hembras_dia + se.despacho_h) OVER w_ord)::bigint AS saldo_aves_vivas_hembras,
                                    GREATEST(0::numeric, li.aves_iniciales_machos::numeric - sum(se.perdidas_machos_dia + se.despacho_m) OVER w_ord)::bigint AS saldo_aves_vivas_machos,
CASE
 WHEN GREATEST(0::numeric, ai.inicial::numeric - COALESCE(sum(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0::numeric)) > 0::numeric THEN round(100.0 * se.total_mort_sel_dia::numeric / GREATEST(0::numeric, ai.inicial::numeric - COALESCE(sum(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0::numeric)), 2)
 WHEN se.total_mort_sel_dia > 0 THEN 100::numeric
 ELSE NULL::numeric
END AS pct_perdidas_dia,
                                    se.pt - LEAST(0::double precision, min(se.pt) OVER w_ord) AS saldo_alimento_kg_calc
                                   FROM pt_calc se
                                     JOIN lote_info li ON li.lote_ave_engorde_id = se.lote_ave_engorde_id
                                     JOIN aves_iniciales ai ON ai.lote_ave_engorde_id = se.lote_ave_engorde_id
                                  WINDOW w_ord AS (PARTITION BY se.lote_ave_engorde_id ORDER BY se.fecha, (COALESCE(se.seg_id, 0::bigint)) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW), w_prev AS (PARTITION BY se.lote_ave_engorde_id ORDER BY se.fecha, (COALESCE(se.seg_id, 0::bigint)) ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
                                )
                         SELECT calc.seg_id AS seguimiento_id,
                            calc.lote_ave_engorde_id,
                            calc.lote_nombre,
                            calc.company_id,
                            calc.company_nombre,
                            calc.granja_id,
                            calc.granja_nombre,
                            calc.galpon_id,
                            calc.galpon_nombre,
                            calc.nucleo_id,
                            calc.nucleo_nombre,
                            to_char(calc.fecha::timestamp with time zone, 'DD/MM/YYYY'::text) AS fecha_dmy,
                            calc.fecha AS fecha_registro,
                            calc.semana,
                            calc.edad_dia AS edad_dias_vida,
                            to_char(calc.fecha::timestamp with time zone, 'Dy, DD Mon'::text) AS dia_calendario_corto,
                            calc.mortalidad_hembras,
                            calc.mortalidad_machos,
                            calc.sel_h AS seleccion_hembras,
                            calc.sel_m AS seleccion_machos,
                            calc.total_mort_sel_dia AS total_mort_mas_sel_dia,
                            calc.error_sexaje_hembras,
                            calc.error_sexaje_machos,
                            calc.despacho_h AS despacho_hembras_hist,
                            calc.despacho_m AS despacho_machos_hist,
                            calc.despacho_x AS despacho_mixtas_hist,
                            trim_scale(calc.saldo_alimento_kg_bd::numeric) AS saldo_alimento_kg_bd,
                            trim_scale(calc.saldo_alimento_kg_calc::numeric) AS saldo_alimento_kg_calculado,
                            calc.saldo_aves_vivas,
                            calc.saldo_aves_vivas_hembras,
                            calc.saldo_aves_vivas_machos,
                            calc.tipo_alimento,
                                CASE
                                    WHEN upper(COALESCE(calc.tipo_alimento, ''::character varying)::text) ~~ '%PRE%'::text THEN 'PRE'::text
                                    WHEN upper(COALESCE(calc.tipo_alimento, ''::character varying)::text) ~~ '%INI%'::text THEN 'INI'::text
                                    WHEN upper(COALESCE(calc.tipo_alimento, ''::character varying)::text) ~~ '%ENG%'::text THEN 'ENG'::text
                                    WHEN upper(COALESCE(calc.tipo_alimento, ''::character varying)::text) ~~ '%FIN%'::text THEN 'FIN-D'::text
                                    WHEN COALESCE(calc.tipo_alimento, ''::character varying)::text = ''::text THEN '—'::text
                                    ELSE ""left""(calc.tipo_alimento::text, 8)
                                END AS tipo_alimento_corto,
                                CASE
                                    WHEN COALESCE(calc.ingreso_alimento_kg, 0::double precision) > 0::double precision THEN to_char(calc.ingreso_alimento_kg::numeric, 'FM9999999999990.999'::text) || ' kg'::text
                                    ELSE NULL::text
                                END AS ingreso_alimento_texto_hist,
                                CASE
                                    WHEN COALESCE(calc.traslado_entrada_kg, 0::double precision) = 0::double precision AND COALESCE(calc.traslado_salida_kg, 0::double precision) = 0::double precision THEN NULL::text
                                    ELSE concat_ws(' · '::text,
                                    CASE
WHEN COALESCE(calc.traslado_entrada_kg, 0::double precision) > 0::double precision THEN ('Entrada '::text || to_char(calc.traslado_entrada_kg::numeric, 'FM9999999999990.999'::text)) || ' kg'::text
ELSE NULL::text
                                    END,
                                    CASE
WHEN COALESCE(calc.traslado_salida_kg, 0::double precision) > 0::double precision THEN ('Salida '::text || to_char(calc.traslado_salida_kg::numeric, 'FM9999999999990.999'::text)) || ' kg'::text
ELSE NULL::text
                                    END)
                                END AS traslado_texto_hist,
                            COALESCE(calc.documento, ''::text) AS documento_hist,
                            calc.metadata ->> 'ingresoAlimento'::text AS metadata_ingreso_alimento,
                            calc.metadata ->> 'traslado'::text AS metadata_traslado,
                            calc.metadata ->> 'documento'::text AS metadata_documento,
                            trim_scale(calc.consumo_kg_hembras::numeric) AS consumo_kg_hembras,
                            trim_scale(calc.consumo_kg_machos::numeric) AS consumo_kg_machos,
                            trim_scale(calc.consumo_dia_kg::numeric) AS consumo_real_dia_kg,
                            trim_scale(calc.acum_consumo_kg::numeric) AS consumo_acumulado_kg,
                            trim_scale(calc.consumo_bodega_kg::numeric) AS consumo_bodega_kg,
                            trim_scale(calc.consumo_agua_diario::numeric) AS consumo_agua_diario,
                            trim_scale(calc.pct_perdidas_dia) AS pct_perdidas_dia,
                            trim_scale(calc.peso_prom_hembras::numeric) AS peso_prom_hembras,
                            trim_scale(calc.peso_prom_machos::numeric) AS peso_prom_machos,
                            calc.observaciones,
                            calc.metadata,
                            calc.items_adicionales,
                                CASE
                                    WHEN calc.seg_id IS NULL THEN 'movimiento'::text
                                    ELSE 'seguimiento'::text
                                END AS tipo_fila,
                            trim_scale(calc.uniformidad_hembras::numeric) AS uniformidad_hembras,
                            trim_scale(calc.uniformidad_machos::numeric) AS uniformidad_machos,
                            trim_scale(calc.cv_hembras::numeric) AS cv_hembras,
                            trim_scale(calc.cv_machos::numeric) AS cv_machos,
                            trim_scale(calc.consumo_agua_ph::numeric) AS consumo_agua_ph,
                            trim_scale(calc.consumo_agua_orp::numeric) AS consumo_agua_orp,
                            trim_scale(calc.consumo_agua_temperatura::numeric) AS consumo_agua_temperatura,
                            calc.ciclo,
                            calc.historico_consumo_alimento,
                            trim_scale(calc.despacho_peso_neto::numeric) AS despacho_peso_neto,
                            trim_scale(calc.despacho_peso_tara::numeric) AS despacho_peso_tara,
                            trim_scale(
                                CASE
                                    WHEN (calc.despacho_h + calc.despacho_m + calc.despacho_x) > 0 THEN calc.despacho_peso_neto / (calc.despacho_h + calc.despacho_m + calc.despacho_x)::double precision
                                    ELSE 0::double precision
                                END::numeric) AS despacho_promedio_peso_ave,
                            calc.created_by_user_id
                           FROM calc
                          ORDER BY calc.lote_ave_engorde_id, calc.fecha, (COALESCE(calc.seg_id, 0::bigint))) v_1_1) v_1) v;";
    }
}
