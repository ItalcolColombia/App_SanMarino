-- =============================================================================
-- MIGRACIÓN 03: Cerrar LPL con edad>=26 y crear LPP (H y M)
-- Para cada LPL con edad >= 26 semanas y estado_cierre = 'Abierto':
-- 1. Calcular aves_h_actual, aves_m_actual desde seguimiento_diario (tipo=levante)
-- 2. Cerrar LPL
-- 3. Crear LPP-H y LPP-M
-- NOTA: Deshabilitar temporalmente el trigger trg_lpl_cerrar_produccion para evitar
--       que se ejecute durante este INSERT, ya que nosotros hacemos el cierre manualmente.
-- =============================================================================

DO $$
DECLARE
  r RECORD;
  aves_h INT;
  aves_m INT;
  base_nombre TEXT;
  uid INT;
  now_ts TIMESTAMPTZ;
  edad_sw INT;
BEGIN
  now_ts := (NOW() AT TIME ZONE 'utc');

  FOR r IN (
    SELECT lpl.lote_postura_levante_id, lpl.lote_nombre, lpl.granja_id, lpl.nucleo_id, lpl.galpon_id,
           lpl.regional, lpl.fecha_encaset, lpl.hembras_l, lpl.machos_l, lpl.peso_inicial_h, lpl.peso_inicial_m,
           lpl.unif_h, lpl.unif_m, lpl.mort_caja_h, lpl.mort_caja_m, lpl.raza, lpl.ano_tabla_genetica,
           lpl.linea, lpl.tipo_linea, lpl.codigo_guia_genetica, lpl.linea_genetica_id, lpl.tecnico,
           lpl.mixtas, lpl.peso_mixto, lpl.aves_encasetadas, lpl.edad_inicial, lpl.lote_erp, lpl.estado_traslado,
           lpl.pais_id, lpl.pais_nombre, lpl.empresa_nombre, lpl.company_id, lpl.created_by_user_id,
           lpl.updated_by_user_id
    FROM public.lote_postura_levante lpl
    WHERE lpl.deleted_at IS NULL
      AND (lpl.estado_cierre IS NULL OR lpl.estado_cierre = 'Abierto')
      AND (
        (lpl.edad IS NOT NULL AND lpl.edad >= 26)
        OR (lpl.fecha_encaset IS NOT NULL AND (CURRENT_DATE - (lpl.fecha_encaset AT TIME ZONE 'utc')::date) / 7 >= 26)
      )
  )
  LOOP
    -- Calcular aves finales desde seguimiento_diario
    SELECT
      COALESCE(r.hembras_l, r.aves_h_inicial, 0) - COALESCE(SUM(sd.mortalidad_hembras), 0) - COALESCE(SUM(sd.sel_h), 0) - COALESCE(SUM(sd.error_sexaje_hembras), 0),
      COALESCE(r.machos_l, r.aves_m_inicial, 0) - COALESCE(SUM(sd.mortalidad_machos), 0) - COALESCE(SUM(sd.sel_m), 0) - COALESCE(SUM(sd.error_sexaje_machos), 0)
    INTO aves_h, aves_m
    FROM public.seguimiento_diario sd
    WHERE sd.tipo_seguimiento = 'levante'
      AND sd.lote_postura_levante_id = r.lote_postura_levante_id;

    aves_h := GREATEST(0, COALESCE(aves_h, r.hembras_l, 0));
    aves_m := GREATEST(0, COALESCE(aves_m, r.machos_l, 0));

    base_nombre := COALESCE(TRIM(r.lote_nombre), 'Lote-' || r.lote_postura_levante_id);
    uid := COALESCE(r.updated_by_user_id, r.created_by_user_id);

    -- Crear LPP-H
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
      base_nombre || '-H', r.granja_id, r.nucleo_id, r.galpon_id, r.regional, r.fecha_encaset,
      r.hembras_l, r.machos_l, r.peso_inicial_h, r.peso_inicial_m, r.unif_h, r.unif_m,
      r.mort_caja_h, r.mort_caja_m, r.raza, r.ano_tabla_genetica, r.linea, r.tipo_linea,
      r.codigo_guia_genetica, r.linea_genetica_id, r.tecnico, r.mixtas, r.peso_mixto,
      r.aves_encasetadas, r.edad_inicial, r.lote_erp, r.estado_traslado,
      r.pais_id, r.pais_nombre, r.empresa_nombre,
      now_ts, aves_h, 0,
      r.lote_postura_levante_id, aves_h, 0, aves_h, 0,
      r.company_id, uid, 'Produccion', 'Produccion', 26, 'Abierta',
      r.company_id, uid, now_ts, r.updated_by_user_id, now_ts, NULL
    );

    -- Crear LPP-M
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
      base_nombre || '-M', r.granja_id, r.nucleo_id, r.galpon_id, r.regional, r.fecha_encaset,
      r.hembras_l, r.machos_l, r.peso_inicial_h, r.peso_inicial_m, r.unif_h, r.unif_m,
      r.mort_caja_h, r.mort_caja_m, r.raza, r.ano_tabla_genetica, r.linea, r.tipo_linea,
      r.codigo_guia_genetica, r.linea_genetica_id, r.tecnico, r.mixtas, r.peso_mixto,
      r.aves_encasetadas, r.edad_inicial, r.lote_erp, r.estado_traslado,
      r.pais_id, r.pais_nombre, r.empresa_nombre,
      now_ts, 0, aves_m,
      r.lote_postura_levante_id, 0, aves_m, 0, aves_m,
      r.company_id, uid, 'Produccion', 'Produccion', 26, 'Abierta',
      r.company_id, uid, now_ts, r.updated_by_user_id, now_ts, NULL
    );

    -- Cerrar LPL
    UPDATE public.lote_postura_levante
    SET estado_cierre = 'Cerrado', updated_by_user_id = r.updated_by_user_id, updated_at = now_ts
    WHERE lote_postura_levante_id = r.lote_postura_levante_id;

  END LOOP;
END $$;
