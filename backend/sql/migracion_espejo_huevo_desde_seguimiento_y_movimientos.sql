-- =============================================================================
-- MIGRACIÓN: Espejo Huevo Producción desde Seguimiento Diario + Ajuste por Movimientos
-- =============================================================================
-- Objetivo:
--   1. Llevar al espejo_huevo_produccion todo el historial de huevos por categoría
--      desde seguimiento_diario (por lote_postura_produccion_id).
--   2. historico_* = acumulado de lo registrado en seguimiento (solo suma).
--   3. dinamico_* = mismo que historico al inicio; luego se restan los movimientos
--      (traslados/ventas) ya procesados (estado = 'Completado').
--
-- Requisitos previos:
--   - Tabla espejo_huevo_produccion creada (create_espejo_huevo_produccion.sql).
--   - Trigger en seguimiento_diario instalado (trigger_espejo_huevo_produccion_seguimiento_diario.sql).
--   - Columna lote_postura_produccion_id en traslado_huevos (si existe; si no, se usa lote_id para mapear a LPP).
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- PASO 1: Backfill espejo desde seguimiento_diario (historico + dinamico inicial)
-- -----------------------------------------------------------------------------
INSERT INTO public.espejo_huevo_produccion (
    lote_postura_produccion_id,
    company_id,
    huevo_tot_historico, huevo_tot_dinamico,
    huevo_inc_historico, huevo_inc_dinamico,
    huevo_limpio_historico, huevo_limpio_dinamico,
    huevo_tratado_historico, huevo_tratado_dinamico,
    huevo_sucio_historico, huevo_sucio_dinamico,
    huevo_deforme_historico, huevo_deforme_dinamico,
    huevo_blanco_historico, huevo_blanco_dinamico,
    huevo_doble_yema_historico, huevo_doble_yema_dinamico,
    huevo_piso_historico, huevo_piso_dinamico,
    huevo_pequeno_historico, huevo_pequeno_dinamico,
    huevo_roto_historico, huevo_roto_dinamico,
    huevo_desecho_historico, huevo_desecho_dinamico,
    huevo_otro_historico, huevo_otro_dinamico,
    historico_semanal,
    created_at,
    updated_at
)
SELECT
    sd.lote_postura_produccion_id,
    lpp.company_id,
    COALESCE(SUM(sd.huevo_tot), 0), COALESCE(SUM(sd.huevo_tot), 0),
    COALESCE(SUM(sd.huevo_inc), 0), COALESCE(SUM(sd.huevo_inc), 0),
    COALESCE(SUM(sd.huevo_limpio), 0), COALESCE(SUM(sd.huevo_limpio), 0),
    COALESCE(SUM(sd.huevo_tratado), 0), COALESCE(SUM(sd.huevo_tratado), 0),
    COALESCE(SUM(sd.huevo_sucio), 0), COALESCE(SUM(sd.huevo_sucio), 0),
    COALESCE(SUM(sd.huevo_deforme), 0), COALESCE(SUM(sd.huevo_deforme), 0),
    COALESCE(SUM(sd.huevo_blanco), 0), COALESCE(SUM(sd.huevo_blanco), 0),
    COALESCE(SUM(sd.huevo_doble_yema), 0), COALESCE(SUM(sd.huevo_doble_yema), 0),
    COALESCE(SUM(sd.huevo_piso), 0), COALESCE(SUM(sd.huevo_piso), 0),
    COALESCE(SUM(sd.huevo_pequeno), 0), COALESCE(SUM(sd.huevo_pequeno), 0),
    COALESCE(SUM(sd.huevo_roto), 0), COALESCE(SUM(sd.huevo_roto), 0),
    COALESCE(SUM(sd.huevo_desecho), 0), COALESCE(SUM(sd.huevo_desecho), 0),
    COALESCE(SUM(sd.huevo_otro), 0), COALESCE(SUM(sd.huevo_otro), 0),
    COALESCE((
        SELECT jsonb_object_agg(
            semana,
            jsonb_build_object(
                'semana', semana,
                'huevo_tot', huevo_tot,
                'huevo_inc', huevo_inc,
                'huevo_limpio', huevo_limpio,
                'huevo_tratado', huevo_tratado,
                'huevo_sucio', huevo_sucio,
                'huevo_deforme', huevo_deforme,
                'huevo_blanco', huevo_blanco,
                'huevo_doble_yema', huevo_doble_yema,
                'huevo_piso', huevo_piso,
                'huevo_pequeno', huevo_pequeno,
                'huevo_roto', huevo_roto,
                'huevo_desecho', huevo_desecho,
                'huevo_otro', huevo_otro
            )
        )
        FROM (
            SELECT
                GREATEST(26, ((sd2.fecha::date - COALESCE(lpp2.fecha_encaset, lpp2.fecha_inicio_produccion, sd2.fecha)::date) / 7) + 1) AS semana,
                SUM(COALESCE(sd2.huevo_tot, 0)) AS huevo_tot,
                SUM(COALESCE(sd2.huevo_inc, 0)) AS huevo_inc,
                SUM(COALESCE(sd2.huevo_limpio, 0)) AS huevo_limpio,
                SUM(COALESCE(sd2.huevo_tratado, 0)) AS huevo_tratado,
                SUM(COALESCE(sd2.huevo_sucio, 0)) AS huevo_sucio,
                SUM(COALESCE(sd2.huevo_deforme, 0)) AS huevo_deforme,
                SUM(COALESCE(sd2.huevo_blanco, 0)) AS huevo_blanco,
                SUM(COALESCE(sd2.huevo_doble_yema, 0)) AS huevo_doble_yema,
                SUM(COALESCE(sd2.huevo_piso, 0)) AS huevo_piso,
                SUM(COALESCE(sd2.huevo_pequeno, 0)) AS huevo_pequeno,
                SUM(COALESCE(sd2.huevo_roto, 0)) AS huevo_roto,
                SUM(COALESCE(sd2.huevo_desecho, 0)) AS huevo_desecho,
                SUM(COALESCE(sd2.huevo_otro, 0)) AS huevo_otro
            FROM public.seguimiento_diario sd2
            JOIN public.lote_postura_produccion lpp2 ON lpp2.lote_postura_produccion_id = sd2.lote_postura_produccion_id AND lpp2.deleted_at IS NULL
            WHERE sd2.tipo_seguimiento = 'produccion'
              AND sd2.lote_postura_produccion_id = sd.lote_postura_produccion_id
            GROUP BY GREATEST(26, ((sd2.fecha::date - COALESCE(lpp2.fecha_encaset, lpp2.fecha_inicio_produccion, sd2.fecha)::date) / 7) + 1)
        ) sw
    ), '{}'::jsonb),
    NOW() AT TIME ZONE 'utc',
    NOW() AT TIME ZONE 'utc'
