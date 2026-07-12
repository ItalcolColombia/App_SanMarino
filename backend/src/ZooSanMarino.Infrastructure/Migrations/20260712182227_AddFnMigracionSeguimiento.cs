using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFnMigracionSeguimiento : Migration
    {
        // Registra las funciones de migración masiva de seguimientos históricos (Postura).
        // CREATE OR REPLACE → idempotente; se aplican solas en cada deploy.
        // Fuente canónica: backend/sql/fn_migracion_seguimiento.sql.

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.fn_migracion_seguimiento_levante(integer, text, jsonb);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.fn_migracion_seguimiento_produccion(integer, integer, jsonb);");
        }
    }
}
