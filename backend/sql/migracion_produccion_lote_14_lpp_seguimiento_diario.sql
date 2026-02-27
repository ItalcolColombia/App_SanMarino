-- =============================================================================
-- MIGRACIÓN: Lote 14 - Producción (LPP-H, LPP-M) + produccion_diaria → seguimiento_diario
--
-- Precondición: Ejecutar antes la migración de levante para lote 14
--   (migracion_seguimiento_levante_to_diario_lote_14.sql) para tener LPL con
--   aves_h_actual y aves_m_actual.
--
-- Este script:
--   1. Crea LPP-H y LPP-M desde el LPL del lote 14 (aves actuales del levante).
--   2. Cierra el LPL (estado_cierre = 'Cerrado').
--   3. Copia registros de produccion_diaria (lote 14) a seguimiento_diario
--      (tipo=produccion) vinculados al LPP-H (evita duplicar totales).
--
-- Idempotente: si ya existen LPP para ese LPL, no los duplica; solo copia
--   produccion_diaria que aún no estén en seguimiento_diario.
--
-- Nota: Si produccion_diaria no tiene columnas consumo_agua_*, metadata,
--   observaciones_pesaje, uniformidad, coeficiente_variacion, peso_h, peso_m,
--   elimínalas del SELECT o usa NULL para esas columnas.
-- =============================================================================

DO $$
DECLARE
  p_lote_id INT := 14;  -- Lote 14
  r_lpl RECORD;
  aves_h INT;
  aves_m INT;
  base_nombre TEXT;
  uid INT;
  now_ts TIMESTAMPTZ;
  lpp_h_id INT;
  lpl_id INT;
