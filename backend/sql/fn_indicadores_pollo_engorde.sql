-- ============================================================================
-- fn_indicadores_pollo_engorde (Parte A) — Liquidación Técnica Pollo Engorde EC
-- ----------------------------------------------------------------------------
-- Consolida en UNA consulta todo el cálculo que el servicio C#
-- (CalcularIndicadorLoteAveEngordeAsync) hacía con 8-10 queries N+1 por lote.
-- Replica EXACTAMENTE las primitivas del servicio, incluyendo:
--   * FIX R3.1: kg de carne = SUM(COALESCE(peso_neto, peso_bruto-peso_tara))
--     (peso INDIVIDUAL prorrateado, no el global clonado).
--   * R1/R2: merma, ajuste de aves, % ajuste, producción kilo en pie,
--     total a cliente, días de engorde, sobrante.
--   * R1 (campos vacíos): cuando Costos NO registró merma (merma_unidades y
--     merma_kilos ambos NULL en lote_ave_engorde), los 6 campos derivados
--     (merma_unidades, merma_kilos, merma_porcentaje, ajuste_aves,
--     porcentaje_ajuste, total_kilos_despachados_cliente) salen NULL para que
--     el reporte los muestre vacíos. Con merma registrada la aritmética es
--     idéntica a la versión previa.
-- Resultado: 1 fila por lote padre (lote_ave_engorde).
-- NO redondea (el servicio C# usa decimal sin redondeo intermedio).
-- ============================================================================

CREATE OR REPLACE FUNCTION public.fn_indicadores_pollo_engorde(
    p_lote_id        INT,
    p_peso_ajuste    NUMERIC DEFAULT 2.7,
    p_divisor_ajuste NUMERIC DEFAULT 4.5
)
RETURNS TABLE (
    granja_id                        INT,
    granja_nombre                    TEXT,
    lote_id                          INT,
    lote_nombre                      TEXT,
    galpon_id                        TEXT,
    galpon_nombre                    TEXT,
    aves_encasetadas                 INT,
    aves_sacrificadas                INT,
    mortalidad                       INT,
    mortalidad_porcentaje            NUMERIC,
    supervivencia_porcentaje         NUMERIC,
    consumo_total_alimento_kg        NUMERIC,
    consumo_ave_gramos               NUMERIC,
    kg_carne_pollos                  NUMERIC,
    peso_promedio_kilos              NUMERIC,
    conversion                       NUMERIC,
    conversion_ajustada2700          NUMERIC,
    peso_ajuste_variable             NUMERIC,
    divisor_ajuste_variable          NUMERIC,
    edad_promedio                    NUMERIC,
    metros_cuadrados                 NUMERIC,
    aves_por_metro_cuadrado          NUMERIC,
    kg_por_metro_cuadrado            NUMERIC,
    eficiencia_americana             NUMERIC,
    eficiencia_europea               NUMERIC,
    indice_productividad             NUMERIC,
    ganancia_dia                     NUMERIC,
    fecha_inicio_lote                TIMESTAMPTZ,
    fecha_cierre_lote                TIMESTAMPTZ,
    lote_cerrado                     BOOLEAN,
    fecha_alistamiento               TIMESTAMPTZ,
    merma_unidades                   INT,
    merma_kilos                      NUMERIC,
    merma_porcentaje                 NUMERIC,
    ajuste_aves                      INT,
    porcentaje_ajuste                NUMERIC,
    produccion_kilo_en_pie           NUMERIC,
    total_kilos_despachados_cliente  NUMERIC,
    dias_engorde                     INT,
    fecha_liquidacion                TIMESTAMPTZ,
    aves_sobrante                    INT,
    -- Marcadores administrativos (para filtros del wrapper C#)
    estado_operativo_lote            TEXT,
    liquidado_at_marker              TIMESTAMPTZ,
    ratio_sacrificadas               NUMERIC
)
LANGUAGE sql
STABLE
AS $$
WITH lote AS (
    SELECT
        l.lote_ave_engorde_id                                   AS lote_id,
        l.granja_id,
        f.name                                                  AS granja_nombre,
        l.galpon_id,
        g.galpon_nombre,
        l.lote_nombre,
        COALESCE(l.aves_encasetadas,
                 COALESCE(l.hembras_l,0)+COALESCE(l.machos_l,0)+COALESCE(l.mixtas,0)) AS aves_encasetadas,
        l.fecha_encaset,
        l.fecha_alistamiento,
        l.estado_operativo_lote,
        l.liquidado_at,
        l.merma_unidades,
        l.merma_kilos,
        -- R1: merma "registrada" = Costos digitó al menos uno de los dos valores.
        (l.merma_unidades IS NOT NULL OR l.merma_kilos IS NOT NULL) AS merma_registrada,
        COALESCE(l.aves_sobrante,0)                             AS aves_sobrante
    FROM public.lote_ave_engorde l
    JOIN public.farms f       ON f.id = l.granja_id
    LEFT JOIN public.galpones g ON g.galpon_id = l.galpon_id AND g.granja_id = l.granja_id
    WHERE l.lote_ave_engorde_id = p_lote_id AND l.deleted_at IS NULL
),
movs AS (
    SELECT
        COALESCE(SUM(m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas), 0)::INT AS aves_sacrificadas,
        COALESCE(SUM(COALESCE(m.peso_neto,
                 CASE WHEN m.peso_bruto IS NOT NULL AND m.peso_tara IS NOT NULL
                      THEN m.peso_bruto - m.peso_tara ELSE 0 END)), 0)::NUMERIC          AS kg_carne,
        AVG(m.edad_aves) FILTER (WHERE m.edad_aves IS NOT NULL)                          AS edad_promedio,
        MAX(m.fecha_movimiento)                                                          AS fecha_cierre_venta
    FROM public.movimiento_pollo_engorde m
    WHERE m.lote_ave_engorde_origen_id = p_lote_id
      AND m.estado <> 'Cancelado' AND m.deleted_at IS NULL
      AND m.tipo_movimiento IN ('Venta','Despacho','Retiro')
),
trasl AS (
    SELECT COALESCE(SUM(m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas), 0)::INT AS aves_trasladadas
    FROM public.movimiento_pollo_engorde m
    WHERE m.lote_ave_engorde_origen_id = p_lote_id
      AND m.estado <> 'Cancelado' AND m.deleted_at IS NULL
      AND m.tipo_movimiento = 'Traslado'
      AND m.lote_reproductora_ave_engorde_destino_id IS NOT NULL
),
seg AS (
    SELECT
        COALESCE(SUM(COALESCE(s.mortalidad_hembras,0)+COALESCE(s.mortalidad_machos,0)
                   + COALESCE(s.sel_h,0)+COALESCE(s.sel_m,0)), 0)::INT                   AS mortalidad,
        COALESCE(SUM(COALESCE(s.consumo_kg_hembras,0)+COALESCE(s.consumo_kg_machos,0)), 0)::NUMERIC AS consumo_total,
        MAX(s.fecha)::TIMESTAMPTZ                                                        AS ult_seg_fecha
    FROM public.seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),
ult_mov AS (
    SELECT MAX(m.fecha_movimiento) AS ult_mov_fecha
    FROM public.movimiento_pollo_engorde m
    WHERE m.lote_ave_engorde_origen_id = p_lote_id
      AND m.estado <> 'Cancelado' AND m.deleted_at IS NULL
),
area AS (
    SELECT CASE
        WHEN (SELECT galpon_id FROM lote) IS NOT NULL AND (SELECT galpon_id FROM lote) <> '' THEN
            COALESCE((
                SELECT (g.ancho::NUMERIC * g.largo::NUMERIC)
                FROM public.galpones g, lote l
                WHERE g.galpon_id = l.galpon_id AND g.granja_id = l.granja_id
                  AND g.ancho ~ '^-?[0-9]+(\.[0-9]+)?$' AND g.largo ~ '^-?[0-9]+(\.[0-9]+)?$'
                LIMIT 1
            ), 0)
        ELSE
            COALESCE((
                SELECT SUM(g.ancho::NUMERIC * g.largo::NUMERIC)
                FROM public.galpones g, lote l
                WHERE g.granja_id = l.granja_id AND g.deleted_at IS NULL
                  AND g.ancho ~ '^-?[0-9]+(\.[0-9]+)?$' AND g.largo ~ '^-?[0-9]+(\.[0-9]+)?$'
            ), 0)
    END AS metros_cuadrados
),
repro AS (
    SELECT r.id,
           CASE WHEN (COALESCE(r.aves_inicio_hembras,0)+COALESCE(r.aves_inicio_machos,0)+COALESCE(r.mixtas,0)) = 0
                THEN (COALESCE(r.h,0)+COALESCE(r.m,0)+COALESCE(r.mixtas,0))
                ELSE (COALESCE(r.aves_inicio_hembras,0)+COALESCE(r.aves_inicio_machos,0)+COALESCE(r.mixtas,0))
           END AS encaset,
           COALESCE((SELECT SUM(mm.cantidad_hembras+mm.cantidad_machos+mm.cantidad_mixtas)
                     FROM public.movimiento_pollo_engorde mm
                     WHERE mm.lote_reproductora_ave_engorde_origen_id = r.id
                       AND mm.estado <> 'Cancelado' AND mm.deleted_at IS NULL
                       AND mm.tipo_movimiento IN ('Venta','Despacho','Retiro')), 0) AS ventas,
           COALESCE((SELECT SUM(COALESCE(sr.mortalidad_hembras,0)+COALESCE(sr.mortalidad_machos,0)
                              + COALESCE(sr.sel_h,0)+COALESCE(sr.sel_m,0))
                     FROM public.seguimiento_diario_lote_reproductora_aves_engorde sr
                     WHERE sr.lote_reproductora_ave_engorde_id = r.id), 0) AS mort_sel
    FROM public.lote_reproductora_ave_engorde r
    WHERE r.lote_ave_engorde_id = p_lote_id
),
repro_estado AS (
    SELECT
        (SELECT count(*) FROM repro) > 0                                         AS tiene_repro,
        (SELECT count(*) FROM repro WHERE (encaset - mort_sel - ventas) > 0) = 0 AS todos_cero
),
calc AS (
    SELECT
        l.*,
        mv.aves_sacrificadas, mv.kg_carne, mv.edad_promedio, mv.fecha_cierre_venta,
        t.aves_trasladadas,
        sg.mortalidad, sg.consumo_total, sg.ult_seg_fecha,
        um.ult_mov_fecha,
        ar.metros_cuadrados,
        re.tiene_repro, re.todos_cero
    FROM lote l, movs mv, trasl t, seg sg, ult_mov um, area ar, repro_estado re
),
-- Valores derivados base, calculados UNA sola vez
d AS (
    SELECT c.*,
        CASE WHEN c.aves_sacrificadas > 0 THEN c.kg_carne / c.aves_sacrificadas ELSE 0 END AS peso_promedio,
        CASE WHEN c.kg_carne > 0 THEN c.consumo_total / c.kg_carne ELSE 0 END               AS conversion,
        CASE WHEN c.aves_encasetadas > 0 THEN c.mortalidad::NUMERIC / c.aves_encasetadas * 100 ELSE 0 END AS mort_pct,
        CASE WHEN c.aves_encasetadas > 0 THEN (c.aves_encasetadas - c.mortalidad)::NUMERIC / c.aves_encasetadas * 100 ELSE 0 END AS superv_pct,
        COALESCE(c.edad_promedio, 0)                                                        AS edad,
        ((GREATEST(0, c.aves_encasetadas - c.mortalidad - c.aves_sacrificadas - c.aves_trasladadas) = 0)
          OR (c.aves_sacrificadas = 0 AND c.mortalidad = 0 AND c.tiene_repro AND c.todos_cero)) AS lote_cerrado
    FROM calc c
),
d2 AS (
    SELECT d.*,
        COALESCE(d.fecha_cierre_venta,
                 CASE WHEN d.lote_cerrado THEN COALESCE(d.ult_seg_fecha, d.ult_mov_fecha, d.fecha_encaset) END) AS fecha_cierre_final
    FROM d
)
SELECT
    d2.granja_id,
    d2.granja_nombre::TEXT,
    d2.lote_id,
    d2.lote_nombre::TEXT,
    d2.galpon_id::TEXT,
    COALESCE(d2.galpon_nombre,'')::TEXT,
    d2.aves_encasetadas,
    d2.aves_sacrificadas,
    d2.mortalidad,
    ROUND(d2.mort_pct, 6),
    ROUND(d2.superv_pct, 6),
    d2.consumo_total,
    ROUND(CASE WHEN d2.aves_sacrificadas > 0 THEN d2.consumo_total / d2.aves_sacrificadas * 1000 ELSE 0 END, 6),
    d2.kg_carne,
    ROUND(d2.peso_promedio, 6),
    ROUND(d2.conversion, 6),
    ROUND(CASE WHEN d2.conversion <= 0 THEN 0
         ELSE d2.conversion + ((p_peso_ajuste - d2.peso_promedio) / p_divisor_ajuste) END, 6),
    p_peso_ajuste,
    p_divisor_ajuste,
    ROUND(d2.edad, 6),
    ROUND(d2.metros_cuadrados, 6),
    ROUND(CASE WHEN d2.metros_cuadrados > 0 THEN d2.aves_sacrificadas::NUMERIC / d2.metros_cuadrados ELSE 0 END, 6),
    ROUND(CASE WHEN d2.metros_cuadrados > 0 THEN d2.kg_carne / d2.metros_cuadrados ELSE 0 END, 6),
    ROUND(CASE WHEN d2.conversion > 0 THEN (d2.peso_promedio / d2.conversion) * 100 ELSE 0 END, 6),
    ROUND(CASE WHEN d2.conversion > 0 AND d2.edad > 0
         THEN ((d2.peso_promedio * d2.superv_pct) / (d2.edad * d2.conversion)) * 100 ELSE 0 END, 6),
    ROUND(CASE WHEN d2.conversion > 0 THEN (d2.peso_promedio / d2.conversion) / d2.conversion * 100 ELSE 0 END, 6),
    ROUND(CASE WHEN d2.edad > 0 THEN (d2.peso_promedio / d2.edad) * 1000 ELSE 0 END, 6),
    d2.fecha_encaset,
    d2.fecha_cierre_final,
    d2.lote_cerrado,
    d2.fecha_alistamiento,
    -- R1 (campos vacíos): NULL cuando Costos no registró merma; con merma, misma aritmética previa.
    CASE WHEN d2.merma_registrada THEN COALESCE(d2.merma_unidades,0) END,
    CASE WHEN d2.merma_registrada THEN COALESCE(d2.merma_kilos,0) END,
    CASE WHEN d2.merma_registrada
         THEN ROUND(CASE WHEN d2.aves_sacrificadas > 0 THEN COALESCE(d2.merma_unidades,0)::NUMERIC / d2.aves_sacrificadas * 100 ELSE 0 END, 6)
    END,
    CASE WHEN d2.merma_registrada
         THEN (d2.aves_encasetadas - d2.aves_sacrificadas - d2.mortalidad - COALESCE(d2.merma_unidades,0))::INT
    END,
    CASE WHEN d2.merma_registrada
         THEN ROUND(CASE WHEN d2.aves_encasetadas > 0
              THEN (d2.aves_encasetadas - d2.aves_sacrificadas - d2.mortalidad - COALESCE(d2.merma_unidades,0))::NUMERIC / d2.aves_encasetadas * 100 ELSE 0 END, 6)
    END,
    d2.kg_carne,
    CASE WHEN d2.merma_registrada THEN d2.kg_carne - COALESCE(d2.merma_kilos,0) END,
    CASE WHEN d2.fecha_encaset IS NOT NULL AND d2.fecha_cierre_final IS NOT NULL
         THEN GREATEST(0, (d2.fecha_cierre_final::date - d2.fecha_encaset::date))
         ELSE 0 END,
    d2.liquidado_at,
    d2.aves_sobrante,
    d2.estado_operativo_lote::TEXT,
    d2.liquidado_at,
    ROUND(CASE WHEN d2.aves_encasetadas > 0 THEN d2.aves_sacrificadas::NUMERIC / d2.aves_encasetadas ELSE 0 END, 6)
FROM d2;
$$;
