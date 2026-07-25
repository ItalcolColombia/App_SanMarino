using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seguimiento reproductora engorde: el día del encasetamiento cuenta como DÍA 1 de la
    /// semana de recogida (edad 0 en días de calendario). Antes la fecha mínima era el día
    /// siguiente al encaset (edad 1) y fn_cruce_reproductora_a_engorde solo consolidaba
    /// edades 1..7, por lo que un registro del mismo día del encaset se rechazaba.
    ///   - fn_cruce_reproductora_a_engorde ahora consolida edades 0..7 (la 7 se conserva para
    ///     que los lotes que arrancaron al día siguiente del encaset completen su semana) y
    ///     las aves vivas de la edad d descuentan retiros de edades [0, d).
    ///   - Recalcula los lotes engorde que ya tienen registros CONFIRMADOS en edad 0
    ///     (histórico previo a la validación de jul-2026) para backfillear su día 1.
    /// La validación de fecha [0,7] vive en ReproductoraEngordeCalculos (front+back+carga masiva).
    /// Sin cambios de modelo. Idempotente (CREATE OR REPLACE + recálculo regenerativo).
    /// </summary>
    public partial class CruceReproductoraEngordeEdadCero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FN_EDAD_CERO);
            migrationBuilder.Sql(RECALC_LOTES_CON_EDAD_CERO);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaurar la función previa (solo edades 1..7), quitar los cruces de edad 0 que
            // esa versión nunca regeneraría y recalcular los lotes afectados con la fn vieja.
            migrationBuilder.Sql(FN_PREVIA_1_A_7);
            migrationBuilder.Sql(@"
DELETE FROM seguimiento_diario_aves_engorde
 WHERE origen_cruce
   AND (metadata->>'edad')::int = 0;
");
            migrationBuilder.Sql(RECALC_LOTES_CON_EDAD_CERO);
        }

        // ── Recalculo dirigido: lotes engorde con algún registro reproductora CONFIRMADO en edad 0 ──
        private const string RECALC_LOTES_CON_EDAD_CERO = @"
DO $$
DECLARE
    v_lote int;
BEGIN
    FOR v_lote IN
        SELECT DISTINCT lr.lote_ave_engorde_id
          FROM lote_reproductora_ave_engorde lr
          JOIN seguimiento_diario_lote_reproductora_aves_engorde p
            ON p.lote_reproductora_ave_engorde_id = lr.id
         WHERE p.confirmado = true
           AND lr.fecha_encasetamiento IS NOT NULL
           AND (p.fecha::date - lr.fecha_encasetamiento::date) = 0
    LOOP
        PERFORM fn_cruce_reproductora_a_engorde(v_lote);
    END LOOP;
END $$;
";

        // ── Función NUEVA: consolida edades 0..7 (día 1 de negocio = día del encaset = edad 0) ──
        // Espejo exacto de backend/sql/fn_cruce_reproductora_a_engorde.sql.
        private const string FN_EDAD_CERO = @"
CREATE OR REPLACE FUNCTION fn_cruce_reproductora_a_engorde(p_lote_ave_engorde_id int)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    c_max_dias   constant int := 7;
    v_fecha_enc  date;
    v_n_lotes    int;
    d            int;
    r            record;
    v_fecha_dest date;
