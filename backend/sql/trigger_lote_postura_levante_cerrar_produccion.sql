-- ============================================================
-- TRIGGER: Cerrar levante y crear producción cuando edad >= 26
-- Se ejecuta en lote_postura_levante AFTER INSERT o UPDATE.
-- Si edad (de edad o calculada por fecha_encaset) >= 26 semanas,
-- cierra el levante y crea QLK345-H y QLK345-M en lote_postura_produccion.
-- ============================================================

CREATE OR REPLACE FUNCTION trg_lote_postura_levante_cerrar_produccion()
RETURNS TRIGGER AS $$
DECLARE
  edad_semanas INT;
  aves_h INT;
  aves_m INT;
  base_nombre TEXT;
  uid INT;
  now_ts TIMESTAMPTZ;
BEGIN
  -- En UPDATE: si ya estaba Cerrado, no volver a procesar
  IF TG_OP = 'UPDATE' AND OLD.estado_cierre = 'Cerrado' THEN
    RETURN NEW;
  END IF;

  -- Solo si no está eliminado
  IF NEW.deleted_at IS NOT NULL THEN
    RETURN NEW;
  END IF;

  -- Calcular edad en semanas: usar edad o (hoy - fecha_encaset) / 7
  edad_semanas := COALESCE(NEW.edad, 0);
  IF edad_semanas = 0 AND NEW.fecha_encaset IS NOT NULL THEN
    edad_semanas := GREATEST(0, (CURRENT_DATE - (NEW.fecha_encaset AT TIME ZONE 'utc')::date) / 7);
  END IF;

  -- Solo actuar si edad >= 26 y está Abierto
  IF edad_semanas < 26 THEN
    RETURN NEW;
  END IF;
  IF NEW.estado_cierre = 'Cerrado' THEN
    RETURN NEW;
  END IF;

  now_ts := (NOW() AT TIME ZONE 'utc');
  uid := COALESCE(NEW.updated_by_user_id, NEW.created_by_user_id);

  aves_h := COALESCE(NEW.aves_h_actual, NEW.aves_h_inicial, NEW.hembras_l, 0);
  aves_m := COALESCE(NEW.aves_m_actual, NEW.aves_m_inicial, NEW.machos_l, 0);
  base_nombre := COALESCE(TRIM(NEW.lote_nombre), 'Lote-' || NEW.lote_postura_levante_id);

  -- Crear QLK345-H (hembras)
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
    base_nombre || '-H', NEW.granja_id, NEW.nucleo_id, NEW.galpon_id, NEW.regional, NEW.fecha_encaset,
    NEW.hembras_l, NEW.machos_l, NEW.peso_inicial_h, NEW.peso_inicial_m, NEW.unif_h, NEW.unif_m,
    NEW.mort_caja_h, NEW.mort_caja_m, NEW.raza, NEW.ano_tabla_genetica, NEW.linea, NEW.tipo_linea,
    NEW.codigo_guia_genetica, NEW.linea_genetica_id, NEW.tecnico, NEW.mixtas, NEW.peso_mixto,
    NEW.aves_encasetadas, NEW.edad_inicial, NEW.lote_erp, NEW.estado_traslado,
    NEW.pais_id, NEW.pais_nombre, NEW.empresa_nombre,
    now_ts, aves_h, 0,
    NEW.lote_postura_levante_id, aves_h, 0, aves_h, 0,
    NEW.company_id, uid, 'Produccion', 'Produccion', NEW.edad, 'Abierta',
    NEW.company_id, uid, now_ts, NEW.updated_by_user_id, now_ts, NULL
  );

  -- Crear QLK345-M (machos)
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
    base_nombre || '-M', NEW.granja_id, NEW.nucleo_id, NEW.galpon_id, NEW.regional, NEW.fecha_encaset,
    NEW.hembras_l, NEW.machos_l, NEW.peso_inicial_h, NEW.peso_inicial_m, NEW.unif_h, NEW.unif_m,
    NEW.mort_caja_h, NEW.mort_caja_m, NEW.raza, NEW.ano_tabla_genetica, NEW.linea, NEW.tipo_linea,
    NEW.codigo_guia_genetica, NEW.linea_genetica_id, NEW.tecnico, NEW.mixtas, NEW.peso_mixto,
    NEW.aves_encasetadas, NEW.edad_inicial, NEW.lote_erp, NEW.estado_traslado,
    NEW.pais_id, NEW.pais_nombre, NEW.empresa_nombre,
    now_ts, 0, aves_m,
    NEW.lote_postura_levante_id, 0, aves_m, 0, aves_m,
    NEW.company_id, uid, 'Produccion', 'Produccion', NEW.edad, 'Abierta',
    NEW.company_id, uid, now_ts, NEW.updated_by_user_id, now_ts, NULL
  );

  -- Marcar levante como cerrado
  UPDATE public.lote_postura_levante
  SET estado_cierre = 'Cerrado', updated_by_user_id = NEW.updated_by_user_id, updated_at = now_ts
  WHERE lote_postura_levante_id = NEW.lote_postura_levante_id;

  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_lpl_cerrar_produccion ON public.lote_postura_levante;
CREATE TRIGGER trg_lpl_cerrar_produccion
  AFTER INSERT OR UPDATE ON public.lote_postura_levante
  FOR EACH ROW
  WHEN (
    (NEW.estado_cierre IS NULL OR NEW.estado_cierre = 'Abierto')
    AND (
      (NEW.edad IS NOT NULL AND NEW.edad >= 26)
      OR (NEW.fecha_encaset IS NOT NULL AND (CURRENT_DATE - (NEW.fecha_encaset AT TIME ZONE 'utc')::date) / 7 >= 26)
    )
  )
  EXECUTE PROCEDURE trg_lote_postura_levante_cerrar_produccion();

COMMENT ON FUNCTION trg_lote_postura_levante_cerrar_produccion() IS 'Cierra levante y crea lotes producción H/M cuando edad >= 26 semanas.';
