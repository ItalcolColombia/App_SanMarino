-- =============================================================================
-- Funciones de migración masiva de SEGUIMIENTOS históricos (Postura).
-- Insertan el estado final (set-based, idempotente por índice único) y recomputan
-- los agregados de aves UNA sola vez al final (no re-disparan efectos incrementales
-- ni tocan inventario de alimento — la carga histórica no asume stock).
-- Construidas contra el esquema REAL post-rename:
--   levante    → seguimiento_diario_levante   (uq: tipo_seguimiento, lote_id, coalesce(rep,''), fecha)
--   produccion → seguimiento_diario_produccion (dedup en fn por lote_postura_produccion_id + fecha_registro)
-- p_rows = jsonb array de filas ya validadas por el backend.
-- =============================================================================

-- ── LEVANTE ──────────────────────────────────────────────────────────────────
-- Fix (aves-fix): el descuento de aves es INCREMENTAL (igual semántica que el alta
-- manual, SeguimientoDiarioService.AplicarDescuentoLevanteAsync) — NO se recalcula
-- aves_h_actual/aves_m_actual desde cero, porque ese campo también lo tocan los
-- traslados entre lotes (TrasladoAvesDesdeSegService) y el módulo Movimiento de
-- Aves (MovimientoAvesService), que no dejan mortalidad/sel/error en esta tabla.
-- Un recálculo total pisaría esos ajustes. Además, si la fecha ya tiene una fila
-- "solo traslado" (es_traslado=true, sin datos manuales) se completa (merge) en
-- vez de saltear la fila del Excel en silencio — mismo criterio que el merge
-- manual ("Feature 13").
CREATE OR REPLACE FUNCTION public.fn_migracion_seguimiento_levante(
    p_company_id integer,
    p_usuario    text,
    p_rows       jsonb
) RETURNS integer
LANGUAGE plpgsql AS $$
DECLARE
    v_actualizados integer := 0;
    v_insertados   integer := 0;
