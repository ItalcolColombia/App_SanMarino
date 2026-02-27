-- =============================================================================
-- MIGRACIÓN: Lote 13 - Levante a LPL + seguimiento_lote_levante → seguimiento_diario
-- Orden:
--   1. Crear lote_postura_levante para lote 13 si no existe.
--   2. Copiar registros de seguimiento_lote_levante (lote 13) a seguimiento_diario.
--   3. Actualizar LPL con aves_h_actual y aves_m_actual calculados desde seguimiento.
-- Nota: Ejecutar antes migracion_00_calculo_aves_lote_13.sql para validar los cálculos.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- PASO 1: Crear LPL para lote 13 si no existe (idempotente)
-- -----------------------------------------------------------------------------
INSERT INTO public.lote_postura_levante (
    lote_nombre, granja_id, nucleo_id, galpon_id, regional, fecha_encaset,
    hembras_l, machos_l, peso_inicial_h, peso_inicial_m, unif_h, unif_m,
    mort_caja_h, mort_caja_m, raza, ano_tabla_genetica, linea, tipo_linea,
    codigo_guia_genetica, linea_genetica_id, tecnico, mixtas, peso_mixto,
    aves_encasetadas, edad_inicial, lote_erp, estado_traslado,
    pais_id, pais_nombre, empresa_nombre,
    lote_id, lote_padre_id,
    aves_h_inicial, aves_m_inicial, aves_h_actual, aves_m_actual,
    empresa_id, usuario_id, estado, etapa, edad, estado_cierre,
    company_id, created_by_user_id, created_at, updated_by_user_id, updated_at, deleted_at
)
SELECT
    l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.regional, l.fecha_encaset,
    l.hembras_l, l.machos_l, l.peso_inicial_h, l.peso_inicial_m, l.unif_h, l.unif_m,
    l.mort_caja_h, l.mort_caja_m, l.raza, l.ano_tabla_genetica, l.linea, l.tipo_linea,
    l.codigo_guia_genetica, l.linea_genetica_id, l.tecnico, l.mixtas, l.peso_mixto,
    l.aves_encasetadas, l.edad_inicial, l.lote_erp, l.estado_traslado,
    l.pais_id, l.pais_nombre, l.empresa_nombre,
    l.lote_id, l.lote_padre_id,
    COALESCE(l.hembras_l, 0), COALESCE(l.machos_l, 0),
    COALESCE(l.hembras_l, 0), COALESCE(l.machos_l, 0),
    l.company_id, COALESCE(l.updated_by_user_id, l.created_by_user_id),
    COALESCE(l.fase, 'Levante'), COALESCE(l.fase, 'Levante'),
    COALESCE(l.edad_inicial, CASE WHEN l.fecha_encaset IS NOT NULL
        THEN GREATEST(0, (CURRENT_DATE - (l.fecha_encaset AT TIME ZONE 'utc')::date) / 7)
        ELSE 0 END),
    'Abierto',
    l.company_id, l.created_by_user_id, COALESCE(l.created_at, NOW() AT TIME ZONE 'utc'),
    l.updated_by_user_id, l.updated_at, l.deleted_at
FROM public.lotes l
WHERE l.lote_id = 13
  AND l.deleted_at IS NULL
  AND NOT EXISTS (
    SELECT 1 FROM public.lote_postura_levante lpl
    WHERE lpl.lote_id = 13 AND lpl.deleted_at IS NULL
  );