BEGIN
    -- Fecha de encasetamiento del lote pollo engorde (padre), para fechar el destino.
    SELECT lae.fecha_encaset::date
      INTO v_fecha_enc
      FROM lote_ave_engorde lae
     WHERE lae.lote_ave_engorde_id = p_lote_ave_engorde_id;

    -- Número de lotes reproductora hijos.
    SELECT COUNT(*)
      INTO v_n_lotes
      FROM lote_reproductora_ave_engorde lr
     WHERE lr.lote_ave_engorde_id = p_lote_ave_engorde_id;

    -- Sin lotes reproductora: limpiar cualquier cruce previo y salir.
    IF v_n_lotes = 0 THEN
        DELETE FROM seguimiento_diario_aves_engorde
         WHERE lote_ave_engorde_id = p_lote_ave_engorde_id
           AND origen_cruce;
        RETURN;
    END IF;

    FOR d IN 0..c_max_dias LOOP
        -- Agregados de la edad d sobre todos los lotes reproductora del padre.
        SELECT
            COUNT(DISTINCT dia.repro_id)                                   AS n_con,
            COALESCE(SUM(dia.aves_m), 0)                                   AS machos,
            COALESCE(SUM(dia.aves_h), 0)                                   AS hembras,
            SUM(dia.consumo_kg_machos)                                     AS consumo_m,
            SUM(dia.consumo_kg_hembras)                                    AS consumo_h,
            SUM(dia.mortalidad_machos)                                     AS mort_m,
            SUM(dia.mortalidad_hembras)                                    AS mort_h,
            SUM(dia.sel_m)                                                 AS sel_m,
            SUM(dia.sel_h)                                                 AS sel_h,
            SUM(dia.error_sexaje_machos)                                   AS err_m,
            SUM(dia.error_sexaje_hembras)                                  AS err_h,
            CASE WHEN SUM(dia.aves_m) > 0
                 THEN SUM(dia.aves_m * dia.peso_prom_machos) / SUM(dia.aves_m)
            END                                                            AS peso_m,
            CASE WHEN SUM(dia.aves_h) > 0
                 THEN SUM(dia.aves_h * dia.peso_prom_hembras) / SUM(dia.aves_h)
            END                                                            AS peso_h,
            MAX(dia.fecha_reg)                                             AS fecha_reg,
            (array_agg(dia.consumo_agua_diario       ORDER BY dia.repro_id))[1]  AS agua_diario,
            (array_agg(dia.consumo_agua_ph           ORDER BY dia.repro_id))[1]  AS agua_ph,
            (array_agg(dia.consumo_agua_orp          ORDER BY dia.repro_id))[1]  AS agua_orp,
            (array_agg(dia.consumo_agua_temperatura  ORDER BY dia.repro_id))[1]  AS agua_temp,
            MAX(dia.tipo_alimento)                                         AS tipo_alimento,
            jsonb_agg(dia.repro_id ORDER BY dia.repro_id)                  AS lotes_json
          INTO r
          FROM (
            SELECT
                lr.id AS repro_id,
                -- aves vivas al inicio de la edad d = inicio - retiros de edades [0, d)
                COALESCE(lr.aves_inicio_machos, lr.m, 0)
                  - COALESCE((
                        SELECT SUM(COALESCE(p.mortalidad_machos,0)
                                 + COALESCE(p.sel_m,0)
                                 + COALESCE(p.error_sexaje_machos,0))
                          FROM seguimiento_diario_lote_reproductora_aves_engorde p
                         WHERE p.lote_reproductora_ave_engorde_id = lr.id
                           AND p.confirmado = true
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) >= 0
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) <  d
                    ), 0)                                                  AS aves_m,
                COALESCE(lr.aves_inicio_hembras, lr.h, 0)
                  - COALESCE((
                        SELECT SUM(COALESCE(p.mortalidad_hembras,0)
                                 + COALESCE(p.sel_h,0)
                                 + COALESCE(p.error_sexaje_hembras,0))
                          FROM seguimiento_diario_lote_reproductora_aves_engorde p
                         WHERE p.lote_reproductora_ave_engorde_id = lr.id
                           AND p.confirmado = true
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) >= 0
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) <  d
                    ), 0)                                                  AS aves_h,
                s.consumo_kg_machos, s.consumo_kg_hembras,
                s.mortalidad_machos, s.mortalidad_hembras,
                s.sel_m, s.sel_h, s.error_sexaje_machos, s.error_sexaje_hembras,
                s.peso_prom_machos, s.peso_prom_hembras,
                s.consumo_agua_diario, s.consumo_agua_ph,
                s.consumo_agua_orp, s.consumo_agua_temperatura,
                s.tipo_alimento,
                s.fecha::date AS fecha_reg
              FROM lote_reproductora_ave_engorde lr
              JOIN seguimiento_diario_lote_reproductora_aves_engorde s
                ON s.lote_reproductora_ave_engorde_id = lr.id
               AND s.confirmado = true
               AND (s.fecha::date - lr.fecha_encasetamiento::date) = d
             WHERE lr.lote_ave_engorde_id = p_lote_ave_engorde_id
          ) dia;

        -- Borrar cualquier cruce previo de esta edad (se regenera abajo si aplica).
        DELETE FROM seguimiento_diario_aves_engorde
         WHERE lote_ave_engorde_id = p_lote_ave_engorde_id
           AND origen_cruce
           AND (metadata->>'edad')::int = d;

        -- Generar solo si TODOS los lotes reproductora tienen registro de la edad d.
        -- Edad 0 -> mismo día del encaset (día 1 de la semana de recogida).
        IF r.n_con = v_n_lotes AND v_n_lotes > 0 THEN
            v_fecha_dest := COALESCE(v_fecha_enc + d, r.fecha_reg);

            INSERT INTO seguimiento_diario_aves_engorde (
                lote_ave_engorde_id, fecha,
                mortalidad_machos, mortalidad_hembras,
                sel_m, sel_h, error_sexaje_machos, error_sexaje_hembras,
                consumo_kg_machos, consumo_kg_hembras,
                peso_prom_machos, peso_prom_hembras,
                consumo_agua_diario, consumo_agua_ph,
                consumo_agua_orp, consumo_agua_temperatura,
                tipo_alimento, ciclo, observaciones,
                metadata, origen_cruce, created_by_user_id, created_at
            ) VALUES (
                p_lote_ave_engorde_id, v_fecha_dest,
                r.mort_m, r.mort_h, r.sel_m, r.sel_h, r.err_m, r.err_h,
                r.consumo_m, r.consumo_h, r.peso_m, r.peso_h,
                r.agua_diario, r.agua_ph, r.agua_orp, r.agua_temp,
                CASE
                    WHEN r.tipo_alimento ~ '^\s*H:.*/\s*M:'
                    THEN btrim(split_part(regexp_replace(r.tipo_alimento, '^\s*H:\s*', ''), ' / M:', 1))
                    ELSE r.tipo_alimento
                END,
                'Normal',
                'Generado automáticamente desde ' || v_n_lotes
                  || ' lote(s) reproductora.',
                jsonb_build_object(
                    'origenCruce', true,
                    'edad', d,
                    'lotesReproductora', r.lotes_json
                ),
                true, 'SYSTEM_CRUCE', now()
            );
        END IF;
    END LOOP;
