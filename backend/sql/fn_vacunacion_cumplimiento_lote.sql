-- ============================================================================
-- fn_vacunacion_cumplimiento_lote — Módulo Vacunación: cumplimiento por lote
-- ----------------------------------------------------------------------------
-- Una fila por lote (Levante/Producción/Engorde) con % a tiempo, % tardío
-- (desglosado bajo/sobre el umbral configurable de vacunacion_configuracion,
-- default 14 días), % no aplicado y promedio de días de atraso. Sirve tanto
-- para el reporte de un solo lote como para el comparativo (pasar varios
-- p_lote_ids o varios p_granja_ids devuelve una fila por lote, comparables
-- lado a lado) — no hace falta una función aparte para el comparativo.
--
-- fecha_objetivo_efectiva replica en SQL el cálculo de VacunacionCalculos.
-- CalcularFranja (Application/Calculos, C#) para poder filtrar por
-- p_fecha_desde/p_fecha_hasta: Semana = fecha_encaset + (valor-1)*7;
-- Dia = fecha_encaset + valor; Fecha = fecha_objetivo. Si cambia esa fórmula
-- en VacunacionCalculos, actualizar aquí también.
--
-- Filtros (todos opcionales salvo company):
--   p_company_id       : empresa (obligatorio)
--   p_pais_id          : NULL = todos los países de la empresa
--   p_granja_ids       : NULL o {} = todas las granjas asignadas al caller
--   p_nucleo_id        : NULL = todos
--   p_galpon_id        : NULL = todos
--   p_lote_ids         : NULL = todos los que pasen el resto de filtros
--   p_linea_productiva : NULL = todas ('Levante' | 'Produccion' | 'Engorde')
--   p_fecha_desde/hasta: NULL = sin tope (overlap contra fecha_objetivo_efectiva)
--
-- v1 (2026-07-14).
-- ============================================================================

DROP FUNCTION IF EXISTS public.fn_vacunacion_cumplimiento_lote(INT, INT, INT[], TEXT, TEXT, INT[], TEXT, DATE, DATE);

CREATE OR REPLACE FUNCTION public.fn_vacunacion_cumplimiento_lote(
    p_company_id       INT,
    p_pais_id          INT     DEFAULT NULL,
    p_granja_ids       INT[]   DEFAULT NULL,
    p_nucleo_id        TEXT    DEFAULT NULL,
    p_galpon_id        TEXT    DEFAULT NULL,
    p_lote_ids         INT[]   DEFAULT NULL,
    p_linea_productiva TEXT    DEFAULT NULL,
    p_fecha_desde      DATE    DEFAULT NULL,
    p_fecha_hasta      DATE    DEFAULT NULL
)
RETURNS TABLE (
    lote_id                     INT,
    lote_nombre                 TEXT,
    linea_productiva            TEXT,
    granja_id                   INT,
    granja_nombre                TEXT,
    total_programadas           INT,
    total_a_tiempo               INT,
    total_tardio_1_semana        INT,
    total_tardio_2_mas_semanas   INT,
    total_no_aplicado            INT,
    total_pendiente              INT,
    porcentaje_a_tiempo          NUMERIC,
    porcentaje_tardio            NUMERIC,
    porcentaje_no_aplicado       NUMERIC,
    promedio_dias_atraso         NUMERIC
)
LANGUAGE sql
STABLE
AS $$
WITH umbral AS (
    SELECT COALESCE(
        (SELECT vc.dias_umbral_incumplido
         FROM public.vacunacion_configuracion vc
         WHERE vc.company_id = p_company_id
           AND (p_pais_id IS NULL OR vc.pais_id = p_pais_id)
         ORDER BY vc.pais_id NULLS LAST
         LIMIT 1),
        14
    ) AS dias
),
base AS (
    SELECT
        ci.id AS item_id,
        COALESCE(ci.lote_postura_levante_id, ci.lote_postura_produccion_id, ci.lote_ave_engorde_id) AS lote_id,
        ci.linea_productiva,
        ci.granja_id,
        f.name AS granja_nombre,
        COALESCE(lpl.lote_nombre, lpp.lote_nombre, lae.lote_nombre) AS lote_nombre,
        COALESCE(ra.estado, 'Pendiente') AS estado,
        ra.dias_desviacion,
        ra.incumplido,
        (CASE ci.unidad_objetivo
            WHEN 'Semana' THEN COALESCE(lpl.fecha_encaset, lpp.fecha_encaset, lae.fecha_encaset)::date + ((ci.valor_objetivo - 1) * 7)
            WHEN 'Dia'    THEN COALESCE(lpl.fecha_encaset, lpp.fecha_encaset, lae.fecha_encaset)::date + ci.valor_objetivo
            WHEN 'Fecha'  THEN ci.fecha_objetivo
         END) AS fecha_objetivo_efectiva
    FROM public.vacunacion_cronograma_item ci
    LEFT JOIN public.vacunacion_registro_aplicacion ra ON ra.vacunacion_cronograma_item_id = ci.id
    LEFT JOIN public.farms f ON f.id = ci.granja_id
    LEFT JOIN public.lote_postura_levante lpl ON lpl.lote_postura_levante_id = ci.lote_postura_levante_id
    LEFT JOIN public.lote_postura_produccion lpp ON lpp.lote_postura_produccion_id = ci.lote_postura_produccion_id
    LEFT JOIN public.lote_ave_engorde lae ON lae.lote_ave_engorde_id = ci.lote_ave_engorde_id
    WHERE ci.company_id = p_company_id
      AND ci.activo = true
      AND (p_pais_id IS NULL OR ci.pais_id = p_pais_id)
      AND (p_granja_ids IS NULL OR ci.granja_id = ANY(p_granja_ids))
      AND (p_nucleo_id IS NULL OR ci.nucleo_id = p_nucleo_id)
      AND (p_galpon_id IS NULL OR ci.galpon_id = p_galpon_id)
      AND (p_linea_productiva IS NULL OR ci.linea_productiva = p_linea_productiva)
),
filtrado AS (
    SELECT b.*
    FROM base b
    WHERE (p_lote_ids IS NULL OR b.lote_id = ANY(p_lote_ids))
      AND (p_fecha_desde IS NULL OR b.fecha_objetivo_efectiva IS NULL OR b.fecha_objetivo_efectiva >= p_fecha_desde)
      AND (p_fecha_hasta IS NULL OR b.fecha_objetivo_efectiva IS NULL OR b.fecha_objetivo_efectiva <= p_fecha_hasta)
)
SELECT
    f.lote_id,
    MAX(f.lote_nombre)      AS lote_nombre,
    MAX(f.linea_productiva) AS linea_productiva,
    MAX(f.granja_id)        AS granja_id,
    MAX(f.granja_nombre)    AS granja_nombre,
    COUNT(*)::INT                                                                    AS total_programadas,
    COUNT(*) FILTER (WHERE f.estado = 'Aplicado')::INT                               AS total_a_tiempo,
    COUNT(*) FILTER (WHERE f.estado = 'AplicadoTardio' AND NOT COALESCE(f.incumplido, false))::INT AS total_tardio_1_semana,
    COUNT(*) FILTER (WHERE f.estado = 'AplicadoTardio' AND COALESCE(f.incumplido, false))::INT     AS total_tardio_2_mas_semanas,
    COUNT(*) FILTER (WHERE f.estado = 'NoAplicado')::INT                             AS total_no_aplicado,
    COUNT(*) FILTER (WHERE f.estado IN ('Pendiente', 'AplicadoAdelantado'))::INT      AS total_pendiente,
    ROUND(100.0 * COUNT(*) FILTER (WHERE f.estado = 'Aplicado') / NULLIF(COUNT(*), 0), 1)         AS porcentaje_a_tiempo,
    ROUND(100.0 * COUNT(*) FILTER (WHERE f.estado = 'AplicadoTardio') / NULLIF(COUNT(*), 0), 1)    AS porcentaje_tardio,
    ROUND(100.0 * COUNT(*) FILTER (WHERE f.estado = 'NoAplicado') / NULLIF(COUNT(*), 0), 1)        AS porcentaje_no_aplicado,
    ROUND(AVG(f.dias_desviacion) FILTER (WHERE f.estado = 'AplicadoTardio'), 1)                    AS promedio_dias_atraso
FROM filtrado f
GROUP BY f.lote_id
ORDER BY f.lote_id;
$$;
