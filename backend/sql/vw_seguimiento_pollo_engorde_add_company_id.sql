-- =============================================================================
-- Agrega a vw_seguimiento_pollo_engorde:
--   • company_id        (farms.company_id de la granja del lote)
--   • saldo_aves_vivas_hembras  (aves iniciales hembras − pérdidas acumuladas hembras)
--   • saldo_aves_vivas_machos   (aves iniciales machos  − pérdidas acumuladas machos)
--
-- Pérdidas por género = mortalidad + selección + error_sexaje del mismo género.
-- Ejecutar en PostgreSQL de producción / desarrollo.
-- =============================================================================

CREATE OR REPLACE VIEW public.vw_seguimiento_pollo_engorde AS
WITH RECURSIVE hist_base AS (
    SELECT
        h.id                 AS hid,
        h.lote_ave_engorde_id,
        h.tipo_evento,
        h.created_at,
        TRIM(
            COALESCE(h.referencia, '') || ' ' || COALESCE(h.numero_documento, '')
        ) AS ref_full,
        COALESCE(
            CASE
                WHEN lower(TRIM(COALESCE(h.referencia,'') || ' ' || COALESCE(h.numero_documento,'')))
                         ~ 'seguimiento\s+aves\s+engorde\s+#\d+\s+(\d{4}-\d{2}-\d{2})'
                THEN substring(
                        lower(TRIM(COALESCE(h.referencia,'') || ' ' || COALESCE(h.numero_documento,''))),
                        'seguimiento\s+aves\s+engorde\s+#\d+\s+(\d{4}-\d{2}-\d{2})'
                     )::date
                ELSE NULL::date
            END,
            CASE
                WHEN h.tipo_evento = 'INV_CONSUMO'
                     AND TRIM(COALESCE(h.referencia,'') || ' ' || COALESCE(h.numero_documento,''))
                             ~ '(\d{4}-\d{2}-\d{2})'
                THEN substring(
                        TRIM(COALESCE(h.referencia,'') || ' ' || COALESCE(h.numero_documento,'')),
                        '(\d{4}-\d{2}-\d{2})'
                     )::date
                ELSE NULL::date
            END,
            h.fecha_operacion
        ) AS ymd_efe,
        CASE
            WHEN h.anulado                                                           THEN NULL::numeric
            WHEN h.tipo_evento = 'INV_INGRESO'          AND COALESCE(h.cantidad_kg,0) <> 0 THEN  h.cantidad_kg::numeric
            WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA' AND COALESCE(h.cantidad_kg,0) <> 0 THEN  h.cantidad_kg::numeric
            WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'  AND COALESCE(h.cantidad_kg,0) <> 0 THEN -abs(h.cantidad_kg::numeric)
            WHEN h.tipo_evento = 'INV_OTRO'
                 AND lower(TRIM(COALESCE(h.movement_type_original,''))) = 'ajustestock'     THEN  h.cantidad_kg::numeric
            WHEN h.tipo_evento = 'INV_OTRO'
                 AND lower(TRIM(COALESCE(h.movement_type_original,''))) = 'eliminacionstock'
                 AND COALESCE(h.cantidad_kg,0) <> 0                                         THEN -abs(h.cantidad_kg::numeric)
            ELSE NULL::numeric
        END AS delta_kg,
        CASE h.tipo_evento
            WHEN 'INV_INGRESO'          THEN 0
            WHEN 'INV_TRASLADO_ENTRADA' THEN 1
            WHEN 'INV_TRASLADO_SALIDA'  THEN 2
            WHEN 'INV_OTRO'             THEN 2
            ELSE 99
        END AS ord_hist,
        (EXTRACT(EPOCH FROM h.created_at) * 1000)::bigint AS tie_h_ms
    FROM lote_registro_historico_unificado h
    WHERE NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
),

first_seg_f AS (
    SELECT s.lote_ave_engorde_id, min(s.fecha::date) AS d0
    FROM seguimiento_diario_aves_engorde s
    GROUP BY s.lote_ave_engorde_id
),

hist_opening AS (
    SELECT hb.lote_ave_engorde_id, 0 AS phase, hb.ymd_efe,
           0 AS ord_sort, hb.tie_h_ms AS tie, NULL::bigint AS seg_id, hb.delta_kg
    FROM hist_base hb
    JOIN first_seg_f f ON f.lote_ave_engorde_id = hb.lote_ave_engorde_id
    WHERE hb.delta_kg IS NOT NULL AND hb.ymd_efe < f.d0
),