FROM public.seguimiento_diario sd
JOIN public.lote_postura_produccion lpp ON lpp.lote_postura_produccion_id = sd.lote_postura_produccion_id AND lpp.deleted_at IS NULL
WHERE sd.tipo_seguimiento = 'produccion'
  AND sd.lote_postura_produccion_id IS NOT NULL
GROUP BY sd.lote_postura_produccion_id, lpp.company_id
ON CONFLICT (lote_postura_produccion_id) DO UPDATE SET
    huevo_tot_historico    = EXCLUDED.huevo_tot_historico,
    huevo_tot_dinamico     = EXCLUDED.huevo_tot_dinamico,
    huevo_inc_historico    = EXCLUDED.huevo_inc_historico,
    huevo_inc_dinamico     = EXCLUDED.huevo_inc_dinamico,
    huevo_limpio_historico = EXCLUDED.huevo_limpio_historico,
    huevo_limpio_dinamico  = EXCLUDED.huevo_limpio_dinamico,
    huevo_tratado_historico= EXCLUDED.huevo_tratado_historico,
    huevo_tratado_dinamico = EXCLUDED.huevo_tratado_dinamico,
    huevo_sucio_historico  = EXCLUDED.huevo_sucio_historico,
    huevo_sucio_dinamico   = EXCLUDED.huevo_sucio_dinamico,
    huevo_deforme_historico= EXCLUDED.huevo_deforme_historico,
    huevo_deforme_dinamico = EXCLUDED.huevo_deforme_dinamico,
    huevo_blanco_historico = EXCLUDED.huevo_blanco_historico,
    huevo_blanco_dinamico  = EXCLUDED.huevo_blanco_dinamico,
    huevo_doble_yema_historico= EXCLUDED.huevo_doble_yema_historico,
    huevo_doble_yema_dinamico = EXCLUDED.huevo_doble_yema_dinamico,
    huevo_piso_historico   = EXCLUDED.huevo_piso_historico,
    huevo_piso_dinamico    = EXCLUDED.huevo_piso_dinamico,
    huevo_pequeno_historico= EXCLUDED.huevo_pequeno_historico,
    huevo_pequeno_dinamico = EXCLUDED.huevo_pequeno_dinamico,
    huevo_roto_historico   = EXCLUDED.huevo_roto_historico,
    huevo_roto_dinamico    = EXCLUDED.huevo_roto_dinamico,
    huevo_desecho_historico= EXCLUDED.huevo_desecho_historico,
    huevo_desecho_dinamico = EXCLUDED.huevo_desecho_dinamico,
    huevo_otro_historico   = EXCLUDED.huevo_otro_historico,
    huevo_otro_dinamico    = EXCLUDED.huevo_otro_dinamico,
    historico_semanal      = EXCLUDED.historico_semanal,
    updated_at             = NOW() AT TIME ZONE 'utc';