-- -----------------------------------------------------------------------------
-- PASO 2: Copiar seguimiento_lote_levante (lote 13) → seguimiento_diario (idempotente)
-- Evita duplicados por (tipo_seguimiento, lote_id, fecha). Usar lote_postura_levante_id.
-- Nota: Si tu BD usa nombres de columna distintos en seguimiento_lote_levante
--       (p. ej. created en vez de created_at), ajusta el SELECT.
-- -----------------------------------------------------------------------------
INSERT INTO public.seguimiento_diario (
    tipo_seguimiento, lote_id, lote_postura_levante_id, lote_postura_produccion_id,
    reproductora_id, fecha,
    mortalidad_hembras, mortalidad_machos, sel_h, sel_m,
    error_sexaje_hembras, error_sexaje_machos,
    consumo_kg_hembras, consumo_kg_machos, tipo_alimento, observaciones, ciclo,
    peso_prom_hembras, peso_prom_machos, uniformidad_hembras, uniformidad_machos,
    cv_hembras, cv_machos,
    consumo_agua_diario, consumo_agua_ph, consumo_agua_orp, consumo_agua_temperatura,
    metadata, items_adicionales,
    kcal_al_h, prot_al_h, kcal_ave_h, prot_ave_h,
    created_by_user_id, created_at, updated_at
)
SELECT
    'levante',
    sl.lote_id::text,
    lpl.lote_postura_levante_id,
    NULL,
    NULL,
    sl.fecha_registro,
    COALESCE(sl.mortalidad_hembras, 0),
    COALESCE(sl.mortalidad_machos, 0),
    COALESCE(sl.sel_h, 0),
    COALESCE(sl.sel_m, 0),
    COALESCE(sl.error_sexaje_hembras, 0),
    COALESCE(sl.error_sexaje_machos, 0),
    sl.consumo_kg_hembras,
    sl.consumo_kg_machos,
    sl.tipo_alimento,
    sl.observaciones,
    COALESCE(sl.ciclo, 'Normal'),
    sl.peso_prom_h,
    sl.peso_prom_m,
    sl.uniformidad_h,
    sl.uniformidad_m,
    sl.cv_h,
    sl.cv_m,
    sl.consumo_agua_diario,
    sl.consumo_agua_ph,
    sl.consumo_agua_orp,
    sl.consumo_agua_temperatura,
    sl.metadata,
    sl.items_adicionales,
    sl.kcal_al_h,
    sl.prot_al_h,
    sl.kcal_ave_h,
    sl.prot_ave_h,
    -- Auditoría: usar fecha_registro como created_at (evita depender de created_at/updated_at en legacy)
    NULL,
    sl.fecha_registro::timestamptz,
    NULL
FROM public.seguimiento_lote_levante sl
INNER JOIN public.lote_postura_levante lpl ON lpl.lote_id = sl.lote_id AND lpl.deleted_at IS NULL
WHERE sl.lote_id = 13
  AND NOT EXISTS (
    SELECT 1 FROM public.seguimiento_diario sd
    WHERE sd.tipo_seguimiento = 'levante'
      AND sd.lote_postura_levante_id = lpl.lote_postura_levante_id
      AND sd.fecha = sl.fecha_registro
  );

-- Si seguimiento_lote_levante no tiene created_by_user_id/created_at, quita esas columnas del INSERT
-- y usa solo: created_at = sl.fecha_registro, created_by_user_id = NULL.
-- Versión alternativa sin created_by_user_id/created_at en origen (descomenta si falla el INSERT):
/*
INSERT INTO public.seguimiento_diario (
    tipo_seguimiento, lote_id, lote_postura_levante_id, lote_postura_produccion_id,
    reproductora_id, fecha,
    mortalidad_hembras, mortalidad_machos, sel_h, sel_m,
    error_sexaje_hembras, error_sexaje_machos,
    consumo_kg_hembras, consumo_kg_machos, tipo_alimento, observaciones, ciclo,
    peso_prom_hembras, peso_prom_machos, uniformidad_hembras, uniformidad_machos,
    cv_hembras, cv_machos,
    consumo_agua_diario, consumo_agua_ph, consumo_agua_orp, consumo_agua_temperatura,
    metadata, items_adicionales,
    kcal_al_h, prot_al_h, kcal_ave_h, prot_ave_h,
    created_by_user_id, created_at, updated_at
)
SELECT
    'levante', sl.lote_id::text, lpl.lote_postura_levante_id, NULL, NULL, sl.fecha_registro,
    COALESCE(sl.mortalidad_hembras, 0), COALESCE(sl.mortalidad_machos, 0),
    COALESCE(sl.sel_h, 0), COALESCE(sl.sel_m, 0),
    COALESCE(sl.error_sexaje_hembras, 0), COALESCE(sl.error_sexaje_machos, 0),
    sl.consumo_kg_hembras, sl.consumo_kg_machos, sl.tipo_alimento, sl.observaciones,
    COALESCE(sl.ciclo, 'Normal'),
    sl.peso_prom_h, sl.peso_prom_m, sl.uniformidad_h, sl.uniformidad_m, sl.cv_h, sl.cv_m,
    sl.consumo_agua_diario, sl.consumo_agua_ph, sl.consumo_agua_orp, sl.consumo_agua_temperatura,
    sl.metadata, sl.items_adicionales,
    sl.kcal_al_h, sl.prot_al_h, sl.kcal_ave_h, sl.prot_ave_h,
    NULL, sl.fecha_registro, NULL
FROM public.seguimiento_lote_levante sl
INNER JOIN public.lote_postura_levante lpl ON lpl.lote_id = sl.lote_id AND lpl.deleted_at IS NULL
WHERE sl.lote_id = 13
  AND NOT EXISTS (
    SELECT 1 FROM public.seguimiento_diario sd
    WHERE sd.tipo_seguimiento = 'levante'
      AND sd.lote_postura_levante_id = lpl.lote_postura_levante_id
      AND sd.fecha = sl.fecha_registro
  );
*/