BEGIN
    -- DROP defensivo: permite invocar la función más de una vez en la misma
    -- transacción/sesión (ON COMMIT DROP sólo limpia al cerrar la transacción).
    DROP TABLE IF EXISTS tmp_filas_lev;
    DROP TABLE IF EXISTS tmp_delta_lev;

    CREATE TEMP TABLE tmp_filas_lev ON COMMIT DROP AS
    SELECT * FROM jsonb_to_recordset(p_rows) AS x(
        lote_id       integer,
        fecha         date,
        mort_h integer, mort_m integer,
        sel_h  integer, sel_m  integer,
        err_h  integer, err_m  integer,
        cons_h numeric, cons_m numeric,
        tipo_alimento text,
        peso_h double precision, peso_m double precision,
        unif_h double precision, unif_m double precision,
        observaciones text
    );

    CREATE TEMP TABLE tmp_delta_lev (lote_postura_levante_id integer, h integer, m integer) ON COMMIT DROP;

    -- Paso 1: completar filas "solo traslado" existentes con los datos históricos (merge).
    WITH upd AS (
        UPDATE public.seguimiento_diario_levante sd
        SET mortalidad_hembras   = COALESCE(f.mort_h,0),
            mortalidad_machos    = COALESCE(f.mort_m,0),
            sel_h                = COALESCE(f.sel_h,0),
            sel_m                = COALESCE(f.sel_m,0),
            error_sexaje_hembras = COALESCE(f.err_h,0),
            error_sexaje_machos  = COALESCE(f.err_m,0),
            consumo_kg_hembras   = f.cons_h,
            consumo_kg_machos    = f.cons_m,
            tipo_alimento        = f.tipo_alimento,
            peso_prom_hembras    = f.peso_h,
            peso_prom_machos     = f.peso_m,
            uniformidad_hembras  = f.unif_h,
            uniformidad_machos   = f.unif_m,
            observaciones        = f.observaciones,
            updated_by_user_id   = p_usuario,
            updated_at           = (NOW() AT TIME ZONE 'utc')
        FROM tmp_filas_lev f
        WHERE sd.tipo_seguimiento = 'levante'
          AND sd.lote_id = f.lote_id::text
          AND COALESCE(sd.reproductora_id,'') = ''
          AND sd.fecha::date = f.fecha
          AND sd.es_traslado = true
          AND COALESCE(sd.mortalidad_hembras,0) = 0 AND COALESCE(sd.mortalidad_machos,0) = 0
          AND COALESCE(sd.sel_h,0) = 0 AND COALESCE(sd.sel_m,0) = 0
          AND COALESCE(sd.error_sexaje_hembras,0) = 0 AND COALESCE(sd.error_sexaje_machos,0) = 0
          AND COALESCE(sd.consumo_kg_hembras,0) = 0 AND COALESCE(sd.consumo_kg_machos,0) = 0
        RETURNING sd.lote_postura_levante_id,
                  COALESCE(f.mort_h,0) + COALESCE(f.sel_h,0) + COALESCE(f.err_h,0) AS h,
                  COALESCE(f.mort_m,0) + COALESCE(f.sel_m,0) + COALESCE(f.err_m,0) AS m
    )
    INSERT INTO tmp_delta_lev SELECT lote_postura_levante_id, h, m FROM upd;
    GET DIAGNOSTICS v_actualizados = ROW_COUNT;

    -- Paso 2: insertar filas nuevas (fechas sin ninguna fila previa para el lote).
    WITH ins AS (
        INSERT INTO public.seguimiento_diario_levante (
            tipo_seguimiento, lote_id, lote_id_int, lote_postura_levante_id, fecha,
            mortalidad_hembras, mortalidad_machos, sel_h, sel_m,
            error_sexaje_hembras, error_sexaje_machos,
            consumo_kg_hembras, consumo_kg_machos, tipo_alimento,
            peso_prom_hembras, peso_prom_machos, uniformidad_hembras, uniformidad_machos,
            observaciones, ciclo, created_by_user_id, created_at
        )
        SELECT
            'levante', f.lote_id::text, f.lote_id, lpl.lote_postura_levante_id, f.fecha::timestamptz,
            COALESCE(f.mort_h,0), COALESCE(f.mort_m,0), COALESCE(f.sel_h,0), COALESCE(f.sel_m,0),
            COALESCE(f.err_h,0), COALESCE(f.err_m,0),
            f.cons_h, f.cons_m, f.tipo_alimento,
            f.peso_h, f.peso_m, f.unif_h, f.unif_m,
            f.observaciones, 'Normal', p_usuario, (NOW() AT TIME ZONE 'utc')
        FROM tmp_filas_lev f
        JOIN public.lotes l
          ON l.lote_id = f.lote_id AND l.company_id = p_company_id AND l.deleted_at IS NULL
        JOIN public.lote_postura_levante lpl
          ON lpl.lote_id = f.lote_id AND lpl.deleted_at IS NULL
        WHERE NOT EXISTS (
            SELECT 1 FROM public.seguimiento_diario_levante sd
            WHERE sd.tipo_seguimiento = 'levante'
              AND sd.lote_id = f.lote_id::text
              AND COALESCE(sd.reproductora_id,'') = ''
              AND sd.fecha::date = f.fecha
        )
        RETURNING lote_postura_levante_id,
                  COALESCE(mortalidad_hembras,0) + COALESCE(sel_h,0) + COALESCE(error_sexaje_hembras,0) AS h,
                  COALESCE(mortalidad_machos,0)  + COALESCE(sel_m,0) + COALESCE(error_sexaje_machos,0)  AS m
    )
    INSERT INTO tmp_delta_lev SELECT lote_postura_levante_id, h, m FROM ins;
    GET DIAGNOSTICS v_insertados = ROW_COUNT;

    -- Paso 3: descuento INCREMENTAL sobre el valor actual (no recálculo total) —
    -- conserva cualquier ajuste ya reflejado por traslados o movimientos de aves.
    UPDATE public.lote_postura_levante lpl
    SET aves_h_actual = GREATEST(0, COALESCE(lpl.aves_h_actual, lpl.aves_h_inicial, lpl.hembras_l, 0) - sub.h),
        aves_m_actual = GREATEST(0, COALESCE(lpl.aves_m_actual, lpl.aves_m_inicial, lpl.machos_l, 0) - sub.m),
        updated_at    = (NOW() AT TIME ZONE 'utc')
    FROM (
        SELECT lote_postura_levante_id, SUM(h) AS h, SUM(m) AS m
        FROM tmp_delta_lev
        WHERE lote_postura_levante_id IS NOT NULL
        GROUP BY lote_postura_levante_id
    ) sub
    WHERE lpl.lote_postura_levante_id = sub.lote_postura_levante_id
      AND (sub.h <> 0 OR sub.m <> 0);

    RETURN v_actualizados + v_insertados;