END;
$$;
";

        // ── Función PREVIA (rollback): solo edades 1..7, retiros [1, d) — versión de la
        //    migración 20260722015730_AddConfirmacionSeguimientoReproductoraEngorde ──
        private const string FN_PREVIA_1_A_7 = @"
CREATE OR REPLACE FUNCTION fn_cruce_reproductora_a_engorde(p_lote_ave_engorde_id int)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    c_max_dias   constant int := 7;
    v_fecha_enc  date;
    v_n_lotes    int;
    d            int;
    r            record;
    v_fecha_dest date;
BEGIN
    SELECT lae.fecha_encaset::date
      INTO v_fecha_enc
      FROM lote_ave_engorde lae
     WHERE lae.lote_ave_engorde_id = p_lote_ave_engorde_id;

    SELECT COUNT(*)
      INTO v_n_lotes
      FROM lote_reproductora_ave_engorde lr
     WHERE lr.lote_ave_engorde_id = p_lote_ave_engorde_id;

    IF v_n_lotes = 0 THEN
        DELETE FROM seguimiento_diario_aves_engorde
         WHERE lote_ave_engorde_id = p_lote_ave_engorde_id
           AND origen_cruce;
        RETURN;
    END IF;

    FOR d IN 1..c_max_dias LOOP
        SELECT
            COUNT(DISTINCT dia.repro_id)                                   AS n_con,
            COALESCE(SUM(dia.aves_m), 0)                                   AS machos,
            COALESCE(SUM(dia.aves_h), 0)                                   AS hembras,
            SUM(dia.consumo_kg_machos)                                     AS consumo_m,
            SUM(dia.consumo_kg_hembras)                                    AS consumo_h,
            SUM(dia.mortalidad_machos)                                     AS mort_m,
            SUM(dia.mortalidad_hembras)                                    AS mort_h,
            SUM(dia.sel_m)                                                 AS sel_m,
            SUM(dia.sel_h)                                                 AS sel_h,
            SUM(dia.error_sexaje_machos)                                   AS err_m,
            SUM(dia.error_sexaje_hembras)                                  AS err_h,
            CASE WHEN SUM(dia.aves_m) > 0
                 THEN SUM(dia.aves_m * dia.peso_prom_machos) / SUM(dia.aves_m)
            END                                                            AS peso_m,
            CASE WHEN SUM(dia.aves_h) > 0
                 THEN SUM(dia.aves_h * dia.peso_prom_hembras) / SUM(dia.aves_h)
            END                                                            AS peso_h,
            MAX(dia.fecha_reg)                                             AS fecha_reg,
            (array_agg(dia.consumo_agua_diario       ORDER BY dia.repro_id))[1]  AS agua_diario,
            (array_agg(dia.consumo_agua_ph           ORDER BY dia.repro_id))[1]  AS agua_ph,
            (array_agg(dia.consumo_agua_orp          ORDER BY dia.repro_id))[1]  AS agua_orp,
            (array_agg(dia.consumo_agua_temperatura  ORDER BY dia.repro_id))[1]  AS agua_temp,
            MAX(dia.tipo_alimento)                                         AS tipo_alimento,
            jsonb_agg(dia.repro_id ORDER BY dia.repro_id)                  AS lotes_json
          INTO r
          FROM (
            SELECT
                lr.id AS repro_id,
                COALESCE(lr.aves_inicio_machos, lr.m, 0)
                  - COALESCE((
                        SELECT SUM(COALESCE(p.mortalidad_machos,0)
                                 + COALESCE(p.sel_m,0)
                                 + COALESCE(p.error_sexaje_machos,0))
                          FROM seguimiento_diario_lote_reproductora_aves_engorde p
                         WHERE p.lote_reproductora_ave_engorde_id = lr.id
                           AND p.confirmado = true
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) >= 1
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) <  d
                    ), 0)                                                  AS aves_m,
                COALESCE(lr.aves_inicio_hembras, lr.h, 0)
                  - COALESCE((
                        SELECT SUM(COALESCE(p.mortalidad_hembras,0)
                                 + COALESCE(p.sel_h,0)
                                 + COALESCE(p.error_sexaje_hembras,0))
                          FROM seguimiento_diario_lote_reproductora_aves_engorde p
                         WHERE p.lote_reproductora_ave_engorde_id = lr.id
                           AND p.confirmado = true
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) >= 1
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) <  d
                    ), 0)                                                  AS aves_h,
                s.consumo_kg_machos, s.consumo_kg_hembras,
                s.mortalidad_machos, s.mortalidad_hembras,
                s.sel_m, s.sel_h, s.error_sexaje_machos, s.error_sexaje_hembras,
                s.peso_prom_machos, s.peso_prom_hembras,
                s.consumo_agua_diario, s.consumo_agua_ph,
                s.consumo_agua_orp, s.consumo_agua_temperatura,
                s.tipo_alimento,
                s.fecha::date AS fecha_reg
              FROM lote_reproductora_ave_engorde lr
              JOIN seguimiento_diario_lote_reproductora_aves_engorde s
                ON s.lote_reproductora_ave_engorde_id = lr.id
               AND s.confirmado = true
               AND (s.fecha::date - lr.fecha_encasetamiento::date) = d
             WHERE lr.lote_ave_engorde_id = p_lote_ave_engorde_id
          ) dia;

        DELETE FROM seguimiento_diario_aves_engorde
         WHERE lote_ave_engorde_id = p_lote_ave_engorde_id
           AND origen_cruce
           AND (metadata->>'edad')::int = d;

        IF r.n_con = v_n_lotes AND v_n_lotes > 0 THEN
            v_fecha_dest := COALESCE(v_fecha_enc + d, r.fecha_reg);

            INSERT INTO seguimiento_diario_aves_engorde (
                lote_ave_engorde_id, fecha,
                mortalidad_machos, mortalidad_hembras,
                sel_m, sel_h, error_sexaje_machos, error_sexaje_hembras,
                consumo_kg_machos, consumo_kg_hembras,
                peso_prom_machos, peso_prom_hembras,
                consumo_agua_diario, consumo_agua_ph,
                consumo_agua_orp, consumo_agua_temperatura,
                tipo_alimento, ciclo, observaciones,
                metadata, origen_cruce, created_by_user_id, created_at
            ) VALUES (
                p_lote_ave_engorde_id, v_fecha_dest,
                r.mort_m, r.mort_h, r.sel_m, r.sel_h, r.err_m, r.err_h,
                r.consumo_m, r.consumo_h, r.peso_m, r.peso_h,
                r.agua_diario, r.agua_ph, r.agua_orp, r.agua_temp,
                CASE
                    WHEN r.tipo_alimento ~ '^\s*H:.*/\s*M:'
                    THEN btrim(split_part(regexp_replace(r.tipo_alimento, '^\s*H:\s*', ''), ' / M:', 1))
                    ELSE r.tipo_alimento
                END,
                'Normal',
                'Generado automáticamente desde ' || v_n_lotes
                  || ' lote(s) reproductora (día ' || d || ').',
                jsonb_build_object(
                    'origenCruce', true,
                    'edad', d,
                    'lotesReproductora', r.lotes_json
                ),
                true, 'SYSTEM_CRUCE', now()
            );
        END IF;
    END LOOP;
END;
$$;
";
    }
}
