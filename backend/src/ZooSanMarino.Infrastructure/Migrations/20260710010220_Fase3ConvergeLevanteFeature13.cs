using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Fase 3 — Convergencia de LEVANTE al modelo Feature-13 (una sola tanda).
    /// 1) (idempotente) asegura las columnas de traslado/venta en la canónica.
    /// 2) Backfill-CONVERSIÓN del delta de la deprecada seguimiento_lote_levante:
    ///    filas-hack (tipo_alimento='N/A', ±Sel) → columnas dedicadas traslado_salida/ingreso;
    ///    filas genuinas se copian tal cual (sel = sel). Dedupe por (lote, fecha::date).
    /// 3) Recompute de acumulados LPL desde las filas convertidas (idempotente).
    /// 4/5) CREATE OR REPLACE de fn_indicadores_levante_postura y
    ///    sp_recalcular_seguimiento_levante apuntando a la canónica con la fórmula de
    ///    saldo Feature-13 (out = mort+sel+err+traslado_salida−traslado_ingreso).
    /// Local only. Idempotente.
    /// </summary>
    public partial class Fase3ConvergeLevanteFeature13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // (1) Columnas Feature-13 en la canónica (ya existen local ⇒ no-op; defensivo prod).
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    ADD COLUMN IF NOT EXISTS traslado_salida_hembras  integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_salida_machos   integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_machos  integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS es_traslado boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS traslado_direccion varchar(20),
                    ADD COLUMN IF NOT EXISTS venta_aves_cantidad integer,
                    ADD COLUMN IF NOT EXISTS venta_aves_motivo text;
            ");

            // (2) Backfill-conversión del delta (idempotente NOT EXISTS por día). Guard por si
            //     la deprecada ya fue renombrada (A5).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF to_regclass('public.seguimiento_lote_levante') IS NOT NULL THEN
                        INSERT INTO public.seguimiento_diario_levante_reproductoras (
                            tipo_seguimiento, lote_id, lote_id_int, lote_postura_levante_id, fecha,
                            mortalidad_hembras, mortalidad_machos, sel_h, sel_m,
                            error_sexaje_hembras, error_sexaje_machos,
                            consumo_kg_hembras, consumo_kg_machos, tipo_alimento, observaciones, ciclo,
                            peso_prom_hembras, peso_prom_machos, uniformidad_hembras, uniformidad_machos,
                            cv_hembras, cv_machos,
                            consumo_agua_diario, consumo_agua_ph, consumo_agua_orp, consumo_agua_temperatura,
                            kcal_al_h, prot_al_h, kcal_ave_h, prot_ave_h,
                            metadata, items_adicionales,
                            traslado_salida_hembras, traslado_salida_machos,
                            traslado_ingreso_hembras, traslado_ingreso_machos,
                            es_traslado, traslado_direccion,
                            created_at, updated_at
                        )
                        SELECT
                            -- lote_id_int se deja NULL (convención de TODA la canónica: 536/536
                            -- filas con lote_id_int NULL). Evita la FK fk_seguimiento_diario_lote_int
                            -- (lote_id_int → lotes.lote_id) para lotes legacy/orfanos. La clave real
                            -- es lote_id (texto), que sí usan todos los lectores.
                            'levante', sll.lote_id::text, NULL::integer,
                            (SELECT lpl.lote_postura_levante_id
                               FROM public.lote_postura_levante lpl
                              WHERE lpl.lote_id = sll.lote_id AND lpl.deleted_at IS NULL
                              LIMIT 1),
                            sll.fecha_registro,
                            COALESCE(sll.mortalidad_hembras,0), COALESCE(sll.mortalidad_machos,0),
                            -- Selección genuina: en filas-hack ('N/A') el movimiento pasa a traslado ⇒ 0.
                            CASE WHEN sll.tipo_alimento = 'N/A' THEN 0 ELSE COALESCE(sll.sel_h,0) END,
                            CASE WHEN sll.tipo_alimento = 'N/A' THEN 0 ELSE COALESCE(sll.sel_m,0) END,
                            COALESCE(sll.error_sexaje_hembras,0), COALESCE(sll.error_sexaje_machos,0),
                            sll.consumo_kg_hembras::numeric, sll.consumo_kg_machos::numeric,
                            CASE WHEN sll.tipo_alimento = 'N/A' THEN '—' ELSE sll.tipo_alimento END,
                            sll.observaciones, COALESCE(sll.ciclo,'Normal'),
                            sll.peso_prom_h, sll.peso_prom_m, sll.uniformidad_h, sll.uniformidad_m, sll.cv_h, sll.cv_m,
                            sll.consumo_agua_diario, sll.consumo_agua_ph, sll.consumo_agua_orp, sll.consumo_agua_temperatura,
                            sll.kcal_al_h, sll.prot_al_h, sll.kcal_ave_h, sll.prot_ave_h,
                            COALESCE(sll.metadata, '{}'::jsonb) || jsonb_strip_nulls(jsonb_build_object(
                                'qqMixtas', sll.qq_mixtas, 'qqHembras', sll.qq_hembras, 'qqMachos', sll.qq_machos,
                                'medicamentoNombre', sll.medicamento_nombre, 'medicamentoDosis', sll.medicamento_dosis)),
                            sll.items_adicionales,
                            -- Conversión ±Sel → Feature-13 (solo filas-hack 'N/A'; genuinas ⇒ 0).
                            CASE WHEN sll.tipo_alimento = 'N/A' AND COALESCE(sll.sel_h,0) < 0 THEN -sll.sel_h ELSE 0 END,
                            CASE WHEN sll.tipo_alimento = 'N/A' AND COALESCE(sll.sel_m,0) < 0 THEN -sll.sel_m ELSE 0 END,
                            CASE WHEN sll.tipo_alimento = 'N/A' AND COALESCE(sll.sel_h,0) > 0 THEN  sll.sel_h ELSE 0 END,
                            CASE WHEN sll.tipo_alimento = 'N/A' AND COALESCE(sll.sel_m,0) > 0 THEN  sll.sel_m ELSE 0 END,
                            (sll.tipo_alimento = 'N/A'),
                            CASE WHEN sll.tipo_alimento = 'N/A' AND (COALESCE(sll.sel_h,0)+COALESCE(sll.sel_m,0)) < 0 THEN 'SALIDA'
                                 WHEN sll.tipo_alimento = 'N/A' AND (COALESCE(sll.sel_h,0)+COALESCE(sll.sel_m,0)) > 0 THEN 'INGRESO' END,
                            sll.fecha_registro, NULL
                        FROM public.seguimiento_lote_levante sll
                        WHERE NOT EXISTS (
                            SELECT 1 FROM public.seguimiento_diario_levante_reproductoras sd
                            WHERE sd.tipo_seguimiento = 'levante'
                              AND sd.lote_id = sll.lote_id::text
                              AND sd.fecha::date = sll.fecha_registro::date
                        );
                    END IF;
                END $$;
            ");

            // (3) Recompute de acumulados LPL desde las filas de traslado de la canónica.
            //     Une por lote_id (texto): las filas Feature-13 tienen lote_id_int NULL.
            //     Idempotente: fija el total (SUM), no incrementa.
            migrationBuilder.Sql(@"
                UPDATE public.lote_postura_levante lpl SET
                    levante_traslado_salida_hembras  = COALESCE(agg.s_h,0),
                    levante_traslado_salida_machos   = COALESCE(agg.s_m,0),
                    levante_traslado_ingreso_hembras = COALESCE(agg.i_h,0),
                    levante_traslado_ingreso_machos  = COALESCE(agg.i_m,0)
                FROM (
                    SELECT lote_id AS lid,
                           SUM(traslado_salida_hembras)  AS s_h, SUM(traslado_salida_machos)  AS s_m,
                           SUM(traslado_ingreso_hembras) AS i_h, SUM(traslado_ingreso_machos) AS i_m
                    FROM public.seguimiento_diario_levante_reproductoras
                    WHERE tipo_seguimiento = 'levante'
                    GROUP BY lote_id
                ) agg
                WHERE lpl.lote_id::text = agg.lid AND lpl.deleted_at IS NULL;
            ");

            // (4) fn_indicadores_levante_postura → canónica + fórmula Feature-13.
            migrationBuilder.Sql(FnIndicadoresLevanteCanonica);

            // (5) sp_recalcular_seguimiento_levante → canónica + out con traslado.
            migrationBuilder.Sql(SpRecalcularSeguimientoLevanteCanonica);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaura fn/sp a su definición previa (apuntando a la deprecada
            // seguimiento_lote_levante). El backfill NO es reversible por migración
            // (revertir por marca/pg_dump, ver §10 del plan).
            migrationBuilder.Sql(FnIndicadoresLevanteDeprecada);
            migrationBuilder.Sql(SpRecalcularSeguimientoLevanteDeprecada);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  NUEVAS definiciones (canónica + Feature-13)
        // ─────────────────────────────────────────────────────────────────────
        private const string FnIndicadoresLevanteCanonica = @"
CREATE OR REPLACE FUNCTION fn_indicadores_levante_postura(p_lote_id integer)
RETURNS TABLE(
    semana                          integer,
    aves_inicio_semana              double precision,
    aves_fin_semana                 double precision,
    consumo_diario                  double precision,
    consumo_tabla                   double precision,
    consumo_total_semana            double precision,
    conversion_alimenticia          double precision,
    peso_tabla                      double precision,
    unif_real                       double precision,
    unif_tabla                      double precision,
    mort_tabla                      double precision,
    dif_peso_pct                    double precision,
    ganancia_semana                 double precision,
    ganancia_diaria_acumulada       double precision,
    ganancia_tabla                  double precision,
    mortalidad_sem                  double precision,
    seleccion_sem                   double precision,
    error_sexaje_sem                double precision,
    mortalidad_mas_seleccion        double precision,
    eficiencia                      double precision,
    ip                              double precision,
    vpi                             double precision,
    saldo_aves_semanal              double precision,
    mortalidad_acum                 double precision,
    seleccion_acum                  double precision,
    mortalidad_mas_seleccion_acum   double precision,
    piso_termico_visible            boolean,
    peso_inicial                    double precision,
    peso_cierre                     double precision,
    dias_con_registro               integer
)
LANGUAGE plpgsql VOLATILE AS $$
DECLARE
    v_raza        text;
    v_anio        text;
    v_company     integer;
    v_aves_enc    double precision;
    v_peso_ini    double precision;
    v_enc_date    date;
    v_aves_acum       double precision;
    v_mort_acum       double precision := 0;
    v_sel_acum        double precision := 0;
    v_peso_anterior   double precision;
    v_peso_tabla_ant  double precision := 0;
    v_max_sem     integer;
    s             integer;
    r_mort_tot    double precision;
    r_sel_tot     double precision;
    r_cons_kg     double precision;
    r_err_tot     double precision;
    r_tras_sal    double precision;
    r_tras_ing    double precision;
    r_dias        integer;
    r_aves_fin    double precision;
    r_pH          double precision;
    r_pM          double precision;
    r_peso_prom   double precision;
    r_uH          double precision;
    r_uM          double precision;
    r_unif_real   double precision;
    r_cons_g      double precision;
    r_aves_prom   double precision;
    r_cons_dia    double precision;
    r_cons_tabla  double precision;
    r_peso_tabla  double precision;
    r_unif_tabla  double precision;
    r_mort_tabla  double precision;
    r_gan_sem     double precision;
    r_cons_ave    double precision;
    r_conv        double precision;
    r_gan_dia_ac  double precision;
    r_gan_tabla   double precision;
    r_mort_sem    double precision;
    r_sel_sem     double precision;
    r_err_sem     double precision;
    r_mort_mas_sel double precision;
    r_efic        double precision;
    r_superv      double precision;
    r_ip          double precision;
BEGIN
    SELECT l.raza, l.ano_tabla_genetica::text, l.company_id,
           COALESCE(l.aves_encasetadas,0)::double precision,
           COALESCE(l.peso_inicial_h,0)::double precision,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date
      INTO v_raza, v_anio, v_company, v_aves_enc, v_peso_ini, v_enc_date
      FROM lotes l
     WHERE l.lote_id = p_lote_id AND l.deleted_at IS NULL;

    IF NOT FOUND THEN RETURN; END IF;

    v_aves_acum     := v_aves_enc;
    v_peso_anterior := v_peso_ini;

    CREATE TEMP TABLE _seg_sem ON COMMIT DROP AS
    SELECT
        GREATEST(1, LEAST(25,
            (floor(( (sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date ) / 7.0)::int) + 1
        )) AS sem,
        (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date,
        COALESCE(sl.mortalidad_hembras,0) + COALESCE(sl.mortalidad_machos,0) AS mort,
        COALESCE(sl.sel_h,0) + COALESCE(sl.sel_m,0) AS sel,
        COALESCE(sl.consumo_kg_hembras,0) + COALESCE(sl.consumo_kg_machos,0) AS cons_kg,
        COALESCE(sl.error_sexaje_hembras,0) + COALESCE(sl.error_sexaje_machos,0) AS err,
        COALESCE(sl.traslado_salida_hembras,0) + COALESCE(sl.traslado_salida_machos,0) AS tras_sal,
        COALESCE(sl.traslado_ingreso_hembras,0) + COALESCE(sl.traslado_ingreso_machos,0) AS tras_ing,
        COALESCE(sl.peso_prom_hembras,0) AS ph,
        COALESCE(sl.peso_prom_machos,0) AS pm,
        COALESCE(sl.uniformidad_hembras,0) AS uh,
        COALESCE(sl.uniformidad_machos,0) AS um,
        sl.id
      FROM seguimiento_diario_levante_reproductoras sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text;

    SELECT MAX(sem) INTO v_max_sem FROM _seg_sem;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    FOR s IN 1..v_max_sem LOOP
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem WHERE sem = s);

        SELECT COALESCE(SUM(mort),0), COALESCE(SUM(sel),0), COALESCE(SUM(cons_kg),0),
               COALESCE(SUM(err),0), COALESCE(SUM(tras_sal),0), COALESCE(SUM(tras_ing),0), COUNT(*)::int
          INTO r_mort_tot, r_sel_tot, r_cons_kg, r_err_tot, r_tras_sal, r_tras_ing, r_dias
          FROM _seg_sem WHERE sem = s;

        r_aves_fin := v_aves_acum - r_mort_tot - r_sel_tot - r_err_tot - r_tras_sal + r_tras_ing;

        SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
          FROM _seg_sem
         WHERE sem = s AND (ph > 0 OR pm > 0)
         ORDER BY reg_date DESC, id DESC LIMIT 1;
        IF NOT FOUND THEN
            SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
              FROM _seg_sem WHERE sem = s ORDER BY reg_date DESC, id DESC LIMIT 1;
        END IF;
        r_pH := COALESCE(r_pH,0); r_pM := COALESCE(r_pM,0);
        r_uH := COALESCE(r_uH,0); r_uM := COALESCE(r_uM,0);

        r_peso_prom := CASE WHEN r_pH > 0 AND r_pM > 0 THEN (r_pH + r_pM)/2
                            WHEN r_pH > 0 THEN r_pH ELSE r_pM END;
        IF r_peso_prom <= 0 THEN r_peso_prom := COALESCE(v_peso_anterior,0); END IF;
        r_unif_real := CASE WHEN r_uH > 0 AND r_uM > 0 THEN (r_uH + r_uM)/2
                            WHEN r_uH > 0 THEN r_uH ELSE r_uM END;

        r_cons_g    := r_cons_kg * 1000;
        r_aves_prom := (v_aves_acum + r_aves_fin)/2;
        r_cons_dia  := CASE WHEN r_aves_prom > 0 AND r_dias > 0 THEN r_cons_g/(r_aves_prom*r_dias) ELSE 0 END;

        SELECT (COALESCE(NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,0)
              + COALESCE(NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,0))/2,
               (COALESCE(NULLIF(btrim(g.peso_h),'')::double precision,0)
              + COALESCE(NULLIF(btrim(g.peso_m),'')::double precision,0))/2,
               COALESCE(NULLIF(btrim(g.uniformidad),'')::double precision,0),
               (COALESCE(NULLIF(btrim(g.mort_sem_h),'')::double precision,0)
              + COALESCE(NULLIF(btrim(g.mort_sem_m),'')::double precision,0))/2
          INTO r_cons_tabla, r_peso_tabla, r_unif_tabla, r_mort_tabla
          FROM guia_genetica_sanmarino_colombia g
         WHERE g.raza = v_raza AND g.anio_guia = v_anio AND g.company_id = v_company
           AND btrim(g.edad) = s::text
         LIMIT 1;
        r_cons_tabla := COALESCE(r_cons_tabla,0);
        r_peso_tabla := COALESCE(r_peso_tabla,0);
        r_unif_tabla := COALESCE(r_unif_tabla,0);
        r_mort_tabla := COALESCE(r_mort_tabla,0);

        r_gan_sem   := r_peso_prom - v_peso_anterior;
        r_cons_ave  := CASE WHEN r_aves_prom > 0 THEN r_cons_g/r_aves_prom ELSE 0 END;
        r_conv      := CASE WHEN r_gan_sem > 0 THEN r_cons_ave/r_gan_sem ELSE 0 END;
        r_gan_dia_ac := r_gan_sem/7;
        r_gan_tabla := CASE WHEN r_peso_tabla > 0 AND v_peso_tabla_ant > 0 THEN r_peso_tabla - v_peso_tabla_ant ELSE 0 END;

        r_mort_sem  := CASE WHEN v_aves_acum > 0 THEN (r_mort_tot/v_aves_acum)*100 ELSE 0 END;
        r_sel_sem   := CASE WHEN v_aves_acum > 0 THEN (r_sel_tot/v_aves_acum)*100 ELSE 0 END;
        r_err_sem   := CASE WHEN v_aves_acum > 0 THEN (r_err_tot/v_aves_acum)*100 ELSE 0 END;
        r_mort_mas_sel := r_mort_sem + r_sel_sem;

        r_efic   := CASE WHEN r_cons_ave > 0 THEN r_gan_sem/r_cons_ave ELSE 0 END;
        r_superv := CASE WHEN v_aves_acum > 0 THEN r_aves_fin/v_aves_acum ELSE 0 END;
        r_ip     := r_efic * r_superv;

        semana                        := s;
        aves_inicio_semana            := v_aves_acum;
        aves_fin_semana               := r_aves_fin;
        consumo_diario                := r_cons_dia;
        consumo_tabla                 := r_cons_tabla;
        consumo_total_semana          := r_cons_g;
        conversion_alimenticia        := r_conv;
        peso_tabla                    := r_peso_tabla;
        unif_real                     := r_unif_real;
        unif_tabla                    := r_unif_tabla;
        mort_tabla                    := r_mort_tabla;
        dif_peso_pct                  := CASE WHEN r_peso_tabla > 0 THEN ((r_peso_prom - r_peso_tabla)/r_peso_tabla)*100 ELSE 0 END;
        ganancia_semana               := r_gan_sem;
        ganancia_diaria_acumulada     := r_gan_dia_ac;
        ganancia_tabla                := r_gan_tabla;
        mortalidad_sem                := r_mort_sem;
        seleccion_sem                 := r_sel_sem;
        error_sexaje_sem              := r_err_sem;
        mortalidad_mas_seleccion      := r_mort_mas_sel;
        eficiencia                    := r_efic;
        ip                            := r_ip;
        vpi                           := r_ip;
        saldo_aves_semanal            := r_aves_fin;
        mortalidad_acum               := v_mort_acum + r_mort_sem;
        seleccion_acum                := v_sel_acum + r_sel_sem;
        mortalidad_mas_seleccion_acum := (v_mort_acum + r_mort_sem) + (v_sel_acum + r_sel_sem);
        piso_termico_visible          := false;
        peso_inicial                  := v_peso_anterior;
        peso_cierre                   := r_peso_prom;
        dias_con_registro             := r_dias;

        RETURN NEXT;

        v_aves_acum      := r_aves_fin;
        v_mort_acum      := v_mort_acum + r_mort_sem;
        v_sel_acum       := v_sel_acum + r_sel_sem;
        v_peso_anterior  := r_peso_prom;
        v_peso_tabla_ant := r_peso_tabla;
    END LOOP;

    RETURN;
END;
$$;
";

        private const string SpRecalcularSeguimientoLevanteCanonica = @"
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

  select fecha_encaset,
         coalesce(hembras_l, 0),
         coalesce(machos_l, 0),
         coalesce(mort_caja_h, 0),
         coalesce(mort_caja_m, 0),
         codigo_guia_genetica,
         raza,
         ano_tabla_genetica
    into v_fecha_encaset, v_h_ini, v_m_ini, v_mort_caja_h, v_mort_caja_m, v_codigo_guia, v_raza, v_ano_gen
  from lotes
  where lote_id = l_lote_id::integer;   -- lotes.lote_id es integer; l_lote_id es text

  if not found then
    raise exception 'Lote % no existe', l_lote_id;
  end if;

  delete from produccion_resultado_levante where lote_id = l_lote_id;

  insert into produccion_resultado_levante (
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
    select
           s.fecha as fecha_registro,
           s.mortalidad_hembras, s.mortalidad_machos,
           s.sel_h, s.sel_m,
           s.error_sexaje_hembras, s.error_sexaje_machos,
           s.consumo_kg_hembras, s.consumo_kg_machos,
           s.peso_prom_hembras as peso_prom_h, s.peso_prom_machos as peso_prom_m,
           s.uniformidad_hembras as uniformidad_h, s.uniformidad_machos as uniformidad_m,
           s.cv_hembras as cv_h, s.cv_machos as cv_m,
           case when v_fecha_encaset is null then null
                else (1 + floor(extract(epoch from (s.fecha - v_fecha_encaset)) / 86400.0 / 7.0)::int)
           end as edad_sem,
           (coalesce(s.mortalidad_hembras,0) + coalesce(s.sel_h,0) + coalesce(s.error_sexaje_hembras,0)
             + coalesce(s.traslado_salida_hembras,0) - coalesce(s.traslado_ingreso_hembras,0)) as out_h,
           (coalesce(s.mortalidad_machos,0)  + coalesce(s.sel_m,0) + coalesce(s.error_sexaje_machos,0)
             + coalesce(s.traslado_salida_machos,0) - coalesce(s.traslado_ingreso_machos,0)) as out_m
    from seguimiento_diario_levante_reproductoras s
    where s.tipo_seguimiento = 'levante' and s.lote_id = l_lote_id
  ),
  ac_base as (
    select b.*,
           sum(b.out_h) over (order by b.fecha_registro
                              rows between unbounded preceding and 1 preceding) as ac_out_h_prev,
           sum(b.out_m) over (order by b.fecha_registro
                              rows between unbounded preceding and 1 preceding) as ac_out_m_prev,
           sum(coalesce(b.mortalidad_hembras,0)) over (order by b.fecha_registro) as ac_mort_h,
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
    select a.*,
           greatest(0, (coalesce(v_h_ini,0) - coalesce(v_mort_caja_h,0) - coalesce(a.ac_out_h_prev,0)))::int as hembra_viva,
           greatest(0, (coalesce(v_m_ini,0) - coalesce(v_mort_caja_m,0) - coalesce(a.ac_out_m_prev,0)))::int as macho_vivo
    from ac_base a
  ),
  gh as (
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
    p.hembra_viva,
    coalesce(p.mortalidad_hembras,0) as mort_h,
    coalesce(p.sel_h,0)              as sel_h_out,
    coalesce(p.error_sexaje_hembras,0) as err_h,
    p.consumo_kg_hembras             as cons_kg_h,
    p.peso_prom_h                    as peso_h,
    p.uniformidad_h                  as unif_h,
    p.cv_h                           as cv_h,
    case when p.hembra_viva>0 then dpr(p.mortalidad_hembras * 100.0 / p.hembra_viva, 3) end as mort_h_pct,
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
    p.macho_vivo,
    coalesce(p.mortalidad_machos,0) as mort_m,
    coalesce(p.sel_m,0)             as sel_m_out,
    coalesce(p.error_sexaje_machos,0) as err_m,
    p.consumo_kg_machos             as cons_kg_m,
    p.peso_prom_m                   as peso_m,
    p.uniformidad_m                 as unif_m,
    p.cv_m                          as cv_m,
    case when p.macho_vivo>0 then dpr(p.mortalidad_machos * 100.0 / p.macho_vivo, 3) end as mort_m_pct,
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
    gh.peso_obj, gh.unif_obj, gh.cons_ac_gr_obj, gh.gr_ave_dia_obj, gh.mort_pct_obj,
    gm.peso_obj, gm.unif_obj, gm.cons_ac_gr_obj, gm.gr_ave_dia_obj, gm.mort_pct_obj,
    gh.alimento_nom, gm.alimento_nom
  from pobl p
  left join gh on gh.semana = p.edad_sem
  left join gm on gm.semana = p.edad_sem
  order by p.fecha_registro;

end;
$function$;
";

        // ─────────────────────────────────────────────────────────────────────
        //  DEFINICIONES PREVIAS (para Down): apuntan a la deprecada seguimiento_lote_levante
        // ─────────────────────────────────────────────────────────────────────
        private const string FnIndicadoresLevanteDeprecada = @"
CREATE OR REPLACE FUNCTION fn_indicadores_levante_postura(p_lote_id integer)
RETURNS TABLE(
    semana integer, aves_inicio_semana double precision, aves_fin_semana double precision,
    consumo_diario double precision, consumo_tabla double precision, consumo_total_semana double precision,
    conversion_alimenticia double precision, peso_tabla double precision, unif_real double precision,
    unif_tabla double precision, mort_tabla double precision, dif_peso_pct double precision,
    ganancia_semana double precision, ganancia_diaria_acumulada double precision, ganancia_tabla double precision,
    mortalidad_sem double precision, seleccion_sem double precision, error_sexaje_sem double precision,
    mortalidad_mas_seleccion double precision, eficiencia double precision, ip double precision,
    vpi double precision, saldo_aves_semanal double precision, mortalidad_acum double precision,
    seleccion_acum double precision, mortalidad_mas_seleccion_acum double precision,
    piso_termico_visible boolean, peso_inicial double precision, peso_cierre double precision,
    dias_con_registro integer
)
LANGUAGE plpgsql VOLATILE AS $$
DECLARE
    v_raza text; v_anio text; v_company integer; v_aves_enc double precision;
    v_peso_ini double precision; v_enc_date date;
    v_aves_acum double precision; v_mort_acum double precision := 0; v_sel_acum double precision := 0;
    v_peso_anterior double precision; v_peso_tabla_ant double precision := 0;
    v_max_sem integer; s integer;
    r_mort_tot double precision; r_sel_tot double precision; r_cons_kg double precision;
    r_err_tot double precision; r_dias integer; r_aves_fin double precision;
    r_pH double precision; r_pM double precision; r_peso_prom double precision;
    r_uH double precision; r_uM double precision; r_unif_real double precision;
    r_cons_g double precision; r_aves_prom double precision; r_cons_dia double precision;
    r_cons_tabla double precision; r_peso_tabla double precision; r_unif_tabla double precision;
    r_mort_tabla double precision; r_gan_sem double precision; r_cons_ave double precision;
    r_conv double precision; r_gan_dia_ac double precision; r_gan_tabla double precision;
    r_mort_sem double precision; r_sel_sem double precision; r_err_sem double precision;
    r_mort_mas_sel double precision; r_efic double precision; r_superv double precision; r_ip double precision;
BEGIN
    SELECT l.raza, l.ano_tabla_genetica::text, l.company_id,
           COALESCE(l.aves_encasetadas,0)::double precision,
           COALESCE(l.peso_inicial_h,0)::double precision,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date
      INTO v_raza, v_anio, v_company, v_aves_enc, v_peso_ini, v_enc_date
      FROM lotes l WHERE l.lote_id = p_lote_id AND l.deleted_at IS NULL;
    IF NOT FOUND THEN RETURN; END IF;
    v_aves_acum := v_aves_enc; v_peso_anterior := v_peso_ini;
    CREATE TEMP TABLE _seg_sem ON COMMIT DROP AS
    SELECT
        GREATEST(1, LEAST(25, (floor(( (sl.fecha_registro AT TIME ZONE 'America/Bogota')::date - v_enc_date ) / 7.0)::int) + 1)) AS sem,
        (sl.fecha_registro AT TIME ZONE 'America/Bogota')::date AS reg_date,
        COALESCE(sl.mortalidad_hembras,0) + COALESCE(sl.mortalidad_machos,0) AS mort,
        COALESCE(sl.sel_h,0) + COALESCE(sl.sel_m,0) AS sel,
        COALESCE(sl.consumo_kg_hembras,0) + COALESCE(sl.consumo_kg_machos,0) AS cons_kg,
        COALESCE(sl.error_sexaje_hembras,0) + COALESCE(sl.error_sexaje_machos,0) AS err,
        COALESCE(sl.peso_prom_h,0) AS ph, COALESCE(sl.peso_prom_m,0) AS pm,
        COALESCE(sl.uniformidad_h,0) AS uh, COALESCE(sl.uniformidad_m,0) AS um, sl.id
      FROM seguimiento_lote_levante sl WHERE sl.lote_id = p_lote_id;
    SELECT MAX(sem) INTO v_max_sem FROM _seg_sem;
    IF v_max_sem IS NULL THEN RETURN; END IF;
    FOR s IN 1..v_max_sem LOOP
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem WHERE sem = s);
        SELECT COALESCE(SUM(mort),0), COALESCE(SUM(sel),0), COALESCE(SUM(cons_kg),0),
               COALESCE(SUM(err),0), COUNT(*)::int
          INTO r_mort_tot, r_sel_tot, r_cons_kg, r_err_tot, r_dias FROM _seg_sem WHERE sem = s;
        r_aves_fin := v_aves_acum - r_mort_tot - r_sel_tot;
        SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
          FROM _seg_sem WHERE sem = s AND (ph > 0 OR pm > 0) ORDER BY reg_date DESC, id DESC LIMIT 1;
        IF NOT FOUND THEN
            SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
              FROM _seg_sem WHERE sem = s ORDER BY reg_date DESC, id DESC LIMIT 1;
        END IF;
        r_pH := COALESCE(r_pH,0); r_pM := COALESCE(r_pM,0); r_uH := COALESCE(r_uH,0); r_uM := COALESCE(r_uM,0);
        r_peso_prom := CASE WHEN r_pH > 0 AND r_pM > 0 THEN (r_pH + r_pM)/2 WHEN r_pH > 0 THEN r_pH ELSE r_pM END;
        IF r_peso_prom <= 0 THEN r_peso_prom := COALESCE(v_peso_anterior,0); END IF;
        r_unif_real := CASE WHEN r_uH > 0 AND r_uM > 0 THEN (r_uH + r_uM)/2 WHEN r_uH > 0 THEN r_uH ELSE r_uM END;
        r_cons_g := r_cons_kg * 1000; r_aves_prom := (v_aves_acum + r_aves_fin)/2;
        r_cons_dia := CASE WHEN r_aves_prom > 0 AND r_dias > 0 THEN r_cons_g/(r_aves_prom*r_dias) ELSE 0 END;
        SELECT (COALESCE(NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,0)
              + COALESCE(NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,0))/2,
               (COALESCE(NULLIF(btrim(g.peso_h),'')::double precision,0)
              + COALESCE(NULLIF(btrim(g.peso_m),'')::double precision,0))/2,
               COALESCE(NULLIF(btrim(g.uniformidad),'')::double precision,0),
               (COALESCE(NULLIF(btrim(g.mort_sem_h),'')::double precision,0)
              + COALESCE(NULLIF(btrim(g.mort_sem_m),'')::double precision,0))/2
          INTO r_cons_tabla, r_peso_tabla, r_unif_tabla, r_mort_tabla
          FROM guia_genetica_sanmarino_colombia g
         WHERE g.raza = v_raza AND g.anio_guia = v_anio AND g.company_id = v_company AND btrim(g.edad) = s::text LIMIT 1;
        r_cons_tabla := COALESCE(r_cons_tabla,0); r_peso_tabla := COALESCE(r_peso_tabla,0);
        r_unif_tabla := COALESCE(r_unif_tabla,0); r_mort_tabla := COALESCE(r_mort_tabla,0);
        r_gan_sem := r_peso_prom - v_peso_anterior;
        r_cons_ave := CASE WHEN r_aves_prom > 0 THEN r_cons_g/r_aves_prom ELSE 0 END;
        r_conv := CASE WHEN r_gan_sem > 0 THEN r_cons_ave/r_gan_sem ELSE 0 END;
        r_gan_dia_ac := r_gan_sem/7;
        r_gan_tabla := CASE WHEN r_peso_tabla > 0 AND v_peso_tabla_ant > 0 THEN r_peso_tabla - v_peso_tabla_ant ELSE 0 END;
        r_mort_sem := CASE WHEN v_aves_acum > 0 THEN (r_mort_tot/v_aves_acum)*100 ELSE 0 END;
        r_sel_sem := CASE WHEN v_aves_acum > 0 THEN (r_sel_tot/v_aves_acum)*100 ELSE 0 END;
        r_err_sem := CASE WHEN v_aves_acum > 0 THEN (r_err_tot/v_aves_acum)*100 ELSE 0 END;
        r_mort_mas_sel := r_mort_sem + r_sel_sem;
        r_efic := CASE WHEN r_cons_ave > 0 THEN r_gan_sem/r_cons_ave ELSE 0 END;
        r_superv := CASE WHEN v_aves_acum > 0 THEN r_aves_fin/v_aves_acum ELSE 0 END;
        r_ip := r_efic * r_superv;
        semana := s; aves_inicio_semana := v_aves_acum; aves_fin_semana := r_aves_fin;
        consumo_diario := r_cons_dia; consumo_tabla := r_cons_tabla; consumo_total_semana := r_cons_g;
        conversion_alimenticia := r_conv; peso_tabla := r_peso_tabla; unif_real := r_unif_real;
        unif_tabla := r_unif_tabla; mort_tabla := r_mort_tabla;
        dif_peso_pct := CASE WHEN r_peso_tabla > 0 THEN ((r_peso_prom - r_peso_tabla)/r_peso_tabla)*100 ELSE 0 END;
        ganancia_semana := r_gan_sem; ganancia_diaria_acumulada := r_gan_dia_ac; ganancia_tabla := r_gan_tabla;
        mortalidad_sem := r_mort_sem; seleccion_sem := r_sel_sem; error_sexaje_sem := r_err_sem;
        mortalidad_mas_seleccion := r_mort_mas_sel; eficiencia := r_efic; ip := r_ip; vpi := r_ip;
        saldo_aves_semanal := r_aves_fin; mortalidad_acum := v_mort_acum + r_mort_sem;
        seleccion_acum := v_sel_acum + r_sel_sem;
        mortalidad_mas_seleccion_acum := (v_mort_acum + r_mort_sem) + (v_sel_acum + r_sel_sem);
        piso_termico_visible := false; peso_inicial := v_peso_anterior; peso_cierre := r_peso_prom;
        dias_con_registro := r_dias;
        RETURN NEXT;
        v_aves_acum := r_aves_fin; v_mort_acum := v_mort_acum + r_mort_sem; v_sel_acum := v_sel_acum + r_sel_sem;
        v_peso_anterior := r_peso_prom; v_peso_tabla_ant := r_peso_tabla;
    END LOOP;
    RETURN;
END;
$$;
";

        private const string SpRecalcularSeguimientoLevanteDeprecada = @"
CREATE OR REPLACE FUNCTION public.sp_recalcular_seguimiento_levante(l_lote_id text)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
declare
  v_fecha_encaset date; v_h_ini int; v_m_ini int; v_mort_caja_h int; v_mort_caja_m int;
  v_codigo_guia text; v_raza text; v_ano_gen int;
begin
  select fecha_encaset, coalesce(hembras_l, 0), coalesce(machos_l, 0),
         coalesce(mort_caja_h, 0), coalesce(mort_caja_m, 0),
         codigo_guia_genetica, raza, ano_tabla_genetica
    into v_fecha_encaset, v_h_ini, v_m_ini, v_mort_caja_h, v_mort_caja_m, v_codigo_guia, v_raza, v_ano_gen
  from lotes where lote_id = l_lote_id;
  if not found then raise exception 'Lote % no existe', l_lote_id; end if;
  delete from produccion_resultado_levante where lote_id = l_lote_id;
  insert into produccion_resultado_levante (
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
    select s.*,
           case when v_fecha_encaset is null then null
                else (1 + floor(extract(epoch from (s.fecha_registro - v_fecha_encaset)) / 86400.0 / 7.0)::int)
           end as edad_sem,
           (coalesce(s.mortalidad_hembras,0) + coalesce(s.sel_h,0) + coalesce(s.error_sexaje_hembras,0)) as out_h,
           (coalesce(s.mortalidad_machos,0)  + coalesce(s.sel_m,0) + coalesce(s.error_sexaje_machos,0) ) as out_m
    from seguimiento_lote_levante s where s.lote_id = l_lote_id
  ),
  ac_base as (
    select b.*,
           sum(b.out_h) over (order by b.fecha_registro rows between unbounded preceding and 1 preceding) as ac_out_h_prev,
           sum(b.out_m) over (order by b.fecha_registro rows between unbounded preceding and 1 preceding) as ac_out_m_prev,
           sum(coalesce(b.mortalidad_hembras,0)) over (order by b.fecha_registro) as ac_mort_h,
           sum(coalesce(b.sel_h,0)) over (order by b.fecha_registro) as ac_sel_h,
           sum(coalesce(b.error_sexaje_hembras,0)) over (order by b.fecha_registro) as ac_err_h,
           sum(coalesce(b.consumo_kg_hembras,0)) over (order by b.fecha_registro) as ac_cons_kg_h,
           sum(coalesce(b.mortalidad_machos,0)) over (order by b.fecha_registro) as ac_mort_m,
           sum(coalesce(b.sel_m,0)) over (order by b.fecha_registro) as ac_sel_m,
           sum(coalesce(b.error_sexaje_machos,0)) over (order by b.fecha_registro) as ac_err_m,
           sum(coalesce(b.consumo_kg_machos,0)) over (order by b.fecha_registro) as ac_cons_kg_m,
           lag(b.peso_prom_h) over (order by b.fecha_registro) as peso_h_prev,
           lag(b.peso_prom_m) over (order by b.fecha_registro) as peso_m_prev
    from base b
  ),
  pobl as (
    select a.*,
           greatest(0, (coalesce(v_h_ini,0) - coalesce(v_mort_caja_h,0) - coalesce(a.ac_out_h_prev,0)))::int as hembra_viva,
           greatest(0, (coalesce(v_m_ini,0) - coalesce(v_mort_caja_m,0) - coalesce(a.ac_out_m_prev,0)))::int as macho_vivo
    from ac_base a
  ),
  gh as (
    select semana, peso_obj, unif_obj, mort_pct_obj, cons_ac_gr_obj, gr_ave_dia_obj, incr_cons_obj,
           kcal_sem_obj, kcal_sem_ac_obj, prot_sem_obj, prot_sem_ac_obj, alimento_nom
    from guia_semana where sexo='H'
      and (codigo_guia_genetica is not distinct from v_codigo_guia)
      and (raza is not distinct from v_raza)
      and (ano_tabla_genetica is not distinct from v_ano_gen)
  ),
  gm as (
    select semana, peso_obj, unif_obj, mort_pct_obj, cons_ac_gr_obj, gr_ave_dia_obj, incr_cons_obj,
           kcal_sem_obj, kcal_sem_ac_obj, prot_sem_obj, prot_sem_ac_obj, alimento_nom
    from guia_semana where sexo='M'
      and (codigo_guia_genetica is not distinct from v_codigo_guia)
      and (raza is not distinct from v_raza)
      and (ano_tabla_genetica is not distinct from v_ano_gen)
  )
  select
    l_lote_id as lote_id, p.fecha_registro as fecha, p.edad_sem as edad_semana,
    p.hembra_viva,
    coalesce(p.mortalidad_hembras,0) as mort_h, coalesce(p.sel_h,0) as sel_h_out,
    coalesce(p.error_sexaje_hembras,0) as err_h, p.consumo_kg_hembras as cons_kg_h,
    p.peso_prom_h as peso_h, p.uniformidad_h as unif_h, p.cv_h as cv_h,
    case when p.hembra_viva>0 then dpr(p.mortalidad_hembras * 100.0 / p.hembra_viva, 3) end as mort_h_pct,
    case when p.hembra_viva>0 then dpr(p.sel_h * 100.0 / p.hembra_viva, 3) end as sel_h_pct,
    case when p.hembra_viva>0 then dpr(p.error_sexaje_hembras * 100.0 / p.hembra_viva, 3) end as err_h_pct,
    (coalesce(p.mortalidad_hembras,0)+coalesce(p.sel_h,0)+coalesce(p.error_sexaje_hembras,0)) as ms_eh_h,
    p.ac_mort_h, p.ac_sel_h, p.ac_err_h, p.ac_cons_kg_h,
    case when p.hembra_viva>0 then dpr( (p.ac_cons_kg_h*1000.0)/p.hembra_viva, 3) end as cons_ac_gr_h,
    case when p.peso_prom_h is null or p.peso_h_prev is null then null else dpr(p.peso_prom_h - p.peso_h_prev, 2) end as gr_ave_dia_h,
    case when gh.cons_ac_gr_obj is null or p.hembra_viva<=0 then null
         else dpr( (((p.ac_cons_kg_h*1000.0)/p.hembra_viva) - gh.cons_ac_gr_obj) * 100.0 / gh.cons_ac_gr_obj, 3) end as dif_cons_h_pct,
    case when gh.peso_obj is null or p.peso_prom_h is null then null
         else dpr( (p.peso_prom_h - gh.peso_obj) * 100.0 / gh.peso_obj, 3) end as dif_peso_h_pct,
    case when p.hembra_viva>0 then dpr( (coalesce(p.sel_h,0)+coalesce(p.error_sexaje_hembras,0)) * 100.0 / p.hembra_viva, 3) end as retiro_h_pct,
    case when (p.hembra_viva + p.ac_mort_h + p.ac_sel_h + p.ac_err_h)>0
         then dpr( (p.ac_sel_h + p.ac_err_h) * 100.0 / (p.hembra_viva + p.ac_mort_h + p.ac_sel_h + p.ac_err_h), 3) end as retiro_h_ac_pct,
    p.macho_vivo,
    coalesce(p.mortalidad_machos,0) as mort_m, coalesce(p.sel_m,0) as sel_m_out,
    coalesce(p.error_sexaje_machos,0) as err_m, p.consumo_kg_machos as cons_kg_m,
    p.peso_prom_m as peso_m, p.uniformidad_m as unif_m, p.cv_m as cv_m,
    case when p.macho_vivo>0 then dpr(p.mortalidad_machos * 100.0 / p.macho_vivo, 3) end as mort_m_pct,
    case when p.macho_vivo>0 then dpr(p.sel_m * 100.0 / p.macho_vivo, 3) end as sel_m_pct,
    case when p.macho_vivo>0 then dpr(p.error_sexaje_machos * 100.0 / p.macho_vivo, 3) end as err_m_pct,
    (coalesce(p.mortalidad_machos,0)+coalesce(p.sel_m,0)+coalesce(p.error_sexaje_machos,0)) as ms_em_m,
    p.ac_mort_m, p.ac_sel_m, p.ac_err_m, p.ac_cons_kg_m,
    case when p.macho_vivo>0 then dpr( (p.ac_cons_kg_m*1000.0)/p.macho_vivo, 3) end as cons_ac_gr_m,
    case when p.peso_prom_m is null or p.peso_m_prev is null then null else dpr(p.peso_prom_m - p.peso_m_prev, 2) end as gr_ave_dia_m,
    case when gm.cons_ac_gr_obj is null or p.macho_vivo<=0 then null
         else dpr( (((p.ac_cons_kg_m*1000.0)/p.macho_vivo) - gm.cons_ac_gr_obj) * 100.0 / gm.cons_ac_gr_obj, 3) end as dif_cons_m_pct,
    case when gm.peso_obj is null or p.peso_prom_m is null then null
         else dpr( (p.peso_prom_m - gm.peso_obj) * 100.0 / gm.peso_obj, 3) end as dif_peso_m_pct,
    case when p.macho_vivo>0 then dpr( (coalesce(p.sel_m,0)+coalesce(p.error_sexaje_machos,0)) * 100.0 / p.macho_vivo, 3) end as retiro_m_pct,
    case when (p.macho_vivo + p.ac_mort_m + p.ac_sel_m + p.ac_err_m)>0
         then dpr( (p.ac_sel_m + p.ac_err_m) * 100.0 / (p.macho_vivo + p.ac_mort_m + p.ac_sel_m + p.ac_err_m), 3) end as retiro_m_ac_pct,
    case when p.hembra_viva is null or p.hembra_viva=0 then null else dpr(p.macho_vivo * 100.0 / p.hembra_viva, 3) end as rel_m_h_pct,
    gh.peso_obj, gh.unif_obj, gh.cons_ac_gr_obj, gh.gr_ave_dia_obj, gh.mort_pct_obj,
    gm.peso_obj, gm.unif_obj, gm.cons_ac_gr_obj, gm.gr_ave_dia_obj, gm.mort_pct_obj,
    gh.alimento_nom, gm.alimento_nom
  from pobl p
  left join gh on gh.semana = p.edad_sem
  left join gm on gm.semana = p.edad_sem
  order by p.fecha_registro;
end;
$function$;
";
    }
}