END;
$$;

-- ── PRODUCCIÓN ───────────────────────────────────────────────────────────────
-- Nota de contrato: el módulo de producción almacena el consumo TOTAL en cons_kg_h
-- (cons_kg_m = 0) y tipo_alimento = ''. La migración replica ese contrato para que la
-- data histórica sea idéntica a la creada por el módulo.
-- Fix (aves-fix, paridad con Levante): descuento INCREMENTAL igual semántica que el alta manual
-- (SeguimientoProduccionService.AplicarDescuentoLppAsync) — NO se recalcula aves_h_actual/aves_m_actual
-- desde cero, porque ese campo también lo tocan los traslados entre lotes de Producción
-- (TrasladoAvesDesdeSegService, rama Producción↔Producción) y el módulo Movimiento de Aves
-- (MovimientoAvesService.Postura.cs), que no dejan mortalidad/sel/error en esta tabla. Un
-- recálculo total pisaría esos ajustes. Además, si la fecha ya tiene una fila "solo traslado"
-- (es_traslado=true, sin datos manuales) se completa (merge) en vez de saltear la fila del Excel
-- en silencio — mismo criterio que el merge manual ("Feature 14" en SeguimientoProduccionService).
-- Importante: las filas de traslado NO setean lote_postura_produccion_id (ver
-- TrasladoAvesDesdeSegService), así que el matching es por lote_id crudo + fecha calendario, no
-- por lote_postura_produccion_id (el dedup original por ese FK nunca encontraba esas filas).
CREATE OR REPLACE FUNCTION public.fn_migracion_seguimiento_produccion(
    p_company_id integer,
    p_usuario    integer,
    p_rows       jsonb
) RETURNS integer
LANGUAGE plpgsql AS $$
DECLARE
    v_actualizados integer := 0;
    v_insertados   integer := 0;