BEGIN
  now_ts := (NOW() AT TIME ZONE 'utc');

  -- 1) Obtener LPL del lote (debe existir tras migración levante)
  SELECT lpl.lote_postura_levante_id, lpl.lote_nombre, lpl.granja_id, lpl.nucleo_id, lpl.galpon_id,
         lpl.regional, lpl.fecha_encaset, lpl.hembras_l, lpl.machos_l, lpl.peso_inicial_h, lpl.peso_inicial_m,
         lpl.unif_h, lpl.unif_m, lpl.mort_caja_h, lpl.mort_caja_m, lpl.raza, lpl.ano_tabla_genetica,
         lpl.linea, lpl.tipo_linea, lpl.codigo_guia_genetica, lpl.linea_genetica_id, lpl.tecnico,
         lpl.mixtas, lpl.peso_mixto, lpl.aves_encasetadas, lpl.edad_inicial, lpl.lote_erp, lpl.estado_traslado,
         lpl.pais_id, lpl.pais_nombre, lpl.empresa_nombre, lpl.company_id, lpl.created_by_user_id,
         lpl.updated_by_user_id, lpl.aves_h_actual, lpl.aves_m_actual
  INTO r_lpl
  FROM public.lote_postura_levante lpl
  WHERE lpl.lote_id = p_lote_id AND lpl.deleted_at IS NULL;

  IF r_lpl.lote_postura_levante_id IS NULL THEN
    RAISE EXCEPTION 'No existe LPL para lote_id %. Ejecuta antes migracion_seguimiento_levante_to_diario_lote_14.sql', p_lote_id;
  END IF;

  lpl_id := r_lpl.lote_postura_levante_id;
  aves_h := GREATEST(0, COALESCE(r_lpl.aves_h_actual, r_lpl.hembras_l, 0));
  aves_m := GREATEST(0, COALESCE(r_lpl.aves_m_actual, r_lpl.machos_l, 0));
  base_nombre := COALESCE(TRIM(r_lpl.lote_nombre), 'Lote-' || lpl_id);
  uid := COALESCE(r_lpl.updated_by_user_id, r_lpl.created_by_user_id);
  IF uid IS NULL THEN uid := 1; END IF;

  -- 2) Crear LPP-H y LPP-M si no existen
  IF NOT EXISTS (SELECT 1 FROM public.lote_postura_produccion lpp WHERE lpp.lote_postura_levante_id = lpl_id AND lpp.deleted_at IS NULL) THEN
    -- LPP-H
    INSERT INTO public.lote_postura_produccion (
      lote_nombre, granja_id, nucleo_id, galpon_id, regional, fecha_encaset,
      hembras_l, machos_l, peso_inicial_h, peso_inicial_m, unif_h, unif_m,
      mort_caja_h, mort_caja_m, raza, ano_tabla_genetica, linea, tipo_linea,
      codigo_guia_genetica, linea_genetica_id, tecnico, mixtas, peso_mixto,
      aves_encasetadas, edad_inicial, lote_erp, estado_traslado,
      pais_id, pais_nombre, empresa_nombre,
      fecha_inicio_produccion, hembras_iniciales_prod, machos_iniciales_prod,
      lote_postura_levante_id, aves_h_inicial, aves_m_inicial, aves_h_actual, aves_m_actual,
      empresa_id, usuario_id, estado, etapa, edad, estado_cierre,
      company_id, created_by_user_id, created_at, updated_by_user_id, updated_at, deleted_at
    ) VALUES (
      base_nombre || '-H', r_lpl.granja_id, r_lpl.nucleo_id, r_lpl.galpon_id, r_lpl.regional, r_lpl.fecha_encaset,
      r_lpl.hembras_l, r_lpl.machos_l, r_lpl.peso_inicial_h, r_lpl.peso_inicial_m, r_lpl.unif_h, r_lpl.unif_m,
      r_lpl.mort_caja_h, r_lpl.mort_caja_m, r_lpl.raza, r_lpl.ano_tabla_genetica, r_lpl.linea, r_lpl.tipo_linea,
      r_lpl.codigo_guia_genetica, r_lpl.linea_genetica_id, r_lpl.tecnico, r_lpl.mixtas, r_lpl.peso_mixto,
      r_lpl.aves_encasetadas, r_lpl.edad_inicial, r_lpl.lote_erp, r_lpl.estado_traslado,
      r_lpl.pais_id, r_lpl.pais_nombre, r_lpl.empresa_nombre,
      now_ts, aves_h, 0,
      lpl_id, aves_h, 0, aves_h, 0,
      r_lpl.company_id, uid, 'Produccion', 'Produccion', 26, 'Abierta',
      r_lpl.company_id, uid, now_ts, r_lpl.updated_by_user_id, now_ts, NULL
    );

    -- LPP-M
    INSERT INTO public.lote_postura_produccion (
      lote_nombre, granja_id, nucleo_id, galpon_id, regional, fecha_encaset,
      hembras_l, machos_l, peso_inicial_h, peso_inicial_m, unif_h, unif_m,
      mort_caja_h, mort_caja_m, raza, ano_tabla_genetica, linea, tipo_linea,
      codigo_guia_genetica, linea_genetica_id, tecnico, mixtas, peso_mixto,
      aves_encasetadas, edad_inicial, lote_erp, estado_traslado,
      pais_id, pais_nombre, empresa_nombre,
      fecha_inicio_produccion, hembras_iniciales_prod, machos_iniciales_prod,
      lote_postura_levante_id, aves_h_inicial, aves_m_inicial, aves_h_actual, aves_m_actual,
      empresa_id, usuario_id, estado, etapa, edad, estado_cierre,
      company_id, created_by_user_id, created_at, updated_by_user_id, updated_at, deleted_at
    ) VALUES (
      base_nombre || '-M', r_lpl.granja_id, r_lpl.nucleo_id, r_lpl.galpon_id, r_lpl.regional, r_lpl.fecha_encaset,
      r_lpl.hembras_l, r_lpl.machos_l, r_lpl.peso_inicial_h, r_lpl.peso_inicial_m, r_lpl.unif_h, r_lpl.unif_m,
      r_lpl.mort_caja_h, r_lpl.mort_caja_m, r_lpl.raza, r_lpl.ano_tabla_genetica, r_lpl.linea, r_lpl.tipo_linea,
      r_lpl.codigo_guia_genetica, r_lpl.linea_genetica_id, r_lpl.tecnico, r_lpl.mixtas, r_lpl.peso_mixto,
      r_lpl.aves_encasetadas, r_lpl.edad_inicial, r_lpl.lote_erp, r_lpl.estado_traslado,
      r_lpl.pais_id, r_lpl.pais_nombre, r_lpl.empresa_nombre,
      now_ts, 0, aves_m,
      lpl_id, 0, aves_m, 0, aves_m,
      r_lpl.company_id, uid, 'Produccion', 'Produccion', 26, 'Abierta',
      r_lpl.company_id, uid, now_ts, r_lpl.updated_by_user_id, now_ts, NULL
    );
  END IF;

  -- 3) Cerrar LPL
  UPDATE public.lote_postura_levante
  SET estado_cierre = 'Cerrado', updated_by_user_id = r_lpl.updated_by_user_id, updated_at = now_ts
  WHERE lote_postura_levante_id = lpl_id;

  -- 4) ID del LPP-H (para vincular seguimiento_diario)
  SELECT lpp.lote_postura_produccion_id INTO lpp_h_id
  FROM public.lote_postura_produccion lpp
  WHERE lpp.lote_postura_levante_id = lpl_id AND lpp.deleted_at IS NULL
    AND lpp.lote_nombre LIKE '%-H'
  LIMIT 1;

  -- Si no hay sufijo -H, tomar el primero (por si el nombre es distinto)
  IF lpp_h_id IS NULL THEN
    SELECT lpp.lote_postura_produccion_id INTO lpp_h_id
    FROM public.lote_postura_produccion lpp
    WHERE lpp.lote_postura_levante_id = lpl_id AND lpp.deleted_at IS NULL
    ORDER BY lpp.lote_postura_produccion_id
    LIMIT 1;
  END IF;

  -- 5) Copiar produccion_diaria → seguimiento_diario (lote 14 → LPP-H)
  --    produccion_diaria.lote_id puede ser INT o VARCHAR según BD; usamos ::text = '14'
  INSERT INTO public.seguimiento_diario (
    tipo_seguimiento, lote_id, lote_postura_levante_id, lote_postura_produccion_id,
    reproductora_id, fecha,
    mortalidad_hembras, mortalidad_machos, sel_h, sel_m,
    error_sexaje_hembras, error_sexaje_machos,
    consumo_kg_hembras, consumo_kg_machos, tipo_alimento, observaciones, ciclo,
    huevo_tot, huevo_inc, huevo_limpio, huevo_tratado, huevo_sucio, huevo_deforme,
    huevo_blanco, huevo_doble_yema, huevo_piso, huevo_pequeno, huevo_roto, huevo_desecho, huevo_otro,
    peso_huevo, etapa,
    peso_prom_hembras, peso_prom_machos, uniformidad_hembras, uniformidad_machos,
    peso_h, peso_m, uniformidad, coeficiente_variacion, observaciones_pesaje,
    consumo_agua_diario, consumo_agua_ph, consumo_agua_orp, consumo_agua_temperatura,
    metadata, created_by_user_id, created_at, updated_at
  )
  SELECT
    'produccion',
    pd.lote_id::text,
    NULL,
    lpp_h_id,
    NULL,
    pd.fecha_registro,
    COALESCE(pd.mortalidad_hembras, 0),
    COALESCE(pd.mortalidad_machos, 0),
    COALESCE(pd.sel_h, 0),
    COALESCE(pd.sel_m, 0),
    NULL, NULL,
    pd.cons_kg_h,
    pd.cons_kg_m,
    pd.tipo_alimento,
    pd.observaciones,
    NULL,
    COALESCE(pd.huevo_tot, 0),
    COALESCE(pd.huevo_inc, 0),
    COALESCE(pd.huevo_limpio, 0),
    COALESCE(pd.huevo_tratado, 0),
    COALESCE(pd.huevo_sucio, 0),
    COALESCE(pd.huevo_deforme, 0),
    COALESCE(pd.huevo_blanco, 0),
    COALESCE(pd.huevo_doble_yema, 0),
    COALESCE(pd.huevo_piso, 0),
    COALESCE(pd.huevo_pequeno, 0),
    COALESCE(pd.huevo_roto, 0),
    COALESCE(pd.huevo_desecho, 0),
    COALESCE(pd.huevo_otro, 0),
    pd.peso_huevo,
    pd.etapa,
    pd.peso_h,
    pd.peso_m,
    NULL, NULL,
    pd.peso_h,
    pd.peso_m,
    pd.uniformidad,
    pd.coeficiente_variacion,
    pd.observaciones_pesaje,
    pd.consumo_agua_diario,
    pd.consumo_agua_ph,
    pd.consumo_agua_orp,
    pd.consumo_agua_temperatura,
    pd.metadata,
    NULL,
    COALESCE(pd.fecha_registro, (NOW() AT TIME ZONE 'utc')),
    NULL
  FROM public.produccion_diaria pd
  WHERE (pd.lote_id::text = p_lote_id::text)
    AND NOT EXISTS (
      SELECT 1 FROM public.seguimiento_diario sd
      WHERE sd.tipo_seguimiento = 'produccion'
        AND sd.lote_postura_produccion_id = lpp_h_id
        AND sd.fecha = pd.fecha_registro
    );

END $$;

-- Resumen
SELECT 'Lote 14 producción: LPP creados' AS paso;
SELECT lpp.lote_postura_produccion_id, lpp.lote_nombre, lpp.lote_postura_levante_id,
       lpp.aves_h_inicial, lpp.aves_m_inicial, lpp.aves_h_actual, lpp.aves_m_actual
FROM public.lote_postura_produccion lpp
WHERE lpp.lote_postura_levante_id = (SELECT lote_postura_levante_id FROM public.lote_postura_levante WHERE lote_id = 14 AND deleted_at IS NULL LIMIT 1)
  AND lpp.deleted_at IS NULL;

SELECT 'Registros produccion_diaria migrados a seguimiento_diario (producción)' AS paso,
       COUNT(*) AS total
FROM public.seguimiento_diario sd
WHERE sd.tipo_seguimiento = 'produccion'
  AND sd.lote_postura_produccion_id IN (
    SELECT lote_postura_produccion_id FROM public.lote_postura_produccion
    WHERE lote_postura_levante_id = (SELECT lote_postura_levante_id FROM public.lote_postura_levante WHERE lote_id = 14 AND deleted_at IS NULL LIMIT 1)
      AND deleted_at IS NULL
  );
