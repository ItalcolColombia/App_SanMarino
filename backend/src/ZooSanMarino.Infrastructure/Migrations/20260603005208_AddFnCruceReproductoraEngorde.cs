using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cruce automático Seguimiento Reproductora → Seguimiento Pollo Engorde.
    /// Función fn_cruce_reproductora_a_engorde + función trigger + trigger + índice único parcial.
    /// Mismo contenido que backend/sql/fn_cruce_reproductora_a_engorde.sql (idempotente).
    /// Se incrusta aquí para que se aplique automáticamente en deploy (Database__RunMigrations=true),
    /// siguiendo el patrón del proyecto (ej: AddFnSeguimientoDiarioEngorde).
    /// </summary>
    public partial class AddFnCruceReproductoraEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Índice único parcial: un registro de cruce por (lote, fecha)
CREATE UNIQUE INDEX IF NOT EXISTS ux_seg_engorde_cruce_lote_fecha
    ON seguimiento_diario_aves_engorde (lote_ave_engorde_id, fecha)
    WHERE origen_cruce;

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
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) >= 1
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) <  d
                    ), 0)                                                  AS aves_h,
                s.consumo_kg_machos, s.consumo_kg_hembras,
                s.mortalidad_machos, s.mortalidad_hembras,
                s.sel_m, s.sel_h, s.error_sexaje_machos, s.error_sexaje_hembras,
                s.peso_prom_machos, s.peso_prom_hembras,
                s.tipo_alimento,
                s.fecha::date AS fecha_reg
              FROM lote_reproductora_ave_engorde lr
              JOIN seguimiento_diario_lote_reproductora_aves_engorde s
                ON s.lote_reproductora_ave_engorde_id = lr.id
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
                tipo_alimento, ciclo, observaciones,
                metadata, origen_cruce, created_by_user_id, created_at
            ) VALUES (
                p_lote_ave_engorde_id, v_fecha_dest,
                r.mort_m, r.mort_h, r.sel_m, r.sel_h, r.err_m, r.err_h,
                r.consumo_m, r.consumo_h, r.peso_m, r.peso_h,
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

CREATE OR REPLACE FUNCTION trg_fn_cruce_reproductora_engorde()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_lote_ave int;
BEGIN
    SELECT lr.lote_ave_engorde_id
      INTO v_lote_ave
      FROM lote_reproductora_ave_engorde lr
     WHERE lr.id = COALESCE(NEW.lote_reproductora_ave_engorde_id,
                            OLD.lote_reproductora_ave_engorde_id);

    IF v_lote_ave IS NOT NULL THEN
        PERFORM fn_cruce_reproductora_a_engorde(v_lote_ave);
    END IF;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS trg_cruce_reproductora_engorde
    ON seguimiento_diario_lote_reproductora_aves_engorde;

CREATE TRIGGER trg_cruce_reproductora_engorde
    AFTER INSERT OR UPDATE OR DELETE
    ON seguimiento_diario_lote_reproductora_aves_engorde
    FOR EACH ROW
    EXECUTE FUNCTION trg_fn_cruce_reproductora_engorde();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_cruce_reproductora_engorde
    ON seguimiento_diario_lote_reproductora_aves_engorde;
DROP FUNCTION IF EXISTS trg_fn_cruce_reproductora_engorde();
DROP FUNCTION IF EXISTS fn_cruce_reproductora_a_engorde(int);
DROP INDEX IF EXISTS ux_seg_engorde_cruce_lote_fecha;
");
        }
    }
}
