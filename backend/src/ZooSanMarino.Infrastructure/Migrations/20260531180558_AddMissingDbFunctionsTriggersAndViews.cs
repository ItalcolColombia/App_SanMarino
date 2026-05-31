using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Registra en migraciones EF todos los objetos de BD (funciones, trigger-functions,
    /// triggers y vistas) que existian en la BD pero solo se habian creado por scripts SQL
    /// manuales. Idempotente: CREATE OR REPLACE para funciones/vistas y DROP+CREATE para
    /// triggers, de modo que sea seguro en BD nuevas y en produccion (donde ya existen).
    /// DDL extraido del estado real de la BD via pg_get_functiondef/viewdef/triggerdef.
    /// </summary>
    /// <inheritdoc />
    public partial class AddMissingDbFunctionsTriggersAndViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===================== FUNCIONES (CREATE OR REPLACE) =====================
            // ---- weeknum_iso ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.weeknum_iso(p_date date)
 RETURNS integer
 LANGUAGE sql
 IMMUTABLE
AS $function$
  select cast(to_char($1, 'IW') as int)
$function$
");

            // ---- dpr ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.dpr(x double precision, n integer)
 RETURNS double precision
 LANGUAGE sql
 IMMUTABLE
AS $function$
  select round(x::numeric, n)::double precision
$function$
");

            // ---- fn_acumulado_entradas_alimento ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_acumulado_entradas_alimento(p_lote_id integer, p_hasta_id bigint)
 RETURNS numeric
 LANGUAGE sql
 STABLE
AS $function$
    SELECT COALESCE(SUM(h.cantidad_kg), 0)::NUMERIC(18, 3)
    FROM public.lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.anulado = FALSE
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA')
      AND h.id <= p_hasta_id;
$function$
");

            // ---- fn_espejo_huevo_produccion_upsert ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_espejo_huevo_produccion_upsert()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
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
$function$
");

            // ---- fn_lote_ave_engorde_id_desde_ubicacion ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_lote_ave_engorde_id_desde_ubicacion(p_farm_id integer, p_nucleo_id character varying, p_galpon_id character varying)
 RETURNS integer
 LANGUAGE sql
 STABLE
AS $function$
    SELECT l.lote_ave_engorde_id
    FROM public.lote_ave_engorde l
    WHERE l.granja_id = p_farm_id
      AND COALESCE(TRIM(l.nucleo_id), '') = COALESCE(TRIM(p_nucleo_id), '')
      AND COALESCE(TRIM(l.galpon_id), '') = COALESCE(TRIM(p_galpon_id), '')
      AND l.deleted_at IS NULL
    ORDER BY l.lote_ave_engorde_id DESC
    LIMIT 1;
$function$
");

            // ---- fn_tipo_evento_inventario ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_tipo_evento_inventario(p_mt character varying)
 RETURNS character varying
 LANGUAGE plpgsql
 IMMUTABLE
AS $function$
BEGIN
    IF p_mt IS NULL THEN RETURN 'INV_OTRO'; END IF;
    IF p_mt ILIKE 'Ingreso' THEN RETURN 'INV_INGRESO'; END IF;
    IF p_mt ILIKE 'TrasladoEntrada' OR p_mt ILIKE 'TrasladoInterGranjaEntrada' THEN RETURN 'INV_TRASLADO_ENTRADA'; END IF;
    IF p_mt ILIKE 'TrasladoSalida' OR p_mt ILIKE 'TrasladoInterGranjaSalida'
       OR p_mt ILIKE 'TrasladoInterGranjaPendiente' THEN RETURN 'INV_TRASLADO_SALIDA'; END IF;
    IF p_mt ILIKE 'Consumo' THEN RETURN 'INV_CONSUMO'; END IF;
    RETURN 'INV_OTRO';
END;
$function$
");

            // ---- sp_recalcular_seguimiento_levante ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.sp_recalcular_seguimiento_levante(l_lote_id text)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
declare
  v_fecha_encaset date;
  v_h_ini int;
  v_m_ini int;
  v_mort_caja_h int;
  v_mort_caja_m int;
  v_codigo_guia text;
  v_raza text;
  v_ano_gen int;
begin

  select fecha_encaset,   -- 1) Traer datos base del lote
         coalesce(hembras_l, 0),
         coalesce(machos_l, 0),
         coalesce(mort_caja_h, 0),
         coalesce(mort_caja_m, 0),
         codigo_guia_genetica,
         raza,
         ano_tabla_genetica
    into v_fecha_encaset, v_h_ini, v_m_ini, v_mort_caja_h, v_mort_caja_m, v_codigo_guia, v_raza, v_ano_gen
  from lotes
  where lote_id = l_lote_id;

  if not found then
    raise exception 'Lote % no existe', l_lote_id;
  end if;

 
  delete from produccion_resultado_levante where lote_id = l_lote_id;  -- 2) Borrar snapshot previo

 
  insert into produccion_resultado_levante (  -- 3) Cálculo
    lote_id, fecha, edad_semana,

    hembra_viva, mort_h, sel_h_out, err_h, cons_kg_h, peso_h, unif_h, cv_h,
    mort_h_pct, sel_h_pct, err_h_pct, ms_eh_h,
    ac_mort_h, ac_sel_h, ac_err_h, ac_cons_kg_h, cons_ac_gr_h, gr_ave_dia_h,
    dif_cons_h_pct, dif_peso_h_pct, retiro_h_pct, retiro_h_ac_pct,

    macho_vivo, mort_m, sel_m_out, err_m, cons_kg_m, peso_m, unif_m, cv_m,
    mort_m_pct, sel_m_pct, err_m_pct, ms_em_m,
    ac_mort_m, ac_sel_m, ac_err_m, ac_cons_kg_m, cons_ac_gr_m, gr_ave_dia_m,
    dif_cons_m_pct, dif_peso_m_pct, retiro_m_pct, retiro_m_ac_pct,

    rel_m_h_pct,

    peso_h_guia, unif_h_guia, cons_ac_gr_h_guia, gr_ave_dia_h_guia, mort_h_pct_guia,
    peso_m_guia, unif_m_guia, cons_ac_gr_m_guia, gr_ave_dia_m_guia, mort_m_pct_guia,
    alimento_h_guia, alimento_m_guia
  )
  with base as (

    select s.*,     -- Datos diarios + edad
           case when v_fecha_encaset is null then null
                else (1 + floor(extract(epoch from (s.fecha_registro - v_fecha_encaset)) / 86400.0 / 7.0)::int)
           end as edad_sem,
          
           (coalesce(s.mortalidad_hembras,0) + coalesce(s.sel_h,0) + coalesce(s.error_sexaje_hembras,0)) as out_h,  -- retiros del día
           (coalesce(s.mortalidad_machos,0)  + coalesce(s.sel_m,0) + coalesce(s.error_sexaje_machos,0) ) as out_m
    from seguimiento_lote_levante s
    where s.lote_id = l_lote_id
  ),
  ac_base as (
   
    select b.*,  -- Acumulados de retiros y consumo (hasta el día anterior para reconstruir población viva)
           sum(b.out_h) over (order by b.fecha_registro
                              rows between unbounded preceding and 1 preceding) as ac_out_h_prev,
           sum(b.out_m) over (order by b.fecha_registro
                              rows between unbounded preceding and 1 preceding) as ac_out_m_prev,

         
           sum(coalesce(b.mortalidad_hembras,0)) over (order by b.fecha_registro) as ac_mort_h,   -- acumulados ""al día"" (para KPIs)
           sum(coalesce(b.sel_h,0))             over (order by b.fecha_registro) as ac_sel_h,
           sum(coalesce(b.error_sexaje_hembras,0)) over (order by b.fecha_registro) as ac_err_h,
           sum(coalesce(b.consumo_kg_hembras,0))   over (order by b.fecha_registro) as ac_cons_kg_h,

           sum(coalesce(b.mortalidad_machos,0)) over (order by b.fecha_registro) as ac_mort_m,
           sum(coalesce(b.sel_m,0))             over (order by b.fecha_registro) as ac_sel_m,
           sum(coalesce(b.error_sexaje_machos,0)) over (order by b.fecha_registro) as ac_err_m,
           sum(coalesce(b.consumo_kg_machos,0))   over (order by b.fecha_registro) as ac_cons_kg_m,

           lag(b.peso_prom_h) over (order by b.fecha_registro) as peso_h_prev,
           lag(b.peso_prom_m) over (order by b.fecha_registro) as peso_m_prev
    from base b
  ),
  pobl as (
 
    select a.*,    -- Reconstrucción de poblaciones vivas del día
           greatest(0, (coalesce(v_h_ini,0) - coalesce(v_mort_caja_h,0) - coalesce(a.ac_out_h_prev,0)))::int as hembra_viva,
           greatest(0, (coalesce(v_m_ini,0) - coalesce(v_mort_caja_m,0) - coalesce(a.ac_out_m_prev,0)))::int as macho_vivo
    from ac_base a
  ),

  gh as (   -- Guías filtradas por guía genética/raza/año si están definidos en el lote
    select semana, peso_obj, unif_obj, mort_pct_obj, cons_ac_gr_obj, gr_ave_dia_obj, incr_cons_obj,
           kcal_sem_obj, kcal_sem_ac_obj, prot_sem_obj, prot_sem_ac_obj, alimento_nom
    from guia_semana
    where sexo='H'
      and (codigo_guia_genetica is not distinct from v_codigo_guia)
      and (raza is not distinct from v_raza)
      and (ano_tabla_genetica is not distinct from v_ano_gen)
  ),
  gm as (
    select semana, peso_obj, unif_obj, mort_pct_obj, cons_ac_gr_obj, gr_ave_dia_obj, incr_cons_obj,
           kcal_sem_obj, kcal_sem_ac_obj, prot_sem_obj, prot_sem_ac_obj, alimento_nom
    from guia_semana
    where sexo='M'
      and (codigo_guia_genetica is not distinct from v_codigo_guia)
      and (raza is not distinct from v_raza)
      and (ano_tabla_genetica is not distinct from v_ano_gen)
  )
  select
    l_lote_id as lote_id,
    p.fecha_registro as fecha,
    p.edad_sem as edad_semana,


    p.hembra_viva,     -- ===== Hembras: medidos =====
    coalesce(p.mortalidad_hembras,0) as mort_h,
    coalesce(p.sel_h,0)              as sel_h_out,
    coalesce(p.error_sexaje_hembras,0) as err_h,
    p.consumo_kg_hembras             as cons_kg_h,
    p.peso_prom_h                    as peso_h,
    p.uniformidad_h                  as unif_h,
    p.cv_h                           as cv_h,

   
    case when p.hembra_viva>0 then dpr(p.mortalidad_hembras * 100.0 / p.hembra_viva, 3) end as mort_h_pct,  -- ===== Hembras: calculados =====
    case when p.hembra_viva>0 then dpr(p.sel_h * 100.0 / p.hembra_viva, 3) end              as sel_h_pct,
    case when p.hembra_viva>0 then dpr(p.error_sexaje_hembras * 100.0 / p.hembra_viva, 3) end as err_h_pct,
    (coalesce(p.mortalidad_hembras,0)+coalesce(p.sel_h,0)+coalesce(p.error_sexaje_hembras,0))   as ms_eh_h,

    p.ac_mort_h, p.ac_sel_h, p.ac_err_h,
    p.ac_cons_kg_h,
    case when p.hembra_viva>0 then dpr( (p.ac_cons_kg_h*1000.0)/p.hembra_viva, 3) end as cons_ac_gr_h,
    case when p.peso_prom_h is null or p.peso_h_prev is null then null
         else dpr(p.peso_prom_h - p.peso_h_prev, 2)
    end as gr_ave_dia_h,

    case when gh.cons_ac_gr_obj is null or p.hembra_viva<=0 then null
         else dpr( (((p.ac_cons_kg_h*1000.0)/p.hembra_viva) - gh.cons_ac_gr_obj) * 100.0 / gh.cons_ac_gr_obj, 3)
    end as dif_cons_h_pct,

    case when gh.peso_obj is null or p.peso_prom_h is null then null
         else dpr( (p.peso_prom_h - gh.peso_obj) * 100.0 / gh.peso_obj, 3)
    end as dif_peso_h_pct,

    case when p.hembra_viva>0 then dpr( (coalesce(p.sel_h,0)+coalesce(p.error_sexaje_hembras,0)) * 100.0 / p.hembra_viva, 3) end as retiro_h_pct,
    case when (p.hembra_viva + p.ac_mort_h + p.ac_sel_h + p.ac_err_h)>0
         then dpr( (p.ac_sel_h + p.ac_err_h) * 100.0 / (p.hembra_viva + p.ac_mort_h + p.ac_sel_h + p.ac_err_h), 3)
    end as retiro_h_ac_pct,

    
    p.macho_vivo,-- ===== Machos: medidos =====
    coalesce(p.mortalidad_machos,0) as mort_m,
    coalesce(p.sel_m,0)             as sel_m_out,
    coalesce(p.error_sexaje_machos,0) as err_m,
    p.consumo_kg_machos             as cons_kg_m,
    p.peso_prom_m                   as peso_m,
    p.uniformidad_m                 as unif_m,
    p.cv_m                          as cv_m,

 
    case when p.macho_vivo>0 then dpr(p.mortalidad_machos * 100.0 / p.macho_vivo, 3) end as mort_m_pct,    -- ===== Machos: calculados =====
    case when p.macho_vivo>0 then dpr(p.sel_m * 100.0 / p.macho_vivo, 3) end             as sel_m_pct,
    case when p.macho_vivo>0 then dpr(p.error_sexaje_machos * 100.0 / p.macho_vivo, 3) end as err_m_pct,
    (coalesce(p.mortalidad_machos,0)+coalesce(p.sel_m,0)+coalesce(p.error_sexaje_machos,0))   as ms_em_m,

    p.ac_mort_m, p.ac_sel_m, p.ac_err_m,
    p.ac_cons_kg_m,
    case when p.macho_vivo>0 then dpr( (p.ac_cons_kg_m*1000.0)/p.macho_vivo, 3) end as cons_ac_gr_m,
    case when p.peso_prom_m is null or p.peso_m_prev is null then null
         else dpr(p.peso_prom_m - p.peso_m_prev, 2)
    end as gr_ave_dia_m,

    case when gm.cons_ac_gr_obj is null or p.macho_vivo<=0 then null
         else dpr( (((p.ac_cons_kg_m*1000.0)/p.macho_vivo) - gm.cons_ac_gr_obj) * 100.0 / gm.cons_ac_gr_obj, 3)
    end as dif_cons_m_pct,

    case when gm.peso_obj is null or p.peso_prom_m is null then null
         else dpr( (p.peso_prom_m - gm.peso_obj) * 100.0 / gm.peso_obj, 3)
    end as dif_peso_m_pct,

    case when p.macho_vivo>0 then dpr( (coalesce(p.sel_m,0)+coalesce(p.error_sexaje_machos,0)) * 100.0 / p.macho_vivo, 3) end as retiro_m_pct,
    case when (p.macho_vivo + p.ac_mort_m + p.ac_sel_m + p.ac_err_m)>0
         then dpr( (p.ac_sel_m + p.ac_err_m) * 100.0 / (p.macho_vivo + p.ac_mort_m + p.ac_sel_m + p.ac_err_m), 3)
    end as retiro_m_ac_pct,

    case when p.hembra_viva is null or p.hembra_viva=0 then null
         else dpr(p.macho_vivo * 100.0 / p.hembra_viva, 3)
    end as rel_m_h_pct,


    gh.peso_obj, gh.unif_obj, gh.cons_ac_gr_obj, gh.gr_ave_dia_obj, gh.mort_pct_obj,     -- Guías H
 
    gm.peso_obj, gm.unif_obj, gm.cons_ac_gr_obj, gm.gr_ave_dia_obj, gm.mort_pct_obj,    -- Guías M
    gh.alimento_nom, gm.alimento_nom
  from pobl p
  left join gh on gh.semana = p.edad_sem
  left join gm on gm.semana = p.edad_sem
  order by p.fecha_registro;

