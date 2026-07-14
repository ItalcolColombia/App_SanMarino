using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMigracionSeguimientoLevanteAvesIncremental : Migration
    {
        // Fix: fn_migracion_seguimiento_levante recalculaba aves_h_actual/aves_m_actual desde
        // cero (aves_h_inicial - Σmort - Σsel - Σerror), pisando traslados (TrasladoAvesDesdeSegService)
        // y movimientos de aves (MovimientoAvesService) ya aplicados al lote. Ahora el descuento es
        // INCREMENTAL sobre el valor actual, igual semántica que el alta manual
        // (SeguimientoDiarioService.AplicarDescuentoLevanteAsync). Además, filas "solo traslado"
        // existentes para la misma fecha se completan (merge) en vez de saltear la fila del Excel
        // en silencio. CREATE OR REPLACE → idempotente. Fuente canónica: backend/sql/fn_migracion_seguimiento.sql.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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

    -- Paso 1: completar filas ""solo traslado"" existentes con los datos históricos (merge).
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
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_migracion_seguimiento_levante(
    p_company_id integer,
    p_usuario    text,
    p_rows       jsonb
) RETURNS integer
LANGUAGE plpgsql AS $$
DECLARE
    v_insertados integer := 0;
BEGIN
    WITH filas AS (
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
        )
    ),
    ins AS (
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
        FROM filas f
        JOIN public.lotes l
          ON l.lote_id = f.lote_id AND l.company_id = p_company_id AND l.deleted_at IS NULL
        JOIN public.lote_postura_levante lpl
          ON lpl.lote_id = f.lote_id AND lpl.deleted_at IS NULL
        WHERE NOT EXISTS (
            SELECT 1 FROM public.seguimiento_diario_levante sd
            WHERE sd.tipo_seguimiento = 'levante'
              AND sd.lote_id = f.lote_id::text
              AND COALESCE(sd.reproductora_id,'') = ''
              AND sd.fecha = f.fecha::timestamptz
        )
        RETURNING 1
    )
    SELECT COUNT(*) INTO v_insertados FROM ins;

    UPDATE public.lote_postura_levante lpl
    SET aves_h_actual = GREATEST(0, sub.h),
        aves_m_actual = GREATEST(0, sub.m),
        updated_at    = (NOW() AT TIME ZONE 'utc')
    FROM (
        SELECT lpl2.lote_postura_levante_id,
               (COALESCE(lpl2.aves_h_inicial, lpl2.hembras_l, 0)
                - COALESCE(SUM(sd.mortalidad_hembras),0) - COALESCE(SUM(sd.sel_h),0) - COALESCE(SUM(sd.error_sexaje_hembras),0)) AS h,
               (COALESCE(lpl2.aves_m_inicial, lpl2.machos_l, 0)
                - COALESCE(SUM(sd.mortalidad_machos),0) - COALESCE(SUM(sd.sel_m),0) - COALESCE(SUM(sd.error_sexaje_machos),0)) AS m
        FROM public.lote_postura_levante lpl2
        LEFT JOIN public.seguimiento_diario_levante sd
          ON sd.tipo_seguimiento = 'levante' AND sd.lote_postura_levante_id = lpl2.lote_postura_levante_id
        WHERE lpl2.deleted_at IS NULL
          AND lpl2.company_id = p_company_id
          AND lpl2.lote_id IN (SELECT DISTINCT (e->>'lote_id')::int FROM jsonb_array_elements(p_rows) e)
        GROUP BY lpl2.lote_postura_levante_id, lpl2.aves_h_inicial, lpl2.hembras_l, lpl2.aves_m_inicial, lpl2.machos_l
    ) sub
    WHERE lpl.lote_postura_levante_id = sub.lote_postura_levante_id;

    RETURN v_insertados;
END;
$$;
");
        }
    }
}
