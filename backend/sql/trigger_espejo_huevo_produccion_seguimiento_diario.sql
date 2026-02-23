-- =============================================================================
-- TRIGGER: seguimiento_diario -> espejo_huevo_produccion
-- Al INSERT/UPDATE/DELETE en seguimiento_diario (producción con LPP):
--   INSERT: suma huevos a historico y dinamico, actualiza historico_semanal
--   UPDATE: resta OLD, suma NEW
--   DELETE: resta OLD
-- =============================================================================

CREATE OR REPLACE FUNCTION fn_espejo_huevo_produccion_upsert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_lpp_id    INTEGER;
    v_company   INTEGER;
    v_fecha_encaset DATE;
    v_semana    INTEGER;
    v_val       JSONB;
    v_old_val   JSONB;
    v_new_val   JSONB;
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF NEW.tipo_seguimiento != 'produccion' OR NEW.lote_postura_produccion_id IS NULL THEN
            RETURN NEW;
        END IF;
        v_lpp_id := NEW.lote_postura_produccion_id;

        SELECT lpp.fecha_encaset::date, lpp.company_id
        INTO v_fecha_encaset, v_company
        FROM public.lote_postura_produccion lpp
        WHERE lpp.lote_postura_produccion_id = v_lpp_id AND lpp.deleted_at IS NULL;

        IF v_fecha_encaset IS NULL THEN
            SELECT lpp.fecha_inicio_produccion::date, lpp.company_id
            INTO v_fecha_encaset, v_company
            FROM public.lote_postura_produccion lpp
            WHERE lpp.lote_postura_produccion_id = v_lpp_id;
        END IF;
        IF v_fecha_encaset IS NULL THEN v_fecha_encaset := NEW.fecha::date; END IF;
        IF v_company IS NULL THEN v_company := 1; END IF;

        v_semana := GREATEST(26, ((NEW.fecha::date - v_fecha_encaset) / 7) + 1);

        INSERT INTO public.espejo_huevo_produccion (
            lote_postura_produccion_id, company_id,
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
            historico_semanal, updated_at
        )
        VALUES (
            v_lpp_id, v_company,
            COALESCE(NEW.huevo_tot, 0), COALESCE(NEW.huevo_tot, 0),
            COALESCE(NEW.huevo_inc, 0), COALESCE(NEW.huevo_inc, 0),
            COALESCE(NEW.huevo_limpio, 0), COALESCE(NEW.huevo_limpio, 0),
            COALESCE(NEW.huevo_tratado, 0), COALESCE(NEW.huevo_tratado, 0),
            COALESCE(NEW.huevo_sucio, 0), COALESCE(NEW.huevo_sucio, 0),
            COALESCE(NEW.huevo_deforme, 0), COALESCE(NEW.huevo_deforme, 0),
            COALESCE(NEW.huevo_blanco, 0), COALESCE(NEW.huevo_blanco, 0),
            COALESCE(NEW.huevo_doble_yema, 0), COALESCE(NEW.huevo_doble_yema, 0),
            COALESCE(NEW.huevo_piso, 0), COALESCE(NEW.huevo_piso, 0),
            COALESCE(NEW.huevo_pequeno, 0), COALESCE(NEW.huevo_pequeno, 0),
            COALESCE(NEW.huevo_roto, 0), COALESCE(NEW.huevo_roto, 0),
            COALESCE(NEW.huevo_desecho, 0), COALESCE(NEW.huevo_desecho, 0),
            COALESCE(NEW.huevo_otro, 0), COALESCE(NEW.huevo_otro, 0),
            '{}'::jsonb, NOW() AT TIME ZONE 'utc'
        )
        ON CONFLICT (lote_postura_produccion_id) DO UPDATE SET
            huevo_tot_historico    = espejo_huevo_produccion.huevo_tot_historico    + COALESCE(NEW.huevo_tot, 0),
            huevo_tot_dinamico     = espejo_huevo_produccion.huevo_tot_dinamico     + COALESCE(NEW.huevo_tot, 0),
            huevo_inc_historico    = espejo_huevo_produccion.huevo_inc_historico    + COALESCE(NEW.huevo_inc, 0),
            huevo_inc_dinamico     = espejo_huevo_produccion.huevo_inc_dinamico     + COALESCE(NEW.huevo_inc, 0),
            huevo_limpio_historico = espejo_huevo_produccion.huevo_limpio_historico + COALESCE(NEW.huevo_limpio, 0),
            huevo_limpio_dinamico  = espejo_huevo_produccion.huevo_limpio_dinamico  + COALESCE(NEW.huevo_limpio, 0),
            huevo_tratado_historico= espejo_huevo_produccion.huevo_tratado_historico+ COALESCE(NEW.huevo_tratado, 0),
            huevo_tratado_dinamico = espejo_huevo_produccion.huevo_tratado_dinamico + COALESCE(NEW.huevo_tratado, 0),
            huevo_sucio_historico  = espejo_huevo_produccion.huevo_sucio_historico  + COALESCE(NEW.huevo_sucio, 0),
            huevo_sucio_dinamico   = espejo_huevo_produccion.huevo_sucio_dinamico   + COALESCE(NEW.huevo_sucio, 0),
            huevo_deforme_historico= espejo_huevo_produccion.huevo_deforme_historico+ COALESCE(NEW.huevo_deforme, 0),
            huevo_deforme_dinamico = espejo_huevo_produccion.huevo_deforme_dinamico + COALESCE(NEW.huevo_deforme, 0),
            huevo_blanco_historico = espejo_huevo_produccion.huevo_blanco_historico + COALESCE(NEW.huevo_blanco, 0),
            huevo_blanco_dinamico  = espejo_huevo_produccion.huevo_blanco_dinamico  + COALESCE(NEW.huevo_blanco, 0),
            huevo_doble_yema_historico= espejo_huevo_produccion.huevo_doble_yema_historico+ COALESCE(NEW.huevo_doble_yema, 0),
            huevo_doble_yema_dinamico = espejo_huevo_produccion.huevo_doble_yema_dinamico + COALESCE(NEW.huevo_doble_yema, 0),
            huevo_piso_historico   = espejo_huevo_produccion.huevo_piso_historico   + COALESCE(NEW.huevo_piso, 0),
            huevo_piso_dinamico    = espejo_huevo_produccion.huevo_piso_dinamico    + COALESCE(NEW.huevo_piso, 0),
            huevo_pequeno_historico= espejo_huevo_produccion.huevo_pequeno_historico+ COALESCE(NEW.huevo_pequeno, 0),
            huevo_pequeno_dinamico = espejo_huevo_produccion.huevo_pequeno_dinamico + COALESCE(NEW.huevo_pequeno, 0),
            huevo_roto_historico   = espejo_huevo_produccion.huevo_roto_historico   + COALESCE(NEW.huevo_roto, 0),
            huevo_roto_dinamico    = espejo_huevo_produccion.huevo_roto_dinamico    + COALESCE(NEW.huevo_roto, 0),
            huevo_desecho_historico= espejo_huevo_produccion.huevo_desecho_historico+ COALESCE(NEW.huevo_desecho, 0),
            huevo_desecho_dinamico = espejo_huevo_produccion.huevo_desecho_dinamico + COALESCE(NEW.huevo_desecho, 0),
            huevo_otro_historico   = espejo_huevo_produccion.huevo_otro_historico   + COALESCE(NEW.huevo_otro, 0),
            huevo_otro_dinamico    = espejo_huevo_produccion.huevo_otro_dinamico    + COALESCE(NEW.huevo_otro, 0),
            historico_semanal = jsonb_set(
                COALESCE(espejo_huevo_produccion.historico_semanal, '{}'::jsonb),
                ARRAY[v_semana::text],
                jsonb_build_object(
                    'semana', v_semana,
                    'huevo_tot', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_tot')::int, 0) + COALESCE(NEW.huevo_tot, 0),
                    'huevo_inc', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_inc')::int, 0) + COALESCE(NEW.huevo_inc, 0),
                    'huevo_limpio', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_limpio')::int, 0) + COALESCE(NEW.huevo_limpio, 0),
                    'huevo_tratado', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_tratado')::int, 0) + COALESCE(NEW.huevo_tratado, 0),
                    'huevo_sucio', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_sucio')::int, 0) + COALESCE(NEW.huevo_sucio, 0),
                    'huevo_deforme', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_deforme')::int, 0) + COALESCE(NEW.huevo_deforme, 0),
                    'huevo_blanco', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_blanco')::int, 0) + COALESCE(NEW.huevo_blanco, 0),
                    'huevo_doble_yema', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_doble_yema')::int, 0) + COALESCE(NEW.huevo_doble_yema, 0),
                    'huevo_piso', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_piso')::int, 0) + COALESCE(NEW.huevo_piso, 0),
                    'huevo_pequeno', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_pequeno')::int, 0) + COALESCE(NEW.huevo_pequeno, 0),
                    'huevo_roto', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_roto')::int, 0) + COALESCE(NEW.huevo_roto, 0),
                    'huevo_desecho', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_desecho')::int, 0) + COALESCE(NEW.huevo_desecho, 0),
                    'huevo_otro', COALESCE((COALESCE(espejo_huevo_produccion.historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_otro')::int, 0) + COALESCE(NEW.huevo_otro, 0)
                )
            ),
            updated_at = NOW() AT TIME ZONE 'utc';

        RETURN NEW;
    END IF;

    IF TG_OP = 'UPDATE' THEN
        IF NEW.tipo_seguimiento != 'produccion' OR NEW.lote_postura_produccion_id IS NULL THEN
            RETURN NEW;
        END IF;
        v_lpp_id := NEW.lote_postura_produccion_id;

        UPDATE public.espejo_huevo_produccion SET
            huevo_tot_historico    = huevo_tot_historico    - COALESCE(OLD.huevo_tot, 0)    + COALESCE(NEW.huevo_tot, 0),
            huevo_tot_dinamico     = huevo_tot_dinamico     - COALESCE(OLD.huevo_tot, 0)    + COALESCE(NEW.huevo_tot, 0),
            huevo_inc_historico    = huevo_inc_historico    - COALESCE(OLD.huevo_inc, 0)    + COALESCE(NEW.huevo_inc, 0),
            huevo_inc_dinamico     = huevo_inc_dinamico     - COALESCE(OLD.huevo_inc, 0)    + COALESCE(NEW.huevo_inc, 0),
            huevo_limpio_historico = huevo_limpio_historico - COALESCE(OLD.huevo_limpio, 0) + COALESCE(NEW.huevo_limpio, 0),
            huevo_limpio_dinamico  = huevo_limpio_dinamico  - COALESCE(OLD.huevo_limpio, 0) + COALESCE(NEW.huevo_limpio, 0),
            huevo_tratado_historico= huevo_tratado_historico- COALESCE(OLD.huevo_tratado, 0)+ COALESCE(NEW.huevo_tratado, 0),
            huevo_tratado_dinamico = huevo_tratado_dinamico - COALESCE(OLD.huevo_tratado, 0)+ COALESCE(NEW.huevo_tratado, 0),
            huevo_sucio_historico  = huevo_sucio_historico  - COALESCE(OLD.huevo_sucio, 0)  + COALESCE(NEW.huevo_sucio, 0),
            huevo_sucio_dinamico   = huevo_sucio_dinamico   - COALESCE(OLD.huevo_sucio, 0)  + COALESCE(NEW.huevo_sucio, 0),
            huevo_deforme_historico= huevo_deforme_historico- COALESCE(OLD.huevo_deforme, 0)+ COALESCE(NEW.huevo_deforme, 0),
            huevo_deforme_dinamico = huevo_deforme_dinamico - COALESCE(OLD.huevo_deforme, 0)+ COALESCE(NEW.huevo_deforme, 0),
            huevo_blanco_historico = huevo_blanco_historico - COALESCE(OLD.huevo_blanco, 0) + COALESCE(NEW.huevo_blanco, 0),
            huevo_blanco_dinamico  = huevo_blanco_dinamico  - COALESCE(OLD.huevo_blanco, 0) + COALESCE(NEW.huevo_blanco, 0),
            huevo_doble_yema_historico= huevo_doble_yema_historico- COALESCE(OLD.huevo_doble_yema, 0)+ COALESCE(NEW.huevo_doble_yema, 0),
            huevo_doble_yema_dinamico = huevo_doble_yema_dinamico - COALESCE(OLD.huevo_doble_yema, 0)+ COALESCE(NEW.huevo_doble_yema, 0),
            huevo_piso_historico   = huevo_piso_historico   - COALESCE(OLD.huevo_piso, 0)   + COALESCE(NEW.huevo_piso, 0),
            huevo_piso_dinamico    = huevo_piso_dinamico    - COALESCE(OLD.huevo_piso, 0)   + COALESCE(NEW.huevo_piso, 0),
            huevo_pequeno_historico= huevo_pequeno_historico- COALESCE(OLD.huevo_pequeno, 0)+ COALESCE(NEW.huevo_pequeno, 0),
            huevo_pequeno_dinamico = huevo_pequeno_dinamico - COALESCE(OLD.huevo_pequeno, 0)+ COALESCE(NEW.huevo_pequeno, 0),
            huevo_roto_historico   = huevo_roto_historico   - COALESCE(OLD.huevo_roto, 0)   + COALESCE(NEW.huevo_roto, 0),
            huevo_roto_dinamico    = huevo_roto_dinamico    - COALESCE(OLD.huevo_roto, 0)   + COALESCE(NEW.huevo_roto, 0),
            huevo_desecho_historico= huevo_desecho_historico- COALESCE(OLD.huevo_desecho, 0)+ COALESCE(NEW.huevo_desecho, 0),
            huevo_desecho_dinamico = huevo_desecho_dinamico - COALESCE(OLD.huevo_desecho, 0)+ COALESCE(NEW.huevo_desecho, 0),
            huevo_otro_historico   = huevo_otro_historico   - COALESCE(OLD.huevo_otro, 0)   + COALESCE(NEW.huevo_otro, 0),
            huevo_otro_dinamico    = huevo_otro_dinamico    - COALESCE(OLD.huevo_otro, 0)   + COALESCE(NEW.huevo_otro, 0),
            updated_at = NOW() AT TIME ZONE 'utc'
        WHERE lote_postura_produccion_id = v_lpp_id;

        RETURN NEW;
    END IF;

    IF TG_OP = 'DELETE' THEN
        IF OLD.tipo_seguimiento != 'produccion' OR OLD.lote_postura_produccion_id IS NULL THEN
            RETURN OLD;
        END IF;
        v_lpp_id := OLD.lote_postura_produccion_id;

        SELECT lpp.fecha_encaset::date
        INTO v_fecha_encaset
        FROM public.lote_postura_produccion lpp
        WHERE lpp.lote_postura_produccion_id = v_lpp_id AND lpp.deleted_at IS NULL;
        IF v_fecha_encaset IS NULL THEN
            SELECT lpp.fecha_inicio_produccion::date INTO v_fecha_encaset
            FROM public.lote_postura_produccion lpp WHERE lpp.lote_postura_produccion_id = v_lpp_id;
        END IF;
        IF v_fecha_encaset IS NULL THEN v_fecha_encaset := OLD.fecha::date; END IF;
        v_semana := GREATEST(26, ((OLD.fecha::date - v_fecha_encaset) / 7) + 1);

        UPDATE public.espejo_huevo_produccion SET
            huevo_tot_historico    = GREATEST(0, huevo_tot_historico    - COALESCE(OLD.huevo_tot, 0)),
            huevo_tot_dinamico     = GREATEST(0, huevo_tot_dinamico     - COALESCE(OLD.huevo_tot, 0)),
            huevo_inc_historico    = GREATEST(0, huevo_inc_historico    - COALESCE(OLD.huevo_inc, 0)),
            huevo_inc_dinamico     = GREATEST(0, huevo_inc_dinamico     - COALESCE(OLD.huevo_inc, 0)),
            huevo_limpio_historico = GREATEST(0, huevo_limpio_historico - COALESCE(OLD.huevo_limpio, 0)),
            huevo_limpio_dinamico  = GREATEST(0, huevo_limpio_dinamico  - COALESCE(OLD.huevo_limpio, 0)),
            huevo_tratado_historico= GREATEST(0, huevo_tratado_historico- COALESCE(OLD.huevo_tratado, 0)),
            huevo_tratado_dinamico = GREATEST(0, huevo_tratado_dinamico - COALESCE(OLD.huevo_tratado, 0)),
            huevo_sucio_historico  = GREATEST(0, huevo_sucio_historico  - COALESCE(OLD.huevo_sucio, 0)),
            huevo_sucio_dinamico   = GREATEST(0, huevo_sucio_dinamico   - COALESCE(OLD.huevo_sucio, 0)),
            huevo_deforme_historico= GREATEST(0, huevo_deforme_historico- COALESCE(OLD.huevo_deforme, 0)),
            huevo_deforme_dinamico = GREATEST(0, huevo_deforme_dinamico - COALESCE(OLD.huevo_deforme, 0)),
            huevo_blanco_historico = GREATEST(0, huevo_blanco_historico - COALESCE(OLD.huevo_blanco, 0)),
            huevo_blanco_dinamico  = GREATEST(0, huevo_blanco_dinamico  - COALESCE(OLD.huevo_blanco, 0)),
            huevo_doble_yema_historico= GREATEST(0, huevo_doble_yema_historico- COALESCE(OLD.huevo_doble_yema, 0)),
            huevo_doble_yema_dinamico = GREATEST(0, huevo_doble_yema_dinamico - COALESCE(OLD.huevo_doble_yema, 0)),
            huevo_piso_historico   = GREATEST(0, huevo_piso_historico   - COALESCE(OLD.huevo_piso, 0)),
            huevo_piso_dinamico    = GREATEST(0, huevo_piso_dinamico    - COALESCE(OLD.huevo_piso, 0)),
            huevo_pequeno_historico= GREATEST(0, huevo_pequeno_historico- COALESCE(OLD.huevo_pequeno, 0)),
            huevo_pequeno_dinamico = GREATEST(0, huevo_pequeno_dinamico - COALESCE(OLD.huevo_pequeno, 0)),
            huevo_roto_historico   = GREATEST(0, huevo_roto_historico   - COALESCE(OLD.huevo_roto, 0)),
            huevo_roto_dinamico    = GREATEST(0, huevo_roto_dinamico    - COALESCE(OLD.huevo_roto, 0)),
            huevo_desecho_historico= GREATEST(0, huevo_desecho_historico- COALESCE(OLD.huevo_desecho, 0)),
            huevo_desecho_dinamico = GREATEST(0, huevo_desecho_dinamico - COALESCE(OLD.huevo_desecho, 0)),
            huevo_otro_historico   = GREATEST(0, huevo_otro_historico   - COALESCE(OLD.huevo_otro, 0)),
            huevo_otro_dinamico    = GREATEST(0, huevo_otro_dinamico    - COALESCE(OLD.huevo_otro, 0)),
            historico_semanal = jsonb_set(
                COALESCE(historico_semanal, '{}'::jsonb),
                ARRAY[v_semana::text],
                jsonb_build_object(
                    'semana', v_semana,
                    'huevo_tot', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_tot')::int, 0) - COALESCE(OLD.huevo_tot, 0)),
                    'huevo_inc', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_inc')::int, 0) - COALESCE(OLD.huevo_inc, 0)),
                    'huevo_limpio', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_limpio')::int, 0) - COALESCE(OLD.huevo_limpio, 0)),
                    'huevo_tratado', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_tratado')::int, 0) - COALESCE(OLD.huevo_tratado, 0)),
                    'huevo_sucio', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_sucio')::int, 0) - COALESCE(OLD.huevo_sucio, 0)),
                    'huevo_deforme', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_deforme')::int, 0) - COALESCE(OLD.huevo_deforme, 0)),
                    'huevo_blanco', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_blanco')::int, 0) - COALESCE(OLD.huevo_blanco, 0)),
                    'huevo_doble_yema', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_doble_yema')::int, 0) - COALESCE(OLD.huevo_doble_yema, 0)),
                    'huevo_piso', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_piso')::int, 0) - COALESCE(OLD.huevo_piso, 0)),
                    'huevo_pequeno', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_pequeno')::int, 0) - COALESCE(OLD.huevo_pequeno, 0)),
                    'huevo_roto', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_roto')::int, 0) - COALESCE(OLD.huevo_roto, 0)),
                    'huevo_desecho', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_desecho')::int, 0) - COALESCE(OLD.huevo_desecho, 0)),
                    'huevo_otro', GREATEST(0, COALESCE((COALESCE(historico_semanal->v_semana::text, '{}'::jsonb)->>'huevo_otro')::int, 0) - COALESCE(OLD.huevo_otro, 0))
                )
            ),
            updated_at = NOW() AT TIME ZONE 'utc'
        WHERE lote_postura_produccion_id = v_lpp_id;

        RETURN OLD;
    END IF;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS tr_espejo_huevo_produccion_aiud ON public.seguimiento_diario;
CREATE TRIGGER tr_espejo_huevo_produccion_aiud
    AFTER INSERT OR UPDATE OR DELETE ON public.seguimiento_diario
    FOR EACH ROW
    EXECUTE FUNCTION fn_espejo_huevo_produccion_upsert();