end;
$function$
");

            // ---- trg_historico_lote_postura_levante ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.trg_historico_lote_postura_levante()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    INSERT INTO public.historico_lote_postura (
        company_id, tipo_lote, lote_postura_levante_id, lote_postura_produccion_id,
        tipo_registro, fecha_registro, usuario_id, snapshot
    ) VALUES (
        NEW.company_id, 'LotePosturaLevante', NEW.lote_postura_levante_id, NULL,
        CASE WHEN TG_OP = 'INSERT' THEN 'Creacion' ELSE 'Actualizacion' END,
        NOW() AT TIME ZONE 'utc',
        COALESCE(NEW.updated_by_user_id, NEW.created_by_user_id),
        to_jsonb(NEW)
    );
    RETURN NEW;
END;
$function$
");

            // ---- trg_historico_lote_postura_produccion ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.trg_historico_lote_postura_produccion()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    INSERT INTO public.historico_lote_postura (
        company_id, tipo_lote, lote_postura_levante_id, lote_postura_produccion_id,
        tipo_registro, fecha_registro, usuario_id, snapshot
    ) VALUES (
        NEW.company_id, 'LotePosturaProduccion', NULL, NEW.lote_postura_produccion_id,
        CASE WHEN TG_OP = 'INSERT' THEN 'Creacion' ELSE 'Actualizacion' END,
        NOW() AT TIME ZONE 'utc',
        COALESCE(NEW.updated_by_user_id, NEW.created_by_user_id),
        to_jsonb(NEW)
    );
    RETURN NEW;
END;
$function$
");

            // ---- trg_lote_hist_desde_inventario_gestion ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.trg_lote_hist_desde_inventario_gestion()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_lote INTEGER;
    v_tipo VARCHAR(40);
    v_item_txt VARCHAR(400);
    v_acum NUMERIC(18, 3);
    v_hist_id BIGINT;
BEGIN
    v_lote := public.fn_lote_ave_engorde_id_desde_ubicacion(
        NEW.farm_id, NEW.nucleo_id, NEW.galpon_id
    );
    v_tipo := public.fn_tipo_evento_inventario(NEW.movement_type);

    SELECT CONCAT(i.codigo, ' — ', i.nombre)
    INTO v_item_txt
    FROM public.item_inventario_ecuador i
    WHERE i.id = NEW.item_inventario_ecuador_id;

    INSERT INTO public.lote_registro_historico_unificado (
        company_id, lote_ave_engorde_id, farm_id, nucleo_id, galpon_id,
        fecha_operacion, tipo_evento, origen_tabla, origen_id,
        movement_type_original, item_inventario_ecuador_id, item_resumen,
        cantidad_kg, unidad, referencia, numero_documento,
        acumulado_entradas_alimento_kg
    ) VALUES (
        NEW.company_id,
        v_lote,
        NEW.farm_id,
        NEW.nucleo_id,
        NEW.galpon_id,
        (NEW.created_at AT TIME ZONE 'UTC')::DATE,
        v_tipo,
        'inventario_gestion_movimiento',
        NEW.id,
        NEW.movement_type,
        NEW.item_inventario_ecuador_id,
        v_item_txt,
        NEW.quantity,
        NEW.unit,
        NEW.reference,
        NULL,
        NULL
    )
    RETURNING id INTO v_hist_id;

    IF v_lote IS NOT NULL AND v_tipo IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA') THEN
        v_acum := public.fn_acumulado_entradas_alimento(v_lote, v_hist_id);
        UPDATE public.lote_registro_historico_unificado
        SET acumulado_entradas_alimento_kg = v_acum
        WHERE id = v_hist_id;
    END IF;

    RETURN NEW;
END;
$function$
");

            // ---- trg_lote_hist_mov_pollo_anulado ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.trg_lote_hist_mov_pollo_anulado()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    IF NEW.tipo_movimiento IS DISTINCT FROM 'Venta' THEN
        RETURN NEW;
    END IF;
    IF NEW.estado IS DISTINCT FROM 'Anulado' AND NEW.deleted_at IS NULL THEN
        RETURN NEW;
    END IF;
    UPDATE public.lote_registro_historico_unificado
    SET anulado = TRUE
    WHERE origen_tabla = 'movimiento_pollo_engorde'
      AND origen_id = NEW.id;
    RETURN NEW;
END;
$function$
");

            // ---- trg_lote_postura_levante_cerrar_produccion ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.trg_lote_postura_levante_cerrar_produccion()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
DECLARE
  edad_semanas INT;
  aves_h INT;
  aves_m INT;
  nombre_produccion TEXT;
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

  aves_h := GREATEST(0, COALESCE(NEW.aves_h_actual, NEW.aves_h_inicial, NEW.hembras_l, 0));
  aves_m := GREATEST(0, COALESCE(NEW.aves_m_actual, NEW.aves_m_inicial, NEW.machos_l, 0));
  -- Nombre: prefijo ""P-"" (Producción) + nombre del levante (sin sufijos -H/-M)
  nombre_produccion := 'P-' || COALESCE(TRIM(NEW.lote_nombre), 'Lote-' || NEW.lote_postura_levante_id);

  -- Un solo lote de producción con hembras y machos (reducciones se llevan en seguimiento_diario por sexo)
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
    nombre_produccion, NEW.granja_id, NEW.nucleo_id, NEW.galpon_id, NEW.regional, NEW.fecha_encaset,
    NEW.hembras_l, NEW.machos_l, NEW.peso_inicial_h, NEW.peso_inicial_m, NEW.unif_h, NEW.unif_m,
    NEW.mort_caja_h, NEW.mort_caja_m, NEW.raza, NEW.ano_tabla_genetica, NEW.linea, NEW.tipo_linea,
    NEW.codigo_guia_genetica, NEW.linea_genetica_id, NEW.tecnico, NEW.mixtas, NEW.peso_mixto,
    NEW.aves_encasetadas, NEW.edad_inicial, NEW.lote_erp, NEW.estado_traslado,
    NEW.pais_id, NEW.pais_nombre, NEW.empresa_nombre,
    now_ts, aves_h, aves_m,
    NEW.lote_postura_levante_id, aves_h, aves_m, aves_h, aves_m,
    NEW.company_id, uid, 'Produccion', 'Produccion', NEW.edad, 'Abierta',
    NEW.company_id, uid, now_ts, NEW.updated_by_user_id, now_ts, NULL
  );

  -- Marcar levante como cerrado
  UPDATE public.lote_postura_levante
  SET estado_cierre = 'Cerrado', updated_by_user_id = NEW.updated_by_user_id, updated_at = now_ts
  WHERE lote_postura_levante_id = NEW.lote_postura_levante_id;

  RETURN NEW;
