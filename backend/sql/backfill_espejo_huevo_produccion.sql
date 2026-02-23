-- =============================================================================
-- BACKFILL: espejo_huevo_produccion desde seguimiento_diario existente
-- Ejecutar una vez después de crear la tabla y el trigger.
-- Pobla espejo para lotes que ya tienen seguimientos.
-- =============================================================================

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