-- -----------------------------------------------------------------------------
-- PASO 2: Restar de *_dinamico los traslados/ventas ya Completados
--         (por lote_postura_produccion_id; legacy: lote_id -> LPP)
-- -----------------------------------------------------------------------------
WITH th_with_lpp AS (
    SELECT
        COALESCE(
            th.lote_postura_produccion_id,
            CASE
                WHEN th.lote_id ~ '^LPP-[0-9]+$' THEN (SUBSTRING(th.lote_id FROM 5))::INTEGER
                ELSE (SELECT lpp.lote_postura_produccion_id
                      FROM public.lote_postura_produccion lpp
                      WHERE lpp.deleted_at IS NULL AND lpp.lote_id::text = th.lote_id
                      LIMIT 1)
            END
        ) AS lpp_id,
        th.cantidad_limpio,
        th.cantidad_tratado,
        th.cantidad_sucio,
        th.cantidad_deforme,
        th.cantidad_blanco,
        th.cantidad_doble_yema,
        th.cantidad_piso,
        th.cantidad_pequeno,
        th.cantidad_roto,
        th.cantidad_desecho,
        th.cantidad_otro
    FROM public.traslado_huevos th
    WHERE th.estado = 'Completado'
      AND th.deleted_at IS NULL
),
mov AS (
    SELECT
        lpp_id,
        SUM(cantidad_limpio)    AS rest_limpio,
        SUM(cantidad_tratado)  AS rest_tratado,
        SUM(cantidad_sucio)    AS rest_sucio,
        SUM(cantidad_deforme)  AS rest_deforme,
        SUM(cantidad_blanco)   AS rest_blanco,
        SUM(cantidad_doble_yema) AS rest_doble_yema,
        SUM(cantidad_piso)     AS rest_piso,
        SUM(cantidad_pequeno)  AS rest_pequeno,
        SUM(cantidad_roto)     AS rest_roto,
        SUM(cantidad_desecho)  AS rest_desecho,
        SUM(cantidad_otro)     AS rest_otro,
        SUM(cantidad_limpio + cantidad_tratado) AS rest_inc,
        SUM(cantidad_limpio + cantidad_tratado + cantidad_sucio + cantidad_deforme
            + cantidad_blanco + cantidad_doble_yema + cantidad_piso + cantidad_pequeno
            + cantidad_roto + cantidad_desecho + cantidad_otro) AS rest_tot
    FROM th_with_lpp
    WHERE lpp_id IS NOT NULL
    GROUP BY lpp_id
)
UPDATE public.espejo_huevo_produccion e
SET
    huevo_tot_dinamico     = GREATEST(0, e.huevo_tot_dinamico     - mov.rest_tot),
    huevo_inc_dinamico     = GREATEST(0, e.huevo_inc_dinamico     - mov.rest_inc),
    huevo_limpio_dinamico  = GREATEST(0, e.huevo_limpio_dinamico  - mov.rest_limpio),
    huevo_tratado_dinamico = GREATEST(0, e.huevo_tratado_dinamico - mov.rest_tratado),
    huevo_sucio_dinamico   = GREATEST(0, e.huevo_sucio_dinamico   - mov.rest_sucio),
    huevo_deforme_dinamico = GREATEST(0, e.huevo_deforme_dinamico - mov.rest_deforme),
    huevo_blanco_dinamico   = GREATEST(0, e.huevo_blanco_dinamico   - mov.rest_blanco),
    huevo_doble_yema_dinamico = GREATEST(0, e.huevo_doble_yema_dinamico - mov.rest_doble_yema),
    huevo_piso_dinamico    = GREATEST(0, e.huevo_piso_dinamico    - mov.rest_piso),
    huevo_pequeno_dinamico = GREATEST(0, e.huevo_pequeno_dinamico - mov.rest_pequeno),
    huevo_roto_dinamico    = GREATEST(0, e.huevo_roto_dinamico    - mov.rest_roto),
    huevo_desecho_dinamico = GREATEST(0, e.huevo_desecho_dinamico - mov.rest_desecho),
    huevo_otro_dinamico    = GREATEST(0, e.huevo_otro_dinamico    - mov.rest_otro),
    updated_at             = NOW() AT TIME ZONE 'utc'