END;
$function$
");

            // ---- trg_lotes_sync_lote_postura_levante ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.trg_lotes_sync_lote_postura_levante()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    IF TG_OP = 'INSERT' THEN
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
        ) VALUES (
            NEW.lote_nombre, NEW.granja_id, NEW.nucleo_id, NEW.galpon_id, NEW.regional, NEW.fecha_encaset,
            NEW.hembras_l, NEW.machos_l, NEW.peso_inicial_h, NEW.peso_inicial_m, NEW.unif_h, NEW.unif_m,
            NEW.mort_caja_h, NEW.mort_caja_m, NEW.raza, NEW.ano_tabla_genetica, NEW.linea, NEW.tipo_linea,
            NEW.codigo_guia_genetica, NEW.linea_genetica_id, NEW.tecnico, NEW.mixtas, NEW.peso_mixto,
            NEW.aves_encasetadas, NEW.edad_inicial, NEW.lote_erp, NEW.estado_traslado,
            NEW.pais_id, NEW.pais_nombre, NEW.empresa_nombre,
            NEW.lote_id, NEW.lote_padre_id,
            NEW.hembras_l, NEW.machos_l, NEW.hembras_l, NEW.machos_l,
            NEW.company_id, NEW.created_by_user_id, NEW.fase, NEW.fase,
            COALESCE(NEW.edad_inicial, CASE WHEN NEW.fecha_encaset IS NOT NULL THEN GREATEST(0, (CURRENT_DATE - (NEW.fecha_encaset AT TIME ZONE 'utc')::date) / 7) ELSE 0 END),
            'Abierto',
            NEW.company_id, NEW.created_by_user_id, COALESCE(NEW.created_at, NOW() AT TIME ZONE 'utc'),
            NEW.updated_by_user_id, NEW.updated_at, NEW.deleted_at
        );
        RETURN NEW;
    ELSIF TG_OP = 'UPDATE' THEN
        UPDATE public.lote_postura_levante SET
            lote_nombre         = NEW.lote_nombre,
            granja_id           = NEW.granja_id,
            nucleo_id           = NEW.nucleo_id,
            galpon_id           = NEW.galpon_id,
            regional            = NEW.regional,
            fecha_encaset       = NEW.fecha_encaset,
            hembras_l           = NEW.hembras_l,
            machos_l            = NEW.machos_l,
            peso_inicial_h      = NEW.peso_inicial_h,
            peso_inicial_m      = NEW.peso_inicial_m,
            unif_h              = NEW.unif_h,
            unif_m              = NEW.unif_m,
            mort_caja_h         = NEW.mort_caja_h,
            mort_caja_m         = NEW.mort_caja_m,
            raza                = NEW.raza,
            ano_tabla_genetica  = NEW.ano_tabla_genetica,
            linea               = NEW.linea,
            tipo_linea          = NEW.tipo_linea,
            codigo_guia_genetica= NEW.codigo_guia_genetica,
            linea_genetica_id   = NEW.linea_genetica_id,
            tecnico             = NEW.tecnico,
            mixtas              = NEW.mixtas,
            peso_mixto          = NEW.peso_mixto,
            aves_encasetadas    = NEW.aves_encasetadas,
            edad_inicial        = NEW.edad_inicial,
            lote_erp            = NEW.lote_erp,
            estado_traslado     = NEW.estado_traslado,
            pais_id             = NEW.pais_id,
            pais_nombre         = NEW.pais_nombre,
            empresa_nombre      = NEW.empresa_nombre,
            lote_padre_id       = NEW.lote_padre_id,
            aves_h_inicial      = NEW.hembras_l,
            aves_m_inicial      = NEW.machos_l,
            aves_h_actual       = NEW.hembras_l,
            aves_m_actual       = NEW.machos_l,
            empresa_id          = NEW.company_id,
            usuario_id          = COALESCE(NEW.updated_by_user_id, NEW.created_by_user_id),
            estado              = NEW.fase,
            etapa               = NEW.fase,
            edad                = COALESCE(NEW.edad_inicial, CASE WHEN NEW.fecha_encaset IS NOT NULL THEN GREATEST(0, (CURRENT_DATE - (NEW.fecha_encaset AT TIME ZONE 'utc')::date) / 7) ELSE 0 END),
            updated_by_user_id  = NEW.updated_by_user_id,
            updated_at          = COALESCE(NEW.updated_at, NOW() AT TIME ZONE 'utc'),
            deleted_at          = NEW.deleted_at
        WHERE lote_id = NEW.lote_id;

        -- Si no existía registro (ej: lote creado antes del trigger), crearlo
        IF NOT FOUND THEN
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
            ) VALUES (
                NEW.lote_nombre, NEW.granja_id, NEW.nucleo_id, NEW.galpon_id, NEW.regional, NEW.fecha_encaset,
                NEW.hembras_l, NEW.machos_l, NEW.peso_inicial_h, NEW.peso_inicial_m, NEW.unif_h, NEW.unif_m,
                NEW.mort_caja_h, NEW.mort_caja_m, NEW.raza, NEW.ano_tabla_genetica, NEW.linea, NEW.tipo_linea,
                NEW.codigo_guia_genetica, NEW.linea_genetica_id, NEW.tecnico, NEW.mixtas, NEW.peso_mixto,
                NEW.aves_encasetadas, NEW.edad_inicial, NEW.lote_erp, NEW.estado_traslado,
                NEW.pais_id, NEW.pais_nombre, NEW.empresa_nombre,
                NEW.lote_id, NEW.lote_padre_id,
                NEW.hembras_l, NEW.machos_l, NEW.hembras_l, NEW.machos_l,
                NEW.company_id, COALESCE(NEW.updated_by_user_id, NEW.created_by_user_id), NEW.fase, NEW.fase,
                COALESCE(NEW.edad_inicial, CASE WHEN NEW.fecha_encaset IS NOT NULL THEN GREATEST(0, (CURRENT_DATE - (NEW.fecha_encaset AT TIME ZONE 'utc')::date) / 7) ELSE 0 END), 'Abierto',
                NEW.company_id, NEW.created_by_user_id, COALESCE(NEW.created_at, NOW() AT TIME ZONE 'utc'),
                NEW.updated_by_user_id, COALESCE(NEW.updated_at, NOW() AT TIME ZONE 'utc'), NEW.deleted_at
            );
        END IF;
        RETURN NEW;
    END IF;
    RETURN NULL;
