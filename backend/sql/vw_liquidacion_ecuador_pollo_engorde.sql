-- =============================================================================
-- Vista: vw_liquidacion_ecuador_pollo_engorde  (REBUILD 2026-06-24)
-- =============================================================================
-- Conserva TODAS las columnas y la lógica de tiempo real ya desplegadas
-- (aves_actuales, lote_cerrado_logico, cerrado_por_*, fecha_cierre_efectiva, …)
-- y AGREGA el bloque de liquidación de fn_indicadores_pollo_engorde (R1/R2):
--   merma_unidades, merma_kilos, merma_porcentaje, ajuste_aves, porcentaje_ajuste,
--   produccion_kilo_en_pie, total_kilos_despachados_cliente, aves_sobrante,
--   dias_engorde, ratio_sacrificadas, fecha_inicio_lote, fecha_cierre_lote,
--   fecha_liquidacion, fecha_alistamiento.
--
-- R1: si Costos NO registró merma (merma_unidades y merma_kilos ambos NULL),
--     los 6 campos derivados salen NULL (reporte vacío). Con merma registrada,
--     aritmética idéntica a la función.
-- CAMBIO DE CÁLCULO (corrección): kg_carne pasa a COALESCE(peso_neto, peso_bruto−peso_tara)
--     = fix R3.1 de la función. Esto corrige kg_carne_pollos y sus derivados
--     (peso_promedio_kilos, conversion, conversion_ajustada2700, consumo_ave_gramos,
--     eficiencia_*, kg_por_metro_cuadrado, produccion_kilo_en_pie, total_kilos_despachados_cliente).
--     Antes la vista sobre-contaba kg de carne (sumaba el peso global de factura clonado).
-- No se renombra ni se elimina ninguna columna previa. Nombre de vista intacto.
-- =============================================================================

DROP VIEW IF EXISTS public.vw_liquidacion_ecuador_pollo_engorde;

