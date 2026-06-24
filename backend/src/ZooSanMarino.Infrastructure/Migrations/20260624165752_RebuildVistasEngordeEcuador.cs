using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Reconstruye las 3 vistas de reporting de Pollo Engorde Ecuador alineándolas a sus
    /// funciones/cómputos corregidos (mismos nombres → Power BI). Idempotente: DROP+CREATE.
    ///  - vw_liquidacion_ecuador_pollo_engorde: + bloque merma/ajuste/producción (fn_indicadores_pollo_engorde R1/R2) y kg_carne fix R3.1 (peso_neto).
    ///  - vw_seguimiento_pollo_engorde: espejo set-based de fn_seguimiento_diario_engorde (días movimiento-only, tipo_fila, saldo M1) + mediciones/agua/ciclo/historico/despacho peso individual.
    ///  - vw_indicadores_diarios_engorde: alineada al cómputo del front (guía por company+pais_id, consumo hembras, ganancia vs último peso>0, despachos de metadata) + columna pais_id.
    /// Backfill: guia_genetica_ecuador_header.pais_id (0 → país de sus lotes) para que el match por país funcione.
    /// OWNER/GRANT (repropesa01 / usrDWH) se re-aplican sólo si los roles existen.
    /// </summary>
    /// <inheritdoc />
    public partial class RebuildVistasEngordeEcuador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Backfill pais_id de headers de guía (de 0 al país de sus lotes) ──
            migrationBuilder.Sql(@"
-- =============================================================================
-- Backfill: guia_genetica_ecuador_header.pais_id (de 0 → país de sus lotes)
-- =============================================================================
-- La columna pais_id se agregó (migración 20260623135557) con default 0 en los
-- headers existentes, pero GuiaGeneticaEcuadorService.GetDatosAsync ahora resuelve
-- la guía por company_id + pais_id. Sin backfill, ningún lote (país 2/3) encuentra
-- su header (país 0). Se deriva el país del MODE (más frecuente) de los lotes que
-- cada header empareja por company + raza + año. Idempotente: solo toca pais_id = 0.
-- =============================================================================

UPDATE public.guia_genetica_ecuador_header gh
SET pais_id = sub.pais_id
FROM (
    SELECT h.id,
        MODE() WITHIN GROUP (ORDER BY l.pais_id) AS pais_id
    FROM public.guia_genetica_ecuador_header h
    JOIN public.lote_ave_engorde l
      ON l.company_id = h.company_id
     AND TRIM(LOWER(l.raza)) = TRIM(LOWER(h.raza))
     AND l.ano_tabla_genetica = h.anio_guia
     AND l.deleted_at IS NULL
     AND l.pais_id IS NOT NULL
    WHERE h.deleted_at IS NULL
      AND COALESCE(h.pais_id, 0) = 0
    GROUP BY h.id
) sub
WHERE gh.id = sub.id
  AND sub.pais_id IS NOT NULL
  AND sub.pais_id <> 0;
");

            // ── Vista liquidación (rebuild) ──
            migrationBuilder.Sql(@"
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


COMMENT ON VIEW public.vw_liquidacion_ecuador_pollo_engorde IS
  'Liquidación técnica Pollo Engorde (lote padre). Tiempo real (aves_actuales, lote_cerrado_logico) + bloque liquidación fn_indicadores_pollo_engorde (merma/ajuste/producción/sobrante). R1: campos de merma NULL si Costos no registró merma.';
");

            // ── Vista seguimiento (rebuild set-based) ──
            migrationBuilder.Sql(@"
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


COMMENT ON VIEW public.vw_seguimiento_pollo_engorde IS
  'Seguimiento diario engorde (espejo set-based de fn_seguimiento_diario_engorde v7). Incluye días movimiento-only (tipo_fila). saldo_alimento_kg_calculado = M1; saldo_aves_vivas resta ventas. Nombre y columnas previas conservados; columnas nuevas al final.';
");

            // ── Vista indicadores diarios (rebuild alineado al front) ──
            migrationBuilder.Sql(@"
-- =============================================================================
-- Vista: vw_indicadores_diarios_engorde  (REBUILD 2026-06-24)
-- =============================================================================
-- Alinea la vista con el indicador diario corregido del front
-- (IndicadoresDiariosEngordeComputeService) y el nuevo flujo de resolución de
-- guía (GuiaGeneticaEcuadorService.GetDatosAsync: por company_id + pais_id).
--
-- Cambios vs versión previa:
--  1. Guía emparejada por company_id + PAIS_ID (del lote) + raza + año + deleted_at IS NULL;
--     SIN exigir estado='active' (GetDatosAsync no lo exige). LATERAL LIMIT 1 → un solo header.
--  2. Consumo mixto = consumo_kg_hembras (campo mixto); solo si es 0 cae a hembras+machos.
--     (ítems generales de metadata = 0 en datos; no se replican aquí.)
--  3. Ganancia diaria real contra el ÚLTIMO peso medido > 0 (no el LAG del día anterior,
--     que se disparaba tras un día sin pesaje). Primer pesaje vs peso inicial del lote.
--  4. Aves vivas (inicio/fin) y mort_acum_pct restan también despachos de metadata
--     (sistema antiguo: despachoHembras/despachoH/despacho_hembra + variantes machos),
--     además de mortalidad + selección + error de sexaje.
-- Se conservan nombres de columnas; se agrega pais_id al final.
-- Backfill de pais_id de headers (de 0 al país de sus lotes) va en migración/script aparte.
-- =============================================================================

DROP VIEW IF EXISTS public.vw_indicadores_diarios_engorde;

CREATE VIEW public.vw_indicadores_diarios_engorde AS
WITH lote_filtrado AS (
    SELECT l.lote_ave_engorde_id,
        l.company_id,
        COALESCE(l.pais_id, 0) AS pais_id,
        COALESCE(co.name, l.empresa_nombre) AS empresa_nombre,
        l.lote_nombre,
        l.granja_id,
        fa.name AS granja_nombre,
        l.galpon_id,
        gp.galpon_nombre,
        l.nucleo_id,
        nu.nucleo_nombre,
        l.fecha_encaset,
        TRIM(BOTH FROM l.raza) AS raza,
        l.ano_tabla_genetica,
        l.peso_mixto,
        l.peso_inicial_h,
        l.peso_inicial_m,
        CASE
            WHEN COALESCE(l.aves_encasetadas, 0) > 0 THEN l.aves_encasetadas::bigint
            WHEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0)) > 0
                THEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0))::bigint
            ELSE 0::bigint
        END AS aves_iniciales
    FROM lote_ave_engorde l
        LEFT JOIN companies co ON co.id = l.company_id
        LEFT JOIN farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
        LEFT JOIN nucleos nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
        LEFT JOIN galpones gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
    WHERE l.deleted_at IS NULL
),
seg_agregado AS (
    SELECT s.lote_ave_engorde_id,
        s.fecha::date AS fecha_registro,
        sum(COALESCE(s.mortalidad_hembras, 0)) AS sum_mort_h,
        sum(COALESCE(s.mortalidad_machos, 0)) AS sum_mort_m,
        sum(COALESCE(s.sel_h, 0)) AS sum_sel_h,
        sum(COALESCE(s.sel_m, 0)) AS sum_sel_m,
        sum(COALESCE(s.error_sexaje_hembras, 0)) AS sum_err_h,
        sum(COALESCE(s.error_sexaje_machos, 0)) AS sum_err_m,
        -- Consumo mixto (corregido): hembras si >0, si no hembras+machos
        sum(CASE WHEN COALESCE(s.consumo_kg_hembras, 0) > 0
                 THEN s.consumo_kg_hembras
                 ELSE COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0) END) AS consumo_kg_dia,
        -- Despachos desde metadata (sistema antiguo): hembras + machos
        sum(
            COALESCE(NULLIF(regexp_replace(COALESCE(s.metadata->>'despachoHembras', s.metadata->>'despachoH', s.metadata->>'despacho_hembra', ''), '[^0-9.\-]', '', 'g'), '')::numeric, 0)
          + COALESCE(NULLIF(regexp_replace(COALESCE(s.metadata->>'despachoMachos', s.metadata->>'despachoM', s.metadata->>'despacho_macho', ''), '[^0-9.\-]', '', 'g'), '')::numeric, 0)
        ) AS despachos_dia
    FROM seguimiento_diario_aves_engorde s
        JOIN lote_filtrado l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
    GROUP BY s.lote_ave_engorde_id, (s.fecha::date)
),
seg_peso_ultimo AS (
    SELECT DISTINCT ON (s.lote_ave_engorde_id, (s.fecha::date)) s.lote_ave_engorde_id,
        s.fecha::date AS fecha_registro,
        s.peso_prom_hembras AS peso_h,
        s.peso_prom_machos AS peso_m
    FROM seguimiento_diario_aves_engorde s
        JOIN lote_filtrado l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
    ORDER BY s.lote_ave_engorde_id, (s.fecha::date), s.id DESC
),
dia_base AS (
    SELECT l.company_id,
        l.pais_id,
        l.empresa_nombre,
        l.lote_ave_engorde_id,
        l.lote_nombre,
        l.granja_id,
        l.granja_nombre,
        l.galpon_id,
        l.galpon_nombre,
        l.nucleo_id,
        l.nucleo_nombre,
        l.raza,
        l.ano_tabla_genetica,
        a.fecha_registro,
        GREATEST(0, a.fecha_registro - l.fecha_encaset::date) AS dia_vida,
        l.aves_iniciales,
        CASE
            WHEN l.peso_mixto IS NOT NULL AND l.peso_mixto > 0::double precision THEN l.peso_mixto::numeric
            WHEN COALESCE(l.peso_inicial_h, 0::double precision) > 0::double precision AND COALESCE(l.peso_inicial_m, 0::double precision) > 0::double precision
                THEN ((l.peso_inicial_h + l.peso_inicial_m) / 2.0::double precision)::numeric
            ELSE COALESCE(l.peso_inicial_h, l.peso_inicial_m, 0::double precision)::numeric
        END AS peso_inicial_mixto_g,
        -- Pérdidas que reducen aves vivas (corregido): mort + sel + errSexaje + despachos
        a.sum_mort_h + a.sum_mort_m + a.sum_sel_h + a.sum_sel_m + a.sum_err_h + a.sum_err_m + a.despachos_dia AS perdidas_aves_dia,
        -- Mort+sel del día (para % mort/sel; NO incluye errSexaje ni despachos)
        a.sum_mort_h + a.sum_mort_m + a.sum_sel_h + a.sum_sel_m AS mort_sel_dia,
        a.consumo_kg_dia,
        CASE
            WHEN COALESCE(p.peso_h, 0::double precision) > 0::double precision AND COALESCE(p.peso_m, 0::double precision) > 0::double precision
                THEN ((p.peso_h + p.peso_m) / 2.0::double precision)::numeric
            ELSE COALESCE(NULLIF(p.peso_h, 0::double precision), NULLIF(p.peso_m, 0::double precision), 0::double precision)::numeric
        END AS peso_mixto_dia_g
    FROM seg_agregado a
        JOIN lote_filtrado l ON l.lote_ave_engorde_id = a.lote_ave_engorde_id
        JOIN seg_peso_ultimo p ON p.lote_ave_engorde_id = a.lote_ave_engorde_id AND p.fecha_registro = a.fecha_registro
    WHERE l.fecha_encaset IS NOT NULL
),
con_aves AS (
    SELECT d.*,
        COALESCE(sum(d.perdidas_aves_dia) OVER (PARTITION BY d.lote_ave_engorde_id ORDER BY d.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0::numeric) AS perdidas_acum_prev,
        sum(d.perdidas_aves_dia) OVER (PARTITION BY d.lote_ave_engorde_id ORDER BY d.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS perdidas_acum_total,
        -- Carry-forward del último peso medido > 0 (grupo entre pesajes positivos)
        CASE WHEN d.peso_mixto_dia_g > 0 THEN d.peso_mixto_dia_g END AS peso_pos,
        count(CASE WHEN d.peso_mixto_dia_g > 0 THEN 1 END) OVER (PARTITION BY d.lote_ave_engorde_id ORDER BY d.fecha_registro ROWS UNBOUNDED PRECEDING) AS peso_grp
    FROM dia_base d
),
con_aves2 AS (
    SELECT c.*,
        GREATEST(0::bigint, c.aves_iniciales - c.perdidas_acum_prev::bigint) AS aves_inicio_dia,
        GREATEST(0::bigint, c.aves_iniciales - c.perdidas_acum_total::bigint) AS aves_fin_dia,
        max(c.peso_pos) OVER (PARTITION BY c.lote_ave_engorde_id, c.peso_grp) AS lpos_incl
    FROM con_aves c
),
con_guia AS (
    SELECT c.*,
        LAG(c.lpos_incl) OVER (PARTITION BY c.lote_ave_engorde_id ORDER BY c.fecha_registro) AS peso_medido_prev,
        gh.id AS guia_genetica_ecuador_header_id,
        gd.peso_corporal_g::numeric AS peso_tabla_g,
        gd.ganancia_diaria_g::numeric AS ganancia_diaria_tabla_g,
        gd.cantidad_alimento_diario_g::numeric AS consumo_diario_tabla_g,
        gd.alimento_acumulado_g::numeric AS alimento_acum_tabla_g,
        gd.ca::numeric AS ca_tabla,
        gd.mortalidad_seleccion_diaria::numeric AS mort_sel_tabla_pct
    FROM con_aves2 c
        LEFT JOIN LATERAL (
            SELECT h.id
            FROM guia_genetica_ecuador_header h
            WHERE h.company_id = c.company_id
              AND h.pais_id = c.pais_id
              AND TRIM(BOTH FROM lower(h.raza::text)) = TRIM(BOTH FROM lower(COALESCE(c.raza, ''::text)))
              AND h.anio_guia = c.ano_tabla_genetica
              AND c.ano_tabla_genetica IS NOT NULL
              AND TRIM(BOTH FROM COALESCE(c.raza, ''::text)) <> ''::text
              AND h.deleted_at IS NULL
            ORDER BY h.id
            LIMIT 1
        ) gh ON TRUE
        LEFT JOIN LATERAL (
            SELECT d.peso_corporal_g, d.ganancia_diaria_g, d.cantidad_alimento_diario_g, d.alimento_acumulado_g, d.ca, d.mortalidad_seleccion_diaria
            FROM guia_genetica_ecuador_detalle d
            WHERE d.guia_genetica_ecuador_header_id = gh.id
              AND lower(TRIM(BOTH FROM d.sexo)) = 'mixto'::text
              AND d.deleted_at IS NULL
              AND d.dia <= c.dia_vida
            ORDER BY d.dia DESC
            LIMIT 1
        ) gd ON TRUE
),
con_calc AS (
    SELECT g.*,
        CASE WHEN g.aves_inicio_dia > 0 THEN g.consumo_kg_dia * 1000.0 / g.aves_inicio_dia::numeric ELSE 0::numeric END AS consumo_diario_real_g,
        sum(CASE WHEN g.aves_inicio_dia > 0 THEN g.consumo_kg_dia * 1000.0 / g.aves_inicio_dia::numeric ELSE 0::numeric END)
            OVER (PARTITION BY g.lote_ave_engorde_id ORDER BY g.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS alimento_acum_real_g
    FROM con_guia g
),
final AS (
    SELECT c.company_id,
        c.pais_id,
        c.empresa_nombre,
        c.lote_ave_engorde_id,
        c.lote_nombre,
        c.granja_id,
        c.granja_nombre,
        c.galpon_id,
        c.galpon_nombre,
        c.nucleo_id,
        c.nucleo_nombre,
        c.raza,
        c.ano_tabla_genetica,
        c.guia_genetica_ecuador_header_id,
        c.fecha_registro,
        c.dia_vida,
        c.aves_iniciales,
        c.aves_inicio_dia,
        c.aves_fin_dia,
        c.peso_inicial_mixto_g,
        c.peso_mixto_dia_g AS peso_real_g,
        c.peso_tabla_g,
        -- Ganancia diaria real (corregido): contra el último peso medido > 0
        CASE
            WHEN c.peso_mixto_dia_g > 0::numeric
                THEN c.peso_mixto_dia_g - COALESCE(c.peso_medido_prev, c.peso_inicial_mixto_g)
            ELSE NULL::numeric
        END AS ganancia_diaria_real_g,
        c.ganancia_diaria_tabla_g,
        c.consumo_diario_real_g,
        c.consumo_diario_tabla_g,
        c.alimento_acum_real_g,
        c.alimento_acum_tabla_g,
        CASE
            WHEN c.peso_mixto_dia_g > 0::numeric AND c.alimento_acum_real_g > 0::numeric
                THEN c.alimento_acum_real_g / NULLIF(c.peso_mixto_dia_g, 0::numeric)
            ELSE NULL::numeric
        END AS ca_real,
        c.ca_tabla,
        CASE WHEN c.aves_inicio_dia > 0 THEN c.mort_sel_dia::numeric * 100.0 / c.aves_inicio_dia::numeric ELSE 0::numeric END AS mort_sel_real_pct,
        c.mort_sel_tabla_pct,
        CASE WHEN c.peso_tabla_g > 0::numeric AND c.peso_mixto_dia_g > 0::numeric
             THEN (c.peso_mixto_dia_g - c.peso_tabla_g) / NULLIF(c.peso_tabla_g, 0::numeric) * 100.0
             ELSE 0::numeric END AS dif_peso_vs_tabla_pct,
        -- % acum pérdidas (corregido): (inicial − aves_fin)/inicial, topado a 100 por construcción
        CASE WHEN c.aves_iniciales > 0
             THEN (c.aves_iniciales - c.aves_fin_dia)::numeric * 100.0 / c.aves_iniciales::numeric
             ELSE 0::numeric END AS mort_acum_pct
    FROM con_calc c
)
SELECT company_id,
    empresa_nombre,
    lote_ave_engorde_id,
    lote_nombre,
    granja_id,
    granja_nombre,
    galpon_id,
    galpon_nombre,
    nucleo_id,
    nucleo_nombre,
    raza,
    ano_tabla_genetica,
    guia_genetica_ecuador_header_id,
    to_char(fecha_registro::timestamp with time zone, 'YYYY-MM-DD'::text) AS fecha_ymd,
    fecha_registro,
    dia_vida,
    aves_iniciales,
    aves_inicio_dia,
    aves_fin_dia,
    trim_scale(peso_inicial_mixto_g) AS peso_inicial_mixto_g,
    trim_scale(peso_real_g) AS peso_real_g,
    trim_scale(peso_tabla_g) AS peso_tabla_g,
    trim_scale(ganancia_diaria_real_g) AS ganancia_diaria_real_g,
    trim_scale(ganancia_diaria_tabla_g) AS ganancia_diaria_tabla_g,
    trim_scale(consumo_diario_real_g) AS consumo_diario_real_g,
    trim_scale(consumo_diario_tabla_g) AS consumo_diario_tabla_g,
    trim_scale(alimento_acum_real_g) AS alimento_acum_real_g,
    trim_scale(alimento_acum_tabla_g) AS alimento_acum_tabla_g,
    trim_scale(ca_real) AS ca_real,
    trim_scale(ca_tabla) AS ca_tabla,
    trim_scale(mort_sel_real_pct) AS mort_sel_real_pct,
    trim_scale(mort_sel_tabla_pct) AS mort_sel_tabla_pct,
    trim_scale(dif_peso_vs_tabla_pct) AS dif_peso_vs_tabla_pct,
    trim_scale(mort_acum_pct) AS mort_acum_pct,
    -- NUEVO: país (para filtros Power BI)
    pais_id
FROM final f;


COMMENT ON VIEW public.vw_indicadores_diarios_engorde IS
  'Indicadores diarios engorde vs guía Ecuador (mixto), alineado al cómputo del front. Guía por company_id + pais_id (sin estado=active). Consumo mixto = consumo_kg_hembras. Ganancia vs último peso>0. Aves/mort_acum restan despachos de metadata.';
");

            // ── OWNER/GRANT (guardado por existencia de rol) ──
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'repropesa01') THEN
    ALTER VIEW public.vw_seguimiento_pollo_engorde OWNER TO repropesa01;
    ALTER VIEW public.vw_liquidacion_ecuador_pollo_engorde OWNER TO repropesa01;
    ALTER VIEW public.vw_indicadores_diarios_engorde OWNER TO repropesa01;
  END IF;
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'usrDWH') THEN
    GRANT SELECT ON public.vw_seguimiento_pollo_engorde TO ""usrDWH"";
    GRANT SELECT ON public.vw_liquidacion_ecuador_pollo_engorde TO ""usrDWH"";
    GRANT SELECT ON public.vw_indicadores_diarios_engorde TO ""usrDWH"";
  END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaura las definiciones previas de las vistas (el backfill de pais_id NO se revierte).
            migrationBuilder.Sql(@"
DROP VIEW IF EXISTS public.vw_liquidacion_ecuador_pollo_engorde;
CREATE OR REPLACE VIEW public.vw_liquidacion_ecuador_pollo_engorde AS
 WITH params AS (
         SELECT 2.7 AS peso_ajuste,
            4.5 AS divisor_ajuste
        ), seg_padre AS (
         SELECT s.lote_ave_engorde_id,
            sum(COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)) AS sum_mort,
            sum(COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)) AS sum_sel,
            sum(COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric)) AS consumo_kg
           FROM seguimiento_diario_aves_engorde s
          GROUP BY s.lote_ave_engorde_id
        ), mov_salida AS (
         SELECT m.lote_ave_engorde_origen_id AS lote_ave_engorde_id,
            sum(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0)) AS aves_sacrificadas,
            sum(
                CASE
                    WHEN m.peso_bruto IS NOT NULL AND m.peso_tara IS NOT NULL THEN m.peso_bruto::numeric - m.peso_tara::numeric
                    ELSE 0::numeric
                END) AS kg_carne,
            avg(m.edad_aves::numeric) FILTER (WHERE m.edad_aves IS NOT NULL) AS edad_promedio,
            max(m.fecha_movimiento) AS fecha_ultimo_despacho
           FROM movimiento_pollo_engorde m
          WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.lote_ave_engorde_origen_id IS NOT NULL AND (m.tipo_movimiento::text = ANY (ARRAY['Venta'::character varying::text, 'Despacho'::character varying::text, 'Retiro'::character varying::text]))
          GROUP BY m.lote_ave_engorde_origen_id
        ), mov_traslado_rep AS (
         SELECT m.lote_ave_engorde_origen_id AS lote_ave_engorde_id,
            sum(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0)) AS aves_trasladadas_rep
           FROM movimiento_pollo_engorde m
          WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.tipo_movimiento::text = 'Traslado'::text AND m.lote_ave_engorde_origen_id IS NOT NULL AND m.lote_reproductora_ave_engorde_destino_id IS NOT NULL
          GROUP BY m.lote_ave_engorde_origen_id
        ), rep_base AS (
         SELECT r.id AS lote_reproductora_id,
            r.lote_ave_engorde_id,
                CASE
                    WHEN (COALESCE(r.aves_inicio_hembras, 0) + COALESCE(r.aves_inicio_machos, 0) + COALESCE(r.mixtas, 0)) > 0 THEN COALESCE(r.aves_inicio_hembras, 0) + COALESCE(r.aves_inicio_machos, 0) + COALESCE(r.mixtas, 0)
                    ELSE COALESCE(r.h, 0) + COALESCE(r.m, 0) + COALESCE(r.mixtas, 0)
                END::bigint AS encaset_rep
           FROM lote_reproductora_ave_engorde r
        ), rep_seg AS (
         SELECT s.lote_reproductora_ave_engorde_id,
            sum(COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)) AS mort_sel_rep
           FROM seguimiento_diario_lote_reproductora_aves_engorde s
          GROUP BY s.lote_reproductora_ave_engorde_id
        ), rep_mov AS (
         SELECT m.lote_reproductora_ave_engorde_origen_id AS lote_reproductora_id,
            sum(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0)) AS ventas_rep
           FROM movimiento_pollo_engorde m
          WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.lote_reproductora_ave_engorde_origen_id IS NOT NULL AND (m.tipo_movimiento::text = ANY (ARRAY['Venta'::character varying::text, 'Despacho'::character varying::text, 'Retiro'::character varying::text]))
          GROUP BY m.lote_reproductora_ave_engorde_origen_id
        ), rep_tiene_aves AS (
         SELECT rb.lote_ave_engorde_id,
            bool_or(GREATEST(0::bigint, rb.encaset_rep - COALESCE(rs.mort_sel_rep, 0::bigint) - COALESCE(rm.ventas_rep, 0::bigint)) > 0) AS alguna_rep_con_aves_positivas
           FROM rep_base rb
             LEFT JOIN rep_seg rs ON rs.lote_reproductora_ave_engorde_id = rb.lote_reproductora_id
             LEFT JOIN rep_mov rm ON rm.lote_reproductora_id = rb.lote_reproductora_id
          GROUP BY rb.lote_ave_engorde_id
        ), rep_counts AS (
         SELECT r.lote_ave_engorde_id,
            count(*)::integer AS cnt_rep
           FROM lote_reproductora_ave_engorde r
          GROUP BY r.lote_ave_engorde_id
        ), ult_seg_padre AS (
         SELECT DISTINCT ON (s.lote_ave_engorde_id) s.lote_ave_engorde_id,
            s.fecha::date AS ultima_fecha_seg
           FROM seguimiento_diario_aves_engorde s
          ORDER BY s.lote_ave_engorde_id, s.fecha DESC, s.id DESC
        ), ult_mov_cualquier AS (
         SELECT DISTINCT ON (m.lote_ave_engorde_origen_id) m.lote_ave_engorde_origen_id AS lote_ave_engorde_id,
            m.fecha_movimiento AS ultima_fecha_mov
           FROM movimiento_pollo_engorde m
          WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.lote_ave_engorde_origen_id IS NOT NULL
          ORDER BY m.lote_ave_engorde_origen_id, m.fecha_movimiento DESC, m.id DESC
        ), base AS (
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
            COALESCE(l.aves_encasetadas, 0)::bigint AS aves_encasetadas_raw,
                CASE
                    WHEN COALESCE(l.aves_encasetadas, 0) > 0 THEN l.aves_encasetadas::bigint
                    ELSE (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0))::bigint
                END AS aves_encasetadas,
            COALESCE(sp.sum_mort, 0::bigint) + COALESCE(sp.sum_sel, 0::bigint) AS mort_sel_padre,
            COALESCE(sp.consumo_kg, 0::numeric) AS consumo_total_kg,
            COALESCE(ms.aves_sacrificadas, 0::bigint) AS aves_sacrificadas,
            COALESCE(ms.kg_carne, 0::numeric) AS kg_carne_pollos,
            COALESCE(ms.edad_promedio, 0::numeric) AS edad_promedio_mov,
            ms.fecha_ultimo_despacho,
            COALESCE(mt.aves_trasladadas_rep, 0::bigint) AS aves_trasladadas_rep,
            COALESCE(rc.cnt_rep, 0) AS cantidad_lotes_reproductores,
                CASE
                    WHEN COALESCE(rc.cnt_rep, 0) = 0 THEN false
                    ELSE NOT COALESCE(rt.alguna_rep_con_aves_positivas, false)
                END AS todos_reproductores_sin_aves,
            us.ultima_fecha_seg,
            umc.ultima_fecha_mov,
                CASE
                    WHEN l.galpon_id IS NOT NULL AND TRIM(BOTH FROM l.galpon_id) <> ''::text THEN
                    CASE
                        WHEN gp.ancho IS NOT NULL AND gp.largo IS NOT NULL AND TRIM(BOTH FROM gp.ancho::text) <> ''::text AND TRIM(BOTH FROM gp.largo::text) <> ''::text AND TRIM(BOTH FROM gp.ancho::text) ~ '^[0-9]+([.,][0-9]+)?$'::text AND TRIM(BOTH FROM gp.largo::text) ~ '^[0-9]+([.,][0-9]+)?$'::text THEN replace(replace(TRIM(BOTH FROM gp.ancho::text), ','::text, '.'::text), ' '::text, ''::text)::numeric * replace(replace(TRIM(BOTH FROM gp.largo::text), ','::text, '.'::text), ' '::text, ''::text)::numeric
                        ELSE NULL::numeric
                    END
                    ELSE ( SELECT COALESCE(sum(
                            CASE
                                WHEN g.ancho IS NOT NULL AND g.largo IS NOT NULL AND TRIM(BOTH FROM g.ancho::text) <> ''::text AND TRIM(BOTH FROM g.largo::text) <> ''::text AND TRIM(BOTH FROM g.ancho::text) ~ '^[0-9]+([.,][0-9]+)?$'::text AND TRIM(BOTH FROM g.largo::text) ~ '^[0-9]+([.,][0-9]+)?$'::text THEN replace(replace(TRIM(BOTH FROM g.ancho::text), ','::text, '.'::text), ' '::text, ''::text)::numeric * replace(replace(TRIM(BOTH FROM g.largo::text), ','::text, '.'::text), ' '::text, ''::text)::numeric
                                ELSE 0::numeric
                            END), 0::numeric) AS ""coalesce""
                       FROM galpones g
                      WHERE g.granja_id = l.granja_id AND g.deleted_at IS NULL)
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
        ), calc AS (
         SELECT b.lote_ave_engorde_id,
            b.company_id,
            b.empresa_nombre,
            b.granja_id,
            b.granja_nombre,
            b.nucleo_id,
            b.nucleo_nombre,
            b.galpon_id,
            b.galpon_nombre,
            b.lote_nombre,
            b.fecha_encaset,
            b.estado_operativo_lote,
            b.liquidado_at,
            b.aves_encasetadas_raw,
            b.aves_encasetadas,
            b.mort_sel_padre,
            b.consumo_total_kg,
            b.aves_sacrificadas,
            b.kg_carne_pollos,
            b.edad_promedio_mov,
            b.fecha_ultimo_despacho,
            b.aves_trasladadas_rep,
            b.cantidad_lotes_reproductores,
            b.todos_reproductores_sin_aves,
            b.ultima_fecha_seg,
            b.ultima_fecha_mov,
            b.metros_cuadrados,
            b.mort_sel_padre AS mortalidad_unidades,
            GREATEST(0::bigint, b.aves_encasetadas - b.mort_sel_padre - b.aves_sacrificadas - b.aves_trasladadas_rep) AS aves_actuales,
                CASE
                    WHEN b.aves_encasetadas > 0 THEN b.mort_sel_padre::numeric / b.aves_encasetadas::numeric * 100::numeric
                    ELSE 0::numeric
                END AS mortalidad_porcentaje,
                CASE
                    WHEN b.aves_encasetadas > 0 THEN (b.aves_encasetadas - b.mort_sel_padre)::numeric / b.aves_encasetadas::numeric * 100::numeric
                    ELSE 0::numeric
                END AS supervivencia_porcentaje,
                CASE
                    WHEN b.aves_sacrificadas > 0 THEN b.consumo_total_kg / b.aves_sacrificadas::numeric * 1000::numeric
                    ELSE 0::numeric
                END AS consumo_ave_gramos,
                CASE
                    WHEN b.aves_sacrificadas > 0 THEN b.kg_carne_pollos / b.aves_sacrificadas::numeric
                    ELSE 0::numeric
                END AS peso_promedio_kilos,
                CASE
                    WHEN b.kg_carne_pollos > 0::numeric THEN b.consumo_total_kg / b.kg_carne_pollos
                    ELSE 0::numeric
                END AS conversion,
            ( SELECT p.peso_ajuste
                   FROM params p) AS peso_ajuste_variable,
            ( SELECT p.divisor_ajuste
                   FROM params p) AS divisor_ajuste_variable
           FROM base b
        ), calc2 AS (
         SELECT c.lote_ave_engorde_id,
            c.company_id,
            c.empresa_nombre,
            c.granja_id,
            c.granja_nombre,
            c.nucleo_id,
            c.nucleo_nombre,
            c.galpon_id,
            c.galpon_nombre,
            c.lote_nombre,
            c.fecha_encaset,
            c.estado_operativo_lote,
            c.liquidado_at,
            c.aves_encasetadas_raw,
            c.aves_encasetadas,
            c.mort_sel_padre,
            c.consumo_total_kg,
            c.aves_sacrificadas,
            c.kg_carne_pollos,
            c.edad_promedio_mov,
            c.fecha_ultimo_despacho,
            c.aves_trasladadas_rep,
            c.cantidad_lotes_reproductores,
            c.todos_reproductores_sin_aves,
            c.ultima_fecha_seg,
            c.ultima_fecha_mov,
            c.metros_cuadrados,
            c.mortalidad_unidades,
            c.aves_actuales,
            c.mortalidad_porcentaje,
            c.supervivencia_porcentaje,
            c.consumo_ave_gramos,
            c.peso_promedio_kilos,
            c.conversion,
            c.peso_ajuste_variable,
            c.divisor_ajuste_variable,
                CASE
                    WHEN GREATEST(0::bigint, c.aves_actuales) = 0 THEN true
                    ELSE false
                END AS cerrado_por_aves_cero,
                CASE
                    WHEN GREATEST(0::bigint, c.aves_actuales) > 0 AND c.aves_sacrificadas = 0 AND COALESCE(c.mort_sel_padre, 0::bigint) = 0 AND c.todos_reproductores_sin_aves AND c.cantidad_lotes_reproductores > 0 THEN true
                    ELSE false
                END AS cerrado_por_reproductores_vendidos,
                CASE
                    WHEN c.conversion > 0::numeric THEN c.conversion + (c.peso_ajuste_variable - c.peso_promedio_kilos) / c.divisor_ajuste_variable
                    ELSE 0::numeric
                END AS conversion_ajustada2700
           FROM calc c
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
        CASE
            WHEN COALESCE(metros_cuadrados, 0::numeric) > 0::numeric THEN aves_sacrificadas::numeric / metros_cuadrados
            ELSE 0::numeric
        END AS aves_por_metro_cuadrado,
        CASE
            WHEN COALESCE(metros_cuadrados, 0::numeric) > 0::numeric THEN kg_carne_pollos / metros_cuadrados
            ELSE 0::numeric
        END AS kg_por_metro_cuadrado,
        CASE
            WHEN conversion > 0::numeric THEN peso_promedio_kilos / conversion * 100::numeric
            ELSE 0::numeric
        END AS eficiencia_americana,
        CASE
            WHEN conversion > 0::numeric AND edad_promedio_mov > 0::numeric THEN peso_promedio_kilos * supervivencia_porcentaje / (edad_promedio_mov * conversion) * 100::numeric
            ELSE 0::numeric
        END AS eficiencia_europea,
        CASE
            WHEN conversion > 0::numeric THEN peso_promedio_kilos / conversion / conversion * 100::numeric
            ELSE 0::numeric
        END AS indice_productividad,
        CASE
            WHEN edad_promedio_mov > 0::numeric THEN peso_promedio_kilos / edad_promedio_mov * 1000::numeric
            ELSE 0::numeric
        END AS ganancia_dia,
    aves_trasladadas_rep,
    aves_actuales,
    aves_actuales > 0 AS tiene_aves,
    cerrado_por_aves_cero OR cerrado_por_reproductores_vendidos AS lote_cerrado_logico,
    cerrado_por_aves_cero,
    cerrado_por_reproductores_vendidos,
    fecha_ultimo_despacho AS fecha_cierre_ultimo_despacho,
        CASE
            WHEN (cerrado_por_aves_cero OR cerrado_por_reproductores_vendidos) AND fecha_ultimo_despacho IS NULL THEN COALESCE(ultima_fecha_seg::timestamp with time zone, ultima_fecha_mov, fecha_encaset::timestamp with time zone)
            ELSE fecha_ultimo_despacho
        END AS fecha_cierre_efectiva
   FROM calc2 c2;
");

            migrationBuilder.Sql(@"
DROP VIEW IF EXISTS public.vw_seguimiento_pollo_engorde;
CREATE OR REPLACE VIEW public.vw_seguimiento_pollo_engorde AS
 WITH RECURSIVE hist_base AS (
         SELECT h.id AS hid,
            h.lote_ave_engorde_id,
            h.tipo_evento,
            h.created_at,
            TRIM(BOTH FROM (COALESCE(h.referencia, ''::character varying)::text || ' '::text) || COALESCE(h.numero_documento, ''::character varying)::text) AS ref_full,
            COALESCE(
                CASE
                    WHEN lower(TRIM(BOTH FROM (COALESCE(h.referencia, ''::character varying)::text || ' '::text) || COALESCE(h.numero_documento, ''::character varying)::text)) ~ 'seguimiento\s+aves\s+engorde\s+#\d+\s+(\d{4}-\d{2}-\d{2})'::text THEN ""substring""(lower(TRIM(BOTH FROM (COALESCE(h.referencia, ''::character varying)::text || ' '::text) || COALESCE(h.numero_documento, ''::character varying)::text)), 'seguimiento\s+aves\s+engorde\s+#\d+\s+(\d{4}-\d{2}-\d{2})'::text)::date
                    ELSE NULL::date
                END,
                CASE
                    WHEN h.tipo_evento::text = 'INV_CONSUMO'::text AND TRIM(BOTH FROM (COALESCE(h.referencia, ''::character varying)::text || ' '::text) || COALESCE(h.numero_documento, ''::character varying)::text) ~ '(\d{4}-\d{2}-\d{2})'::text THEN ""substring""(TRIM(BOTH FROM (COALESCE(h.referencia, ''::character varying)::text || ' '::text) || COALESCE(h.numero_documento, ''::character varying)::text), '(\d{4}-\d{2}-\d{2})'::text)::date
                    ELSE NULL::date
                END, h.fecha_operacion) AS ymd_efe,
                CASE
                    WHEN h.anulado THEN NULL::numeric
                    WHEN h.tipo_evento::text = 'INV_INGRESO'::text AND COALESCE(h.cantidad_kg, 0::numeric) <> 0::numeric THEN h.cantidad_kg::numeric
                    WHEN h.tipo_evento::text = 'INV_TRASLADO_ENTRADA'::text AND COALESCE(h.cantidad_kg, 0::numeric) <> 0::numeric THEN h.cantidad_kg::numeric
                    WHEN h.tipo_evento::text = 'INV_TRASLADO_SALIDA'::text AND COALESCE(h.cantidad_kg, 0::numeric) <> 0::numeric THEN - abs(h.cantidad_kg::numeric)
                    WHEN h.tipo_evento::text = 'INV_OTRO'::text AND lower(TRIM(BOTH FROM COALESCE(h.movement_type_original, ''::character varying))) = 'ajustestock'::text THEN h.cantidad_kg::numeric
                    WHEN h.tipo_evento::text = 'INV_OTRO'::text AND lower(TRIM(BOTH FROM COALESCE(h.movement_type_original, ''::character varying))) = 'eliminacionstock'::text AND COALESCE(h.cantidad_kg, 0::numeric) <> 0::numeric THEN - abs(h.cantidad_kg::numeric)
                    ELSE NULL::numeric
                END AS delta_kg,
                CASE h.tipo_evento
                    WHEN 'INV_INGRESO'::text THEN 0
                    WHEN 'INV_TRASLADO_ENTRADA'::text THEN 1
                    WHEN 'INV_TRASLADO_SALIDA'::text THEN 2
                    WHEN 'INV_OTRO'::text THEN 2
                    ELSE 99
                END AS ord_hist,
            (EXTRACT(epoch FROM h.created_at) * 1000::numeric)::bigint AS tie_h_ms
           FROM lote_registro_historico_unificado h
          WHERE NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
        ), first_seg_f AS (
         SELECT s.lote_ave_engorde_id,
            min(s.fecha::date) AS d0
           FROM seguimiento_diario_aves_engorde s
          GROUP BY s.lote_ave_engorde_id
        ), hist_opening AS (
         SELECT hb.lote_ave_engorde_id,
            0 AS phase,
            hb.ymd_efe,
            0 AS ord_sort,
            hb.tie_h_ms AS tie,
            NULL::bigint AS seg_id,
            hb.delta_kg
           FROM hist_base hb
             JOIN first_seg_f f_1 ON f_1.lote_ave_engorde_id = hb.lote_ave_engorde_id
          WHERE hb.delta_kg IS NOT NULL AND hb.ymd_efe < f_1.d0
        ), hist_main AS (
         SELECT hb.lote_ave_engorde_id,
            1 AS phase,
            hb.ymd_efe,
            hb.ord_hist AS ord_sort,
            hb.tie_h_ms AS tie,
            NULL::bigint AS seg_id,
            hb.delta_kg
           FROM hist_base hb
             JOIN first_seg_f f_1 ON f_1.lote_ave_engorde_id = hb.lote_ave_engorde_id
             JOIN lote_ave_engorde la ON la.lote_ave_engorde_id = hb.lote_ave_engorde_id
          WHERE hb.delta_kg IS NOT NULL AND hb.ymd_efe >= f_1.d0 AND (la.fecha_encaset IS NULL OR hb.ymd_efe >= la.fecha_encaset::date)
        ), seg_events AS (
         SELECT s.lote_ave_engorde_id,
            1 AS phase,
            s.fecha::date AS ymd_efe,
            3 AS ord_sort,
            (EXTRACT(epoch FROM ((s.fecha::date + '12:00:00'::interval) AT TIME ZONE 'UTC'::text)) * 1000::numeric)::bigint AS tie,
            s.id AS seg_id,
            - (COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric)) AS delta_kg
           FROM seguimiento_diario_aves_engorde s
        ), events_union AS (
         SELECT hist_opening.lote_ave_engorde_id,
            hist_opening.phase,
            hist_opening.ymd_efe,
            hist_opening.ord_sort,
            hist_opening.tie,
            hist_opening.seg_id,
            hist_opening.delta_kg
           FROM hist_opening
        UNION ALL
         SELECT hist_main.lote_ave_engorde_id,
            hist_main.phase,
            hist_main.ymd_efe,
            hist_main.ord_sort,
            hist_main.tie,
            hist_main.seg_id,
            hist_main.delta_kg
           FROM hist_main
        UNION ALL
         SELECT seg_events.lote_ave_engorde_id,
            seg_events.phase,
            seg_events.ymd_efe,
            seg_events.ord_sort,
            seg_events.tie,
            seg_events.seg_id,
            seg_events.delta_kg
           FROM seg_events
        ), events_ordered AS (
         SELECT eu.lote_ave_engorde_id,
            eu.phase,
            eu.ymd_efe,
            eu.ord_sort,
            eu.tie,
            eu.seg_id,
            eu.delta_kg,
            row_number() OVER (PARTITION BY eu.lote_ave_engorde_id ORDER BY eu.phase, eu.ymd_efe, eu.ord_sort, eu.tie, (COALESCE(eu.seg_id, 0::bigint))) AS seq
           FROM events_union eu
        ), rec AS (
         SELECT eo.lote_ave_engorde_id,
            eo.seq,
            eo.seg_id,
            eo.delta_kg,
            GREATEST(0::numeric, eo.delta_kg) AS bal
           FROM events_ordered eo
          WHERE eo.seq = 1
        UNION ALL
         SELECT eo.lote_ave_engorde_id,
            eo.seq,
            eo.seg_id,
            eo.delta_kg,
            GREATEST(0::numeric, r.bal + eo.delta_kg) AS bal
           FROM rec r
             JOIN events_ordered eo ON eo.lote_ave_engorde_id = r.lote_ave_engorde_id AND eo.seq = (r.seq + 1)
        ), saldo_ui AS (
         SELECT r.seg_id,
            r.bal AS saldo_alimento_kg_calculado
           FROM rec r
          WHERE r.seg_id IS NOT NULL
        ), lote AS (
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
            GREATEST(0,
                CASE
                    WHEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0)) > 0 THEN COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0)
                    ELSE COALESCE(l.aves_encasetadas, 0)
                END)::bigint AS aves_iniciales,
            COALESCE(l.hembras_l, 0)::bigint AS aves_iniciales_hembras,
            COALESCE(l.machos_l, 0)::bigint AS aves_iniciales_machos
           FROM lote_ave_engorde l
             LEFT JOIN farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
             LEFT JOIN companies cp ON cp.id = fa.company_id
             LEFT JOIN nucleos nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
             LEFT JOIN galpones gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
        ), hist_por_dia AS (
         SELECT h.lote_ave_engorde_id,
            h.fecha_operacion AS dia,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'INV_INGRESO'::text AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0::numeric)
                    ELSE 0::numeric
                END) AS ingreso_kg,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'INV_TRASLADO_ENTRADA'::text AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0::numeric)
                    ELSE 0::numeric
                END) AS traslado_entrada_kg,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'INV_TRASLADO_SALIDA'::text AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0::numeric)
                    ELSE 0::numeric
                END) AS traslado_salida_kg,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'INV_CONSUMO'::text AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0::numeric)
                    ELSE 0::numeric
                END) AS consumo_bodega_kg,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'VENTA_AVES'::text AND NOT h.anulado THEN COALESCE(h.cantidad_hembras, 0)
                    ELSE 0
                END) AS venta_hembras,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'VENTA_AVES'::text AND NOT h.anulado THEN COALESCE(h.cantidad_machos, 0)
                    ELSE 0
                END) AS venta_machos,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'VENTA_AVES'::text AND NOT h.anulado THEN COALESCE(h.cantidad_mixtas, 0)
                    ELSE 0
                END) AS venta_mixtas,
            string_agg(DISTINCT NULLIF(TRIM(BOTH FROM COALESCE(h.numero_documento, h.referencia, ''::character varying)), ''::text), ', '::text) FILTER (WHERE TRIM(BOTH FROM COALESCE(h.numero_documento, h.referencia, ''::character varying)) <> ''::text) AS documentos_hist
           FROM lote_registro_historico_unificado h
          WHERE NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
          GROUP BY h.lote_ave_engorde_id, h.fecha_operacion
        ), base AS (
         SELECT s.id AS seguimiento_id,
            s.lote_ave_engorde_id,
            s.fecha::date AS fecha_registro,
            l.lote_nombre,
            l.fecha_encaset,
            l.granja_id,
            l.granja_nombre,
            l.company_id,
            cp.name AS company_nombre,
            l.galpon_id,
            l.galpon_nombre,
            l.nucleo_id,
            l.nucleo_nombre,
            l.aves_iniciales,
            l.aves_iniciales_hembras,
            l.aves_iniciales_machos,
            GREATEST(0, s.fecha::date - l.fecha_encaset::date) AS edad_dias_vida,
            LEAST(8, GREATEST(1, ceil((GREATEST(0, s.fecha::date - l.fecha_encaset::date) + 1)::numeric / 7.0)::integer)) AS semana_ui,
            COALESCE(s.mortalidad_hembras, 0) AS mortalidad_hembras,
            COALESCE(s.mortalidad_machos, 0) AS mortalidad_machos,
            COALESCE(s.sel_h, 0) AS seleccion_hembras,
            COALESCE(s.sel_m, 0) AS seleccion_machos,
            COALESCE(s.error_sexaje_hembras, 0) AS error_sexaje_hembras,
            COALESCE(s.error_sexaje_machos, 0) AS error_sexaje_machos,
            COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) AS total_mort_sel_dia,
            COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_todas_dia,
            COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.error_sexaje_hembras, 0) AS perdidas_hembras_dia,
            COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_m, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_machos_dia,
            s.tipo_alimento,
                CASE
                    WHEN upper(COALESCE(s.tipo_alimento, ''::character varying)::text) ~~ '%PRE%'::text THEN 'PRE'::text
                    WHEN upper(COALESCE(s.tipo_alimento, ''::character varying)::text) ~~ '%INI%'::text THEN 'INI'::text
                    WHEN upper(COALESCE(s.tipo_alimento, ''::character varying)::text) ~~ '%ENG%'::text THEN 'ENG'::text
                    WHEN upper(COALESCE(s.tipo_alimento, ''::character varying)::text) ~~ '%FIN%'::text THEN 'FIN-D'::text
                    WHEN COALESCE(s.tipo_alimento, ''::character varying)::text = ''::text THEN '—'::text
                    ELSE ""left""(s.tipo_alimento::text, 8)
                END AS tipo_alimento_corto,
            COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric) AS consumo_real_dia_kg,
            s.consumo_kg_hembras,
            s.consumo_kg_machos,
            s.consumo_agua_diario,
            s.peso_prom_hembras,
            s.peso_prom_machos,
            s.observaciones,
            s.saldo_alimento_kg AS saldo_alimento_kg_bd,
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
             JOIN lote l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
             LEFT JOIN hist_por_dia h ON h.lote_ave_engorde_id = s.lote_ave_engorde_id AND h.dia = s.fecha::date
             LEFT JOIN companies cp ON cp.id = l.company_id
             LEFT JOIN saldo_ui su ON su.seg_id = s.id
        ), con_acum AS (
         SELECT b.seguimiento_id,
            b.lote_ave_engorde_id,
            b.fecha_registro,
            b.lote_nombre,
            b.fecha_encaset,
            b.granja_id,
            b.granja_nombre,
            b.company_id,
            b.company_nombre,
            b.galpon_id,
            b.galpon_nombre,
            b.nucleo_id,
            b.nucleo_nombre,
            b.aves_iniciales,
            b.aves_iniciales_hembras,
            b.aves_iniciales_machos,
            b.edad_dias_vida,
            b.semana_ui,
            b.mortalidad_hembras,
            b.mortalidad_machos,
            b.seleccion_hembras,
            b.seleccion_machos,
            b.error_sexaje_hembras,
            b.error_sexaje_machos,
            b.total_mort_sel_dia,
            b.perdidas_todas_dia,
            b.perdidas_hembras_dia,
            b.perdidas_machos_dia,
            b.tipo_alimento,
            b.tipo_alimento_corto,
            b.consumo_real_dia_kg,
            b.consumo_kg_hembras,
            b.consumo_kg_machos,
            b.consumo_agua_diario,
            b.peso_prom_hembras,
            b.peso_prom_machos,
            b.observaciones,
            b.saldo_alimento_kg_bd,
            b.saldo_alimento_kg_calculado,
            b.metadata,
            b.items_adicionales,
            b.ingreso_kg,
            b.traslado_entrada_kg,
            b.traslado_salida_kg,
            b.consumo_bodega_kg,
            b.venta_hembras,
            b.venta_machos,
            b.venta_mixtas,
            b.documentos_hist,
            sum(b.perdidas_todas_dia) OVER (PARTITION BY b.lote_ave_engorde_id ORDER BY b.fecha_registro, b.seguimiento_id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS acum_perdidas_todas,
            sum(b.consumo_real_dia_kg) OVER (PARTITION BY b.lote_ave_engorde_id ORDER BY b.fecha_registro, b.seguimiento_id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS consumo_acumulado_kg,
            sum(b.perdidas_hembras_dia) OVER (PARTITION BY b.lote_ave_engorde_id ORDER BY b.fecha_registro, b.seguimiento_id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS acum_perdidas_hembras,
            sum(b.perdidas_machos_dia) OVER (PARTITION BY b.lote_ave_engorde_id ORDER BY b.fecha_registro, b.seguimiento_id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS acum_perdidas_machos
           FROM base b
        ), final AS (
         SELECT c.seguimiento_id,
            c.lote_ave_engorde_id,
            c.fecha_registro,
            c.lote_nombre,
            c.fecha_encaset,
            c.granja_id,
            c.granja_nombre,
            c.company_id,
            c.company_nombre,
            c.galpon_id,
            c.galpon_nombre,
            c.nucleo_id,
            c.nucleo_nombre,
            c.aves_iniciales,
            c.aves_iniciales_hembras,
            c.aves_iniciales_machos,
            c.edad_dias_vida,
            c.semana_ui,
            c.mortalidad_hembras,
            c.mortalidad_machos,
            c.seleccion_hembras,
            c.seleccion_machos,
            c.error_sexaje_hembras,
            c.error_sexaje_machos,
            c.total_mort_sel_dia,
            c.perdidas_todas_dia,
            c.perdidas_hembras_dia,
            c.perdidas_machos_dia,
            c.tipo_alimento,
            c.tipo_alimento_corto,
            c.consumo_real_dia_kg,
            c.consumo_kg_hembras,
            c.consumo_kg_machos,
            c.consumo_agua_diario,
            c.peso_prom_hembras,
            c.peso_prom_machos,
            c.observaciones,
            c.saldo_alimento_kg_bd,
            c.saldo_alimento_kg_calculado,
            c.metadata,
            c.items_adicionales,
            c.ingreso_kg,
            c.traslado_entrada_kg,
            c.traslado_salida_kg,
            c.consumo_bodega_kg,
            c.venta_hembras,
            c.venta_machos,
            c.venta_mixtas,
            c.documentos_hist,
            c.acum_perdidas_todas,
            c.consumo_acumulado_kg,
            c.acum_perdidas_hembras,
            c.acum_perdidas_machos,
            GREATEST(0::bigint, c.aves_iniciales - c.acum_perdidas_todas) AS saldo_aves_vivas_fin_dia,
            GREATEST(0::bigint, c.aves_iniciales - c.acum_perdidas_todas + c.perdidas_todas_dia)::numeric AS saldo_aves_inicio_dia,
            GREATEST(0::bigint, c.aves_iniciales_hembras - c.acum_perdidas_hembras) AS saldo_aves_vivas_hembras,
            GREATEST(0::bigint, c.aves_iniciales_machos - c.acum_perdidas_machos) AS saldo_aves_vivas_machos
           FROM con_acum c
        )
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
    to_char(fecha_registro::timestamp with time zone, 'DD/MM/YYYY'::text) AS fecha_dmy,
    fecha_registro,
    semana_ui AS semana,
    edad_dias_vida,
    to_char(fecha_registro::timestamp with time zone, 'Dy, DD Mon'::text) AS dia_calendario_corto,
    mortalidad_hembras,
    mortalidad_machos,
    seleccion_hembras,
    seleccion_machos,
    total_mort_sel_dia AS total_mort_mas_sel_dia,
    error_sexaje_hembras,
    error_sexaje_machos,
    venta_hembras AS despacho_hembras_hist,
    venta_machos AS despacho_machos_hist,
    venta_mixtas AS despacho_mixtas_hist,
    trim_scale(saldo_alimento_kg_bd) AS saldo_alimento_kg_bd,
    trim_scale(saldo_alimento_kg_calculado) AS saldo_alimento_kg_calculado,
    saldo_aves_vivas_fin_dia AS saldo_aves_vivas,
    saldo_aves_vivas_hembras,
    saldo_aves_vivas_machos,
    tipo_alimento,
    tipo_alimento_corto,
        CASE
            WHEN COALESCE(ingreso_kg, 0::numeric) > 0::numeric THEN to_char(ingreso_kg, 'FM9999999999990.999'::text) || ' kg'::text
            ELSE NULL::text
        END AS ingreso_alimento_texto_hist,
        CASE
            WHEN COALESCE(traslado_entrada_kg, 0::numeric) = 0::numeric AND COALESCE(traslado_salida_kg, 0::numeric) = 0::numeric THEN NULL::text
            ELSE concat_ws(' · '::text,
            CASE
                WHEN COALESCE(traslado_entrada_kg, 0::numeric) > 0::numeric THEN ('Entrada '::text || to_char(traslado_entrada_kg, 'FM9999999999990.999'::text)) || ' kg'::text
                ELSE NULL::text
            END,
            CASE
                WHEN COALESCE(traslado_salida_kg, 0::numeric) > 0::numeric THEN ('Salida '::text || to_char(traslado_salida_kg, 'FM9999999999990.999'::text)) || ' kg'::text
                ELSE NULL::text
            END)
        END AS traslado_texto_hist,
    COALESCE(documentos_hist, ''::text) AS documento_hist,
    metadata ->> 'ingresoAlimento'::text AS metadata_ingreso_alimento,
    metadata ->> 'traslado'::text AS metadata_traslado,
    metadata ->> 'documento'::text AS metadata_documento,
    trim_scale(consumo_kg_hembras::numeric) AS consumo_kg_hembras,
    trim_scale(consumo_kg_machos::numeric) AS consumo_kg_machos,
    trim_scale(consumo_real_dia_kg) AS consumo_real_dia_kg,
    trim_scale(consumo_acumulado_kg) AS consumo_acumulado_kg,
    trim_scale(consumo_bodega_kg) AS consumo_bodega_kg,
    trim_scale(consumo_agua_diario::numeric) AS consumo_agua_diario,
    trim_scale(
        CASE
            WHEN saldo_aves_inicio_dia > 0::numeric THEN round(100.0 * total_mort_sel_dia::numeric / saldo_aves_inicio_dia, 2)
            WHEN total_mort_sel_dia > 0 THEN 100::numeric
            ELSE NULL::numeric
        END) AS pct_perdidas_dia,
    trim_scale(peso_prom_hembras::numeric) AS peso_prom_hembras,
    trim_scale(peso_prom_machos::numeric) AS peso_prom_machos,
    observaciones,
    metadata,
    items_adicionales
   FROM final f;
");

            migrationBuilder.Sql(@"
DROP VIEW IF EXISTS public.vw_indicadores_diarios_engorde;
CREATE OR REPLACE VIEW public.vw_indicadores_diarios_engorde AS
 WITH lote_filtrado AS (
         SELECT l.lote_ave_engorde_id,
            l.company_id,
            COALESCE(co.name, l.empresa_nombre) AS empresa_nombre,
            l.lote_nombre,
            l.granja_id,
            fa.name AS granja_nombre,
            l.galpon_id,
            gp.galpon_nombre,
            l.nucleo_id,
            nu.nucleo_nombre,
            l.fecha_encaset,
            TRIM(BOTH FROM l.raza) AS raza,
            l.ano_tabla_genetica,
            l.peso_mixto,
            l.peso_inicial_h,
            l.peso_inicial_m,
                CASE
                    WHEN COALESCE(l.aves_encasetadas, 0) > 0 THEN l.aves_encasetadas::bigint
                    WHEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0)) > 0 THEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0))::bigint
                    ELSE 0::bigint
                END AS aves_iniciales
           FROM lote_ave_engorde l
             LEFT JOIN companies co ON co.id = l.company_id
             LEFT JOIN farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
             LEFT JOIN nucleos nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
             LEFT JOIN galpones gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
          WHERE l.deleted_at IS NULL
        ), seg_agregado AS (
         SELECT s.lote_ave_engorde_id,
            s.fecha::date AS fecha_registro,
            sum(COALESCE(s.mortalidad_hembras, 0)) AS sum_mort_h,
            sum(COALESCE(s.mortalidad_machos, 0)) AS sum_mort_m,
            sum(COALESCE(s.sel_h, 0)) AS sum_sel_h,
            sum(COALESCE(s.sel_m, 0)) AS sum_sel_m,
            sum(COALESCE(s.error_sexaje_hembras, 0)) AS sum_err_h,
            sum(COALESCE(s.error_sexaje_machos, 0)) AS sum_err_m,
            sum(COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric)) AS consumo_kg_dia
           FROM seguimiento_diario_aves_engorde s
             JOIN lote_filtrado l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
          GROUP BY s.lote_ave_engorde_id, (s.fecha::date)
        ), seg_peso_ultimo AS (
         SELECT DISTINCT ON (s.lote_ave_engorde_id, (s.fecha::date)) s.lote_ave_engorde_id,
            s.fecha::date AS fecha_registro,
            s.peso_prom_hembras AS peso_h,
            s.peso_prom_machos AS peso_m
           FROM seguimiento_diario_aves_engorde s
             JOIN lote_filtrado l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
          ORDER BY s.lote_ave_engorde_id, (s.fecha::date), s.id DESC
        ), dia_base AS (
         SELECT l.company_id,
            l.empresa_nombre,
            l.lote_ave_engorde_id,
            l.lote_nombre,
            l.granja_id,
            l.granja_nombre,
            l.galpon_id,
            l.galpon_nombre,
            l.nucleo_id,
            l.nucleo_nombre,
            l.raza,
            l.ano_tabla_genetica,
            a.fecha_registro,
            GREATEST(0, a.fecha_registro - l.fecha_encaset::date) AS dia_vida,
            l.aves_iniciales,
                CASE
                    WHEN l.peso_mixto IS NOT NULL AND l.peso_mixto > 0::double precision THEN l.peso_mixto::numeric
                    WHEN COALESCE(l.peso_inicial_h, 0::double precision) > 0::double precision AND COALESCE(l.peso_inicial_m, 0::double precision) > 0::double precision THEN ((l.peso_inicial_h + l.peso_inicial_m) / 2.0::double precision)::numeric
                    ELSE COALESCE(l.peso_inicial_h, l.peso_inicial_m, 0::double precision)::numeric
                END AS peso_inicial_mixto_g,
            a.sum_mort_h + a.sum_mort_m + a.sum_sel_h + a.sum_sel_m + a.sum_err_h + a.sum_err_m AS perdidas_dia,
            a.sum_mort_h + a.sum_mort_m + a.sum_sel_h + a.sum_sel_m AS mort_sel_dia,
            a.consumo_kg_dia,
                CASE
                    WHEN COALESCE(p.peso_h, 0::double precision) > 0::double precision AND COALESCE(p.peso_m, 0::double precision) > 0::double precision THEN ((p.peso_h + p.peso_m) / 2.0::double precision)::numeric
                    ELSE COALESCE(NULLIF(p.peso_h, 0::double precision), NULLIF(p.peso_m, 0::double precision), 0::double precision)::numeric
                END AS peso_mixto_dia_g
           FROM seg_agregado a
             JOIN lote_filtrado l ON l.lote_ave_engorde_id = a.lote_ave_engorde_id
             JOIN seg_peso_ultimo p ON p.lote_ave_engorde_id = a.lote_ave_engorde_id AND p.fecha_registro = a.fecha_registro
          WHERE l.fecha_encaset IS NOT NULL
        ), con_aves AS (
         SELECT d.company_id,
            d.empresa_nombre,
            d.lote_ave_engorde_id,
            d.lote_nombre,
            d.granja_id,
            d.granja_nombre,
            d.galpon_id,
            d.galpon_nombre,
            d.nucleo_id,
            d.nucleo_nombre,
            d.raza,
            d.ano_tabla_genetica,
            d.fecha_registro,
            d.dia_vida,
            d.aves_iniciales,
            d.peso_inicial_mixto_g,
            d.perdidas_dia,
            d.mort_sel_dia,
            d.consumo_kg_dia,
            d.peso_mixto_dia_g,
            COALESCE(sum(d.perdidas_dia) OVER (PARTITION BY d.lote_ave_engorde_id ORDER BY d.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0::numeric)::bigint AS perdidas_acum_prev,
            sum(d.perdidas_dia) OVER (PARTITION BY d.lote_ave_engorde_id ORDER BY d.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)::bigint AS perdidas_acum_total
           FROM dia_base d
        ), con_aves2 AS (
         SELECT c.company_id,
            c.empresa_nombre,
            c.lote_ave_engorde_id,
            c.lote_nombre,
            c.granja_id,
            c.granja_nombre,
            c.galpon_id,
            c.galpon_nombre,
            c.nucleo_id,
            c.nucleo_nombre,
            c.raza,
            c.ano_tabla_genetica,
            c.fecha_registro,
            c.dia_vida,
            c.aves_iniciales,
            c.peso_inicial_mixto_g,
            c.perdidas_dia,
            c.mort_sel_dia,
            c.consumo_kg_dia,
            c.peso_mixto_dia_g,
            c.perdidas_acum_prev,
            c.perdidas_acum_total,
            GREATEST(0::bigint, c.aves_iniciales - c.perdidas_acum_prev) AS aves_inicio_dia,
            GREATEST(0::bigint, c.aves_iniciales - c.perdidas_acum_total) AS aves_fin_dia
           FROM con_aves c
        ), con_guia AS (
         SELECT c.company_id,
            c.empresa_nombre,
            c.lote_ave_engorde_id,
            c.lote_nombre,
            c.granja_id,
            c.granja_nombre,
            c.galpon_id,
            c.galpon_nombre,
            c.nucleo_id,
            c.nucleo_nombre,
            c.raza,
            c.ano_tabla_genetica,
            c.fecha_registro,
            c.dia_vida,
            c.aves_iniciales,
            c.peso_inicial_mixto_g,
            c.perdidas_dia,
            c.mort_sel_dia,
            c.consumo_kg_dia,
            c.peso_mixto_dia_g,
            c.perdidas_acum_prev,
            c.perdidas_acum_total,
            c.aves_inicio_dia,
            c.aves_fin_dia,
            gh.id AS guia_genetica_ecuador_header_id,
            gd.peso_corporal_g::numeric AS peso_tabla_g,
            gd.ganancia_diaria_g::numeric AS ganancia_diaria_tabla_g,
            gd.cantidad_alimento_diario_g::numeric AS consumo_diario_tabla_g,
            gd.alimento_acumulado_g::numeric AS alimento_acum_tabla_g,
            gd.ca::numeric AS ca_tabla,
            gd.mortalidad_seleccion_diaria::numeric AS mort_sel_tabla_pct
           FROM con_aves2 c
             LEFT JOIN guia_genetica_ecuador_header gh ON gh.company_id = c.company_id AND TRIM(BOTH FROM lower(gh.raza::text)) = TRIM(BOTH FROM lower(COALESCE(c.raza, ''::text))) AND gh.anio_guia = c.ano_tabla_genetica AND c.ano_tabla_genetica IS NOT NULL AND TRIM(BOTH FROM COALESCE(c.raza, ''::text)) <> ''::text AND gh.estado::text = 'active'::text AND gh.deleted_at IS NULL
             LEFT JOIN LATERAL ( SELECT d.id,
                    d.guia_genetica_ecuador_header_id,
                    d.sexo,
                    d.dia,
                    d.peso_corporal_g,
                    d.ganancia_diaria_g,
                    d.promedio_ganancia_diaria_g,
                    d.cantidad_alimento_diario_g,
                    d.alimento_acumulado_g,
                    d.ca,
                    d.mortalidad_seleccion_diaria,
                    d.company_id,
                    d.created_by_user_id,
                    d.created_at,
                    d.updated_by_user_id,
                    d.updated_at,
                    d.deleted_at
                   FROM guia_genetica_ecuador_detalle d
                  WHERE d.guia_genetica_ecuador_header_id = gh.id AND lower(TRIM(BOTH FROM d.sexo)) = 'mixto'::text AND d.deleted_at IS NULL AND d.dia <= c.dia_vida
                  ORDER BY d.dia DESC
                 LIMIT 1) gd ON true
        ), con_calc AS (
         SELECT g.company_id,
            g.empresa_nombre,
            g.lote_ave_engorde_id,
            g.lote_nombre,
            g.granja_id,
            g.granja_nombre,
            g.galpon_id,
            g.galpon_nombre,
            g.nucleo_id,
            g.nucleo_nombre,
            g.raza,
            g.ano_tabla_genetica,
            g.fecha_registro,
            g.dia_vida,
            g.aves_iniciales,
            g.peso_inicial_mixto_g,
            g.perdidas_dia,
            g.mort_sel_dia,
            g.consumo_kg_dia,
            g.peso_mixto_dia_g,
            g.perdidas_acum_prev,
            g.perdidas_acum_total,
            g.aves_inicio_dia,
            g.aves_fin_dia,
            g.guia_genetica_ecuador_header_id,
            g.peso_tabla_g,
            g.ganancia_diaria_tabla_g,
            g.consumo_diario_tabla_g,
            g.alimento_acum_tabla_g,
            g.ca_tabla,
            g.mort_sel_tabla_pct,
                CASE
                    WHEN g.aves_inicio_dia > 0 THEN g.consumo_kg_dia * 1000.0 / g.aves_inicio_dia::numeric
                    ELSE 0::numeric
                END AS consumo_diario_real_g,
            lag(g.peso_mixto_dia_g) OVER (PARTITION BY g.lote_ave_engorde_id ORDER BY g.fecha_registro) AS peso_mixto_dia_prev,
            sum(
                CASE
                    WHEN g.aves_inicio_dia > 0 THEN g.consumo_kg_dia * 1000.0 / g.aves_inicio_dia::numeric
                    ELSE 0::numeric
                END) OVER (PARTITION BY g.lote_ave_engorde_id ORDER BY g.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS alimento_acum_real_g
           FROM con_guia g
        ), final AS (
         SELECT c.company_id,
            c.empresa_nombre,
            c.lote_ave_engorde_id,
            c.lote_nombre,
            c.granja_id,
            c.granja_nombre,
            c.galpon_id,
            c.galpon_nombre,
            c.nucleo_id,
            c.nucleo_nombre,
            c.raza,
            c.ano_tabla_genetica,
            c.guia_genetica_ecuador_header_id,
            c.fecha_registro,
            c.dia_vida,
            c.aves_iniciales,
            c.aves_inicio_dia,
            c.aves_fin_dia,
            c.peso_inicial_mixto_g,
            c.peso_mixto_dia_g AS peso_real_g,
            c.peso_tabla_g,
                CASE
                    WHEN c.peso_mixto_dia_g > 0::numeric AND c.peso_mixto_dia_prev IS NOT NULL THEN c.peso_mixto_dia_g - c.peso_mixto_dia_prev
                    WHEN c.peso_mixto_dia_g > 0::numeric AND c.peso_mixto_dia_prev IS NULL THEN c.peso_mixto_dia_g - c.peso_inicial_mixto_g
                    ELSE NULL::numeric
                END AS ganancia_diaria_real_g,
            c.ganancia_diaria_tabla_g,
            c.consumo_diario_real_g,
            c.consumo_diario_tabla_g,
            c.alimento_acum_real_g,
            c.alimento_acum_tabla_g,
                CASE
                    WHEN c.peso_mixto_dia_g > 0::numeric AND c.alimento_acum_real_g > 0::numeric THEN c.alimento_acum_real_g / NULLIF(c.peso_mixto_dia_g, 0::numeric)
                    ELSE NULL::numeric
                END AS ca_real,
            c.ca_tabla,
                CASE
                    WHEN c.aves_inicio_dia > 0 THEN c.mort_sel_dia::numeric * 100.0 / c.aves_inicio_dia::numeric
                    ELSE 0::numeric
                END AS mort_sel_real_pct,
            c.mort_sel_tabla_pct,
                CASE
                    WHEN c.peso_tabla_g > 0::numeric AND c.peso_mixto_dia_g > 0::numeric THEN (c.peso_mixto_dia_g - c.peso_tabla_g) / NULLIF(c.peso_tabla_g, 0::numeric) * 100.0
                    ELSE 0::numeric
                END AS dif_peso_vs_tabla_pct,
                CASE
                    WHEN c.aves_iniciales > 0 THEN c.perdidas_acum_total::numeric * 100.0 / c.aves_iniciales::numeric
                    ELSE 0::numeric
                END AS mort_acum_pct
           FROM con_calc c
        )
 SELECT company_id,
    empresa_nombre,
    lote_ave_engorde_id,
    lote_nombre,
    granja_id,
    granja_nombre,
    galpon_id,
    galpon_nombre,
    nucleo_id,
    nucleo_nombre,
    raza,
    ano_tabla_genetica,
    guia_genetica_ecuador_header_id,
    to_char(fecha_registro::timestamp with time zone, 'YYYY-MM-DD'::text) AS fecha_ymd,
    fecha_registro,
    dia_vida,
    aves_iniciales,
    aves_inicio_dia,
    aves_fin_dia,
    trim_scale(peso_inicial_mixto_g) AS peso_inicial_mixto_g,
    trim_scale(peso_real_g) AS peso_real_g,
    trim_scale(peso_tabla_g) AS peso_tabla_g,
    trim_scale(ganancia_diaria_real_g) AS ganancia_diaria_real_g,
    trim_scale(ganancia_diaria_tabla_g) AS ganancia_diaria_tabla_g,
    trim_scale(consumo_diario_real_g) AS consumo_diario_real_g,
    trim_scale(consumo_diario_tabla_g) AS consumo_diario_tabla_g,
    trim_scale(alimento_acum_real_g) AS alimento_acum_real_g,
    trim_scale(alimento_acum_tabla_g) AS alimento_acum_tabla_g,
    trim_scale(ca_real) AS ca_real,
    trim_scale(ca_tabla) AS ca_tabla,
    trim_scale(mort_sel_real_pct) AS mort_sel_real_pct,
    trim_scale(mort_sel_tabla_pct) AS mort_sel_tabla_pct,
    trim_scale(dif_peso_vs_tabla_pct) AS dif_peso_vs_tabla_pct,
    trim_scale(mort_acum_pct) AS mort_acum_pct
   FROM final f;
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'repropesa01') THEN
    ALTER VIEW public.vw_seguimiento_pollo_engorde OWNER TO repropesa01;
    ALTER VIEW public.vw_liquidacion_ecuador_pollo_engorde OWNER TO repropesa01;
    ALTER VIEW public.vw_indicadores_diarios_engorde OWNER TO repropesa01;
  END IF;
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'usrDWH') THEN
    GRANT SELECT ON public.vw_seguimiento_pollo_engorde TO ""usrDWH"";
    GRANT SELECT ON public.vw_liquidacion_ecuador_pollo_engorde TO ""usrDWH"";
    GRANT SELECT ON public.vw_indicadores_diarios_engorde TO ""usrDWH"";
  END IF;
END $$;
");
        }
    }
}
