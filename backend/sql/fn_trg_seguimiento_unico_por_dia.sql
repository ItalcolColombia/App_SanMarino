-- =====================================================================================================
-- fn_trg_seguimiento_unico_por_dia — ESPEJO LEGIBLE
--
-- Lo aplica la migración 20260912100000_SeguimientoUnicoPorDiaSigueFlagEmpresa (el vehículo). Correr
-- este archivo a mano no despliega nada en producción.
--
-- «Un seguimiento por lote y por día» en levante y producción, SOLO para las empresas con
-- companies.permite_multiples_seguimientos_diarios = false. Reemplaza a los índices únicos
-- ux_seguimiento_diario_produccion_lote_dia_utc / ux_sdlr_tipo_lote_rep_dia_utc (que tenían horneados
-- los company_id del flag) y a los únicos por instante (que chocaban con dos registros guardados a
-- mediodía UTC). Encender o apagar el flag desde la pantalla de Empresas actúa al instante.
--
-- Plan: fase_de_desarrollo/seguimiento_varios_por_dia_flag_dinamico_plan.md
-- =====================================================================================================

-- ── PRODUCCIÓN ───────────────────────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION public.fn_trg_seguimiento_produccion_unico_por_dia()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
DECLARE
    v_dia date;
BEGIN
    -- Un UPDATE que no mueve la clave no puede crear un duplicado.
    IF TG_OP = 'UPDATE'
       AND NEW.lote_id IS NOT DISTINCT FROM OLD.lote_id
       AND NEW.company_id IS NOT DISTINCT FROM OLD.company_id
       AND (NEW.fecha_registro AT TIME ZONE 'UTC')::date
           IS NOT DISTINCT FROM (OLD.fecha_registro AT TIME ZONE 'UTC')::date THEN
        RETURN NEW;
    END IF;

    IF NEW.lote_id IS NULL OR NEW.fecha_registro IS NULL THEN
        RETURN NEW;
    END IF;

    -- Empresa con el flag: varios registros por día.
    IF NEW.company_id IS NOT NULL AND EXISTS (
        SELECT 1 FROM public.companies c
         WHERE c.id = NEW.company_id AND c.permite_multiples_seguimientos_diarios
    ) THEN
        RETURN NEW;
    END IF;

    v_dia := (NEW.fecha_registro AT TIME ZONE 'UTC')::date;

    -- Serializa dos altas concurrentes del mismo lote+día (lo que antes hacía el índice único).
    PERFORM pg_advisory_xact_lock(
        hashtext('seguimiento_diario_produccion'),
        hashtext(NEW.lote_id::text || '|' || v_dia::text));

    IF EXISTS (
        SELECT 1 FROM public.seguimiento_diario_produccion s
         WHERE s.lote_id = NEW.lote_id
           AND s.fecha_registro >= (v_dia::timestamp AT TIME ZONE 'UTC')
           AND s.fecha_registro <  ((v_dia + 1)::timestamp AT TIME ZONE 'UTC')
           AND s.id IS DISTINCT FROM NEW.id
    ) THEN
        RAISE EXCEPTION 'Ya existe un seguimiento de produccion para el lote % en la fecha %.', NEW.lote_id, v_dia
            USING ERRCODE = 'unique_violation',
                  CONSTRAINT = 'ux_seguimiento_diario_produccion_lote_dia_utc';
    END IF;

    RETURN NEW;
END
$fn$;

-- ── LEVANTE (clave tipo + lote + reproductora + día) ─────────────────────────────────────────────────
-- Solo el tipo 'levante' queda libre con el flag; reproductora y el legacy 'produccion' no cambian.
-- 1090 (Demo, lote 127, 2026-07-11) sigue excluido como en 20260828120000_IndiceUnicoDiaSeguimientos.
CREATE OR REPLACE FUNCTION public.fn_trg_seguimiento_levante_unico_por_dia()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
DECLARE
    v_dia date;
    v_rep text;
BEGIN
    IF NEW.id = 1090 THEN
        RETURN NEW;
    END IF;

    IF TG_OP = 'UPDATE'
       AND NEW.tipo_seguimiento IS NOT DISTINCT FROM OLD.tipo_seguimiento
       AND NEW.lote_id IS NOT DISTINCT FROM OLD.lote_id
       AND COALESCE(NEW.reproductora_id, '') = COALESCE(OLD.reproductora_id, '')
       AND NEW.company_id IS NOT DISTINCT FROM OLD.company_id
       AND (NEW.fecha AT TIME ZONE 'UTC')::date
           IS NOT DISTINCT FROM (OLD.fecha AT TIME ZONE 'UTC')::date THEN
        RETURN NEW;
    END IF;

    IF NEW.tipo_seguimiento IS NULL OR NEW.lote_id IS NULL OR NEW.fecha IS NULL THEN
        RETURN NEW;
    END IF;

    IF NEW.tipo_seguimiento = 'levante' AND NEW.company_id IS NOT NULL AND EXISTS (
        SELECT 1 FROM public.companies c
         WHERE c.id = NEW.company_id AND c.permite_multiples_seguimientos_diarios
    ) THEN
        RETURN NEW;
    END IF;

    v_dia := (NEW.fecha AT TIME ZONE 'UTC')::date;
    v_rep := COALESCE(NEW.reproductora_id, '');

    PERFORM pg_advisory_xact_lock(
        hashtext('seguimiento_diario_levante'),
        hashtext(NEW.tipo_seguimiento || '|' || NEW.lote_id || '|' || v_rep || '|' || v_dia::text));

    IF EXISTS (
        SELECT 1 FROM public.seguimiento_diario_levante s
         WHERE s.tipo_seguimiento = NEW.tipo_seguimiento
           AND s.lote_id = NEW.lote_id
           AND COALESCE(s.reproductora_id, '') = v_rep
           AND s.fecha >= (v_dia::timestamp AT TIME ZONE 'UTC')
           AND s.fecha <  ((v_dia + 1)::timestamp AT TIME ZONE 'UTC')
           AND s.id <> 1090
           AND s.id IS DISTINCT FROM NEW.id
    ) THEN
        RAISE EXCEPTION 'Ya existe un seguimiento % para el lote % en la fecha %.', NEW.tipo_seguimiento, NEW.lote_id, v_dia
            USING ERRCODE = 'unique_violation',
                  CONSTRAINT = 'ux_sdlr_tipo_lote_rep_dia_utc';
    END IF;

    RETURN NEW;
END
$fn$;

DROP TRIGGER IF EXISTS trg_seguimiento_produccion_unico_por_dia ON public.seguimiento_diario_produccion;
CREATE TRIGGER trg_seguimiento_produccion_unico_por_dia
    BEFORE INSERT OR UPDATE ON public.seguimiento_diario_produccion
    FOR EACH ROW EXECUTE FUNCTION public.fn_trg_seguimiento_produccion_unico_por_dia();

DROP TRIGGER IF EXISTS trg_seguimiento_levante_unico_por_dia ON public.seguimiento_diario_levante;
CREATE TRIGGER trg_seguimiento_levante_unico_por_dia
    BEFORE INSERT OR UPDATE ON public.seguimiento_diario_levante
    FOR EACH ROW EXECUTE FUNCTION public.fn_trg_seguimiento_levante_unico_por_dia();