FROM mov
WHERE e.lote_postura_produccion_id = mov.lpp_id;

COMMIT;

-- -----------------------------------------------------------------------------
-- PASO 3 (opcional): Rellenar lote_postura_produccion_id en traslado_huevos legacy
--                    para que la app use siempre LPP en traslados futuros.
-- -----------------------------------------------------------------------------
-- Descomentar y ejecutar si tienes traslados con lote_id pero sin lote_postura_produccion_id:
/*
UPDATE public.traslado_huevos th
SET lote_postura_produccion_id = CASE
    WHEN th.lote_id ~ '^LPP-[0-9]+$' THEN (SUBSTRING(th.lote_id FROM 5))::INTEGER
    ELSE (SELECT lpp.lote_postura_produccion_id
          FROM public.lote_postura_produccion lpp
          WHERE lpp.deleted_at IS NULL AND lpp.lote_id::text = th.lote_id
          LIMIT 1)
END
WHERE th.lote_postura_produccion_id IS NULL
  AND th.deleted_at IS NULL;
*/

-- =============================================================================
-- NOTAS
-- =============================================================================
-- Después de esta migración:
-- - historico_* refleja todo lo registrado en seguimiento_diario por ese LPP.
-- - dinamico_* refleja el saldo disponible (historico menos traslados/ventas
--   ya Completados). Los nuevos registros de seguimiento suman vía trigger;
--   los nuevos traslados al procesarse restan solo en la app (espejo).
-- Si en tu BD traslado_huevos no tiene columna lote_postura_produccion_id,
-- añádela antes: ALTER TABLE traslado_huevos ADD COLUMN lote_postura_produccion_id INTEGER;
-- =============================================================================
