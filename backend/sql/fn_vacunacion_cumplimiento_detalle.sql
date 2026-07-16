-- ============================================================================
-- fn_vacunacion_cumplimiento_detalle — Reportería ítem a ítem del módulo Vacunación:
-- una fila por vacuna programada con su estado real, fechas, desviación y responsables.
--
-- Complementa a fn_vacunacion_cumplimiento_lote (agregado por lote): mismos 9 parámetros
-- y misma base/filtros, pero devuelve el detalle para la vista "Detalle por vacuna" y el
-- Excel multi-hoja del reporte.
--
-- ⚠ fecha_objetivo_efectiva usa la MISMA fórmula que fn_vacunacion_cumplimiento_lote y
--   VacunacionCalculos.CalcularFranja (Semana: encaset+(valor-1)*7 · Dia: encaset+valor ·
--   Fecha: fecha_objetivo). No divergir.
--
-- Columnas snake_case (gotcha SqlQueryRaw + EFCore.NamingConventions), sin dígitos.
-- Sincronizada con la migración AddFnVacunacionConsultas.
-- ============================================================================
DROP FUNCTION IF EXISTS public.fn_vacunacion_cumplimiento_detalle(INT, INT, INT[], TEXT, TEXT, INT[], TEXT, DATE, DATE);

CREATE OR REPLACE FUNCTION public.fn_vacunacion_cumplimiento_detalle(
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
    item_id                 INT,
    granja_id               INT,
    granja_nombre           TEXT,
    lote_id                 INT,
    lote_nombre             TEXT,
    linea_productiva        TEXT,
    nucleo_id               TEXT,
    galpon_id               TEXT,
    vacuna_nombre           TEXT,
    unidad_objetivo         TEXT,
    valor_objetivo          INT,
    fecha_objetivo_efectiva DATE,
    fecha_inicio_franja     DATE,
    fecha_fin_franja        DATE,
    estado                  TEXT,
    fecha_aplicacion        DATE,
    dias_desviacion         INT,
    incumplido              BOOLEAN,
    motivo                  TEXT,
    aplicado_por            TEXT,
    registrado_por          TEXT,
    notas                   TEXT
)
LANGUAGE sql
STABLE
AS $$
WITH base AS (
    SELECT
        ci.id AS item_id,
        COALESCE(ci.lote_postura_levante_id, ci.lote_postura_produccion_id, ci.lote_ave_engorde_id) AS lote_id,
        ci.linea_productiva,
        ci.granja_id,
        f.name AS granja_nombre,
        ci.nucleo_id,
        ci.galpon_id,
        COALESCE(lpl.lote_nombre, lpp.lote_nombre, lae.lote_nombre) AS lote_nombre,
        COALESCE(ii.nombre, '') AS vacuna_nombre,
        ci.unidad_objetivo,
        ci.valor_objetivo,
        ci.rango_dias_antes,
        ci.rango_dias_despues,
        ci.notas,
        COALESCE(ra.estado, 'Pendiente') AS estado,
        ra.fecha_aplicacion::date AS fecha_aplicacion,
        ra.dias_desviacion,
        COALESCE(ra.incumplido, false) AS incumplido,
        ra.motivo_descripcion AS motivo,
        ra.aplicado_por_nombre_libre,
        ra.aplicado_por_user_id,
        ra.usuario_registra_id,
        (CASE ci.unidad_objetivo
            WHEN 'Semana' THEN COALESCE(lpl.fecha_encaset, lpp.fecha_encaset, lae.fecha_encaset)::date + ((ci.valor_objetivo - 1) * 7)
            WHEN 'Dia'    THEN COALESCE(lpl.fecha_encaset, lpp.fecha_encaset, lae.fecha_encaset)::date + ci.valor_objetivo
            WHEN 'Fecha'  THEN ci.fecha_objetivo::date
         END) AS fecha_objetivo_efectiva
    FROM public.vacunacion_cronograma_item ci
    LEFT JOIN public.vacunacion_registro_aplicacion ra ON ra.vacunacion_cronograma_item_id = ci.id
    LEFT JOIN public.farms f ON f.id = ci.granja_id
    LEFT JOIN public.item_inventario_ecuador ii ON ii.id = ci.item_inventario_id
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
    fl.item_id,
    fl.granja_id,
    fl.granja_nombre,
    fl.lote_id,
    fl.lote_nombre,
    fl.linea_productiva,
    fl.nucleo_id,
    fl.galpon_id,
    fl.vacuna_nombre,
    fl.unidad_objetivo,
    fl.valor_objetivo,
    fl.fecha_objetivo_efectiva,
    (fl.fecha_objetivo_efectiva - fl.rango_dias_antes)   AS fecha_inicio_franja,
    (fl.fecha_objetivo_efectiva + fl.rango_dias_despues) AS fecha_fin_franja,
    fl.estado,
    fl.fecha_aplicacion,
    fl.dias_desviacion,
    fl.incumplido,
    fl.motivo,
    COALESCE(fl.aplicado_por_nombre_libre, ua.nombre)    AS aplicado_por,
    ur.nombre                                            AS registrado_por,
    fl.notas
FROM filtrado fl
LEFT JOIN LATERAL (
    SELECT NULLIF(btrim(COALESCE(u.first_name, '') || ' ' || COALESCE(u.sur_name, '')), '') AS nombre
    FROM public.users u
    WHERE fl.aplicado_por_user_id IS NOT NULL AND u.cedula = fl.aplicado_por_user_id::text
    LIMIT 1
) ua ON true
LEFT JOIN LATERAL (
    SELECT NULLIF(btrim(COALESCE(u.first_name, '') || ' ' || COALESCE(u.sur_name, '')), '') AS nombre
    FROM public.users u
    WHERE fl.usuario_registra_id IS NOT NULL AND u.cedula = fl.usuario_registra_id::text
    LIMIT 1
) ur ON true
ORDER BY fl.granja_nombre NULLS LAST, fl.lote_nombre NULLS LAST,
         fl.fecha_objetivo_efectiva NULLS LAST, fl.item_id;
$$;