END;
$function$
");

            // ===================== VISTAS (CREATE OR REPLACE VIEW) =====================
            // ---- vw_seguimiento_pollo_engorde ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW public.vw_seguimiento_pollo_engorde AS
 WITH RECURSIVE hist_base AS (
         SELECT h.id AS hid,
            h.lote_ave_engorde_id,
            h.tipo_evento,
            h.created_at,
            TRIM(BOTH FROM (COALESCE(h.referencia, ''::character varying)::text || ' '::text) || COALESCE(h.numero_documento, ''::character varying)::text) AS ref_full,
            COALESCE(
                CASE
                    WHEN lower(TRIM(BOTH FROM (COALESCE(h.referencia, ''::character varying)::text || ' '::text) || COALESCE(h.numero_documento, ''::character varying)::text)) ~ 'seguimiento\s+aves\s+engorde\s+#\d+\s+(\d{4}-\d{2}-\d{2})'::text THEN ""substring""(lower(TRIM(BOTH FROM (COALESCE(h.referencia, ''::character varying)::text || ' '::text) || COALESCE(h.numero_documento, ''::character varying)::text)), 'seguimiento\s+aves\s+engorde\s+#\d+\s+(\d{4}-\d{2}-\d{2})'::text)::date
                    ELSE NULL::date
                END,
                CASE
                    WHEN h.tipo_evento::text = 'INV_CONSUMO'::text AND TRIM(BOTH FROM (COALESCE(h.referencia, ''::character varying)::text || ' '::text) || COALESCE(h.numero_documento, ''::character varying)::text) ~ '(\d{4}-\d{2}-\d{2})'::text THEN ""substring""(TRIM(BOTH FROM (COALESCE(h.referencia, ''::character varying)::text || ' '::text) || COALESCE(h.numero_documento, ''::character varying)::text), '(\d{4}-\d{2}-\d{2})'::text)::date
                    ELSE NULL::date
                END, h.fecha_operacion) AS ymd_efe,
                CASE
                    WHEN h.anulado THEN NULL::numeric
                    WHEN h.tipo_evento::text = 'INV_INGRESO'::text AND COALESCE(h.cantidad_kg, 0::numeric) <> 0::numeric THEN h.cantidad_kg::numeric
                    WHEN h.tipo_evento::text = 'INV_TRASLADO_ENTRADA'::text AND COALESCE(h.cantidad_kg, 0::numeric) <> 0::numeric THEN h.cantidad_kg::numeric
                    WHEN h.tipo_evento::text = 'INV_TRASLADO_SALIDA'::text AND COALESCE(h.cantidad_kg, 0::numeric) <> 0::numeric THEN - abs(h.cantidad_kg::numeric)
                    WHEN h.tipo_evento::text = 'INV_OTRO'::text AND lower(TRIM(BOTH FROM COALESCE(h.movement_type_original, ''::character varying))) = 'ajustestock'::text THEN h.cantidad_kg::numeric
                    WHEN h.tipo_evento::text = 'INV_OTRO'::text AND lower(TRIM(BOTH FROM COALESCE(h.movement_type_original, ''::character varying))) = 'eliminacionstock'::text AND COALESCE(h.cantidad_kg, 0::numeric) <> 0::numeric THEN - abs(h.cantidad_kg::numeric)
                    ELSE NULL::numeric
                END AS delta_kg,
                CASE h.tipo_evento
                    WHEN 'INV_INGRESO'::text THEN 0
                    WHEN 'INV_TRASLADO_ENTRADA'::text THEN 1
                    WHEN 'INV_TRASLADO_SALIDA'::text THEN 2
                    WHEN 'INV_OTRO'::text THEN 2
                    ELSE 99
                END AS ord_hist,
            (EXTRACT(epoch FROM h.created_at) * 1000::numeric)::bigint AS tie_h_ms
           FROM lote_registro_historico_unificado h
          WHERE NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
        ), first_seg_f AS (
         SELECT s.lote_ave_engorde_id,
            min(s.fecha::date) AS d0
           FROM seguimiento_diario_aves_engorde s
          GROUP BY s.lote_ave_engorde_id
        ), hist_opening AS (
         SELECT hb.lote_ave_engorde_id,
            0 AS phase,
            hb.ymd_efe,
            0 AS ord_sort,
            hb.tie_h_ms AS tie,
            NULL::bigint AS seg_id,
            hb.delta_kg
           FROM hist_base hb
             JOIN first_seg_f f_1 ON f_1.lote_ave_engorde_id = hb.lote_ave_engorde_id
          WHERE hb.delta_kg IS NOT NULL AND hb.ymd_efe < f_1.d0
        ), hist_main AS (
         SELECT hb.lote_ave_engorde_id,
            1 AS phase,
            hb.ymd_efe,
            hb.ord_hist AS ord_sort,
            hb.tie_h_ms AS tie,
            NULL::bigint AS seg_id,
            hb.delta_kg
           FROM hist_base hb
             JOIN first_seg_f f_1 ON f_1.lote_ave_engorde_id = hb.lote_ave_engorde_id
             JOIN lote_ave_engorde la ON la.lote_ave_engorde_id = hb.lote_ave_engorde_id
          WHERE hb.delta_kg IS NOT NULL AND hb.ymd_efe >= f_1.d0 AND (la.fecha_encaset IS NULL OR hb.ymd_efe >= la.fecha_encaset::date)
        ), seg_events AS (
         SELECT s.lote_ave_engorde_id,
            1 AS phase,
            s.fecha::date AS ymd_efe,
            3 AS ord_sort,
            (EXTRACT(epoch FROM ((s.fecha::date + '12:00:00'::interval) AT TIME ZONE 'UTC'::text)) * 1000::numeric)::bigint AS tie,
            s.id AS seg_id,
            - (COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric)) AS delta_kg
           FROM seguimiento_diario_aves_engorde s
        ), events_union AS (
         SELECT hist_opening.lote_ave_engorde_id,
            hist_opening.phase,
            hist_opening.ymd_efe,
            hist_opening.ord_sort,
            hist_opening.tie,
            hist_opening.seg_id,
            hist_opening.delta_kg
           FROM hist_opening
        UNION ALL
         SELECT hist_main.lote_ave_engorde_id,
            hist_main.phase,
            hist_main.ymd_efe,
            hist_main.ord_sort,
            hist_main.tie,
            hist_main.seg_id,
            hist_main.delta_kg
           FROM hist_main
        UNION ALL
         SELECT seg_events.lote_ave_engorde_id,
            seg_events.phase,
            seg_events.ymd_efe,
            seg_events.ord_sort,
            seg_events.tie,
            seg_events.seg_id,
            seg_events.delta_kg
           FROM seg_events
        ), events_ordered AS (
         SELECT eu.lote_ave_engorde_id,
            eu.phase,
            eu.ymd_efe,
            eu.ord_sort,
            eu.tie,
            eu.seg_id,
            eu.delta_kg,
            row_number() OVER (PARTITION BY eu.lote_ave_engorde_id ORDER BY eu.phase, eu.ymd_efe, eu.ord_sort, eu.tie, (COALESCE(eu.seg_id, 0::bigint))) AS seq
           FROM events_union eu
        ), rec AS (
         SELECT eo.lote_ave_engorde_id,
            eo.seq,
            eo.seg_id,
            eo.delta_kg,
            GREATEST(0::numeric, eo.delta_kg) AS bal
           FROM events_ordered eo
          WHERE eo.seq = 1
        UNION ALL
         SELECT eo.lote_ave_engorde_id,
            eo.seq,
            eo.seg_id,
            eo.delta_kg,
            GREATEST(0::numeric, r.bal + eo.delta_kg) AS bal
           FROM rec r
             JOIN events_ordered eo ON eo.lote_ave_engorde_id = r.lote_ave_engorde_id AND eo.seq = (r.seq + 1)
        ), saldo_ui AS (
         SELECT r.seg_id,
            r.bal AS saldo_alimento_kg_calculado
           FROM rec r
          WHERE r.seg_id IS NOT NULL
        ), lote AS (
         SELECT l.lote_ave_engorde_id,
            l.lote_nombre,
            l.fecha_encaset,
            l.granja_id,
            fa.name AS granja_nombre,
            fa.company_id,
            cp.name AS company_nombre,
            l.galpon_id,
            gp.galpon_nombre,
            l.nucleo_id,
            nu.nucleo_nombre,
            GREATEST(0,
                CASE
                    WHEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0)) > 0 THEN COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0)
                    ELSE COALESCE(l.aves_encasetadas, 0)
                END)::bigint AS aves_iniciales,
            COALESCE(l.hembras_l, 0)::bigint AS aves_iniciales_hembras,
            COALESCE(l.machos_l, 0)::bigint AS aves_iniciales_machos
           FROM lote_ave_engorde l
             LEFT JOIN farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
             LEFT JOIN companies cp ON cp.id = fa.company_id
             LEFT JOIN nucleos nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
             LEFT JOIN galpones gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
        ), hist_por_dia AS (
         SELECT h.lote_ave_engorde_id,
            h.fecha_operacion AS dia,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'INV_INGRESO'::text AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0::numeric)
                    ELSE 0::numeric
                END) AS ingreso_kg,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'INV_TRASLADO_ENTRADA'::text AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0::numeric)
                    ELSE 0::numeric
                END) AS traslado_entrada_kg,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'INV_TRASLADO_SALIDA'::text AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0::numeric)
                    ELSE 0::numeric
                END) AS traslado_salida_kg,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'INV_CONSUMO'::text AND NOT h.anulado THEN COALESCE(h.cantidad_kg, 0::numeric)
                    ELSE 0::numeric
                END) AS consumo_bodega_kg,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'VENTA_AVES'::text AND NOT h.anulado THEN COALESCE(h.cantidad_hembras, 0)
                    ELSE 0
                END) AS venta_hembras,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'VENTA_AVES'::text AND NOT h.anulado THEN COALESCE(h.cantidad_machos, 0)
                    ELSE 0
                END) AS venta_machos,
            sum(
                CASE
                    WHEN h.tipo_evento::text = 'VENTA_AVES'::text AND NOT h.anulado THEN COALESCE(h.cantidad_mixtas, 0)
                    ELSE 0
                END) AS venta_mixtas,
            string_agg(DISTINCT NULLIF(TRIM(BOTH FROM COALESCE(h.numero_documento, h.referencia, ''::character varying)), ''::text), ', '::text) FILTER (WHERE TRIM(BOTH FROM COALESCE(h.numero_documento, h.referencia, ''::character varying)) <> ''::text) AS documentos_hist
           FROM lote_registro_historico_unificado h
          WHERE NOT h.anulado AND h.lote_ave_engorde_id IS NOT NULL
          GROUP BY h.lote_ave_engorde_id, h.fecha_operacion
        ), base AS (
         SELECT s.id AS seguimiento_id,
            s.lote_ave_engorde_id,
            s.fecha::date AS fecha_registro,
            l.lote_nombre,
            l.fecha_encaset,
            l.granja_id,
            l.granja_nombre,
            l.company_id,
            cp.name AS company_nombre,
            l.galpon_id,
            l.galpon_nombre,
            l.nucleo_id,
            l.nucleo_nombre,
            l.aves_iniciales,
            l.aves_iniciales_hembras,
            l.aves_iniciales_machos,
            GREATEST(0, s.fecha::date - l.fecha_encaset::date) AS edad_dias_vida,
            LEAST(8, GREATEST(1, ceil((GREATEST(0, s.fecha::date - l.fecha_encaset::date) + 1)::numeric / 7.0)::integer)) AS semana_ui,
            COALESCE(s.mortalidad_hembras, 0) AS mortalidad_hembras,
            COALESCE(s.mortalidad_machos, 0) AS mortalidad_machos,
            COALESCE(s.sel_h, 0) AS seleccion_hembras,
            COALESCE(s.sel_m, 0) AS seleccion_machos,
            COALESCE(s.error_sexaje_hembras, 0) AS error_sexaje_hembras,
            COALESCE(s.error_sexaje_machos, 0) AS error_sexaje_machos,
            COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) AS total_mort_sel_dia,
            COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_todas_dia,
            COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.error_sexaje_hembras, 0) AS perdidas_hembras_dia,
            COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_m, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_machos_dia,
            s.tipo_alimento,
                CASE
                    WHEN upper(COALESCE(s.tipo_alimento, ''::character varying)::text) ~~ '%PRE%'::text THEN 'PRE'::text
                    WHEN upper(COALESCE(s.tipo_alimento, ''::character varying)::text) ~~ '%INI%'::text THEN 'INI'::text
                    WHEN upper(COALESCE(s.tipo_alimento, ''::character varying)::text) ~~ '%ENG%'::text THEN 'ENG'::text
                    WHEN upper(COALESCE(s.tipo_alimento, ''::character varying)::text) ~~ '%FIN%'::text THEN 'FIN-D'::text
                    WHEN COALESCE(s.tipo_alimento, ''::character varying)::text = ''::text THEN '—'::text
                    ELSE ""left""(s.tipo_alimento::text, 8)
                END AS tipo_alimento_corto,
            COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric) AS consumo_real_dia_kg,
            s.consumo_kg_hembras,
            s.consumo_kg_machos,
            s.consumo_agua_diario,
            s.peso_prom_hembras,
            s.peso_prom_machos,
            s.observaciones,
            s.saldo_alimento_kg AS saldo_alimento_kg_bd,
            su.saldo_alimento_kg_calculado,
            s.metadata,
            s.items_adicionales,
            h.ingreso_kg,
            h.traslado_entrada_kg,
            h.traslado_salida_kg,
            h.consumo_bodega_kg,
            h.venta_hembras,
            h.venta_machos,
            h.venta_mixtas,
            h.documentos_hist
           FROM seguimiento_diario_aves_engorde s
             JOIN lote l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
             LEFT JOIN hist_por_dia h ON h.lote_ave_engorde_id = s.lote_ave_engorde_id AND h.dia = s.fecha::date
             LEFT JOIN companies cp ON cp.id = l.company_id
             LEFT JOIN saldo_ui su ON su.seg_id = s.id
        ), con_acum AS (
         SELECT b.seguimiento_id,
            b.lote_ave_engorde_id,
            b.fecha_registro,
            b.lote_nombre,
            b.fecha_encaset,
            b.granja_id,
            b.granja_nombre,
            b.company_id,
            b.company_nombre,
            b.galpon_id,
            b.galpon_nombre,
            b.nucleo_id,
            b.nucleo_nombre,
            b.aves_iniciales,
            b.aves_iniciales_hembras,
            b.aves_iniciales_machos,
            b.edad_dias_vida,
            b.semana_ui,
            b.mortalidad_hembras,
            b.mortalidad_machos,
            b.seleccion_hembras,
            b.seleccion_machos,
            b.error_sexaje_hembras,
            b.error_sexaje_machos,
            b.total_mort_sel_dia,
            b.perdidas_todas_dia,
            b.perdidas_hembras_dia,
            b.perdidas_machos_dia,
            b.tipo_alimento,
            b.tipo_alimento_corto,
            b.consumo_real_dia_kg,
            b.consumo_kg_hembras,
            b.consumo_kg_machos,
            b.consumo_agua_diario,
            b.peso_prom_hembras,
            b.peso_prom_machos,
            b.observaciones,
            b.saldo_alimento_kg_bd,
            b.saldo_alimento_kg_calculado,
            b.metadata,
            b.items_adicionales,
            b.ingreso_kg,
            b.traslado_entrada_kg,
            b.traslado_salida_kg,
            b.consumo_bodega_kg,
            b.venta_hembras,
            b.venta_machos,
            b.venta_mixtas,
            b.documentos_hist,
            sum(b.perdidas_todas_dia) OVER (PARTITION BY b.lote_ave_engorde_id ORDER BY b.fecha_registro, b.seguimiento_id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS acum_perdidas_todas,
            sum(b.consumo_real_dia_kg) OVER (PARTITION BY b.lote_ave_engorde_id ORDER BY b.fecha_registro, b.seguimiento_id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS consumo_acumulado_kg,
            sum(b.perdidas_hembras_dia) OVER (PARTITION BY b.lote_ave_engorde_id ORDER BY b.fecha_registro, b.seguimiento_id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS acum_perdidas_hembras,
            sum(b.perdidas_machos_dia) OVER (PARTITION BY b.lote_ave_engorde_id ORDER BY b.fecha_registro, b.seguimiento_id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS acum_perdidas_machos
           FROM base b
        ), final AS (
         SELECT c.seguimiento_id,
            c.lote_ave_engorde_id,
            c.fecha_registro,
            c.lote_nombre,
            c.fecha_encaset,
            c.granja_id,
            c.granja_nombre,
            c.company_id,
            c.company_nombre,
            c.galpon_id,
            c.galpon_nombre,
            c.nucleo_id,
            c.nucleo_nombre,
            c.aves_iniciales,
            c.aves_iniciales_hembras,
            c.aves_iniciales_machos,
            c.edad_dias_vida,
            c.semana_ui,
            c.mortalidad_hembras,
            c.mortalidad_machos,
            c.seleccion_hembras,
            c.seleccion_machos,
            c.error_sexaje_hembras,
            c.error_sexaje_machos,
            c.total_mort_sel_dia,
            c.perdidas_todas_dia,
            c.perdidas_hembras_dia,
            c.perdidas_machos_dia,
            c.tipo_alimento,
            c.tipo_alimento_corto,
            c.consumo_real_dia_kg,
            c.consumo_kg_hembras,
            c.consumo_kg_machos,
            c.consumo_agua_diario,
            c.peso_prom_hembras,
            c.peso_prom_machos,
            c.observaciones,
            c.saldo_alimento_kg_bd,
            c.saldo_alimento_kg_calculado,
            c.metadata,
            c.items_adicionales,
            c.ingreso_kg,
            c.traslado_entrada_kg,
            c.traslado_salida_kg,
            c.consumo_bodega_kg,
            c.venta_hembras,
            c.venta_machos,
            c.venta_mixtas,
            c.documentos_hist,
            c.acum_perdidas_todas,
            c.consumo_acumulado_kg,
            c.acum_perdidas_hembras,
            c.acum_perdidas_machos,
            GREATEST(0::bigint, c.aves_iniciales - c.acum_perdidas_todas) AS saldo_aves_vivas_fin_dia,
            GREATEST(0::bigint, c.aves_iniciales - c.acum_perdidas_todas + c.perdidas_todas_dia)::numeric AS saldo_aves_inicio_dia,
            GREATEST(0::bigint, c.aves_iniciales_hembras - c.acum_perdidas_hembras) AS saldo_aves_vivas_hembras,
            GREATEST(0::bigint, c.aves_iniciales_machos - c.acum_perdidas_machos) AS saldo_aves_vivas_machos
           FROM con_acum c
        )
 SELECT seguimiento_id,
    lote_ave_engorde_id,
    lote_nombre,
    company_id,
    company_nombre,
    granja_id,
    granja_nombre,
    galpon_id,
    galpon_nombre,
    nucleo_id,
    nucleo_nombre,
    to_char(fecha_registro::timestamp with time zone, 'DD/MM/YYYY'::text) AS fecha_dmy,
    fecha_registro,
    semana_ui AS semana,
    edad_dias_vida,
    to_char(fecha_registro::timestamp with time zone, 'Dy, DD Mon'::text) AS dia_calendario_corto,
    mortalidad_hembras,
    mortalidad_machos,
    seleccion_hembras,
    seleccion_machos,
    total_mort_sel_dia AS total_mort_mas_sel_dia,
    error_sexaje_hembras,
    error_sexaje_machos,
    venta_hembras AS despacho_hembras_hist,
    venta_machos AS despacho_machos_hist,
    venta_mixtas AS despacho_mixtas_hist,
    trim_scale(saldo_alimento_kg_bd) AS saldo_alimento_kg_bd,
    trim_scale(saldo_alimento_kg_calculado) AS saldo_alimento_kg_calculado,
    saldo_aves_vivas_fin_dia AS saldo_aves_vivas,
    saldo_aves_vivas_hembras,
    saldo_aves_vivas_machos,
    tipo_alimento,
    tipo_alimento_corto,
        CASE
            WHEN COALESCE(ingreso_kg, 0::numeric) > 0::numeric THEN to_char(ingreso_kg, 'FM9999999999990.999'::text) || ' kg'::text
            ELSE NULL::text
        END AS ingreso_alimento_texto_hist,
        CASE
            WHEN COALESCE(traslado_entrada_kg, 0::numeric) = 0::numeric AND COALESCE(traslado_salida_kg, 0::numeric) = 0::numeric THEN NULL::text
            ELSE concat_ws(' · '::text,
            CASE
                WHEN COALESCE(traslado_entrada_kg, 0::numeric) > 0::numeric THEN ('Entrada '::text || to_char(traslado_entrada_kg, 'FM9999999999990.999'::text)) || ' kg'::text
                ELSE NULL::text
            END,
            CASE
                WHEN COALESCE(traslado_salida_kg, 0::numeric) > 0::numeric THEN ('Salida '::text || to_char(traslado_salida_kg, 'FM9999999999990.999'::text)) || ' kg'::text
                ELSE NULL::text
            END)
        END AS traslado_texto_hist,
    COALESCE(documentos_hist, ''::text) AS documento_hist,
    metadata ->> 'ingresoAlimento'::text AS metadata_ingreso_alimento,
    metadata ->> 'traslado'::text AS metadata_traslado,
    metadata ->> 'documento'::text AS metadata_documento,
    trim_scale(consumo_kg_hembras::numeric) AS consumo_kg_hembras,
    trim_scale(consumo_kg_machos::numeric) AS consumo_kg_machos,
    trim_scale(consumo_real_dia_kg) AS consumo_real_dia_kg,
    trim_scale(consumo_acumulado_kg) AS consumo_acumulado_kg,
    trim_scale(consumo_bodega_kg) AS consumo_bodega_kg,
    trim_scale(consumo_agua_diario::numeric) AS consumo_agua_diario,
    trim_scale(
        CASE
            WHEN saldo_aves_inicio_dia > 0::numeric THEN round(100.0 * total_mort_sel_dia::numeric / saldo_aves_inicio_dia, 2)
            WHEN total_mort_sel_dia > 0 THEN 100::numeric
            ELSE NULL::numeric
        END) AS pct_perdidas_dia,
    trim_scale(peso_prom_hembras::numeric) AS peso_prom_hembras,
    trim_scale(peso_prom_machos::numeric) AS peso_prom_machos,
    observaciones,
    metadata,
    items_adicionales
   FROM final f;
");

            // ---- vw_indicadores_diarios_engorde ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW public.vw_indicadores_diarios_engorde AS
 WITH lote_filtrado AS (
         SELECT l.lote_ave_engorde_id,
            l.company_id,
            COALESCE(co.name, l.empresa_nombre) AS empresa_nombre,
            l.lote_nombre,
            l.granja_id,
            fa.name AS granja_nombre,
            l.galpon_id,
            gp.galpon_nombre,
            l.nucleo_id,
            nu.nucleo_nombre,
            l.fecha_encaset,
            TRIM(BOTH FROM l.raza) AS raza,
            l.ano_tabla_genetica,
            l.peso_mixto,
            l.peso_inicial_h,
            l.peso_inicial_m,
                CASE
                    WHEN COALESCE(l.aves_encasetadas, 0) > 0 THEN l.aves_encasetadas::bigint
                    WHEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0)) > 0 THEN (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0))::bigint
                    ELSE 0::bigint
                END AS aves_iniciales
           FROM lote_ave_engorde l
             LEFT JOIN companies co ON co.id = l.company_id
             LEFT JOIN farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
             LEFT JOIN nucleos nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
             LEFT JOIN galpones gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
          WHERE l.deleted_at IS NULL
        ), seg_agregado AS (
         SELECT s.lote_ave_engorde_id,
            s.fecha::date AS fecha_registro,
            sum(COALESCE(s.mortalidad_hembras, 0)) AS sum_mort_h,
            sum(COALESCE(s.mortalidad_machos, 0)) AS sum_mort_m,
            sum(COALESCE(s.sel_h, 0)) AS sum_sel_h,
            sum(COALESCE(s.sel_m, 0)) AS sum_sel_m,
            sum(COALESCE(s.error_sexaje_hembras, 0)) AS sum_err_h,
            sum(COALESCE(s.error_sexaje_machos, 0)) AS sum_err_m,
            sum(COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric)) AS consumo_kg_dia
           FROM seguimiento_diario_aves_engorde s
             JOIN lote_filtrado l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
          GROUP BY s.lote_ave_engorde_id, (s.fecha::date)
        ), seg_peso_ultimo AS (
         SELECT DISTINCT ON (s.lote_ave_engorde_id, (s.fecha::date)) s.lote_ave_engorde_id,
            s.fecha::date AS fecha_registro,
            s.peso_prom_hembras AS peso_h,
            s.peso_prom_machos AS peso_m
           FROM seguimiento_diario_aves_engorde s
             JOIN lote_filtrado l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
          ORDER BY s.lote_ave_engorde_id, (s.fecha::date), s.id DESC
        ), dia_base AS (
         SELECT l.company_id,
            l.empresa_nombre,
            l.lote_ave_engorde_id,
            l.lote_nombre,
            l.granja_id,
            l.granja_nombre,
            l.galpon_id,
            l.galpon_nombre,
            l.nucleo_id,
            l.nucleo_nombre,
            l.raza,
            l.ano_tabla_genetica,
            a.fecha_registro,
            GREATEST(0, a.fecha_registro - l.fecha_encaset::date) AS dia_vida,
            l.aves_iniciales,
                CASE
                    WHEN l.peso_mixto IS NOT NULL AND l.peso_mixto > 0::double precision THEN l.peso_mixto::numeric
                    WHEN COALESCE(l.peso_inicial_h, 0::double precision) > 0::double precision AND COALESCE(l.peso_inicial_m, 0::double precision) > 0::double precision THEN ((l.peso_inicial_h + l.peso_inicial_m) / 2.0::double precision)::numeric
                    ELSE COALESCE(l.peso_inicial_h, l.peso_inicial_m, 0::double precision)::numeric
                END AS peso_inicial_mixto_g,
            a.sum_mort_h + a.sum_mort_m + a.sum_sel_h + a.sum_sel_m + a.sum_err_h + a.sum_err_m AS perdidas_dia,
            a.sum_mort_h + a.sum_mort_m + a.sum_sel_h + a.sum_sel_m AS mort_sel_dia,
            a.consumo_kg_dia,
                CASE
                    WHEN COALESCE(p.peso_h, 0::double precision) > 0::double precision AND COALESCE(p.peso_m, 0::double precision) > 0::double precision THEN ((p.peso_h + p.peso_m) / 2.0::double precision)::numeric
                    ELSE COALESCE(NULLIF(p.peso_h, 0::double precision), NULLIF(p.peso_m, 0::double precision), 0::double precision)::numeric
                END AS peso_mixto_dia_g
           FROM seg_agregado a
             JOIN lote_filtrado l ON l.lote_ave_engorde_id = a.lote_ave_engorde_id
             JOIN seg_peso_ultimo p ON p.lote_ave_engorde_id = a.lote_ave_engorde_id AND p.fecha_registro = a.fecha_registro
          WHERE l.fecha_encaset IS NOT NULL
        ), con_aves AS (
         SELECT d.company_id,
            d.empresa_nombre,
            d.lote_ave_engorde_id,
            d.lote_nombre,
            d.granja_id,
            d.granja_nombre,
            d.galpon_id,
            d.galpon_nombre,
            d.nucleo_id,
            d.nucleo_nombre,
            d.raza,
            d.ano_tabla_genetica,
            d.fecha_registro,
            d.dia_vida,
            d.aves_iniciales,
            d.peso_inicial_mixto_g,
            d.perdidas_dia,
            d.mort_sel_dia,
            d.consumo_kg_dia,
            d.peso_mixto_dia_g,
            COALESCE(sum(d.perdidas_dia) OVER (PARTITION BY d.lote_ave_engorde_id ORDER BY d.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0::numeric)::bigint AS perdidas_acum_prev,
            sum(d.perdidas_dia) OVER (PARTITION BY d.lote_ave_engorde_id ORDER BY d.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)::bigint AS perdidas_acum_total
           FROM dia_base d
        ), con_aves2 AS (
         SELECT c.company_id,
            c.empresa_nombre,
            c.lote_ave_engorde_id,
            c.lote_nombre,
            c.granja_id,
            c.granja_nombre,
            c.galpon_id,
            c.galpon_nombre,
            c.nucleo_id,
            c.nucleo_nombre,
            c.raza,
            c.ano_tabla_genetica,
            c.fecha_registro,
            c.dia_vida,
            c.aves_iniciales,
            c.peso_inicial_mixto_g,
            c.perdidas_dia,
            c.mort_sel_dia,
            c.consumo_kg_dia,
            c.peso_mixto_dia_g,
            c.perdidas_acum_prev,
            c.perdidas_acum_total,
            GREATEST(0::bigint, c.aves_iniciales - c.perdidas_acum_prev) AS aves_inicio_dia,
            GREATEST(0::bigint, c.aves_iniciales - c.perdidas_acum_total) AS aves_fin_dia
           FROM con_aves c
        ), con_guia AS (
         SELECT c.company_id,
            c.empresa_nombre,
            c.lote_ave_engorde_id,
            c.lote_nombre,
            c.granja_id,
            c.granja_nombre,
            c.galpon_id,
            c.galpon_nombre,
            c.nucleo_id,
            c.nucleo_nombre,
            c.raza,
            c.ano_tabla_genetica,
            c.fecha_registro,
            c.dia_vida,
            c.aves_iniciales,
            c.peso_inicial_mixto_g,
            c.perdidas_dia,
            c.mort_sel_dia,
            c.consumo_kg_dia,
            c.peso_mixto_dia_g,
            c.perdidas_acum_prev,
            c.perdidas_acum_total,
            c.aves_inicio_dia,
            c.aves_fin_dia,
            gh.id AS guia_genetica_ecuador_header_id,
            gd.peso_corporal_g::numeric AS peso_tabla_g,
            gd.ganancia_diaria_g::numeric AS ganancia_diaria_tabla_g,
            gd.cantidad_alimento_diario_g::numeric AS consumo_diario_tabla_g,
            gd.alimento_acumulado_g::numeric AS alimento_acum_tabla_g,
            gd.ca::numeric AS ca_tabla,
            gd.mortalidad_seleccion_diaria::numeric AS mort_sel_tabla_pct
           FROM con_aves2 c
             LEFT JOIN guia_genetica_ecuador_header gh ON gh.company_id = c.company_id AND TRIM(BOTH FROM lower(gh.raza::text)) = TRIM(BOTH FROM lower(COALESCE(c.raza, ''::text))) AND gh.anio_guia = c.ano_tabla_genetica AND c.ano_tabla_genetica IS NOT NULL AND TRIM(BOTH FROM COALESCE(c.raza, ''::text)) <> ''::text AND gh.estado::text = 'active'::text AND gh.deleted_at IS NULL
             LEFT JOIN LATERAL ( SELECT d.id,
                    d.guia_genetica_ecuador_header_id,
                    d.sexo,
                    d.dia,
                    d.peso_corporal_g,
                    d.ganancia_diaria_g,
                    d.promedio_ganancia_diaria_g,
                    d.cantidad_alimento_diario_g,
                    d.alimento_acumulado_g,
                    d.ca,
                    d.mortalidad_seleccion_diaria,
                    d.company_id,
                    d.created_by_user_id,
                    d.created_at,
                    d.updated_by_user_id,
                    d.updated_at,
                    d.deleted_at
                   FROM guia_genetica_ecuador_detalle d
                  WHERE d.guia_genetica_ecuador_header_id = gh.id AND lower(TRIM(BOTH FROM d.sexo)) = 'mixto'::text AND d.deleted_at IS NULL AND d.dia <= c.dia_vida
                  ORDER BY d.dia DESC
                 LIMIT 1) gd ON true
        ), con_calc AS (
         SELECT g.company_id,
            g.empresa_nombre,
            g.lote_ave_engorde_id,
            g.lote_nombre,
            g.granja_id,
            g.granja_nombre,
            g.galpon_id,
            g.galpon_nombre,
            g.nucleo_id,
            g.nucleo_nombre,
            g.raza,
            g.ano_tabla_genetica,
            g.fecha_registro,
            g.dia_vida,
            g.aves_iniciales,
            g.peso_inicial_mixto_g,
            g.perdidas_dia,
            g.mort_sel_dia,
            g.consumo_kg_dia,
            g.peso_mixto_dia_g,
            g.perdidas_acum_prev,
            g.perdidas_acum_total,
            g.aves_inicio_dia,
            g.aves_fin_dia,
            g.guia_genetica_ecuador_header_id,
            g.peso_tabla_g,
            g.ganancia_diaria_tabla_g,
            g.consumo_diario_tabla_g,
            g.alimento_acum_tabla_g,
            g.ca_tabla,
            g.mort_sel_tabla_pct,
                CASE
                    WHEN g.aves_inicio_dia > 0 THEN g.consumo_kg_dia * 1000.0 / g.aves_inicio_dia::numeric
                    ELSE 0::numeric
                END AS consumo_diario_real_g,
            lag(g.peso_mixto_dia_g) OVER (PARTITION BY g.lote_ave_engorde_id ORDER BY g.fecha_registro) AS peso_mixto_dia_prev,
            sum(
                CASE
                    WHEN g.aves_inicio_dia > 0 THEN g.consumo_kg_dia * 1000.0 / g.aves_inicio_dia::numeric
                    ELSE 0::numeric
                END) OVER (PARTITION BY g.lote_ave_engorde_id ORDER BY g.fecha_registro ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS alimento_acum_real_g
           FROM con_guia g
        ), final AS (
         SELECT c.company_id,
            c.empresa_nombre,
            c.lote_ave_engorde_id,
            c.lote_nombre,
            c.granja_id,
            c.granja_nombre,
            c.galpon_id,
            c.galpon_nombre,
            c.nucleo_id,
            c.nucleo_nombre,
            c.raza,
            c.ano_tabla_genetica,
            c.guia_genetica_ecuador_header_id,
            c.fecha_registro,
            c.dia_vida,
            c.aves_iniciales,
            c.aves_inicio_dia,
            c.aves_fin_dia,
            c.peso_inicial_mixto_g,
            c.peso_mixto_dia_g AS peso_real_g,
            c.peso_tabla_g,
                CASE
                    WHEN c.peso_mixto_dia_g > 0::numeric AND c.peso_mixto_dia_prev IS NOT NULL THEN c.peso_mixto_dia_g - c.peso_mixto_dia_prev
                    WHEN c.peso_mixto_dia_g > 0::numeric AND c.peso_mixto_dia_prev IS NULL THEN c.peso_mixto_dia_g - c.peso_inicial_mixto_g
                    ELSE NULL::numeric
                END AS ganancia_diaria_real_g,
            c.ganancia_diaria_tabla_g,
            c.consumo_diario_real_g,
            c.consumo_diario_tabla_g,
            c.alimento_acum_real_g,
            c.alimento_acum_tabla_g,
                CASE
                    WHEN c.peso_mixto_dia_g > 0::numeric AND c.alimento_acum_real_g > 0::numeric THEN c.alimento_acum_real_g / NULLIF(c.peso_mixto_dia_g, 0::numeric)
                    ELSE NULL::numeric
                END AS ca_real,
            c.ca_tabla,
                CASE
                    WHEN c.aves_inicio_dia > 0 THEN c.mort_sel_dia::numeric * 100.0 / c.aves_inicio_dia::numeric
                    ELSE 0::numeric
                END AS mort_sel_real_pct,
            c.mort_sel_tabla_pct,
                CASE
                    WHEN c.peso_tabla_g > 0::numeric AND c.peso_mixto_dia_g > 0::numeric THEN (c.peso_mixto_dia_g - c.peso_tabla_g) / NULLIF(c.peso_tabla_g, 0::numeric) * 100.0
                    ELSE 0::numeric
                END AS dif_peso_vs_tabla_pct,
                CASE
                    WHEN c.aves_iniciales > 0 THEN c.perdidas_acum_total::numeric * 100.0 / c.aves_iniciales::numeric
                    ELSE 0::numeric
                END AS mort_acum_pct
           FROM con_calc c
        )
 SELECT company_id,
    empresa_nombre,
    lote_ave_engorde_id,
    lote_nombre,
    granja_id,
    granja_nombre,
    galpon_id,
    galpon_nombre,
    nucleo_id,
    nucleo_nombre,
    raza,
    ano_tabla_genetica,
    guia_genetica_ecuador_header_id,
    to_char(fecha_registro::timestamp with time zone, 'YYYY-MM-DD'::text) AS fecha_ymd,
    fecha_registro,
    dia_vida,
    aves_iniciales,
    aves_inicio_dia,
    aves_fin_dia,
    trim_scale(peso_inicial_mixto_g) AS peso_inicial_mixto_g,
    trim_scale(peso_real_g) AS peso_real_g,
    trim_scale(peso_tabla_g) AS peso_tabla_g,
    trim_scale(ganancia_diaria_real_g) AS ganancia_diaria_real_g,
    trim_scale(ganancia_diaria_tabla_g) AS ganancia_diaria_tabla_g,
    trim_scale(consumo_diario_real_g) AS consumo_diario_real_g,
    trim_scale(consumo_diario_tabla_g) AS consumo_diario_tabla_g,
    trim_scale(alimento_acum_real_g) AS alimento_acum_real_g,
    trim_scale(alimento_acum_tabla_g) AS alimento_acum_tabla_g,
    trim_scale(ca_real) AS ca_real,
    trim_scale(ca_tabla) AS ca_tabla,
    trim_scale(mort_sel_real_pct) AS mort_sel_real_pct,
    trim_scale(mort_sel_tabla_pct) AS mort_sel_tabla_pct,
    trim_scale(dif_peso_vs_tabla_pct) AS dif_peso_vs_tabla_pct,
    trim_scale(mort_acum_pct) AS mort_acum_pct
   FROM final f;
");

            // ---- vw_liquidacion_ecuador_pollo_engorde ----
            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW public.vw_liquidacion_ecuador_pollo_engorde AS
 WITH params AS (
         SELECT 2.7 AS peso_ajuste,
            4.5 AS divisor_ajuste
        ), seg_padre AS (
         SELECT s.lote_ave_engorde_id,
            sum(COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)) AS sum_mort,
            sum(COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)) AS sum_sel,
            sum(COALESCE(s.consumo_kg_hembras, 0::numeric) + COALESCE(s.consumo_kg_machos, 0::numeric)) AS consumo_kg
           FROM seguimiento_diario_aves_engorde s
          GROUP BY s.lote_ave_engorde_id
        ), mov_salida AS (
         SELECT m.lote_ave_engorde_origen_id AS lote_ave_engorde_id,
            sum(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0)) AS aves_sacrificadas,
            sum(
                CASE
                    WHEN m.peso_bruto IS NOT NULL AND m.peso_tara IS NOT NULL THEN m.peso_bruto::numeric - m.peso_tara::numeric
                    ELSE 0::numeric
                END) AS kg_carne,
            avg(m.edad_aves::numeric) FILTER (WHERE m.edad_aves IS NOT NULL) AS edad_promedio,
            max(m.fecha_movimiento) AS fecha_ultimo_despacho
           FROM movimiento_pollo_engorde m
          WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.lote_ave_engorde_origen_id IS NOT NULL AND (m.tipo_movimiento::text = ANY (ARRAY['Venta'::character varying::text, 'Despacho'::character varying::text, 'Retiro'::character varying::text]))
          GROUP BY m.lote_ave_engorde_origen_id
        ), mov_traslado_rep AS (
         SELECT m.lote_ave_engorde_origen_id AS lote_ave_engorde_id,
            sum(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0)) AS aves_trasladadas_rep
           FROM movimiento_pollo_engorde m
          WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.tipo_movimiento::text = 'Traslado'::text AND m.lote_ave_engorde_origen_id IS NOT NULL AND m.lote_reproductora_ave_engorde_destino_id IS NOT NULL
          GROUP BY m.lote_ave_engorde_origen_id
        ), rep_base AS (
         SELECT r.id AS lote_reproductora_id,
            r.lote_ave_engorde_id,
                CASE
                    WHEN (COALESCE(r.aves_inicio_hembras, 0) + COALESCE(r.aves_inicio_machos, 0) + COALESCE(r.mixtas, 0)) > 0 THEN COALESCE(r.aves_inicio_hembras, 0) + COALESCE(r.aves_inicio_machos, 0) + COALESCE(r.mixtas, 0)
                    ELSE COALESCE(r.h, 0) + COALESCE(r.m, 0) + COALESCE(r.mixtas, 0)
                END::bigint AS encaset_rep
           FROM lote_reproductora_ave_engorde r
        ), rep_seg AS (
         SELECT s.lote_reproductora_ave_engorde_id,
            sum(COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)) AS mort_sel_rep
           FROM seguimiento_diario_lote_reproductora_aves_engorde s
          GROUP BY s.lote_reproductora_ave_engorde_id
        ), rep_mov AS (
         SELECT m.lote_reproductora_ave_engorde_origen_id AS lote_reproductora_id,
            sum(COALESCE(m.cantidad_hembras, 0) + COALESCE(m.cantidad_machos, 0) + COALESCE(m.cantidad_mixtas, 0)) AS ventas_rep
           FROM movimiento_pollo_engorde m
          WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.lote_reproductora_ave_engorde_origen_id IS NOT NULL AND (m.tipo_movimiento::text = ANY (ARRAY['Venta'::character varying::text, 'Despacho'::character varying::text, 'Retiro'::character varying::text]))
          GROUP BY m.lote_reproductora_ave_engorde_origen_id
        ), rep_tiene_aves AS (
         SELECT rb.lote_ave_engorde_id,
            bool_or(GREATEST(0::bigint, rb.encaset_rep - COALESCE(rs.mort_sel_rep, 0::bigint) - COALESCE(rm.ventas_rep, 0::bigint)) > 0) AS alguna_rep_con_aves_positivas
           FROM rep_base rb
             LEFT JOIN rep_seg rs ON rs.lote_reproductora_ave_engorde_id = rb.lote_reproductora_id
             LEFT JOIN rep_mov rm ON rm.lote_reproductora_id = rb.lote_reproductora_id
          GROUP BY rb.lote_ave_engorde_id
        ), rep_counts AS (
         SELECT r.lote_ave_engorde_id,
            count(*)::integer AS cnt_rep
           FROM lote_reproductora_ave_engorde r
          GROUP BY r.lote_ave_engorde_id
        ), ult_seg_padre AS (
         SELECT DISTINCT ON (s.lote_ave_engorde_id) s.lote_ave_engorde_id,
            s.fecha::date AS ultima_fecha_seg
           FROM seguimiento_diario_aves_engorde s
          ORDER BY s.lote_ave_engorde_id, s.fecha DESC, s.id DESC
        ), ult_mov_cualquier AS (
         SELECT DISTINCT ON (m.lote_ave_engorde_origen_id) m.lote_ave_engorde_origen_id AS lote_ave_engorde_id,
            m.fecha_movimiento AS ultima_fecha_mov
           FROM movimiento_pollo_engorde m
          WHERE m.estado::text IS DISTINCT FROM 'Cancelado'::text AND m.deleted_at IS NULL AND m.lote_ave_engorde_origen_id IS NOT NULL
          ORDER BY m.lote_ave_engorde_origen_id, m.fecha_movimiento DESC, m.id DESC
        ), base AS (
         SELECT l.lote_ave_engorde_id,
            l.company_id,
            COALESCE(c.name, l.empresa_nombre) AS empresa_nombre,
            l.granja_id,
            fa.name AS granja_nombre,
            l.nucleo_id,
            nu.nucleo_nombre,
            l.galpon_id,
            gp.galpon_nombre,
            l.lote_nombre,
            l.fecha_encaset::date AS fecha_encaset,
            l.estado_operativo_lote,
            l.liquidado_at,
            COALESCE(l.aves_encasetadas, 0)::bigint AS aves_encasetadas_raw,
                CASE
                    WHEN COALESCE(l.aves_encasetadas, 0) > 0 THEN l.aves_encasetadas::bigint
                    ELSE (COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0))::bigint
                END AS aves_encasetadas,
            COALESCE(sp.sum_mort, 0::bigint) + COALESCE(sp.sum_sel, 0::bigint) AS mort_sel_padre,
            COALESCE(sp.consumo_kg, 0::numeric) AS consumo_total_kg,
            COALESCE(ms.aves_sacrificadas, 0::bigint) AS aves_sacrificadas,
            COALESCE(ms.kg_carne, 0::numeric) AS kg_carne_pollos,
            COALESCE(ms.edad_promedio, 0::numeric) AS edad_promedio_mov,
            ms.fecha_ultimo_despacho,
            COALESCE(mt.aves_trasladadas_rep, 0::bigint) AS aves_trasladadas_rep,
            COALESCE(rc.cnt_rep, 0) AS cantidad_lotes_reproductores,
                CASE
                    WHEN COALESCE(rc.cnt_rep, 0) = 0 THEN false
                    ELSE NOT COALESCE(rt.alguna_rep_con_aves_positivas, false)
                END AS todos_reproductores_sin_aves,
            us.ultima_fecha_seg,
            umc.ultima_fecha_mov,
                CASE
                    WHEN l.galpon_id IS NOT NULL AND TRIM(BOTH FROM l.galpon_id) <> ''::text THEN
                    CASE
                        WHEN gp.ancho IS NOT NULL AND gp.largo IS NOT NULL AND TRIM(BOTH FROM gp.ancho::text) <> ''::text AND TRIM(BOTH FROM gp.largo::text) <> ''::text AND TRIM(BOTH FROM gp.ancho::text) ~ '^[0-9]+([.,][0-9]+)?$'::text AND TRIM(BOTH FROM gp.largo::text) ~ '^[0-9]+([.,][0-9]+)?$'::text THEN replace(replace(TRIM(BOTH FROM gp.ancho::text), ','::text, '.'::text), ' '::text, ''::text)::numeric * replace(replace(TRIM(BOTH FROM gp.largo::text), ','::text, '.'::text), ' '::text, ''::text)::numeric
                        ELSE NULL::numeric
                    END
                    ELSE ( SELECT COALESCE(sum(
                            CASE
                                WHEN g.ancho IS NOT NULL AND g.largo IS NOT NULL AND TRIM(BOTH FROM g.ancho::text) <> ''::text AND TRIM(BOTH FROM g.largo::text) <> ''::text AND TRIM(BOTH FROM g.ancho::text) ~ '^[0-9]+([.,][0-9]+)?$'::text AND TRIM(BOTH FROM g.largo::text) ~ '^[0-9]+([.,][0-9]+)?$'::text THEN replace(replace(TRIM(BOTH FROM g.ancho::text), ','::text, '.'::text), ' '::text, ''::text)::numeric * replace(replace(TRIM(BOTH FROM g.largo::text), ','::text, '.'::text), ' '::text, ''::text)::numeric
                                ELSE 0::numeric
                            END), 0::numeric) AS ""coalesce""
                       FROM galpones g
                      WHERE g.granja_id = l.granja_id AND g.deleted_at IS NULL)
                END AS metros_cuadrados
           FROM lote_ave_engorde l
             LEFT JOIN companies c ON c.id = l.company_id
             LEFT JOIN farms fa ON fa.id = l.granja_id AND fa.deleted_at IS NULL
             LEFT JOIN nucleos nu ON nu.nucleo_id::text = l.nucleo_id::text AND nu.granja_id = l.granja_id
             LEFT JOIN galpones gp ON gp.galpon_id::text = l.galpon_id::text AND gp.granja_id = l.granja_id
             LEFT JOIN seg_padre sp ON sp.lote_ave_engorde_id = l.lote_ave_engorde_id
             LEFT JOIN mov_salida ms ON ms.lote_ave_engorde_id = l.lote_ave_engorde_id
             LEFT JOIN mov_traslado_rep mt ON mt.lote_ave_engorde_id = l.lote_ave_engorde_id
             LEFT JOIN rep_tiene_aves rt ON rt.lote_ave_engorde_id = l.lote_ave_engorde_id
             LEFT JOIN rep_counts rc ON rc.lote_ave_engorde_id = l.lote_ave_engorde_id
             LEFT JOIN ult_seg_padre us ON us.lote_ave_engorde_id = l.lote_ave_engorde_id
             LEFT JOIN ult_mov_cualquier umc ON umc.lote_ave_engorde_id = l.lote_ave_engorde_id
          WHERE l.deleted_at IS NULL
        ), calc AS (
         SELECT b.lote_ave_engorde_id,
            b.company_id,
            b.empresa_nombre,
            b.granja_id,
            b.granja_nombre,
            b.nucleo_id,
            b.nucleo_nombre,
            b.galpon_id,
            b.galpon_nombre,
            b.lote_nombre,
            b.fecha_encaset,
            b.estado_operativo_lote,
            b.liquidado_at,
            b.aves_encasetadas_raw,
            b.aves_encasetadas,
            b.mort_sel_padre,
            b.consumo_total_kg,
            b.aves_sacrificadas,
            b.kg_carne_pollos,
            b.edad_promedio_mov,
            b.fecha_ultimo_despacho,
            b.aves_trasladadas_rep,
            b.cantidad_lotes_reproductores,
            b.todos_reproductores_sin_aves,
            b.ultima_fecha_seg,
            b.ultima_fecha_mov,
            b.metros_cuadrados,
            b.mort_sel_padre AS mortalidad_unidades,
            GREATEST(0::bigint, b.aves_encasetadas - b.mort_sel_padre - b.aves_sacrificadas - b.aves_trasladadas_rep) AS aves_actuales,
                CASE
                    WHEN b.aves_encasetadas > 0 THEN b.mort_sel_padre::numeric / b.aves_encasetadas::numeric * 100::numeric
                    ELSE 0::numeric
                END AS mortalidad_porcentaje,
                CASE
                    WHEN b.aves_encasetadas > 0 THEN (b.aves_encasetadas - b.mort_sel_padre)::numeric / b.aves_encasetadas::numeric * 100::numeric
                    ELSE 0::numeric
                END AS supervivencia_porcentaje,
                CASE
                    WHEN b.aves_sacrificadas > 0 THEN b.consumo_total_kg / b.aves_sacrificadas::numeric * 1000::numeric
                    ELSE 0::numeric
                END AS consumo_ave_gramos,
                CASE
                    WHEN b.aves_sacrificadas > 0 THEN b.kg_carne_pollos / b.aves_sacrificadas::numeric
                    ELSE 0::numeric
                END AS peso_promedio_kilos,
                CASE
                    WHEN b.kg_carne_pollos > 0::numeric THEN b.consumo_total_kg / b.kg_carne_pollos
                    ELSE 0::numeric
                END AS conversion,
            ( SELECT p.peso_ajuste
                   FROM params p) AS peso_ajuste_variable,
            ( SELECT p.divisor_ajuste
                   FROM params p) AS divisor_ajuste_variable
           FROM base b
        ), calc2 AS (
         SELECT c.lote_ave_engorde_id,
            c.company_id,
            c.empresa_nombre,
            c.granja_id,
            c.granja_nombre,
            c.nucleo_id,
            c.nucleo_nombre,
            c.galpon_id,
            c.galpon_nombre,
            c.lote_nombre,
            c.fecha_encaset,
            c.estado_operativo_lote,
            c.liquidado_at,
            c.aves_encasetadas_raw,
            c.aves_encasetadas,
            c.mort_sel_padre,
            c.consumo_total_kg,
            c.aves_sacrificadas,
            c.kg_carne_pollos,
            c.edad_promedio_mov,
            c.fecha_ultimo_despacho,
            c.aves_trasladadas_rep,
            c.cantidad_lotes_reproductores,
            c.todos_reproductores_sin_aves,
            c.ultima_fecha_seg,
            c.ultima_fecha_mov,
            c.metros_cuadrados,
            c.mortalidad_unidades,
            c.aves_actuales,
            c.mortalidad_porcentaje,
            c.supervivencia_porcentaje,
            c.consumo_ave_gramos,
            c.peso_promedio_kilos,
            c.conversion,
            c.peso_ajuste_variable,
            c.divisor_ajuste_variable,
                CASE
                    WHEN GREATEST(0::bigint, c.aves_actuales) = 0 THEN true
                    ELSE false
                END AS cerrado_por_aves_cero,
                CASE
                    WHEN GREATEST(0::bigint, c.aves_actuales) > 0 AND c.aves_sacrificadas = 0 AND COALESCE(c.mort_sel_padre, 0::bigint) = 0 AND c.todos_reproductores_sin_aves AND c.cantidad_lotes_reproductores > 0 THEN true
                    ELSE false
                END AS cerrado_por_reproductores_vendidos,
                CASE
                    WHEN c.conversion > 0::numeric THEN c.conversion + (c.peso_ajuste_variable - c.peso_promedio_kilos) / c.divisor_ajuste_variable
                    ELSE 0::numeric
                END AS conversion_ajustada2700
           FROM calc c
        )
 SELECT company_id,
    empresa_nombre,
    granja_id,
    granja_nombre,
    nucleo_id,
    nucleo_nombre,
    galpon_id,
    galpon_nombre,
    lote_ave_engorde_id,
    lote_nombre,
    fecha_encaset,
    estado_operativo_lote,
    liquidado_at,
    cantidad_lotes_reproductores,
    aves_encasetadas,
    aves_sacrificadas,
    mortalidad_unidades AS mortalidad,
    mortalidad_porcentaje,
    supervivencia_porcentaje,
    consumo_total_kg AS consumo_total_alimento_kg,
    consumo_ave_gramos,
    kg_carne_pollos,
    peso_promedio_kilos,
    conversion,
    conversion_ajustada2700,
    peso_ajuste_variable,
    divisor_ajuste_variable,
    edad_promedio_mov AS edad_promedio,
    COALESCE(metros_cuadrados, 0::numeric) AS metros_cuadrados,
        CASE
            WHEN COALESCE(metros_cuadrados, 0::numeric) > 0::numeric THEN aves_sacrificadas::numeric / metros_cuadrados
            ELSE 0::numeric
        END AS aves_por_metro_cuadrado,
        CASE
            WHEN COALESCE(metros_cuadrados, 0::numeric) > 0::numeric THEN kg_carne_pollos / metros_cuadrados
            ELSE 0::numeric
        END AS kg_por_metro_cuadrado,
        CASE
            WHEN conversion > 0::numeric THEN peso_promedio_kilos / conversion * 100::numeric
            ELSE 0::numeric
        END AS eficiencia_americana,
        CASE
            WHEN conversion > 0::numeric AND edad_promedio_mov > 0::numeric THEN peso_promedio_kilos * supervivencia_porcentaje / (edad_promedio_mov * conversion) * 100::numeric
            ELSE 0::numeric
        END AS eficiencia_europea,
        CASE
            WHEN conversion > 0::numeric THEN peso_promedio_kilos / conversion / conversion * 100::numeric
            ELSE 0::numeric
        END AS indice_productividad,
        CASE
            WHEN edad_promedio_mov > 0::numeric THEN peso_promedio_kilos / edad_promedio_mov * 1000::numeric
            ELSE 0::numeric
        END AS ganancia_dia,
    aves_trasladadas_rep,
    aves_actuales,
    aves_actuales > 0 AS tiene_aves,
    cerrado_por_aves_cero OR cerrado_por_reproductores_vendidos AS lote_cerrado_logico,
    cerrado_por_aves_cero,
    cerrado_por_reproductores_vendidos,
    fecha_ultimo_despacho AS fecha_cierre_ultimo_despacho,
        CASE
            WHEN (cerrado_por_aves_cero OR cerrado_por_reproductores_vendidos) AND fecha_ultimo_despacho IS NULL THEN COALESCE(ultima_fecha_seg::timestamp with time zone, ultima_fecha_mov, fecha_encaset::timestamp with time zone)
            ELSE fecha_ultimo_despacho
        END AS fecha_cierre_efectiva
   FROM calc2 c2;