BEGIN
    DROP TABLE IF EXISTS tmp_filas_prod;
    DROP TABLE IF EXISTS tmp_delta_prod;

    CREATE TEMP TABLE tmp_filas_prod ON COMMIT DROP AS
    SELECT * FROM jsonb_to_recordset(p_rows) AS x(
        lote_id  integer,
        fecha    date,
        mort_h integer, mort_m integer,
        sel_h  integer, sel_m  integer,
        err_h  integer, err_m  integer,
        cons_h numeric, cons_m numeric,
        huevo_tot integer, huevo_inc integer, peso_huevo double precision,
        etapa integer, observaciones text
    );

    CREATE TEMP TABLE tmp_delta_prod (lote_postura_produccion_id integer, h integer, m integer) ON COMMIT DROP;

    -- Paso 1: completar filas "solo traslado" existentes con los datos históricos (merge).
    WITH upd AS (
        UPDATE public.seguimiento_diario_produccion sd
        SET mortalidad_hembras   = COALESCE(f.mort_h,0),
            mortalidad_machos    = COALESCE(f.mort_m,0),
            sel_h                = COALESCE(f.sel_h,0),
            sel_m                = COALESCE(f.sel_m,0),
            error_sexaje_hembras = COALESCE(f.err_h,0),
            error_sexaje_machos  = COALESCE(f.err_m,0),
            cons_kg_h            = (COALESCE(f.cons_h,0) + COALESCE(f.cons_m,0)),
            cons_kg_m            = 0,
            huevo_tot            = COALESCE(f.huevo_tot,0),
            huevo_inc            = COALESCE(f.huevo_inc,0),
            peso_huevo           = f.peso_huevo,
            etapa                = COALESCE(f.etapa,1),
            observaciones        = f.observaciones,
            updated_by_user_id   = p_usuario,
            updated_at           = (NOW() AT TIME ZONE 'utc')
        FROM tmp_filas_prod f
        JOIN public.lote_postura_produccion lpp2
          ON lpp2.lote_id = f.lote_id AND lpp2.deleted_at IS NULL AND lpp2.company_id = p_company_id
        WHERE sd.lote_id = f.lote_id
          AND sd.fecha_registro::date = f.fecha
          AND sd.es_traslado = true
          AND COALESCE(sd.mortalidad_hembras,0) = 0 AND COALESCE(sd.mortalidad_machos,0) = 0
          AND COALESCE(sd.sel_h,0) = 0 AND COALESCE(sd.sel_m,0) = 0
          AND COALESCE(sd.error_sexaje_hembras,0) = 0 AND COALESCE(sd.error_sexaje_machos,0) = 0
          AND COALESCE(sd.cons_kg_h,0) = 0 AND COALESCE(sd.cons_kg_m,0) = 0
          AND COALESCE(sd.huevo_tot,0) = 0
        RETURNING lpp2.lote_postura_produccion_id,
                  COALESCE(f.mort_h,0) + COALESCE(f.sel_h,0) + COALESCE(f.err_h,0) AS h,
                  COALESCE(f.mort_m,0) + COALESCE(f.sel_m,0) + COALESCE(f.err_m,0) AS m
    )
    INSERT INTO tmp_delta_prod SELECT lote_postura_produccion_id, h, m FROM upd;
    GET DIAGNOSTICS v_actualizados = ROW_COUNT;

    -- Paso 2: insertar filas nuevas (fechas sin ninguna fila previa para el lote).
    WITH ins AS (
        INSERT INTO public.seguimiento_diario_produccion (
            lote_id, lote_postura_produccion_id, fecha_registro,
            mortalidad_hembras, mortalidad_machos, sel_h, sel_m,
            error_sexaje_hembras, error_sexaje_machos,
            cons_kg_h, cons_kg_m, tipo_alimento,
            huevo_tot, huevo_inc, peso_huevo, etapa, observaciones,
            company_id, created_by_user_id, created_at
        )
        SELECT
            f.lote_id, lpp.lote_postura_produccion_id, f.fecha::timestamptz,
            COALESCE(f.mort_h,0), COALESCE(f.mort_m,0), COALESCE(f.sel_h,0), COALESCE(f.sel_m,0),
            COALESCE(f.err_h,0), COALESCE(f.err_m,0),
            (COALESCE(f.cons_h,0) + COALESCE(f.cons_m,0)), 0, '',
            COALESCE(f.huevo_tot,0), COALESCE(f.huevo_inc,0), f.peso_huevo, COALESCE(f.etapa,1), f.observaciones,
            p_company_id, p_usuario, (NOW() AT TIME ZONE 'utc')
        FROM tmp_filas_prod f
        JOIN public.lotes l
          ON l.lote_id = f.lote_id AND l.company_id = p_company_id AND l.deleted_at IS NULL AND l.fase = 'Produccion'
        JOIN public.lote_postura_produccion lpp
          ON lpp.lote_id = f.lote_id AND lpp.deleted_at IS NULL
        WHERE NOT EXISTS (
            SELECT 1 FROM public.seguimiento_diario_produccion sd
            WHERE sd.lote_id = f.lote_id
              AND sd.fecha_registro::date = f.fecha
        )
        RETURNING lote_postura_produccion_id,
                  COALESCE(mortalidad_hembras,0) + COALESCE(sel_h,0) + COALESCE(error_sexaje_hembras,0) AS h,
                  COALESCE(mortalidad_machos,0)  + COALESCE(sel_m,0) + COALESCE(error_sexaje_machos,0)  AS m
    )
    INSERT INTO tmp_delta_prod SELECT lote_postura_produccion_id, h, m FROM ins;
    GET DIAGNOSTICS v_insertados = ROW_COUNT;

    -- Paso 3: descuento INCREMENTAL sobre el valor actual (no recálculo total) —
    -- conserva cualquier ajuste ya reflejado por traslados o movimientos de aves.
    UPDATE public.lote_postura_produccion lpp
    SET aves_h_actual = GREATEST(0, COALESCE(lpp.aves_h_actual, lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0) - sub.h),
        aves_m_actual = GREATEST(0, COALESCE(lpp.aves_m_actual, lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0) - sub.m),
        updated_at    = (NOW() AT TIME ZONE 'utc')
    FROM (
        SELECT lote_postura_produccion_id, SUM(h) AS h, SUM(m) AS m
        FROM tmp_delta_prod
        WHERE lote_postura_produccion_id IS NOT NULL
        GROUP BY lote_postura_produccion_id
    ) sub
    WHERE lpp.lote_postura_produccion_id = sub.lote_postura_produccion_id
      AND (sub.h <> 0 OR sub.m <> 0);

    RETURN v_actualizados + v_insertados;
END;
$$;
