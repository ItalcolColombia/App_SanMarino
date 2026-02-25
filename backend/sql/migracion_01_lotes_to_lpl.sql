-- =============================================================================
-- MIGRACIÓN 01: Crear lote_postura_levante desde lotes sin LPL
-- Para cada lote en lotes que NO tenga registro en lote_postura_levante (lote_id),
-- insertar uno. Idempotente.
-- =============================================================================

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
WHERE l.lote_id IS NOT NULL
  AND l.deleted_at IS NULL
  AND NOT EXISTS (
    SELECT 1 FROM public.lote_postura_levante lpl 
    WHERE lpl.lote_id = l.lote_id AND lpl.deleted_at IS NULL
  );

-- Resumen
SELECT 'Lotes migrados a LPL' AS paso, COUNT(*) AS registros
FROM public.lote_postura_levante lpl
WHERE lpl.lote_id IS NOT NULL;
