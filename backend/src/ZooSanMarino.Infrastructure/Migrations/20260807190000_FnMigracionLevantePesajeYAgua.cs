using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FnMigracionLevantePesajeYAgua : Migration
    {
        // Carga masiva de LEVANTE a paridad con la de PRODUCCION. La plantilla de levante no tenia
        // donde recibir el pesaje fino ni el agua, aunque las columnas existen en
        // seguimiento_diario_levante y produccion ya las aceptaba desde 20260728130000:
        //   - cv_hembras / cv_machos: el caso grave. fn_reporte_semanal_levante_extras y
        //     fn_resumen_semanal_ra_pesadas_levante LEEN esas columnas, asi que la columna "C.V.%"
        //     del reporte semanal de levante salia SIEMPRE vacia porque ninguna via de captura la
        //     escribia (el modal tampoco la mapea en la entidad).
        //   - observaciones_pesaje y los 4 de agua (consumo_agua_diario/ph/orp/temperatura), que el
        //     modal de levante si captura y la carga masiva descartaba en silencio.
        // Todo OPCIONAL: un archivo sin estas claves produce exactamente el mismo resultado que
        // antes (las claves ausentes llegan como NULL desde jsonb_to_recordset y el UPDATE del
        // paso 1 usa COALESCE con lo que ya tenia la fila).
        // Firma INTACTA => CREATE OR REPLACE, sin DROP FUNCTION y sin DDL de tablas.
        // Fuente canonica: backend/sql/fn_migracion_seguimiento.sql

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE OR REPLACE FUNCTION public.fn_migracion_seguimiento_levante(
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
        observaciones text,
        cv_h double precision, cv_m double precision, obs_pesaje text,
        agua_diario double precision, agua_ph double precision,
        agua_orp double precision, agua_temp double precision,
        -- ── aditivo ──
        metadata jsonb,
        huevo_tot integer, huevo_inc integer, peso_huevo double precision,
        huevo_limpio integer, huevo_tratado integer, huevo_sucio integer,
        huevo_deforme integer, huevo_blanco integer, huevo_doble_yema integer,
        huevo_piso integer, huevo_pequeno integer, huevo_roto integer,
        huevo_desecho integer, huevo_otro integer
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
            -- Pesaje y agua: COALESCE con lo que ya tenía la fila, mismo criterio aditivo de abajo.
            cv_hembras               = COALESCE(f.cv_h, sd.cv_hembras),
            cv_machos                = COALESCE(f.cv_m, sd.cv_machos),
            observaciones_pesaje     = COALESCE(f.obs_pesaje, sd.observaciones_pesaje),
            consumo_agua_diario      = COALESCE(f.agua_diario, sd.consumo_agua_diario),
            consumo_agua_ph          = COALESCE(f.agua_ph, sd.consumo_agua_ph),
            consumo_agua_orp         = COALESCE(f.agua_orp, sd.consumo_agua_orp),
            consumo_agua_temperatura = COALESCE(f.agua_temp, sd.consumo_agua_temperatura),
            -- Aditivo: COALESCE con lo que ya tenía la fila ⇒ una fila de traslado sin estos datos
            -- (el caso real) queda igual que antes cuando el archivo tampoco los trae.
            metadata             = COALESCE(f.metadata, sd.metadata),
            huevo_tot            = COALESCE(f.huevo_tot, sd.huevo_tot),
            huevo_inc            = COALESCE(f.huevo_inc, sd.huevo_inc),
            peso_huevo           = COALESCE(f.peso_huevo, sd.peso_huevo),
            huevo_limpio         = COALESCE(f.huevo_limpio, sd.huevo_limpio),
            huevo_tratado        = COALESCE(f.huevo_tratado, sd.huevo_tratado),
            huevo_sucio          = COALESCE(f.huevo_sucio, sd.huevo_sucio),
            huevo_deforme        = COALESCE(f.huevo_deforme, sd.huevo_deforme),
            huevo_blanco         = COALESCE(f.huevo_blanco, sd.huevo_blanco),
            huevo_doble_yema     = COALESCE(f.huevo_doble_yema, sd.huevo_doble_yema),
            huevo_piso           = COALESCE(f.huevo_piso, sd.huevo_piso),
            huevo_pequeno        = COALESCE(f.huevo_pequeno, sd.huevo_pequeno),
            huevo_roto           = COALESCE(f.huevo_roto, sd.huevo_roto),
            huevo_desecho        = COALESCE(f.huevo_desecho, sd.huevo_desecho),
            huevo_otro           = COALESCE(f.huevo_otro, sd.huevo_otro),
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
            observaciones, ciclo, created_by_user_id, created_at,
            cv_hembras, cv_machos, observaciones_pesaje,
            consumo_agua_diario, consumo_agua_ph, consumo_agua_orp, consumo_agua_temperatura,
            metadata,
            huevo_tot, huevo_inc, peso_huevo,
            huevo_limpio, huevo_tratado, huevo_sucio, huevo_deforme, huevo_blanco,
            huevo_doble_yema, huevo_piso, huevo_pequeno, huevo_roto, huevo_desecho, huevo_otro
        )
        SELECT
            'levante', f.lote_id::text, f.lote_id, lpl.lote_postura_levante_id, f.fecha::timestamptz,
            COALESCE(f.mort_h,0), COALESCE(f.mort_m,0), COALESCE(f.sel_h,0), COALESCE(f.sel_m,0),
            COALESCE(f.err_h,0), COALESCE(f.err_m,0),
            f.cons_h, f.cons_m, f.tipo_alimento,
            f.peso_h, f.peso_m, f.unif_h, f.unif_m,
            f.observaciones, 'Normal', p_usuario, (NOW() AT TIME ZONE 'utc'),
            f.cv_h, f.cv_m, f.obs_pesaje,
            f.agua_diario, f.agua_ph, f.agua_orp, f.agua_temp,
            f.metadata,
            COALESCE(f.huevo_tot,0), COALESCE(f.huevo_inc,0), f.peso_huevo,
            COALESCE(f.huevo_limpio,0), COALESCE(f.huevo_tratado,0), COALESCE(f.huevo_sucio,0),
            COALESCE(f.huevo_deforme,0), COALESCE(f.huevo_blanco,0), COALESCE(f.huevo_doble_yema,0),
            COALESCE(f.huevo_piso,0), COALESCE(f.huevo_pequeno,0), COALESCE(f.huevo_roto,0),
            COALESCE(f.huevo_desecho,0), COALESCE(f.huevo_otro,0)
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
$$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin vuelta atras: la version previa se recupera desplegando el commit anterior de
            // backend/sql/fn_migracion_seguimiento.sql. Revertir aqui dejaria el espejo .sql y la
            // funcion viva desincronizados, que es justo el modo de falla que ya nos costo caro.
        }
    }
}