hist_main AS (
    SELECT hb.lote_ave_engorde_id, 1 AS phase, hb.ymd_efe,
           hb.ord_hist AS ord_sort, hb.tie_h_ms AS tie, NULL::bigint AS seg_id, hb.delta_kg
    FROM hist_base hb
    JOIN first_seg_f f   ON f.lote_ave_engorde_id  = hb.lote_ave_engorde_id
    JOIN lote_ave_engorde la ON la.lote_ave_engorde_id = hb.lote_ave_engorde_id
    WHERE hb.delta_kg IS NOT NULL
      AND hb.ymd_efe >= f.d0
      AND (la.fecha_encaset IS NULL OR hb.ymd_efe >= la.fecha_encaset::date)
),

seg_events AS (
    SELECT s.lote_ave_engorde_id, 1 AS phase, s.fecha::date AS ymd_efe,
           3 AS ord_sort,
           (EXTRACT(EPOCH FROM ((s.fecha::date + INTERVAL '12:00:00') AT TIME ZONE 'UTC')) * 1000)::bigint AS tie,
           s.id AS seg_id,
           -(COALESCE(s.consumo_kg_hembras,0) + COALESCE(s.consumo_kg_machos,0)) AS delta_kg
    FROM seguimiento_diario_aves_engorde s
),

events_union AS (
    SELECT * FROM hist_opening
    UNION ALL SELECT * FROM hist_main
    UNION ALL SELECT * FROM seg_events
),

events_ordered AS (
    SELECT eu.*,
           row_number() OVER (
               PARTITION BY eu.lote_ave_engorde_id
               ORDER BY eu.phase, eu.ymd_efe, eu.ord_sort, eu.tie, COALESCE(eu.seg_id,0)
           ) AS seq
    FROM events_union eu
),

rec AS (
    SELECT eo.lote_ave_engorde_id, eo.seq, eo.seg_id, eo.delta_kg,
           GREATEST(0::numeric, eo.delta_kg) AS bal
    FROM events_ordered eo WHERE eo.seq = 1
    UNION ALL
    SELECT eo.lote_ave_engorde_id, eo.seq, eo.seg_id, eo.delta_kg,
           GREATEST(0::numeric, r.bal + eo.delta_kg) AS bal
    FROM rec r
    JOIN events_ordered eo
        ON  eo.lote_ave_engorde_id = r.lote_ave_engorde_id
        AND eo.seq = r.seq + 1
),

saldo_ui AS (
    SELECT r.seg_id, r.bal AS saldo_alimento_kg_calculado
    FROM rec r WHERE r.seg_id IS NOT NULL
),

-- ─────────────────────────────────────────────────────────────────────────────
-- lote: company_id + aves iniciales desglosadas por género
-- ─────────────────────────────────────────────────────────────────────────────
lote AS (
    SELECT
        l.lote_ave_engorde_id,
        l.lote_nombre,
        l.fecha_encaset,
        l.granja_id,
        fa.name                                AS granja_nombre,
        fa.company_id,                         -- ← empresa
        l.galpon_id,
        gp.galpon_nombre,
        l.nucleo_id,
        nu.nucleo_nombre,
        -- Total inicial (igual que antes)
        GREATEST(0,
            CASE
                WHEN COALESCE(l.hembras_l,0) + COALESCE(l.machos_l,0) > 0
                THEN COALESCE(l.hembras_l,0) + COALESCE(l.machos_l,0)
                ELSE COALESCE(l.aves_encasetadas,0)
            END
        )::bigint                              AS aves_iniciales,
        -- ← NUEVO: iniciales por género
        COALESCE(l.hembras_l, 0)::bigint       AS aves_iniciales_hembras,
        COALESCE(l.machos_l,  0)::bigint       AS aves_iniciales_machos
    FROM lote_ave_engorde l
    LEFT JOIN farms    fa ON fa.id              = l.granja_id AND fa.deleted_at IS NULL
    LEFT JOIN nucleos  nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
    LEFT JOIN galpones gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
),