-- -----------------------------------------------------------------------------
-- PASO 3: Actualizar LPL con aves_h_actual y aves_m_actual (desde seguimiento_diario)
-- Fórmula: inicial - SUM(mortalidad) - SUM(sel) - SUM(error_sexaje)
-- -----------------------------------------------------------------------------
UPDATE public.lote_postura_levante lpl
SET
    aves_h_actual = GREATEST(0, sub.aves_h_actual),
    aves_m_actual = GREATEST(0, sub.aves_m_actual),
    updated_at = (NOW() AT TIME ZONE 'utc')
FROM (
    SELECT
        lpl2.lote_postura_levante_id,
        (COALESCE(lpl2.aves_h_inicial, lpl2.hembras_l, 0)
         - COALESCE(SUM(sd.mortalidad_hembras), 0)
         - COALESCE(SUM(sd.sel_h), 0)
         - COALESCE(SUM(sd.error_sexaje_hembras), 0)) AS aves_h_actual,
        (COALESCE(lpl2.aves_m_inicial, lpl2.machos_l, 0)
         - COALESCE(SUM(sd.mortalidad_machos), 0)
         - COALESCE(SUM(sd.sel_m), 0)
         - COALESCE(SUM(sd.error_sexaje_machos), 0)) AS aves_m_actual
    FROM public.lote_postura_levante lpl2
    LEFT JOIN public.seguimiento_diario sd
      ON sd.tipo_seguimiento = 'levante'
     AND sd.lote_postura_levante_id = lpl2.lote_postura_levante_id
    WHERE lpl2.lote_id = 13 AND lpl2.deleted_at IS NULL
    GROUP BY lpl2.lote_postura_levante_id,
             lpl2.aves_h_inicial, lpl2.hembras_l,
             lpl2.aves_m_inicial, lpl2.machos_l
) sub
WHERE lpl.lote_postura_levante_id = sub.lote_postura_levante_id;

-- Resumen
SELECT 'Lote 13 migrado: LPL + seguimiento_diario' AS paso;
SELECT lpl.lote_postura_levante_id, lpl.lote_nombre, lpl.aves_h_inicial, lpl.aves_m_inicial,
       lpl.aves_h_actual, lpl.aves_m_actual, lpl.estado_cierre
FROM public.lote_postura_levante lpl
WHERE lpl.lote_id = 13 AND lpl.deleted_at IS NULL;

SELECT 'Registros en seguimiento_diario (levante, lote 13)' AS paso, COUNT(*) AS total
FROM public.seguimiento_diario sd
WHERE sd.tipo_seguimiento = 'levante'
  AND sd.lote_postura_levante_id = (SELECT lote_postura_levante_id FROM public.lote_postura_levante WHERE lote_id = 13 AND deleted_at IS NULL LIMIT 1);
