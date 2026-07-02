using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Crea fn_indicadores_levante_postura: indicadores semanales de levante (postura Colombia)
    /// calculados en la BD, para que el front no calcule. Idempotente (CREATE OR REPLACE).
    /// Fuente/spec: backend/sql/fn_indicadores_levante_postura.sql.
    /// </summary>
    public partial class AddFnIndicadoresLevantePostura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
LANGUAGE plpgsql VOLATILE AS $fn$
DECLARE
    v_raza text; v_anio text; v_company integer;
    v_aves_enc double precision; v_peso_ini double precision; v_enc_date date;
    v_aves_acum double precision; v_mort_acum double precision := 0; v_sel_acum double precision := 0;
    v_peso_anterior double precision; v_peso_tabla_ant double precision := 0;
    v_max_sem integer; s integer;
    r_mort_tot double precision; r_sel_tot double precision; r_cons_kg double precision; r_err_tot double precision; r_dias integer;
    r_aves_fin double precision; r_pH double precision; r_pM double precision; r_peso_prom double precision;
    r_uH double precision; r_uM double precision; r_unif_real double precision; r_cons_g double precision;
    r_aves_prom double precision; r_cons_dia double precision; r_cons_tabla double precision; r_peso_tabla double precision;
    r_unif_tabla double precision; r_mort_tabla double precision; r_gan_sem double precision; r_cons_ave double precision;
    r_conv double precision; r_gan_dia_ac double precision; r_gan_tabla double precision;
    r_mort_sem double precision; r_sel_sem double precision; r_err_sem double precision; r_mort_mas_sel double precision;
    r_efic double precision; r_superv double precision; r_ip double precision;
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
    SELECT GREATEST(1, LEAST(25,
             (floor(((sl.fecha_registro AT TIME ZONE 'America/Bogota')::date - v_enc_date)/7.0)::int) + 1)) AS sem,
           (sl.fecha_registro AT TIME ZONE 'America/Bogota')::date AS reg_date,
           COALESCE(sl.mortalidad_hembras,0)+COALESCE(sl.mortalidad_machos,0) AS mort,
           COALESCE(sl.sel_h,0)+COALESCE(sl.sel_m,0) AS sel,
           COALESCE(sl.consumo_kg_hembras,0)+COALESCE(sl.consumo_kg_machos,0) AS cons_kg,
           COALESCE(sl.error_sexaje_hembras,0)+COALESCE(sl.error_sexaje_machos,0) AS err,
           COALESCE(sl.peso_prom_h,0) AS ph, COALESCE(sl.peso_prom_m,0) AS pm,
           COALESCE(sl.uniformidad_h,0) AS uh, COALESCE(sl.uniformidad_m,0) AS um, sl.id
      FROM seguimiento_lote_levante sl WHERE sl.lote_id = p_lote_id;

    SELECT MAX(sem) INTO v_max_sem FROM _seg_sem;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    FOR s IN 1..v_max_sem LOOP
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem WHERE sem = s);

        SELECT COALESCE(SUM(mort),0), COALESCE(SUM(sel),0), COALESCE(SUM(cons_kg),0), COALESCE(SUM(err),0), COUNT(*)::int
          INTO r_mort_tot, r_sel_tot, r_cons_kg, r_err_tot, r_dias FROM _seg_sem WHERE sem = s;

        r_aves_fin := v_aves_acum - r_mort_tot - r_sel_tot;

        SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
          FROM _seg_sem WHERE sem = s AND (ph > 0 OR pm > 0) ORDER BY reg_date DESC, id DESC LIMIT 1;
        IF NOT FOUND THEN
            SELECT ph, pm, uh, um INTO r_pH, r_pM, r_uH, r_uM
              FROM _seg_sem WHERE sem = s ORDER BY reg_date DESC, id DESC LIMIT 1;
        END IF;
        r_pH := COALESCE(r_pH,0); r_pM := COALESCE(r_pM,0); r_uH := COALESCE(r_uH,0); r_uM := COALESCE(r_uM,0);

        r_peso_prom := CASE WHEN r_pH>0 AND r_pM>0 THEN (r_pH+r_pM)/2 WHEN r_pH>0 THEN r_pH ELSE r_pM END;
        IF r_peso_prom <= 0 THEN r_peso_prom := COALESCE(v_peso_anterior,0); END IF;
        r_unif_real := CASE WHEN r_uH>0 AND r_uM>0 THEN (r_uH+r_uM)/2 WHEN r_uH>0 THEN r_uH ELSE r_uM END;

        r_cons_g := r_cons_kg*1000;
        r_aves_prom := (v_aves_acum + r_aves_fin)/2;
        r_cons_dia := CASE WHEN r_aves_prom>0 AND r_dias>0 THEN r_cons_g/(r_aves_prom*r_dias) ELSE 0 END;

        SELECT (COALESCE(NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,0)+COALESCE(NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,0))/2,
               (COALESCE(NULLIF(btrim(g.peso_h),'')::double precision,0)+COALESCE(NULLIF(btrim(g.peso_m),'')::double precision,0))/2,
               COALESCE(NULLIF(btrim(g.uniformidad),'')::double precision,0),
               (COALESCE(NULLIF(btrim(g.mort_sem_h),'')::double precision,0)+COALESCE(NULLIF(btrim(g.mort_sem_m),'')::double precision,0))/2
          INTO r_cons_tabla, r_peso_tabla, r_unif_tabla, r_mort_tabla
          FROM guia_genetica_sanmarino_colombia g
         WHERE g.raza = v_raza AND g.anio_guia = v_anio AND g.company_id = v_company AND btrim(g.edad) = s::text LIMIT 1;
        r_cons_tabla := COALESCE(r_cons_tabla,0); r_peso_tabla := COALESCE(r_peso_tabla,0);
        r_unif_tabla := COALESCE(r_unif_tabla,0); r_mort_tabla := COALESCE(r_mort_tabla,0);

        r_gan_sem := r_peso_prom - v_peso_anterior;
        r_cons_ave := CASE WHEN r_aves_prom>0 THEN r_cons_g/r_aves_prom ELSE 0 END;
        r_conv := CASE WHEN r_gan_sem>0 THEN r_cons_ave/r_gan_sem ELSE 0 END;
        r_gan_dia_ac := r_gan_sem/7;
        r_gan_tabla := CASE WHEN r_peso_tabla>0 AND v_peso_tabla_ant>0 THEN r_peso_tabla - v_peso_tabla_ant ELSE 0 END;

        r_mort_sem := CASE WHEN v_aves_acum>0 THEN (r_mort_tot/v_aves_acum)*100 ELSE 0 END;
        r_sel_sem := CASE WHEN v_aves_acum>0 THEN (r_sel_tot/v_aves_acum)*100 ELSE 0 END;
        r_err_sem := CASE WHEN v_aves_acum>0 THEN (r_err_tot/v_aves_acum)*100 ELSE 0 END;
        r_mort_mas_sel := r_mort_sem + r_sel_sem;

        r_efic := CASE WHEN r_cons_ave>0 THEN r_gan_sem/r_cons_ave ELSE 0 END;
        r_superv := CASE WHEN v_aves_acum>0 THEN r_aves_fin/v_aves_acum ELSE 0 END;
        r_ip := r_efic * r_superv;

        semana := s; aves_inicio_semana := v_aves_acum; aves_fin_semana := r_aves_fin;
        consumo_diario := r_cons_dia; consumo_tabla := r_cons_tabla; consumo_total_semana := r_cons_g;
        conversion_alimenticia := r_conv; peso_tabla := r_peso_tabla; unif_real := r_unif_real;
        unif_tabla := r_unif_tabla; mort_tabla := r_mort_tabla;
        dif_peso_pct := CASE WHEN r_peso_tabla>0 THEN ((r_peso_prom - r_peso_tabla)/r_peso_tabla)*100 ELSE 0 END;
        ganancia_semana := r_gan_sem; ganancia_diaria_acumulada := r_gan_dia_ac; ganancia_tabla := r_gan_tabla;
        mortalidad_sem := r_mort_sem; seleccion_sem := r_sel_sem; error_sexaje_sem := r_err_sem;
        mortalidad_mas_seleccion := r_mort_mas_sel; eficiencia := r_efic; ip := r_ip; vpi := r_ip;
        saldo_aves_semanal := r_aves_fin;
        mortalidad_acum := v_mort_acum + r_mort_sem; seleccion_acum := v_sel_acum + r_sel_sem;
        mortalidad_mas_seleccion_acum := (v_mort_acum + r_mort_sem) + (v_sel_acum + r_sel_sem);
        piso_termico_visible := false; peso_inicial := v_peso_anterior; peso_cierre := r_peso_prom;
        dias_con_registro := r_dias;
        RETURN NEXT;

        v_aves_acum := r_aves_fin; v_mort_acum := v_mort_acum + r_mort_sem; v_sel_acum := v_sel_acum + r_sel_sem;
        v_peso_anterior := r_peso_prom; v_peso_tabla_ant := r_peso_tabla;
    END LOOP;
    RETURN;
END;
$fn$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_indicadores_levante_postura(integer);");
        }
    }
}