hist_por_dia AS (
    SELECT
        h.lote_ave_engorde_id,
        h.fecha_operacion AS dia,
        sum(CASE WHEN h.tipo_evento='INV_INGRESO'          AND NOT h.anulado THEN COALESCE(h.cantidad_kg,0) ELSE 0 END) AS ingreso_kg,
        sum(CASE WHEN h.tipo_evento='INV_TRASLADO_ENTRADA' AND NOT h.anulado THEN COALESCE(h.cantidad_kg,0) ELSE 0 END) AS traslado_entrada_kg,
        sum(CASE WHEN h.tipo_evento='INV_TRASLADO_SALIDA'  AND NOT h.anulado THEN COALESCE(h.cantidad_kg,0) ELSE 0 END) AS traslado_salida_kg,
        sum(CASE WHEN h.tipo_evento='INV_CONSUMO'          AND NOT h.anulado THEN COALESCE(h.cantidad_kg,0) ELSE 0 END) AS consumo_bodega_kg,
        sum(CASE WHEN h.tipo_evento='VENTA_AVES' AND NOT h.anulado THEN COALESCE(h.cantidad_hembras,0) ELSE 0 END) AS venta_hembras,
        sum(CASE WHEN h.tipo_evento='VENTA_AVES' AND NOT h.anulado THEN COALESCE(h.cantidad_machos,0)  ELSE 0 END) AS venta_machos,
        sum(CASE WHEN h.tipo_evento='VENTA_AVES' AND NOT h.anulado THEN COALESCE(h.cantidad_mixtas,0)  ELSE 0 END) AS venta_mixtas,
        string_agg(
            DISTINCT NULLIF(TRIM(COALESCE(h.numero_documento, h.referencia, '')), ''),
            ', '
        ) FILTER (WHERE TRIM(COALESCE(h.numero_documento, h.referencia,'')) <> '') AS documentos_hist
    FROM lote_registro_historico_unificado h
    WHERE NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
    GROUP BY h.lote_ave_engorde_id, h.fecha_operacion
),

-- ─────────────────────────────────────────────────────────────────────────────
-- base: agrega aves iniciales por género y pérdidas diarias por género
-- ─────────────────────────────────────────────────────────────────────────────
base AS (
    SELECT
        s.id                                     AS seguimiento_id,
        s.lote_ave_engorde_id,
        s.fecha::date                            AS fecha_registro,
        l.lote_nombre,
        l.fecha_encaset,
        l.granja_id,
        l.granja_nombre,
        l.company_id,
        l.galpon_id,
        l.galpon_nombre,
        l.nucleo_id,
        l.nucleo_nombre,
        l.aves_iniciales,
        l.aves_iniciales_hembras,                -- ← NUEVO
        l.aves_iniciales_machos,                 -- ← NUEVO
        GREATEST(0, s.fecha::date - l.fecha_encaset::date)             AS edad_dias_vida,
        LEAST(8, GREATEST(1,
            ceil((GREATEST(0, s.fecha::date - l.fecha_encaset::date) + 1)::numeric / 7.0)::integer
        ))                                       AS semana_ui,
        COALESCE(s.mortalidad_hembras,0)         AS mortalidad_hembras,
        COALESCE(s.mortalidad_machos, 0)         AS mortalidad_machos,
        COALESCE(s.sel_h, 0)                     AS seleccion_hembras,
        COALESCE(s.sel_m, 0)                     AS seleccion_machos,
        COALESCE(s.error_sexaje_hembras,0)       AS error_sexaje_hembras,
        COALESCE(s.error_sexaje_machos, 0)       AS error_sexaje_machos,
        COALESCE(s.mortalidad_hembras,0) + COALESCE(s.mortalidad_machos,0)
            + COALESCE(s.sel_h,0) + COALESCE(s.sel_m,0)               AS total_mort_sel_dia,
        COALESCE(s.mortalidad_hembras,0) + COALESCE(s.mortalidad_machos,0)
            + COALESCE(s.sel_h,0) + COALESCE(s.sel_m,0)
            + COALESCE(s.error_sexaje_hembras,0) + COALESCE(s.error_sexaje_machos,0)
                                                 AS perdidas_todas_dia,
        -- ← NUEVO: pérdidas diarias por género (mort + sel + error_sexaje)
        COALESCE(s.mortalidad_hembras,0) + COALESCE(s.sel_h,0) + COALESCE(s.error_sexaje_hembras,0)
                                                 AS perdidas_hembras_dia,
        COALESCE(s.mortalidad_machos,0)  + COALESCE(s.sel_m,0) + COALESCE(s.error_sexaje_machos,0)
                                                 AS perdidas_machos_dia,
        s.tipo_alimento,
        CASE
            WHEN upper(COALESCE(s.tipo_alimento,'')) LIKE '%PRE%' THEN 'PRE'
            WHEN upper(COALESCE(s.tipo_alimento,'')) LIKE '%INI%' THEN 'INI'
            WHEN upper(COALESCE(s.tipo_alimento,'')) LIKE '%ENG%' THEN 'ENG'
            WHEN upper(COALESCE(s.tipo_alimento,'')) LIKE '%FIN%' THEN 'FIN-D'
            WHEN COALESCE(s.tipo_alimento,'') = ''                THEN '—'
            ELSE left(s.tipo_alimento, 8)
        END                                      AS tipo_alimento_corto,
        COALESCE(s.consumo_kg_hembras,0) + COALESCE(s.consumo_kg_machos,0) AS consumo_real_dia_kg,
        s.consumo_kg_hembras,
        s.consumo_kg_machos,
        s.consumo_agua_diario,
        s.peso_prom_hembras,
        s.peso_prom_machos,
        s.observaciones,
        s.saldo_alimento_kg                      AS saldo_alimento_kg_bd,
        su.saldo_alimento_kg_calculado,
        s.metadata,
        s.items_adicionales,
        h.ingreso_kg,
        h.traslado_entrada_kg,
        h.traslado_salida_kg,
        h.consumo_bodega_kg,
        h.venta_hembras,
        h.venta_machos,
        h.venta_mixtas,
        h.documentos_hist
    FROM seguimiento_diario_aves_engorde s
    JOIN lote l        ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
    LEFT JOIN hist_por_dia h ON h.lote_ave_engorde_id = s.lote_ave_engorde_id AND h.dia = s.fecha::date
    LEFT JOIN saldo_ui su    ON su.seg_id = s.id
),

