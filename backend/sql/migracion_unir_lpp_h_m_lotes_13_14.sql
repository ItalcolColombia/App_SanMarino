-- =============================================================================
-- MIGRACIÓN: Unir LPP-H y LPP-M en un solo registro (lotes 13 y 14)
--
-- Situación: Los lotes 13 y 14 en producción tienen dos registros en
-- lote_postura_produccion (uno -H y otro -M). El seguimiento_diario está
-- vinculado solo al LPP-H; el LPP-M no tiene seguimiento.
--
-- Este script:
--   1. Por cada par (LPP-H, LPP-M) de lotes 13 y 14:
--      a) Actualiza el registro -H: suma machos del -M (aves_m_inicial,
--         aves_m_actual, machos_iniciales_prod) y renombra a P-{base} (sin -H).
--      b) Marca el registro -M como eliminado (deleted_at) para no romper FKs.
--   2. El único LPP que queda activo es el que ya tenía el seguimiento; ahora
--      tiene hembras + machos y nombre P-{base}.
--
-- Ejecutar después de las migraciones de producción (migracion_produccion_lote_*)
-- y opcionalmente después ejecutar actualizar_lpp_aves_actuales_desde_seguimiento.sql
-- para recalcular aves_h_actual/aves_m_actual desde seguimiento_diario.
-- =============================================================================

DO $$
DECLARE
  r RECORD;
  now_ts TIMESTAMPTZ;
  base_nombre TEXT;
BEGIN
  now_ts := (NOW() AT TIME ZONE 'utc');

  -- Por cada LPL de lotes 13 y 14 que tenga ambos LPP -H y -M
  FOR r IN (
    SELECT
      lpl.lote_postura_levante_id,
      lpl.lote_id,
      lpl.lote_nombre AS lpl_nombre,
      lpp_h.lote_postura_produccion_id AS lpp_h_id,
      lpp_h.lote_nombre               AS lpp_h_nombre,
      lpp_h.aves_h_inicial,
      lpp_h.aves_m_inicial            AS lpp_h_aves_m_ini,
      lpp_h.aves_h_actual,
      lpp_h.aves_m_actual             AS lpp_h_aves_m_act,
      lpp_h.hembras_iniciales_prod,
      lpp_h.machos_iniciales_prod      AS lpp_h_machos_prod,
      lpp_m.lote_postura_produccion_id AS lpp_m_id,
      lpp_m.aves_m_inicial            AS lpp_m_aves_m_ini,
      lpp_m.aves_m_actual             AS lpp_m_aves_m_act,
      lpp_m.machos_iniciales_prod      AS lpp_m_machos_prod
    FROM public.lote_postura_levante lpl
    JOIN public.lote_postura_produccion lpp_h
      ON lpp_h.lote_postura_levante_id = lpl.lote_postura_levante_id
     AND lpp_h.deleted_at IS NULL
     AND lpp_h.lote_nombre LIKE '%-H'
    JOIN public.lote_postura_produccion lpp_m
      ON lpp_m.lote_postura_levante_id = lpl.lote_postura_levante_id
     AND lpp_m.deleted_at IS NULL
     AND lpp_m.lote_nombre LIKE '%-M'
    WHERE lpl.deleted_at IS NULL
      AND lpl.lote_id IN (13, 14)
  )
  LOOP
    -- Nombre nuevo: P- + base (quitar sufijo -H del nombre actual)
    base_nombre := TRIM(regexp_replace(r.lpp_h_nombre, '-H\s*$', '', 'i'));
    IF COALESCE(base_nombre, '') = '' THEN base_nombre := COALESCE(TRIM(r.lpl_nombre), 'Lote-' || r.lote_postura_levante_id); END IF;

    -- 1) Actualizar LPP-H: incorporar machos del LPP-M y renombrar a P-{base}
    UPDATE public.lote_postura_produccion
    SET
      lote_nombre             = 'P-' || base_nombre,
      aves_m_inicial          = COALESCE(r.lpp_h_aves_m_ini, 0) + COALESCE(r.lpp_m_aves_m_ini, 0),
      aves_m_actual           = COALESCE(r.lpp_h_aves_m_act, 0) + COALESCE(r.lpp_m_aves_m_act, 0),
      machos_iniciales_prod   = COALESCE(r.lpp_h_machos_prod, 0) + COALESCE(r.lpp_m_machos_prod, 0),
      updated_at              = now_ts
    WHERE lote_postura_produccion_id = r.lpp_h_id;

    -- 2) Soft-delete del LPP-M (el seguimiento está en LPP-H; -M no tiene)
    UPDATE public.lote_postura_produccion
    SET deleted_at = now_ts, updated_at = now_ts
    WHERE lote_postura_produccion_id = r.lpp_m_id;

  END LOOP;
END $$;

-- Resumen: un solo LPP activo por lote (nombre P-..., hembras + machos)
SELECT 'Lotes 13 y 14: LPP unificados (H+M en primer registro, -M marcado deleted)' AS mensaje;
SELECT
  lpl.lote_id,
  lpl.lote_nombre       AS nombre_levante,
  lpp.lote_postura_produccion_id,
  lpp.lote_nombre       AS nombre_lpp,
  lpp.aves_h_inicial,
  lpp.aves_m_inicial,
  lpp.aves_h_actual,
  lpp.aves_m_actual,
  lpp.hembras_iniciales_prod,
  lpp.machos_iniciales_prod
FROM public.lote_postura_levante lpl
JOIN public.lote_postura_produccion lpp
  ON lpp.lote_postura_levante_id = lpl.lote_postura_levante_id
 AND lpp.deleted_at IS NULL
WHERE lpl.deleted_at IS NULL
  AND lpl.lote_id IN (13, 14)
ORDER BY lpl.lote_id;

-- Opcional: recalcular aves actuales desde seguimiento_diario (ejecutar
-- actualizar_lpp_aves_actuales_desde_seguimiento.sql después si lo usas).