CREATE VIEW public.vw_liquidacion_ecuador_pollo_engorde AS
WITH params AS (
    SELECT 2.7 AS peso_ajuste, 4.5 AS divisor_ajuste
),
seg_padre AS (
    SELECT s.lote_ave_engorde_id,
        sum(COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)) AS sum_mort,
        sum(COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)) AS sum_sel,
        sum(COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric)) AS consumo_kg
    FROM seguimiento_diario_aves_engorde s
    GROUP BY s.lote_ave_engorde_id
),
mov_salida AS (
    SELECT m.lote_ave_engorde_origen_id AS lote_ave_engorde_id,
        sum(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0)) AS aves_sacrificadas,
        -- FIX R3.1 (igual que fn_indicadores_pollo_engorde): peso INDIVIDUAL prorrateado
        -- (peso_neto) y solo si falta, peso_bruto − peso_tara. Antes sumaba peso_bruto−peso_tara
        -- (global de factura clonado) → sobre-conteo de kg de carne.
        sum(COALESCE(m.peso_neto::numeric,
            CASE WHEN m.peso_bruto IS NOT NULL AND m.peso_tara IS NOT NULL THEN m.peso_bruto::numeric - m.peso_tara::numeric ELSE 0::numeric END)) AS kg_carne,
        avg(m.edad_aves::numeric) FILTER (WHERE m.edad_aves IS NOT NULL) AS edad_promedio,
        max(m.fecha_movimiento) AS fecha_ultimo_despacho
    FROM movimiento_pollo_engorde m
    WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.lote_ave_engorde_origen_id IS NOT NULL
      AND (m.tipo_movimiento::text = ANY (ARRAY['Venta'::character varying::text, 'Despacho'::character varying::text, 'Retiro'::character varying::text]))
    GROUP BY m.lote_ave_engorde_origen_id
),
mov_traslado_rep AS (
    SELECT m.lote_ave_engorde_origen_id AS lote_ave_engorde_id,
        sum(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0)) AS aves_trasladadas_rep
    FROM movimiento_pollo_engorde m
    WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.tipo_movimiento::text = 'Traslado'::text
      AND m.lote_ave_engorde_origen_id IS NOT NULL AND m.lote_reproductora_ave_engorde_destino_id IS NOT NULL
    GROUP BY m.lote_ave_engorde_origen_id
),
rep_base AS (
    SELECT r.id AS lote_reproductora_id, r.lote_ave_engorde_id,
        CASE WHEN (COALESCE(r.aves_inicio_hembras, 0) + COALESCE(r.aves_inicio_machos, 0) + COALESCE(r.mixtas, 0)) > 0
             THEN COALESCE(r.aves_inicio_hembras, 0) + COALESCE(r.aves_inicio_machos, 0) + COALESCE(r.mixtas, 0)
             ELSE COALESCE(r.h, 0) + COALESCE(r.m, 0) + COALESCE(r.mixtas, 0) END::bigint AS encaset_rep
    FROM lote_reproductora_ave_engorde r
),
rep_seg AS (
    SELECT s.lote_reproductora_ave_engorde_id,
        sum(COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)) AS mort_sel_rep
    FROM seguimiento_diario_lote_reproductora_aves_engorde s
    GROUP BY s.lote_reproductora_ave_engorde_id
),
rep_mov AS (
    SELECT m.lote_reproductora_ave_engorde_origen_id AS lote_reproductora_id,
        sum(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0)) AS ventas_rep
    FROM movimiento_pollo_engorde m
    WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.lote_reproductora_ave_engorde_origen_id IS NOT NULL
      AND (m.tipo_movimiento::text = ANY (ARRAY['Venta'::character varying::text, 'Despacho'::character varying::text, 'Retiro'::character varying::text]))
    GROUP BY m.lote_reproductora_ave_engorde_origen_id
),
rep_tiene_aves AS (
    SELECT rb.lote_ave_engorde_id,
        bool_or(GREATEST(0::bigint, rb.encaset_rep - COALESCE(rs.mort_sel_rep, 0::bigint) - COALESCE(rm.ventas_rep, 0::bigint)) > 0) AS alguna_rep_con_aves_positivas
    FROM rep_base rb
        LEFT JOIN rep_seg rs ON rs.lote_reproductora_ave_engorde_id = rb.lote_reproductora_id
        LEFT JOIN rep_mov rm ON rm.lote_reproductora_id = rb.lote_reproductora_id
    GROUP BY rb.lote_ave_engorde_id
),
rep_counts AS (
    SELECT r.lote_ave_engorde_id, count(*)::integer AS cnt_rep
    FROM lote_reproductora_ave_engorde r GROUP BY r.lote_ave_engorde_id
),
ult_seg_padre AS (
    SELECT DISTINCT ON (s.lote_ave_engorde_id) s.lote_ave_engorde_id, s.fecha::date AS ultima_fecha_seg
    FROM seguimiento_diario_aves_engorde s
    ORDER BY s.lote_ave_engorde_id, s.fecha DESC, s.id DESC
),
ult_mov_cualquier AS (
    SELECT DISTINCT ON (m.lote_ave_engorde_origen_id) m.lote_ave_engorde_origen_id AS lote_ave_engorde_id, m.fecha_movimiento AS ultima_fecha_mov
    FROM movimiento_pollo_engorde m
    WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.lote_ave_engorde_origen_id IS NOT NULL
    ORDER BY m.lote_ave_engorde_origen_id, m.fecha_movimiento DESC, m.id DESC
),
base AS (
    SELECT l.lote_ave_engorde_id,
        l.company_id,
        COALESCE(c.name, l.empresa_nombre) AS empresa_nombre,
        l.granja_id,
        fa.name AS granja_nombre,
        l.nucleo_id,
        nu.nucleo_nombre,
        l.galpon_id,
        gp.galpon_nombre,
        l.lote_nombre,
        l.fecha_encaset::date AS fecha_encaset,
        l.estado_operativo_lote,
        l.liquidado_at,
        -- ── NUEVO: campos de liquidación crudos del lote ──
        l.merma_unidades AS merma_unidades_raw,
        l.merma_kilos    AS merma_kilos_raw,
        l.aves_sobrante  AS aves_sobrante_raw,
        l.fecha_alistamiento,
        (l.merma_unidades IS NOT NULL OR l.merma_kilos IS NOT NULL) AS merma_registrada,
        COALESCE(l.aves_encasetadas, 0)::bigint AS aves_encasetadas_raw,
        CASE WHEN COALESCE(l.aves_encasetadas, 0) > 0 THEN l.aves_encasetadas::bigint
             ELSE (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0))::bigint END AS aves_encasetadas,
        COALESCE(sp.sum_mort, 0::bigint) + COALESCE(sp.sum_sel, 0::bigint) AS mort_sel_padre,
        COALESCE(sp.consumo_kg, 0::numeric) AS consumo_total_kg,
        COALESCE(ms.aves_sacrificadas, 0::bigint) AS aves_sacrificadas,
        COALESCE(ms.kg_carne, 0::numeric) AS kg_carne_pollos,
        COALESCE(ms.edad_promedio, 0::numeric) AS edad_promedio_mov,
        ms.fecha_ultimo_despacho,
        COALESCE(mt.aves_trasladadas_rep, 0::bigint) AS aves_trasladadas_rep,
        COALESCE(rc.cnt_rep, 0) AS cantidad_lotes_reproductores,
        CASE WHEN COALESCE(rc.cnt_rep, 0) = 0 THEN false ELSE NOT COALESCE(rt.alguna_rep_con_aves_positivas, false) END AS todos_reproductores_sin_aves,
        us.ultima_fecha_seg,
        umc.ultima_fecha_mov,
        CASE
            WHEN l.galpon_id IS NOT NULL AND TRIM(BOTH FROM l.galpon_id) <> ''::text THEN
                CASE WHEN gp.ancho IS NOT NULL AND gp.largo IS NOT NULL AND TRIM(BOTH FROM gp.ancho::text) <> ''::text AND TRIM(BOTH FROM gp.largo::text) <> ''::text
                          AND TRIM(BOTH FROM gp.ancho::text) ~ '^[0-9]+([.,][0-9]+)?$'::text AND TRIM(BOTH FROM gp.largo::text) ~ '^[0-9]+([.,][0-9]+)?$'::text
                     THEN replace(replace(TRIM(BOTH FROM gp.ancho::text), ','::text, '.'::text), ' '::text, ''::text)::numeric
                        * replace(replace(TRIM(BOTH FROM gp.largo::text), ','::text, '.'::text), ' '::text, ''::text)::numeric
                     ELSE NULL::numeric END
            ELSE ( SELECT COALESCE(sum(
                        CASE WHEN g.ancho IS NOT NULL AND g.largo IS NOT NULL AND TRIM(BOTH FROM g.ancho::text) <> ''::text AND TRIM(BOTH FROM g.largo::text) <> ''::text
                                  AND TRIM(BOTH FROM g.ancho::text) ~ '^[0-9]+([.,][0-9]+)?$'::text AND TRIM(BOTH FROM g.largo::text) ~ '^[0-9]+([.,][0-9]+)?$'::text
                             THEN replace(replace(TRIM(BOTH FROM g.ancho::text), ','::text, '.'::text), ' '::text, ''::text)::numeric
                                * replace(replace(TRIM(BOTH FROM g.largo::text), ','::text, '.'::text), ' '::text, ''::text)::numeric
                             ELSE 0::numeric END), 0::numeric)
                   FROM galpones g WHERE g.granja_id = l.granja_id AND g.deleted_at IS NULL)
        END AS metros_cuadrados
    FROM lote_ave_engorde l
        LEFT JOIN companies c ON c.id = l.company_id
        LEFT JOIN farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
        LEFT JOIN nucleos nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
        LEFT JOIN galpones gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
        LEFT JOIN seg_padre sp ON sp.lote_ave_engorde_id = l.lote_ave_engorde_id
        LEFT JOIN mov_salida ms ON ms.lote_ave_engorde_id = l.lote_ave_engorde_id
        LEFT JOIN mov_traslado_rep mt ON mt.lote_ave_engorde_id = l.lote_ave_engorde_id
        LEFT JOIN rep_tiene_aves rt ON rt.lote_ave_engorde_id = l.lote_ave_engorde_id
        LEFT JOIN rep_counts rc ON rc.lote_ave_engorde_id = l.lote_ave_engorde_id
        LEFT JOIN ult_seg_padre us ON us.lote_ave_engorde_id = l.lote_ave_engorde_id
        LEFT JOIN ult_mov_cualquier umc ON umc.lote_ave_engorde_id = l.lote_ave_engorde_id
    WHERE l.deleted_at IS NULL
),
calc AS (
    SELECT b.*,
        b.mort_sel_padre AS mortalidad_unidades,
        GREATEST(0::bigint, b.aves_encasetadas - b.mort_sel_padre - b.aves_sacrificadas - b.aves_trasladadas_rep) AS aves_actuales,
        CASE WHEN b.aves_encasetadas > 0 THEN b.mort_sel_padre::numeric / b.aves_encasetadas::numeric * 100::numeric ELSE 0::numeric END AS mortalidad_porcentaje,
        CASE WHEN b.aves_encasetadas > 0 THEN (b.aves_encasetadas - b.mort_sel_padre)::numeric / b.aves_encasetadas::numeric * 100::numeric ELSE 0::numeric END AS supervivencia_porcentaje,
        CASE WHEN b.aves_sacrificadas > 0 THEN b.consumo_total_kg / b.aves_sacrificadas::numeric * 1000::numeric ELSE 0::numeric END AS consumo_ave_gramos,
        CASE WHEN b.aves_sacrificadas > 0 THEN b.kg_carne_pollos / b.aves_sacrificadas::numeric ELSE 0::numeric END AS peso_promedio_kilos,
        CASE WHEN b.kg_carne_pollos > 0::numeric THEN b.consumo_total_kg / b.kg_carne_pollos ELSE 0::numeric END AS conversion,
        ( SELECT p.peso_ajuste FROM params p) AS peso_ajuste_variable,
        ( SELECT p.divisor_ajuste FROM params p) AS divisor_ajuste_variable
    FROM base b
),
calc2 AS (
    SELECT c.*,
        CASE WHEN GREATEST(0::bigint, c.aves_actuales) = 0 THEN true ELSE false END AS cerrado_por_aves_cero,
        CASE WHEN GREATEST(0::bigint, c.aves_actuales) > 0 AND c.aves_sacrificadas = 0 AND COALESCE(c.mort_sel_padre, 0::bigint) = 0
                  AND c.todos_reproductores_sin_aves AND c.cantidad_lotes_reproductores > 0 THEN true ELSE false END AS cerrado_por_reproductores_vendidos,
        CASE WHEN c.conversion > 0::numeric THEN c.conversion + (c.peso_ajuste_variable - c.peso_promedio_kilos) / c.divisor_ajuste_variable ELSE 0::numeric END AS conversion_ajustada2700
    FROM calc c
),
calc3 AS (
    SELECT c2.*,
        -- fecha de cierre efectiva (igual que la salida desplegada; se materializa para dias_engorde)
        CASE
            WHEN (c2.cerrado_por_aves_cero OR c2.cerrado_por_reproductores_vendidos) AND c2.fecha_ultimo_despacho IS NULL
            THEN COALESCE(c2.ultima_fecha_seg::timestamp with time zone, c2.ultima_fecha_mov, c2.fecha_encaset::timestamp with time zone)
            ELSE c2.fecha_ultimo_despacho
        END AS fecha_cierre_efectiva
    FROM calc2 c2
)
SELECT company_id,
    empresa_nombre,
    granja_id,
    granja_nombre,
    nucleo_id,
    nucleo_nombre,
    galpon_id,
    galpon_nombre,
    lote_ave_engorde_id,
    lote_nombre,
    fecha_encaset,
    estado_operativo_lote,
    liquidado_at,
    cantidad_lotes_reproductores,
    aves_encasetadas,
    aves_sacrificadas,
    mortalidad_unidades AS mortalidad,
    mortalidad_porcentaje,
    supervivencia_porcentaje,
    consumo_total_kg AS consumo_total_alimento_kg,
    consumo_ave_gramos,
    kg_carne_pollos,
    peso_promedio_kilos,
    conversion,
    conversion_ajustada2700,
    peso_ajuste_variable,
    divisor_ajuste_variable,
    edad_promedio_mov AS edad_promedio,
    COALESCE(metros_cuadrados, 0::numeric) AS metros_cuadrados,
    CASE WHEN COALESCE(metros_cuadrados, 0::numeric) > 0::numeric THEN aves_sacrificadas::numeric / metros_cuadrados ELSE 0::numeric END AS aves_por_metro_cuadrado,
    CASE WHEN COALESCE(metros_cuadrados, 0::numeric) > 0::numeric THEN kg_carne_pollos / metros_cuadrados ELSE 0::numeric END AS kg_por_metro_cuadrado,
    CASE WHEN conversion > 0::numeric THEN peso_promedio_kilos / conversion * 100::numeric ELSE 0::numeric END AS eficiencia_americana,
    CASE WHEN conversion > 0::numeric AND edad_promedio_mov > 0::numeric THEN peso_promedio_kilos * supervivencia_porcentaje / (edad_promedio_mov * conversion) * 100::numeric ELSE 0::numeric END AS eficiencia_europea,
    CASE WHEN conversion > 0::numeric THEN peso_promedio_kilos / conversion / conversion * 100::numeric ELSE 0::numeric END AS indice_productividad,
    CASE WHEN edad_promedio_mov > 0::numeric THEN peso_promedio_kilos / edad_promedio_mov * 1000::numeric ELSE 0::numeric END AS ganancia_dia,
    aves_trasladadas_rep,
    aves_actuales,
    aves_actuales > 0 AS tiene_aves,
    cerrado_por_aves_cero OR cerrado_por_reproductores_vendidos AS lote_cerrado_logico,
    cerrado_por_aves_cero,
    cerrado_por_reproductores_vendidos,
    fecha_ultimo_despacho AS fecha_cierre_ultimo_despacho,
    fecha_cierre_efectiva,
    -- ────────────────────────────────────────────────────────────────────────
    -- NUEVO: bloque liquidación (fn_indicadores_pollo_engorde R1/R2). R1: NULL si no hay merma.
    -- ────────────────────────────────────────────────────────────────────────
    CASE WHEN merma_registrada THEN COALESCE(merma_unidades_raw, 0) END AS merma_unidades,
    CASE WHEN merma_registrada THEN COALESCE(merma_kilos_raw, 0::numeric) END AS merma_kilos,
    CASE WHEN merma_registrada
         THEN round(CASE WHEN aves_sacrificadas > 0 THEN COALESCE(merma_unidades_raw, 0)::numeric / aves_sacrificadas::numeric * 100::numeric ELSE 0::numeric END, 6)
    END AS merma_porcentaje,
    CASE WHEN merma_registrada
         THEN (aves_encasetadas - aves_sacrificadas - mort_sel_padre - COALESCE(merma_unidades_raw, 0))::integer
    END AS ajuste_aves,
    CASE WHEN merma_registrada
         THEN round(CASE WHEN aves_encasetadas > 0
              THEN (aves_encasetadas - aves_sacrificadas - mort_sel_padre - COALESCE(merma_unidades_raw, 0))::numeric / aves_encasetadas::numeric * 100::numeric ELSE 0::numeric END, 6)
    END AS porcentaje_ajuste,
    kg_carne_pollos AS produccion_kilo_en_pie,
    CASE WHEN merma_registrada THEN kg_carne_pollos - COALESCE(merma_kilos_raw, 0::numeric) END AS total_kilos_despachados_cliente,
    COALESCE(aves_sobrante_raw, 0) AS aves_sobrante,
    CASE WHEN fecha_encaset IS NOT NULL AND fecha_cierre_efectiva IS NOT NULL
         THEN GREATEST(0, (fecha_cierre_efectiva::date - fecha_encaset))
         ELSE 0 END AS dias_engorde,
    round(CASE WHEN aves_encasetadas > 0 THEN aves_sacrificadas::numeric / aves_encasetadas::numeric ELSE 0::numeric END, 6) AS ratio_sacrificadas,
    fecha_encaset AS fecha_inicio_lote,
    fecha_cierre_efectiva AS fecha_cierre_lote,
    liquidado_at AS fecha_liquidacion,
    fecha_alistamiento
FROM calc3;

ALTER VIEW public.vw_liquidacion_ecuador_pollo_engorde OWNER TO repropesa01;
GRANT SELECT ON TABLE public.vw_liquidacion_ecuador_pollo_engorde TO "usrDWH";

COMMENT ON VIEW public.vw_liquidacion_ecuador_pollo_engorde IS
  'Liquidación técnica Pollo Engorde (lote padre). Tiempo real (aves_actuales, lote_cerrado_logico) + bloque liquidación fn_indicadores_pollo_engorde (merma/ajuste/producción/sobrante). R1: campos de merma NULL si Costos no registró merma.';