-- ─────────────────────────────────────────────────────────────────────────────
-- con_acum: acumulados globales + acumulados por género
-- ─────────────────────────────────────────────────────────────────────────────
con_acum AS (
    SELECT
        b.*,
        -- acumulado global (igual que antes)
        sum(b.perdidas_todas_dia) OVER (
            PARTITION BY b.lote_ave_engorde_id
            ORDER BY b.fecha_registro, b.seguimiento_id
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS acum_perdidas_todas,
        sum(b.consumo_real_dia_kg) OVER (
            PARTITION BY b.lote_ave_engorde_id
            ORDER BY b.fecha_registro, b.seguimiento_id
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS consumo_acumulado_kg,
        -- ← NUEVO: acumulados por género
        sum(b.perdidas_hembras_dia) OVER (
            PARTITION BY b.lote_ave_engorde_id
            ORDER BY b.fecha_registro, b.seguimiento_id
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS acum_perdidas_hembras,
        sum(b.perdidas_machos_dia) OVER (
            PARTITION BY b.lote_ave_engorde_id
            ORDER BY b.fecha_registro, b.seguimiento_id
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS acum_perdidas_machos
    FROM base b
),

-- ─────────────────────────────────────────────────────────────────────────────
-- final: saldo vivas global + saldo vivas por género
-- ─────────────────────────────────────────────────────────────────────────────
final AS (
    SELECT
        c.*,
        -- global (igual que antes)
        GREATEST(0::bigint, c.aves_iniciales - c.acum_perdidas_todas)             AS saldo_aves_vivas_fin_dia,
        GREATEST(0::bigint, c.aves_iniciales - c.acum_perdidas_todas + c.perdidas_todas_dia)::numeric
                                                                                   AS saldo_aves_inicio_dia,
        -- ← NUEVO: saldo vivas por género al final del día
        GREATEST(0::bigint, c.aves_iniciales_hembras - c.acum_perdidas_hembras)   AS saldo_aves_vivas_hembras,
        GREATEST(0::bigint, c.aves_iniciales_machos  - c.acum_perdidas_machos)    AS saldo_aves_vivas_machos
    FROM con_acum c
)

-- ─────────────────────────────────────────────────────────────────────────────
-- SELECT final
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    -- Identificación y contexto
    seguimiento_id,
    lote_ave_engorde_id,
    lote_nombre,
    company_id,
    granja_id,
    granja_nombre,
    galpon_id,
    galpon_nombre,
    nucleo_id,
    nucleo_nombre,

    -- Fechas y semana
    to_char(fecha_registro::timestamptz, 'DD/MM/YYYY')  AS fecha_dmy,
    fecha_registro,
    semana_ui                                            AS semana,
    edad_dias_vida,
    to_char(fecha_registro::timestamptz, 'Dy, DD Mon')  AS dia_calendario_corto,

    -- Mortalidad / selección
    mortalidad_hembras,
    mortalidad_machos,
    seleccion_hembras,
    seleccion_machos,
    total_mort_sel_dia          AS total_mort_mas_sel_dia,
    error_sexaje_hembras,
    error_sexaje_machos,

    -- Despacho histórico
    venta_hembras               AS despacho_hembras_hist,
    venta_machos                AS despacho_machos_hist,
    venta_mixtas                AS despacho_mixtas_hist,

    -- Saldo alimento
    trim_scale(saldo_alimento_kg_bd)            AS saldo_alimento_kg_bd,
    trim_scale(saldo_alimento_kg_calculado)     AS saldo_alimento_kg_calculado,

    -- ── Saldo aves vivas ────────────────────────────────────────────────────
    saldo_aves_vivas_fin_dia                    AS saldo_aves_vivas,        -- global (igual que antes)
    saldo_aves_vivas_hembras,                                               -- ← NUEVO hembras
    saldo_aves_vivas_machos,                                                -- ← NUEVO machos
    -- ────────────────────────────────────────────────────────────────────────

    -- Alimento
    tipo_alimento,
    tipo_alimento_corto,
    CASE
        WHEN COALESCE(ingreso_kg,0) > 0
        THEN to_char(ingreso_kg,'FM9999999999990.999') || ' kg'
        ELSE NULL
    END                                         AS ingreso_alimento_texto_hist,
    CASE
        WHEN COALESCE(traslado_entrada_kg,0) = 0 AND COALESCE(traslado_salida_kg,0) = 0 THEN NULL
        ELSE concat_ws(' · ',
            CASE WHEN COALESCE(traslado_entrada_kg,0) > 0
                 THEN 'Entrada ' || to_char(traslado_entrada_kg,'FM9999999999990.999') || ' kg' ELSE NULL END,
            CASE WHEN COALESCE(traslado_salida_kg,0)  > 0
                 THEN 'Salida '  || to_char(traslado_salida_kg, 'FM9999999999990.999') || ' kg' ELSE NULL END
        )
    END                                         AS traslado_texto_hist,
    COALESCE(documentos_hist,'')                AS documento_hist,

    -- Metadata
    metadata ->> 'ingresoAlimento'              AS metadata_ingreso_alimento,
    metadata ->> 'traslado'                     AS metadata_traslado,
    metadata ->> 'documento'                    AS metadata_documento,

    -- Consumo
    trim_scale(consumo_kg_hembras::numeric)     AS consumo_kg_hembras,
    trim_scale(consumo_kg_machos::numeric)      AS consumo_kg_machos,
    trim_scale(consumo_real_dia_kg)             AS consumo_real_dia_kg,
    trim_scale(consumo_acumulado_kg)            AS consumo_acumulado_kg,
    trim_scale(consumo_bodega_kg)               AS consumo_bodega_kg,
    trim_scale(consumo_agua_diario::numeric)    AS consumo_agua_diario,

    -- % pérdidas
    trim_scale(
        CASE
            WHEN saldo_aves_inicio_dia > 0
            THEN round(100.0 * total_mort_sel_dia::numeric / saldo_aves_inicio_dia, 2)
            WHEN total_mort_sel_dia > 0 THEN 100
            ELSE NULL
        END
    )                                           AS pct_perdidas_dia,

    -- Pesos
    trim_scale(peso_prom_hembras::numeric)      AS peso_prom_hembras,
    trim_scale(peso_prom_machos::numeric)       AS peso_prom_machos,

    -- Extras
    observaciones,
    metadata,
    items_adicionales

FROM final f;

-- Permisos
ALTER VIEW public.vw_seguimiento_pollo_engorde OWNER TO repropesa01;

COMMENT ON VIEW public.vw_seguimiento_pollo_engorde IS
    'Seguimiento diario engorde + histórico por día. '
    'company_id = farms.company_id. '
    'saldo_aves_vivas = global fin de día. '
    'saldo_aves_vivas_hembras / _machos = desglose por género (aves_iniciales_género − acum_perdidas_género). '
    'saldo_alimento_kg_calculado = lógica UI (sin INV_CONSUMO duplicado). '
    'saldo_alimento_kg_bd = valor persistido.';

GRANT ALL    ON TABLE public.vw_seguimiento_pollo_engorde TO repropesa01;
GRANT SELECT ON TABLE public.vw_seguimiento_pollo_engorde TO "usrDWH";
