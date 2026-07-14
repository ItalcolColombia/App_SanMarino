using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMigracionSeguimientoProduccionAvesIncremental : Migration
    {
        // Fix (paridad con FixMigracionSeguimientoLevanteAvesIncremental): fn_migracion_seguimiento_produccion
        // recalculaba aves_h_actual/aves_m_actual desde cero, pisando traslados (TrasladoAvesDesdeSegService,
        // rama Producción↔Producción) y movimientos de aves (MovimientoAvesService) ya aplicados al lote.
        // Ahora el descuento es INCREMENTAL sobre el valor actual, igual semántica que el alta manual
        // (SeguimientoProduccionService.AplicarDescuentoLppAsync). Además, filas "solo traslado" existentes
        // para la misma fecha se completan (merge) en vez de saltear la fila del Excel en silencio, y el
        // matching pasa a ser por lote_id crudo + fecha calendario (las filas de traslado no setean
        // lote_postura_produccion_id, así que el dedup original por ese FK nunca las encontraba).
        // CREATE OR REPLACE → idempotente. Fuente canónica: backend/sql/fn_migracion_seguimiento.sql.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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

    -- Paso 1: completar filas ""solo traslado"" existentes con los datos históricos (merge).
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
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_migracion_seguimiento_produccion(
    p_company_id integer,
    p_usuario    integer,
    p_rows       jsonb
) RETURNS integer
LANGUAGE plpgsql AS $$
DECLARE
    v_insertados integer := 0;
BEGIN
    WITH filas AS (
        SELECT * FROM jsonb_to_recordset(p_rows) AS x(
            lote_id  integer,
            fecha    date,
            mort_h integer, mort_m integer,
            sel_h  integer, sel_m  integer,
            err_h  integer, err_m  integer,
            cons_h numeric, cons_m numeric,
            huevo_tot integer, huevo_inc integer, peso_huevo double precision,
            etapa integer, observaciones text
        )
    ),
    ins AS (
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
        FROM filas f
        JOIN public.lotes l
          ON l.lote_id = f.lote_id AND l.company_id = p_company_id AND l.deleted_at IS NULL AND l.fase = 'Produccion'
        JOIN public.lote_postura_produccion lpp
          ON lpp.lote_id = f.lote_id AND lpp.deleted_at IS NULL
        WHERE NOT EXISTS (
            SELECT 1 FROM public.seguimiento_diario_produccion sd
            WHERE sd.lote_postura_produccion_id = lpp.lote_postura_produccion_id
              AND sd.fecha_registro = f.fecha::timestamptz
        )
        RETURNING 1
    )
    SELECT COUNT(*) INTO v_insertados FROM ins;

    UPDATE public.lote_postura_produccion lpp
    SET aves_h_actual = GREATEST(0, sub.h),
        aves_m_actual = GREATEST(0, sub.m),
        updated_at    = (NOW() AT TIME ZONE 'utc')
    FROM (
        SELECT lpp2.lote_postura_produccion_id,
               (COALESCE(lpp2.aves_h_inicial, lpp2.hembras_iniciales_prod, 0)
                - COALESCE(SUM(sd.mortalidad_hembras),0) - COALESCE(SUM(sd.sel_h),0) - COALESCE(SUM(sd.error_sexaje_hembras),0)) AS h,
               (COALESCE(lpp2.aves_m_inicial, lpp2.machos_iniciales_prod, 0)
                - COALESCE(SUM(sd.mortalidad_machos),0) - COALESCE(SUM(sd.sel_m),0) - COALESCE(SUM(sd.error_sexaje_machos),0)) AS m
        FROM public.lote_postura_produccion lpp2
        LEFT JOIN public.seguimiento_diario_produccion sd
          ON sd.lote_postura_produccion_id = lpp2.lote_postura_produccion_id
        WHERE lpp2.deleted_at IS NULL
          AND lpp2.company_id = p_company_id
          AND lpp2.lote_id IN (SELECT DISTINCT (e->>'lote_id')::int FROM jsonb_array_elements(p_rows) e)
        GROUP BY lpp2.lote_postura_produccion_id, lpp2.aves_h_inicial, lpp2.hembras_iniciales_prod, lpp2.aves_m_inicial, lpp2.machos_iniciales_prod
    ) sub
    WHERE lpp.lote_postura_produccion_id = sub.lote_postura_produccion_id;

    RETURN v_insertados;
END;
$$;
");
        }
    }
}