");

            // ===================== TRIGGERS (DROP IF EXISTS + CREATE) =====================
            // ---- tr_espejo_huevo_produccion_aiud on seguimiento_diario_levante_reproductoras ----
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS tr_espejo_huevo_produccion_aiud ON public.seguimiento_diario_levante_reproductoras;");
            migrationBuilder.Sql(@"
CREATE TRIGGER tr_espejo_huevo_produccion_aiud AFTER INSERT OR DELETE OR UPDATE ON public.seguimiento_diario_levante_reproductoras FOR EACH ROW EXECUTE FUNCTION fn_espejo_huevo_produccion_upsert()
");

            // ---- trg_hlp_lote_postura_levante on lote_postura_levante ----
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_hlp_lote_postura_levante ON public.lote_postura_levante;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_hlp_lote_postura_levante AFTER INSERT OR UPDATE ON public.lote_postura_levante FOR EACH ROW EXECUTE FUNCTION trg_historico_lote_postura_levante()
");

            // ---- trg_hlp_lote_postura_produccion on lote_postura_produccion ----
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_hlp_lote_postura_produccion ON public.lote_postura_produccion;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_hlp_lote_postura_produccion AFTER INSERT OR UPDATE ON public.lote_postura_produccion FOR EACH ROW EXECUTE FUNCTION trg_historico_lote_postura_produccion()
");

            // ---- trg_inventario_gestion_movimiento_lote_hist on inventario_gestion_movimiento ----
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_inventario_gestion_movimiento_lote_hist ON public.inventario_gestion_movimiento;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_inventario_gestion_movimiento_lote_hist AFTER INSERT ON public.inventario_gestion_movimiento FOR EACH ROW EXECUTE FUNCTION trg_lote_hist_desde_inventario_gestion()
");

            // ---- trg_lotes_sync_lote_postura_levante on lotes ----
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_lotes_sync_lote_postura_levante ON public.lotes;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_lotes_sync_lote_postura_levante AFTER INSERT OR UPDATE ON public.lotes FOR EACH ROW EXECUTE FUNCTION trg_lotes_sync_lote_postura_levante()
");

            // ---- trg_movimiento_pollo_engorde_lote_hist_anula on movimiento_pollo_engorde ----
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_movimiento_pollo_engorde_lote_hist_anula ON public.movimiento_pollo_engorde;");
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_movimiento_pollo_engorde_lote_hist_anula AFTER UPDATE OF estado, deleted_at ON public.movimiento_pollo_engorde FOR EACH ROW WHEN ((((new.estado)::text = 'Anulado'::text) OR (new.deleted_at IS NOT NULL))) EXECUTE FUNCTION trg_lote_hist_mov_pollo_anulado()
");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Triggers
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS tr_espejo_huevo_produccion_aiud ON public.seguimiento_diario_levante_reproductoras;");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_hlp_lote_postura_levante ON public.lote_postura_levante;");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_hlp_lote_postura_produccion ON public.lote_postura_produccion;");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_inventario_gestion_movimiento_lote_hist ON public.inventario_gestion_movimiento;");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_lotes_sync_lote_postura_levante ON public.lotes;");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_movimiento_pollo_engorde_lote_hist_anula ON public.movimiento_pollo_engorde;");
            // Vistas
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS public.vw_seguimiento_pollo_engorde;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS public.vw_indicadores_diarios_engorde;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS public.vw_liquidacion_ecuador_pollo_engorde;");
            // Funciones
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.weeknum_iso(date);");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.dpr(double precision, integer);");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.fn_acumulado_entradas_alimento(integer, bigint);");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.fn_espejo_huevo_produccion_upsert();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.fn_lote_ave_engorde_id_desde_ubicacion(integer, character varying, character varying);");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.fn_tipo_evento_inventario(character varying);");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.sp_recalcular_seguimiento_levante(text);");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.trg_historico_lote_postura_levante();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.trg_historico_lote_postura_produccion();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.trg_lote_hist_desde_inventario_gestion();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.trg_lote_hist_mov_pollo_anulado();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.trg_lote_postura_levante_cerrar_produccion();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.trg_lotes_sync_lote_postura_levante();");
        }
    }
}
