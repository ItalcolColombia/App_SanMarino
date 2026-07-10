using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Fase 3 · FASE C — Rename final de las tablas canónicas de seguimiento:
    ///   seguimiento_diario_levante_reproductoras    → seguimiento_diario_levante
    ///   seguimiento_diario_produccion_reproductoras → seguimiento_diario_produccion
    /// Renames IDEMPOTENTES (tabla + PK renombrado in-place + índice único guardado).
    /// En el MISMO Up() se hace CREATE OR REPLACE de las 3 funciones que referencian esas
    /// tablas, apuntando a los nombres NUEVOS (regla anti-SIGSEGV): fn_indicadores_levante_postura,
    /// fn_indicadores_produccion_postura (UNION de ambas) y sp_recalcular_seguimiento_levante.
    /// Sólo cambia el NOMBRE de la tabla en los cuerpos; la lógica/aritmética es idéntica
    /// (cuerpos tomados de pg_get_functiondef sobre la BD local, i.e. lo desplegado hoy).
    /// Down() revierte los renames y restaura las funciones a los nombres _reproductoras.
    /// </summary>
    public partial class Fase3RenameSeguimientoTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RenameUp);
            migrationBuilder.Sql(FnLevanteNew);
            migrationBuilder.Sql(FnProduccionNew);
            migrationBuilder.Sql(SpLevanteNew);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RenameDown);
            migrationBuilder.Sql(FnLevanteOrig);
            migrationBuilder.Sql(FnProduccionOrig);
            migrationBuilder.Sql(SpLevanteOrig);
        }

        private const string RenameUp = @"
-- Fase 3 · FASE C — rename final de las tablas canónicas (idempotente).
ALTER TABLE IF EXISTS public.seguimiento_diario_levante_reproductoras    RENAME TO seguimiento_diario_levante;
ALTER TABLE IF EXISTS public.seguimiento_diario_produccion_reproductoras RENAME TO seguimiento_diario_produccion;

DO $$
BEGIN
    -- PK (rename in place: renombra también el índice de respaldo homónimo).
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_seguimiento_diario_levante_reproductoras') THEN
        ALTER TABLE public.seguimiento_diario_levante
            RENAME CONSTRAINT pk_seguimiento_diario_levante_reproductoras TO pk_seguimiento_diario_levante;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_seguimiento_diario_produccion_reproductoras') THEN
        ALTER TABLE public.seguimiento_diario_produccion
            RENAME CONSTRAINT pk_seguimiento_diario_produccion_reproductoras TO pk_seguimiento_diario_produccion;
    END IF;
    -- Índice único (lote_id, fecha_registro) de producción (convención EF) — sólo si existe.
    IF EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'i'
               AND relname = 'ix_seguimiento_diario_produccion_reproductoras_lote_id_fecha_r') THEN
        ALTER INDEX public.ix_seguimiento_diario_produccion_reproductoras_lote_id_fecha_r
            RENAME TO ix_seguimiento_diario_produccion_lote_id_fecha_registro;
    END IF;
END $$;
";

        private const string RenameDown = @"
-- Reverso del rename final (idempotente).
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'i'
               AND relname = 'ix_seguimiento_diario_produccion_lote_id_fecha_registro') THEN
        ALTER INDEX public.ix_seguimiento_diario_produccion_lote_id_fecha_registro
            RENAME TO ix_seguimiento_diario_produccion_reproductoras_lote_id_fecha_r;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_seguimiento_diario_levante') THEN
        ALTER TABLE public.seguimiento_diario_levante
            RENAME CONSTRAINT pk_seguimiento_diario_levante TO pk_seguimiento_diario_levante_reproductoras;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_seguimiento_diario_produccion') THEN
        ALTER TABLE public.seguimiento_diario_produccion
            RENAME CONSTRAINT pk_seguimiento_diario_produccion TO pk_seguimiento_diario_produccion_reproductoras;
    END IF;
END $$;

ALTER TABLE IF EXISTS public.seguimiento_diario_levante    RENAME TO seguimiento_diario_levante_reproductoras;
ALTER TABLE IF EXISTS public.seguimiento_diario_produccion RENAME TO seguimiento_diario_produccion_reproductoras;
";

        private const string FnLevanteNew = @"CREATE OR REPLACE FUNCTION public.fn_indicadores_levante_postura(p_lote_id integer)
 RETURNS TABLE(semana integer, aves_inicio_semana double precision, aves_fin_semana double precision, consumo_diario double precision, consumo_tabla double precision, consumo_total_semana double precision, conversion_alimenticia double precision, peso_tabla double precision, unif_real double precision, unif_tabla double precision, mort_tabla double precision, dif_peso_pct double precision, ganancia_semana double precision, ganancia_diaria_acumulada double precision, ganancia_tabla double precision, mortalidad_sem double precision, seleccion_sem double precision, error_sexaje_sem double precision, mortalidad_mas_seleccion double precision, eficiencia double precision, ip double precision, vpi double precision, saldo_aves_semanal double precision, mortalidad_acum double precision, seleccion_acum double precision, mortalidad_mas_seleccion_acum double precision, piso_termico_visible boolean, peso_inicial double precision, peso_cierre double precision, dias_con_registro integer)
 LANGUAGE plpgsql
AS $function$
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
      FROM seguimiento_diario_levante sl
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
$function$

";

        private const string FnProduccionNew = @"CREATE OR REPLACE FUNCTION public.fn_indicadores_produccion_postura(p_company_id integer, p_lote_postura_produccion_id integer DEFAULT NULL::integer, p_lote_id integer DEFAULT NULL::integer, p_semana_desde integer DEFAULT NULL::integer, p_semana_hasta integer DEFAULT NULL::integer, p_fecha_desde date DEFAULT NULL::date, p_fecha_hasta date DEFAULT NULL::date)
 RETURNS TABLE(semana integer, fecha_inicio_semana date, fecha_fin_semana date, total_registros integer, mortalidad_hembras integer, mortalidad_machos integer, porcentaje_mortalidad_hembras double precision, porcentaje_mortalidad_machos double precision, mortalidad_guia_hembras double precision, mortalidad_guia_machos double precision, diferencia_mortalidad_hembras double precision, diferencia_mortalidad_machos double precision, seleccion_hembras integer, porcentaje_seleccion_hembras double precision, consumo_kg_hembras double precision, consumo_kg_machos double precision, consumo_total_kg double precision, consumo_promedio_diario_kg double precision, consumo_guia_hembras double precision, consumo_guia_machos double precision, diferencia_consumo_hembras double precision, diferencia_consumo_machos double precision, huevos_totales integer, huevos_incubables integer, promedio_huevos_por_dia double precision, eficiencia_produccion double precision, huevos_totales_guia double precision, huevos_incubables_guia double precision, porcentaje_produccion_guia double precision, diferencia_huevos_totales double precision, diferencia_huevos_incubables double precision, diferencia_porcentaje_produccion double precision, peso_huevo_promedio double precision, peso_huevo_guia double precision, diferencia_peso_huevo double precision, peso_promedio_hembras double precision, peso_promedio_machos double precision, peso_guia_hembras double precision, peso_guia_machos double precision, diferencia_peso_hembras double precision, diferencia_peso_machos double precision, uniformidad_promedio double precision, uniformidad_guia double precision, diferencia_uniformidad double precision, coeficiente_variacion_promedio double precision, huevos_limpios integer, huevos_tratados integer, huevos_sucios integer, huevos_deformes integer, huevos_blancos integer, huevos_doble_yema integer, huevos_piso integer, huevos_pequenos integer, huevos_rotos integer, huevos_desecho integer, huevos_otro integer, aves_hembras_inicio_semana integer, aves_machos_inicio_semana integer, aves_hembras_fin_semana integer, aves_machos_fin_semana integer, htaa_real double precision, hiaa_real double precision)
 LANGUAGE plpgsql
AS $function$
DECLARE
    -- ── contexto del lote resuelto ──
    v_enc_date       date;            -- fechaEncaset.Date (Bogotá)
    v_aves_h_ini     integer;
    v_aves_m_ini     integer;
    v_raza           text;
    v_ano            text;            -- ano_tabla_genetica::text
    v_lote_id_str    text;            -- para el flujo legacy (lote_id como texto)
    v_has_lote       boolean := false;

    -- ── acumuladores iterativos (mismos que el C#) ──
    v_aves_h_act     integer;
    v_aves_m_act     integer;
    v_cum_h_tot      bigint := 0;
    v_cum_h_inc      bigint := 0;

    v_max_sem        integer;
    s                integer;

    -- ── por semana ──
    r_dias           integer;
    r_mort_h         integer;
    r_mort_m         integer;
    r_sel_h          integer;
    r_cons_kg_h      double precision;
    r_cons_kg_m      double precision;
    r_huevos_tot     integer;
    r_huevos_inc     integer;
    r_prom_huevos    double precision;
    r_efic           double precision;
    r_htaa           double precision;
    r_hiaa           double precision;
    r_peso_h         double precision;
    r_peso_m         double precision;
    r_unif           double precision;
    r_cv             double precision;
    r_peso_huevo     double precision;
    r_porc_mort_h    double precision;
    r_porc_mort_m    double precision;
    r_porc_sel_h     double precision;
    r_aves_h_inicio  integer;
    r_aves_m_inicio  integer;
    -- guía
    g_cons_h         double precision;
    g_cons_m         double precision;
    g_mort_h         double precision;
    g_mort_m         double precision;
    g_peso_h         double precision;
    g_peso_m         double precision;
    g_unif           double precision;
    g_huevos_tot     double precision;
    g_huevos_inc     double precision;
    g_prod_pct       double precision;
    g_peso_huevo     double precision;
    g_found          boolean;
    -- consumo real
    r_cons_real_h    double precision;
    r_cons_real_m    double precision;
    -- clasificadora
    r_limpios        integer;
    r_tratados       integer;
    r_sucios         integer;
    r_deformes       integer;
    r_blancos        integer;
    r_doble_yema     integer;
    r_piso           integer;
    r_pequenos       integer;
    r_rotos          integer;
    r_desecho        integer;
    r_otro           integer;
BEGIN
    -- ════════════════════════════════════════════════════════════════════
    -- 1) RESOLVER LOTE (misma prioridad y semántica que el C#).
    -- ════════════════════════════════════════════════════════════════════
    IF p_lote_postura_produccion_id IS NOT NULL AND p_lote_postura_produccion_id > 0 THEN
        -- ── Flujo LPP ──
        SELECT
            -- fecha ref: encaset del levante ligado -> lpp.fecha_encaset -> lpp.fecha_inicio_produccion
            (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date,
            COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0),
            COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0),
            COALESCE(lpp.raza, ''),
            lpp.ano_tabla_genetica::text
          INTO v_enc_date, v_aves_h_ini, v_aves_m_ini, v_raza, v_ano
          FROM lote_postura_produccion lpp
          LEFT JOIN lote_postura_levante lev
                 ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
                AND lev.deleted_at IS NULL
         WHERE lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
           AND lpp.company_id = p_company_id
           AND lpp.deleted_at IS NULL;

        IF NOT FOUND OR v_enc_date IS NULL THEN
            RETURN;  -- lote inexistente o sin fecha de referencia -> sin filas (el C# lanza; el servicio valida antes)
        END IF;
        v_has_lote := true;

        -- Seguimientos: unificado (tipo produccion) UNION legacy, merge por DÍA (Bogotá),
        -- el registro más temprano (por timestamp) gana el día.  (== C# GroupBy(Fecha.Date).First())
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        WITH crudos AS (
            SELECT sd.fecha AS ts,
                   COALESCE(sd.mortalidad_hembras,0) AS mort_h, COALESCE(sd.mortalidad_machos,0) AS mort_m,
                   COALESCE(sd.sel_h,0) AS sel_h,
                   COALESCE(sd.consumo_kg_hembras,0)::double precision AS cons_h,
                   COALESCE(sd.consumo_kg_machos,0)::double precision AS cons_m,
                   COALESCE(sd.huevo_tot,0) AS huevo_tot, COALESCE(sd.huevo_inc,0) AS huevo_inc,
                   COALESCE(sd.huevo_limpio,0) AS h_limpio, COALESCE(sd.huevo_tratado,0) AS h_tratado,
                   COALESCE(sd.huevo_sucio,0) AS h_sucio, COALESCE(sd.huevo_deforme,0) AS h_deforme,
                   COALESCE(sd.huevo_blanco,0) AS h_blanco, COALESCE(sd.huevo_doble_yema,0) AS h_doble,
                   COALESCE(sd.huevo_piso,0) AS h_piso, COALESCE(sd.huevo_pequeno,0) AS h_pequeno,
                   COALESCE(sd.huevo_roto,0) AS h_roto, COALESCE(sd.huevo_desecho,0) AS h_desecho,
                   COALESCE(sd.huevo_otro,0) AS h_otro,
                   sd.peso_huevo::double precision AS peso_huevo,
                   sd.peso_h::double precision AS peso_h, sd.peso_m::double precision AS peso_m,
                   sd.uniformidad::double precision AS unif, sd.coeficiente_variacion::double precision AS cv
              FROM seguimiento_diario_levante sd
             WHERE sd.tipo_seguimiento = 'produccion'
               AND sd.lote_postura_produccion_id = p_lote_postura_produccion_id
            UNION ALL
            SELECT sp.fecha_registro AS ts,
                   sp.mortalidad_hembras, sp.mortalidad_machos, sp.sel_h,
                   sp.cons_kg_h::double precision, sp.cons_kg_m::double precision,
                   sp.huevo_tot, sp.huevo_inc, sp.huevo_limpio, sp.huevo_tratado, sp.huevo_sucio,
                   sp.huevo_deforme, sp.huevo_blanco, sp.huevo_doble_yema, sp.huevo_piso, sp.huevo_pequeno,
                   sp.huevo_roto, sp.huevo_desecho, sp.huevo_otro,
                   sp.peso_huevo::double precision,
                   sp.peso_h::double precision, sp.peso_m::double precision,
                   sp.uniformidad::double precision, sp.coeficiente_variacion::double precision
              FROM seguimiento_diario_produccion sp
             WHERE sp.lote_postura_produccion_id = p_lote_postura_produccion_id
        ),
        dedup AS (
            SELECT DISTINCT ON ((ts AT TIME ZONE 'America/Bogota')::date) *
              FROM crudos
             ORDER BY (ts AT TIME ZONE 'America/Bogota')::date, ts
        )
        SELECT * FROM dedup;

    ELSIF p_lote_id IS NOT NULL AND p_lote_id > 0 THEN
        -- ── Flujo legacy: Lote en fase Producción ──
        -- lote_prod: hijo (lote_padre_id = p_lote_id) en fase Produccion; si no, el propio lote_id.
        DECLARE
            v_lp_lote_id      integer;
            v_lp_padre_id     integer;
            v_lp_fip          timestamptz;
            v_lp_raza         text;
            v_lp_ano          integer;
        BEGIN
            SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
              INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
              FROM lotes l
             WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
               AND l.fase = 'Produccion' AND l.lote_padre_id = p_lote_id
             ORDER BY l.lote_id
             LIMIT 1;

            IF NOT FOUND THEN
                SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
                  INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
                  FROM lotes l
                 WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
                   AND l.fase = 'Produccion' AND l.lote_id = p_lote_id
                 LIMIT 1;
            END IF;

            IF NOT FOUND THEN
                RETURN;
            END IF;
            v_has_lote := true;
            v_lote_id_str := v_lp_lote_id::text;

            -- fecha ref = fecha_inicio_produccion; si null y hay padre -> fecha_encaset del padre
            IF v_lp_fip IS NULL AND v_lp_padre_id IS NOT NULL THEN
                SELECT p.fecha_encaset INTO v_lp_fip
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
            IF v_lp_fip IS NULL THEN
                RETURN;
            END IF;
            v_enc_date := (v_lp_fip AT TIME ZONE 'America/Bogota')::date;

            SELECT COALESCE(hembras_iniciales_prod,0), COALESCE(machos_iniciales_prod,0)
              INTO v_aves_h_ini, v_aves_m_ini
              FROM lotes WHERE lote_id = v_lp_lote_id;

            -- raza/año del lote; si faltan y hay padre, del padre
            v_raza := COALESCE(v_lp_raza, '');
            v_ano  := v_lp_ano::text;
            IF (v_raza = '' OR v_lp_ano IS NULL) AND v_lp_padre_id IS NOT NULL THEN
                SELECT COALESCE(p.raza,''), p.ano_tabla_genetica::text
                  INTO v_raza, v_ano
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
        END;

        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        WITH crudos AS (
            SELECT sd.fecha AS ts,
                   COALESCE(sd.mortalidad_hembras,0) AS mort_h, COALESCE(sd.mortalidad_machos,0) AS mort_m,
                   COALESCE(sd.sel_h,0) AS sel_h,
                   COALESCE(sd.consumo_kg_hembras,0)::double precision AS cons_h,
                   COALESCE(sd.consumo_kg_machos,0)::double precision AS cons_m,
                   COALESCE(sd.huevo_tot,0) AS huevo_tot, COALESCE(sd.huevo_inc,0) AS huevo_inc,
                   COALESCE(sd.huevo_limpio,0) AS h_limpio, COALESCE(sd.huevo_tratado,0) AS h_tratado,
                   COALESCE(sd.huevo_sucio,0) AS h_sucio, COALESCE(sd.huevo_deforme,0) AS h_deforme,
                   COALESCE(sd.huevo_blanco,0) AS h_blanco, COALESCE(sd.huevo_doble_yema,0) AS h_doble,
                   COALESCE(sd.huevo_piso,0) AS h_piso, COALESCE(sd.huevo_pequeno,0) AS h_pequeno,
                   COALESCE(sd.huevo_roto,0) AS h_roto, COALESCE(sd.huevo_desecho,0) AS h_desecho,
                   COALESCE(sd.huevo_otro,0) AS h_otro,
                   sd.peso_huevo::double precision AS peso_huevo,
                   sd.peso_h::double precision AS peso_h, sd.peso_m::double precision AS peso_m,
                   sd.uniformidad::double precision AS unif, sd.coeficiente_variacion::double precision AS cv
              FROM seguimiento_diario_levante sd
             WHERE sd.tipo_seguimiento = 'produccion'
               AND sd.lote_id = v_lote_id_str
            UNION ALL
            SELECT sp.fecha_registro AS ts,
                   sp.mortalidad_hembras, sp.mortalidad_machos, sp.sel_h,
                   sp.cons_kg_h::double precision, sp.cons_kg_m::double precision,
                   sp.huevo_tot, sp.huevo_inc, sp.huevo_limpio, sp.huevo_tratado, sp.huevo_sucio,
                   sp.huevo_deforme, sp.huevo_blanco, sp.huevo_doble_yema, sp.huevo_piso, sp.huevo_pequeno,
                   sp.huevo_roto, sp.huevo_desecho, sp.huevo_otro,
                   sp.peso_huevo::double precision,
                   sp.peso_h::double precision, sp.peso_m::double precision,
                   sp.uniformidad::double precision, sp.coeficiente_variacion::double precision
              FROM seguimiento_diario_produccion sp
             WHERE sp.lote_id::text = v_lote_id_str
        ),
        dedup AS (
            SELECT DISTINCT ON ((ts AT TIME ZONE 'America/Bogota')::date) *
              FROM crudos
             ORDER BY (ts AT TIME ZONE 'America/Bogota')::date, ts
        )
        SELECT * FROM dedup;

    ELSE
        RETURN;  -- ni LPP ni loteId válido
    END IF;

    IF NOT v_has_lote THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 2) Semana de VIDA de cada registro + filtro de fechas (== C#).
    --    semanaVida = floor(dias/7)+1 con dias = regDate - encDate (división entera).
    -- ════════════════════════════════════════════════════════════════════
    ALTER TABLE _seg ADD COLUMN reg_date date;
    ALTER TABLE _seg ADD COLUMN sem_vida integer;
    UPDATE _seg SET reg_date = (ts AT TIME ZONE 'America/Bogota')::date;
    -- filtro de fechas (request.FechaDesde/Hasta) sobre la fecha local
    IF p_fecha_desde IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date < p_fecha_desde;
    END IF;
    IF p_fecha_hasta IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date > p_fecha_hasta;
    END IF;
    UPDATE _seg SET sem_vida = ((reg_date - v_enc_date) / 7) + 1;  -- división entera == C# (dias/7)+1
    -- Solo semanas de producción (>= 26 de vida)
    DELETE FROM _seg WHERE sem_vida < 26;

    SELECT MAX(sem_vida) INTO v_max_sem FROM _seg;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 3) Iterar semanas presentes en orden (== foreach sobre grupos ordenados).
    --    OJO: el C# itera SOLO las semanas con registros (>=26) y en orden asc.
    --    Los acumuladores (aves actuales, htaa/hiaa) avanzan solo en esas semanas.
    -- ════════════════════════════════════════════════════════════════════
    v_aves_h_act := v_aves_h_ini;
    v_aves_m_act := v_aves_m_ini;

    FOR s IN 26..v_max_sem LOOP
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg WHERE sem_vida = s);

        SELECT COUNT(*)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0), COALESCE(SUM(sel_h),0),
               COALESCE(SUM(cons_h),0), COALESCE(SUM(cons_m),0),
               COALESCE(SUM(huevo_tot),0), COALESCE(SUM(huevo_inc),0),
               COALESCE(SUM(h_limpio),0), COALESCE(SUM(h_tratado),0), COALESCE(SUM(h_sucio),0),
               COALESCE(SUM(h_deforme),0), COALESCE(SUM(h_blanco),0), COALESCE(SUM(h_doble),0),
               COALESCE(SUM(h_piso),0), COALESCE(SUM(h_pequeno),0), COALESCE(SUM(h_roto),0),
               COALESCE(SUM(h_desecho),0), COALESCE(SUM(h_otro),0)
          INTO r_dias, r_mort_h, r_mort_m, r_sel_h, r_cons_kg_h, r_cons_kg_m,
               r_huevos_tot, r_huevos_inc,
               r_limpios, r_tratados, r_sucios, r_deformes, r_blancos, r_doble_yema,
               r_piso, r_pequenos, r_rotos, r_desecho, r_otro
          FROM _seg WHERE sem_vida = s;

        r_prom_huevos := CASE WHEN r_dias > 0 THEN r_huevos_tot::double precision / r_dias ELSE 0 END;

        -- REQ-004a: %Producción hen-day = huevos/día / HEMBRAS vivas (solo hembras) * 100
        r_efic := CASE WHEN v_aves_h_act > 0 THEN r_prom_huevos / v_aves_h_act * 100 ELSE 0 END;

        -- Acumulados por ave alojada (REQ-004c)
        v_cum_h_tot := v_cum_h_tot + r_huevos_tot;
        v_cum_h_inc := v_cum_h_inc + r_huevos_inc;
        r_htaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_tot::double precision / v_aves_h_ini ELSE 0 END;
        r_hiaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_inc::double precision / v_aves_h_ini ELSE 0 END;

        -- Peso aves (kg, REQ-004b): promedio de registros con valor NO NULO, luego normalizar.
        SELECT AVG(peso_h) FILTER (WHERE peso_h IS NOT NULL),
               AVG(peso_m) FILTER (WHERE peso_m IS NOT NULL),
               AVG(unif)   FILTER (WHERE unif   IS NOT NULL),
               AVG(cv)     FILTER (WHERE cv     IS NOT NULL),
               AVG(peso_huevo) FILTER (WHERE peso_huevo > 0)
          INTO r_peso_h, r_peso_m, r_unif, r_cv, r_peso_huevo
          FROM _seg WHERE sem_vida = s;
        IF r_peso_h IS NOT NULL THEN r_peso_h := CASE WHEN r_peso_h > 100 THEN r_peso_h/1000 ELSE r_peso_h END; END IF;
        IF r_peso_m IS NOT NULL THEN r_peso_m := CASE WHEN r_peso_m > 100 THEN r_peso_m/1000 ELSE r_peso_m END; END IF;

        -- %mortalidad / %selección: sobre el saldo REAL de inicio (avesActuales)
        r_porc_mort_h := CASE WHEN v_aves_h_act > 0 THEN r_mort_h::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_porc_mort_m := CASE WHEN v_aves_m_act > 0 THEN r_mort_m::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_porc_sel_h  := CASE WHEN v_aves_h_act > 0 THEN r_sel_h::double precision  / v_aves_h_act * 100 ELSE 0 END;

        -- Censo de inicio de semana (desviación preservada: sobrecuenta con las bajas de la propia semana)
        r_aves_h_inicio := v_aves_h_act + r_mort_h + r_sel_h;
        r_aves_m_inicio := v_aves_m_act + r_mort_m;

        -- ── Guía (una sola tabla) por Edad = semana de VIDA (s) ──
        g_found := false;
        SELECT true,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.uniformidad),'')::double precision,
               NULLIF(btrim(g.h_total_aa),'')::double precision,
               NULLIF(btrim(g.h_inc_aa),'')::double precision,
               NULLIF(btrim(g.prod_porcentaje),'')::double precision,
               NULLIF(btrim(g.peso_huevo),'')::double precision
          INTO g_found, g_cons_h, g_cons_m, g_mort_h, g_mort_m, g_peso_h, g_peso_m, g_unif,
               g_huevos_tot, g_huevos_inc, g_prod_pct, g_peso_huevo
          FROM guia_genetica_sanmarino_colombia g
         WHERE g.company_id = p_company_id
           AND g.deleted_at IS NULL
           AND btrim(lower(g.raza)) = btrim(lower(v_raza))
           AND btrim(g.anio_guia) = v_ano
           AND fn_parse_edad_numerica(g.edad) = s
         LIMIT 1;
        g_found := COALESCE(g_found, false);

        IF g_found THEN
            -- ParseDouble => 0 cuando el string es vacío/no numérico (no NULL). Las columnas de la
            -- guía ""obtenerGuiaGeneticaProduccion"" pasan por ParseDouble (0 si vacío); las del raw
            -- (huevos/%prod/pesoHuevo) por ParseDecimal (NULL si vacío). Se respeta esa diferencia:
            g_cons_h := COALESCE(g_cons_h, 0);
            g_cons_m := COALESCE(g_cons_m, 0);
            g_mort_h := COALESCE(g_mort_h, 0);
            g_mort_m := COALESCE(g_mort_m, 0);
            g_peso_h := COALESCE(g_peso_h, 0) / 1000;   -- peso_h/1000
            g_peso_m := COALESCE(g_peso_m, 0) / 1000;   -- peso_m/1000
            g_unif   := COALESCE(g_unif, 0);
            -- huevos/%prod/pesoHuevo: quedan NULL si vacíos (ParseDecimal), no 0.
        ELSE
            g_cons_h := NULL; g_cons_m := NULL; g_mort_h := NULL; g_mort_m := NULL;
            g_peso_h := NULL; g_peso_m := NULL; g_unif := NULL;
            g_huevos_tot := NULL; g_huevos_inc := NULL; g_prod_pct := NULL; g_peso_huevo := NULL;
        END IF;

        -- Consumo real (g/ave/día) — denominador = censo de inicio sobrecontado (desviación preservada)
        r_cons_real_h := CASE WHEN r_dias > 0 AND r_aves_h_inicio > 0
                              THEN r_cons_kg_h * 1000 / (r_dias * r_aves_h_inicio) ELSE NULL END;
        r_cons_real_m := CASE WHEN r_dias > 0 AND r_aves_m_inicio > 0
                              THEN r_cons_kg_m * 1000 / (r_dias * r_aves_m_inicio) ELSE NULL END;

        -- Decremento de aves (al final, == C#)
        v_aves_h_act := GREATEST(0, v_aves_h_act - r_mort_h - r_sel_h);
        v_aves_m_act := GREATEST(0, v_aves_m_act - r_mort_m);

        -- ── Emitir fila (respetando filtro semanaDesde/Hasta como en C#) ──
        IF (p_semana_desde IS NULL OR s >= p_semana_desde)
           AND (p_semana_hasta IS NULL OR s <= p_semana_hasta) THEN
            semana                           := s;
            fecha_inicio_semana              := v_enc_date + ((s - 1) * 7);
            fecha_fin_semana                 := v_enc_date + ((s - 1) * 7) + 6;
            total_registros                  := r_dias;
            mortalidad_hembras               := r_mort_h;
            mortalidad_machos                := r_mort_m;
            porcentaje_mortalidad_hembras    := r_porc_mort_h;
            porcentaje_mortalidad_machos     := r_porc_mort_m;
            mortalidad_guia_hembras          := g_mort_h;
            mortalidad_guia_machos           := g_mort_m;
            diferencia_mortalidad_hembras    := fn_dif_pct(r_porc_mort_h, g_mort_h);
            diferencia_mortalidad_machos     := fn_dif_pct(r_porc_mort_m, g_mort_m);
            seleccion_hembras                := r_sel_h;
            porcentaje_seleccion_hembras     := r_porc_sel_h;
            consumo_kg_hembras               := r_cons_kg_h;
            consumo_kg_machos                := r_cons_kg_m;
            consumo_total_kg                 := r_cons_kg_h + r_cons_kg_m;
            consumo_promedio_diario_kg       := CASE WHEN r_dias > 0 THEN (r_cons_kg_h + r_cons_kg_m)/r_dias ELSE 0 END;
            consumo_guia_hembras             := g_cons_h;
            consumo_guia_machos              := g_cons_m;
            diferencia_consumo_hembras       := fn_dif_pct(r_cons_real_h, g_cons_h);
            diferencia_consumo_machos        := fn_dif_pct(r_cons_real_m, g_cons_m);
            huevos_totales                   := r_huevos_tot;
            huevos_incubables                := r_huevos_inc;
            promedio_huevos_por_dia          := r_prom_huevos;
            eficiencia_produccion            := r_efic;
            huevos_totales_guia              := g_huevos_tot;
            huevos_incubables_guia           := g_huevos_inc;
            porcentaje_produccion_guia       := g_prod_pct;
            diferencia_huevos_totales        := fn_dif_pct(r_htaa, g_huevos_tot);
            diferencia_huevos_incubables     := fn_dif_pct(r_hiaa, g_huevos_inc);
            diferencia_porcentaje_produccion := fn_dif_pct(r_efic, g_prod_pct);
            peso_huevo_promedio              := r_peso_huevo;
            peso_huevo_guia                  := g_peso_huevo;
            diferencia_peso_huevo            := fn_dif_pct(r_peso_huevo, g_peso_huevo);
            peso_promedio_hembras            := r_peso_h;
            peso_promedio_machos             := r_peso_m;
            peso_guia_hembras                := g_peso_h;
            peso_guia_machos                 := g_peso_m;
            diferencia_peso_hembras          := fn_dif_pct(r_peso_h, g_peso_h);
            diferencia_peso_machos           := fn_dif_pct(r_peso_m, g_peso_m);
            uniformidad_promedio             := r_unif;
            uniformidad_guia                 := g_unif;
            diferencia_uniformidad           := fn_dif_pct(r_unif, g_unif);
            coeficiente_variacion_promedio   := r_cv;
            huevos_limpios                   := r_limpios;
            huevos_tratados                  := r_tratados;
            huevos_sucios                    := r_sucios;
            huevos_deformes                  := r_deformes;
            huevos_blancos                   := r_blancos;
            huevos_doble_yema                := r_doble_yema;
            huevos_piso                      := r_piso;
            huevos_pequenos                  := r_pequenos;
            huevos_rotos                     := r_rotos;
            huevos_desecho                   := r_desecho;
            huevos_otro                      := r_otro;
            aves_hembras_inicio_semana       := r_aves_h_inicio;
            aves_machos_inicio_semana        := r_aves_m_inicio;
            aves_hembras_fin_semana          := v_aves_h_act;
            aves_machos_fin_semana           := v_aves_m_act;
            htaa_real                        := r_htaa;
            hiaa_real                        := r_hiaa;
            RETURN NEXT;
        END IF;
    END LOOP;

    RETURN;
END;
$function$

";

        private const string SpLevanteNew = @"CREATE OR REPLACE FUNCTION public.sp_recalcular_seguimiento_levante(l_lote_id text)
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
    from seguimiento_diario_levante s
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
$function$

";

        private const string FnLevanteOrig = @"CREATE OR REPLACE FUNCTION public.fn_indicadores_levante_postura(p_lote_id integer)
 RETURNS TABLE(semana integer, aves_inicio_semana double precision, aves_fin_semana double precision, consumo_diario double precision, consumo_tabla double precision, consumo_total_semana double precision, conversion_alimenticia double precision, peso_tabla double precision, unif_real double precision, unif_tabla double precision, mort_tabla double precision, dif_peso_pct double precision, ganancia_semana double precision, ganancia_diaria_acumulada double precision, ganancia_tabla double precision, mortalidad_sem double precision, seleccion_sem double precision, error_sexaje_sem double precision, mortalidad_mas_seleccion double precision, eficiencia double precision, ip double precision, vpi double precision, saldo_aves_semanal double precision, mortalidad_acum double precision, seleccion_acum double precision, mortalidad_mas_seleccion_acum double precision, piso_termico_visible boolean, peso_inicial double precision, peso_cierre double precision, dias_con_registro integer)
 LANGUAGE plpgsql
AS $function$
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
$function$

";

        private const string FnProduccionOrig = @"CREATE OR REPLACE FUNCTION public.fn_indicadores_produccion_postura(p_company_id integer, p_lote_postura_produccion_id integer DEFAULT NULL::integer, p_lote_id integer DEFAULT NULL::integer, p_semana_desde integer DEFAULT NULL::integer, p_semana_hasta integer DEFAULT NULL::integer, p_fecha_desde date DEFAULT NULL::date, p_fecha_hasta date DEFAULT NULL::date)
 RETURNS TABLE(semana integer, fecha_inicio_semana date, fecha_fin_semana date, total_registros integer, mortalidad_hembras integer, mortalidad_machos integer, porcentaje_mortalidad_hembras double precision, porcentaje_mortalidad_machos double precision, mortalidad_guia_hembras double precision, mortalidad_guia_machos double precision, diferencia_mortalidad_hembras double precision, diferencia_mortalidad_machos double precision, seleccion_hembras integer, porcentaje_seleccion_hembras double precision, consumo_kg_hembras double precision, consumo_kg_machos double precision, consumo_total_kg double precision, consumo_promedio_diario_kg double precision, consumo_guia_hembras double precision, consumo_guia_machos double precision, diferencia_consumo_hembras double precision, diferencia_consumo_machos double precision, huevos_totales integer, huevos_incubables integer, promedio_huevos_por_dia double precision, eficiencia_produccion double precision, huevos_totales_guia double precision, huevos_incubables_guia double precision, porcentaje_produccion_guia double precision, diferencia_huevos_totales double precision, diferencia_huevos_incubables double precision, diferencia_porcentaje_produccion double precision, peso_huevo_promedio double precision, peso_huevo_guia double precision, diferencia_peso_huevo double precision, peso_promedio_hembras double precision, peso_promedio_machos double precision, peso_guia_hembras double precision, peso_guia_machos double precision, diferencia_peso_hembras double precision, diferencia_peso_machos double precision, uniformidad_promedio double precision, uniformidad_guia double precision, diferencia_uniformidad double precision, coeficiente_variacion_promedio double precision, huevos_limpios integer, huevos_tratados integer, huevos_sucios integer, huevos_deformes integer, huevos_blancos integer, huevos_doble_yema integer, huevos_piso integer, huevos_pequenos integer, huevos_rotos integer, huevos_desecho integer, huevos_otro integer, aves_hembras_inicio_semana integer, aves_machos_inicio_semana integer, aves_hembras_fin_semana integer, aves_machos_fin_semana integer, htaa_real double precision, hiaa_real double precision)
 LANGUAGE plpgsql
AS $function$
DECLARE
    -- ── contexto del lote resuelto ──
    v_enc_date       date;            -- fechaEncaset.Date (Bogotá)
    v_aves_h_ini     integer;
    v_aves_m_ini     integer;
    v_raza           text;
    v_ano            text;            -- ano_tabla_genetica::text
    v_lote_id_str    text;            -- para el flujo legacy (lote_id como texto)
    v_has_lote       boolean := false;

    -- ── acumuladores iterativos (mismos que el C#) ──
    v_aves_h_act     integer;
    v_aves_m_act     integer;
    v_cum_h_tot      bigint := 0;
    v_cum_h_inc      bigint := 0;

    v_max_sem        integer;
    s                integer;

    -- ── por semana ──
    r_dias           integer;
    r_mort_h         integer;
    r_mort_m         integer;
    r_sel_h          integer;
    r_cons_kg_h      double precision;
    r_cons_kg_m      double precision;
    r_huevos_tot     integer;
    r_huevos_inc     integer;
    r_prom_huevos    double precision;
    r_efic           double precision;
    r_htaa           double precision;
    r_hiaa           double precision;
    r_peso_h         double precision;
    r_peso_m         double precision;
    r_unif           double precision;
    r_cv             double precision;
    r_peso_huevo     double precision;
    r_porc_mort_h    double precision;
    r_porc_mort_m    double precision;
    r_porc_sel_h     double precision;
    r_aves_h_inicio  integer;
    r_aves_m_inicio  integer;
    -- guía
    g_cons_h         double precision;
    g_cons_m         double precision;
    g_mort_h         double precision;
    g_mort_m         double precision;
    g_peso_h         double precision;
    g_peso_m         double precision;
    g_unif           double precision;
    g_huevos_tot     double precision;
    g_huevos_inc     double precision;
    g_prod_pct       double precision;
    g_peso_huevo     double precision;
    g_found          boolean;
    -- consumo real
    r_cons_real_h    double precision;
    r_cons_real_m    double precision;
    -- clasificadora
    r_limpios        integer;
    r_tratados       integer;
    r_sucios         integer;
    r_deformes       integer;
    r_blancos        integer;
    r_doble_yema     integer;
    r_piso           integer;
    r_pequenos       integer;
    r_rotos          integer;
    r_desecho        integer;
    r_otro           integer;
BEGIN
    -- ════════════════════════════════════════════════════════════════════
    -- 1) RESOLVER LOTE (misma prioridad y semántica que el C#).
    -- ════════════════════════════════════════════════════════════════════
    IF p_lote_postura_produccion_id IS NOT NULL AND p_lote_postura_produccion_id > 0 THEN
        -- ── Flujo LPP ──
        SELECT
            -- fecha ref: encaset del levante ligado -> lpp.fecha_encaset -> lpp.fecha_inicio_produccion
            (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date,
            COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0),
            COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0),
            COALESCE(lpp.raza, ''),
            lpp.ano_tabla_genetica::text
          INTO v_enc_date, v_aves_h_ini, v_aves_m_ini, v_raza, v_ano
          FROM lote_postura_produccion lpp
          LEFT JOIN lote_postura_levante lev
                 ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
                AND lev.deleted_at IS NULL
         WHERE lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
           AND lpp.company_id = p_company_id
           AND lpp.deleted_at IS NULL;

        IF NOT FOUND OR v_enc_date IS NULL THEN
            RETURN;  -- lote inexistente o sin fecha de referencia -> sin filas (el C# lanza; el servicio valida antes)
        END IF;
        v_has_lote := true;

        -- Seguimientos: unificado (tipo produccion) UNION legacy, merge por DÍA (Bogotá),
        -- el registro más temprano (por timestamp) gana el día.  (== C# GroupBy(Fecha.Date).First())
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        WITH crudos AS (
            SELECT sd.fecha AS ts,
                   COALESCE(sd.mortalidad_hembras,0) AS mort_h, COALESCE(sd.mortalidad_machos,0) AS mort_m,
                   COALESCE(sd.sel_h,0) AS sel_h,
                   COALESCE(sd.consumo_kg_hembras,0)::double precision AS cons_h,
                   COALESCE(sd.consumo_kg_machos,0)::double precision AS cons_m,
                   COALESCE(sd.huevo_tot,0) AS huevo_tot, COALESCE(sd.huevo_inc,0) AS huevo_inc,
                   COALESCE(sd.huevo_limpio,0) AS h_limpio, COALESCE(sd.huevo_tratado,0) AS h_tratado,
                   COALESCE(sd.huevo_sucio,0) AS h_sucio, COALESCE(sd.huevo_deforme,0) AS h_deforme,
                   COALESCE(sd.huevo_blanco,0) AS h_blanco, COALESCE(sd.huevo_doble_yema,0) AS h_doble,
                   COALESCE(sd.huevo_piso,0) AS h_piso, COALESCE(sd.huevo_pequeno,0) AS h_pequeno,
                   COALESCE(sd.huevo_roto,0) AS h_roto, COALESCE(sd.huevo_desecho,0) AS h_desecho,
                   COALESCE(sd.huevo_otro,0) AS h_otro,
                   sd.peso_huevo::double precision AS peso_huevo,
                   sd.peso_h::double precision AS peso_h, sd.peso_m::double precision AS peso_m,
                   sd.uniformidad::double precision AS unif, sd.coeficiente_variacion::double precision AS cv
              FROM seguimiento_diario_levante_reproductoras sd
             WHERE sd.tipo_seguimiento = 'produccion'
               AND sd.lote_postura_produccion_id = p_lote_postura_produccion_id
            UNION ALL
            SELECT sp.fecha_registro AS ts,
                   sp.mortalidad_hembras, sp.mortalidad_machos, sp.sel_h,
                   sp.cons_kg_h::double precision, sp.cons_kg_m::double precision,
                   sp.huevo_tot, sp.huevo_inc, sp.huevo_limpio, sp.huevo_tratado, sp.huevo_sucio,
                   sp.huevo_deforme, sp.huevo_blanco, sp.huevo_doble_yema, sp.huevo_piso, sp.huevo_pequeno,
                   sp.huevo_roto, sp.huevo_desecho, sp.huevo_otro,
                   sp.peso_huevo::double precision,
                   sp.peso_h::double precision, sp.peso_m::double precision,
                   sp.uniformidad::double precision, sp.coeficiente_variacion::double precision
              FROM seguimiento_diario_produccion_reproductoras sp
             WHERE sp.lote_postura_produccion_id = p_lote_postura_produccion_id
        ),
        dedup AS (
            SELECT DISTINCT ON ((ts AT TIME ZONE 'America/Bogota')::date) *
              FROM crudos
             ORDER BY (ts AT TIME ZONE 'America/Bogota')::date, ts
        )
        SELECT * FROM dedup;

    ELSIF p_lote_id IS NOT NULL AND p_lote_id > 0 THEN
        -- ── Flujo legacy: Lote en fase Producción ──
        -- lote_prod: hijo (lote_padre_id = p_lote_id) en fase Produccion; si no, el propio lote_id.
        DECLARE
            v_lp_lote_id      integer;
            v_lp_padre_id     integer;
            v_lp_fip          timestamptz;
            v_lp_raza         text;
            v_lp_ano          integer;
        BEGIN
            SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
              INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
              FROM lotes l
             WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
               AND l.fase = 'Produccion' AND l.lote_padre_id = p_lote_id
             ORDER BY l.lote_id
             LIMIT 1;

            IF NOT FOUND THEN
                SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
                  INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
                  FROM lotes l
                 WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
                   AND l.fase = 'Produccion' AND l.lote_id = p_lote_id
                 LIMIT 1;
            END IF;

            IF NOT FOUND THEN
                RETURN;
            END IF;
            v_has_lote := true;
            v_lote_id_str := v_lp_lote_id::text;

            -- fecha ref = fecha_inicio_produccion; si null y hay padre -> fecha_encaset del padre
            IF v_lp_fip IS NULL AND v_lp_padre_id IS NOT NULL THEN
                SELECT p.fecha_encaset INTO v_lp_fip
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
            IF v_lp_fip IS NULL THEN
                RETURN;
            END IF;
            v_enc_date := (v_lp_fip AT TIME ZONE 'America/Bogota')::date;

            SELECT COALESCE(hembras_iniciales_prod,0), COALESCE(machos_iniciales_prod,0)
              INTO v_aves_h_ini, v_aves_m_ini
              FROM lotes WHERE lote_id = v_lp_lote_id;

            -- raza/año del lote; si faltan y hay padre, del padre
            v_raza := COALESCE(v_lp_raza, '');
            v_ano  := v_lp_ano::text;
            IF (v_raza = '' OR v_lp_ano IS NULL) AND v_lp_padre_id IS NOT NULL THEN
                SELECT COALESCE(p.raza,''), p.ano_tabla_genetica::text
                  INTO v_raza, v_ano
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
        END;

        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        WITH crudos AS (
            SELECT sd.fecha AS ts,
                   COALESCE(sd.mortalidad_hembras,0) AS mort_h, COALESCE(sd.mortalidad_machos,0) AS mort_m,
                   COALESCE(sd.sel_h,0) AS sel_h,
                   COALESCE(sd.consumo_kg_hembras,0)::double precision AS cons_h,
                   COALESCE(sd.consumo_kg_machos,0)::double precision AS cons_m,
                   COALESCE(sd.huevo_tot,0) AS huevo_tot, COALESCE(sd.huevo_inc,0) AS huevo_inc,
                   COALESCE(sd.huevo_limpio,0) AS h_limpio, COALESCE(sd.huevo_tratado,0) AS h_tratado,
                   COALESCE(sd.huevo_sucio,0) AS h_sucio, COALESCE(sd.huevo_deforme,0) AS h_deforme,
                   COALESCE(sd.huevo_blanco,0) AS h_blanco, COALESCE(sd.huevo_doble_yema,0) AS h_doble,
                   COALESCE(sd.huevo_piso,0) AS h_piso, COALESCE(sd.huevo_pequeno,0) AS h_pequeno,
                   COALESCE(sd.huevo_roto,0) AS h_roto, COALESCE(sd.huevo_desecho,0) AS h_desecho,
                   COALESCE(sd.huevo_otro,0) AS h_otro,
                   sd.peso_huevo::double precision AS peso_huevo,
                   sd.peso_h::double precision AS peso_h, sd.peso_m::double precision AS peso_m,
                   sd.uniformidad::double precision AS unif, sd.coeficiente_variacion::double precision AS cv
              FROM seguimiento_diario_levante_reproductoras sd
             WHERE sd.tipo_seguimiento = 'produccion'
               AND sd.lote_id = v_lote_id_str
            UNION ALL
            SELECT sp.fecha_registro AS ts,
                   sp.mortalidad_hembras, sp.mortalidad_machos, sp.sel_h,
                   sp.cons_kg_h::double precision, sp.cons_kg_m::double precision,
                   sp.huevo_tot, sp.huevo_inc, sp.huevo_limpio, sp.huevo_tratado, sp.huevo_sucio,
                   sp.huevo_deforme, sp.huevo_blanco, sp.huevo_doble_yema, sp.huevo_piso, sp.huevo_pequeno,
                   sp.huevo_roto, sp.huevo_desecho, sp.huevo_otro,
                   sp.peso_huevo::double precision,
                   sp.peso_h::double precision, sp.peso_m::double precision,
                   sp.uniformidad::double precision, sp.coeficiente_variacion::double precision
              FROM seguimiento_diario_produccion_reproductoras sp
             WHERE sp.lote_id::text = v_lote_id_str
        ),
        dedup AS (
            SELECT DISTINCT ON ((ts AT TIME ZONE 'America/Bogota')::date) *
              FROM crudos
             ORDER BY (ts AT TIME ZONE 'America/Bogota')::date, ts
        )
        SELECT * FROM dedup;

    ELSE
        RETURN;  -- ni LPP ni loteId válido
    END IF;

    IF NOT v_has_lote THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 2) Semana de VIDA de cada registro + filtro de fechas (== C#).
    --    semanaVida = floor(dias/7)+1 con dias = regDate - encDate (división entera).
    -- ════════════════════════════════════════════════════════════════════
    ALTER TABLE _seg ADD COLUMN reg_date date;
    ALTER TABLE _seg ADD COLUMN sem_vida integer;
    UPDATE _seg SET reg_date = (ts AT TIME ZONE 'America/Bogota')::date;
    -- filtro de fechas (request.FechaDesde/Hasta) sobre la fecha local
    IF p_fecha_desde IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date < p_fecha_desde;
    END IF;
    IF p_fecha_hasta IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date > p_fecha_hasta;
    END IF;
    UPDATE _seg SET sem_vida = ((reg_date - v_enc_date) / 7) + 1;  -- división entera == C# (dias/7)+1
    -- Solo semanas de producción (>= 26 de vida)
    DELETE FROM _seg WHERE sem_vida < 26;

    SELECT MAX(sem_vida) INTO v_max_sem FROM _seg;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 3) Iterar semanas presentes en orden (== foreach sobre grupos ordenados).
    --    OJO: el C# itera SOLO las semanas con registros (>=26) y en orden asc.
    --    Los acumuladores (aves actuales, htaa/hiaa) avanzan solo en esas semanas.
    -- ════════════════════════════════════════════════════════════════════
    v_aves_h_act := v_aves_h_ini;
    v_aves_m_act := v_aves_m_ini;

    FOR s IN 26..v_max_sem LOOP
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg WHERE sem_vida = s);

        SELECT COUNT(*)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0), COALESCE(SUM(sel_h),0),
               COALESCE(SUM(cons_h),0), COALESCE(SUM(cons_m),0),
               COALESCE(SUM(huevo_tot),0), COALESCE(SUM(huevo_inc),0),
               COALESCE(SUM(h_limpio),0), COALESCE(SUM(h_tratado),0), COALESCE(SUM(h_sucio),0),
               COALESCE(SUM(h_deforme),0), COALESCE(SUM(h_blanco),0), COALESCE(SUM(h_doble),0),
               COALESCE(SUM(h_piso),0), COALESCE(SUM(h_pequeno),0), COALESCE(SUM(h_roto),0),
               COALESCE(SUM(h_desecho),0), COALESCE(SUM(h_otro),0)
          INTO r_dias, r_mort_h, r_mort_m, r_sel_h, r_cons_kg_h, r_cons_kg_m,
               r_huevos_tot, r_huevos_inc,
               r_limpios, r_tratados, r_sucios, r_deformes, r_blancos, r_doble_yema,
               r_piso, r_pequenos, r_rotos, r_desecho, r_otro
          FROM _seg WHERE sem_vida = s;

        r_prom_huevos := CASE WHEN r_dias > 0 THEN r_huevos_tot::double precision / r_dias ELSE 0 END;

        -- REQ-004a: %Producción hen-day = huevos/día / HEMBRAS vivas (solo hembras) * 100
        r_efic := CASE WHEN v_aves_h_act > 0 THEN r_prom_huevos / v_aves_h_act * 100 ELSE 0 END;

        -- Acumulados por ave alojada (REQ-004c)
        v_cum_h_tot := v_cum_h_tot + r_huevos_tot;
        v_cum_h_inc := v_cum_h_inc + r_huevos_inc;
        r_htaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_tot::double precision / v_aves_h_ini ELSE 0 END;
        r_hiaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_inc::double precision / v_aves_h_ini ELSE 0 END;

        -- Peso aves (kg, REQ-004b): promedio de registros con valor NO NULO, luego normalizar.
        SELECT AVG(peso_h) FILTER (WHERE peso_h IS NOT NULL),
               AVG(peso_m) FILTER (WHERE peso_m IS NOT NULL),
               AVG(unif)   FILTER (WHERE unif   IS NOT NULL),
               AVG(cv)     FILTER (WHERE cv     IS NOT NULL),
               AVG(peso_huevo) FILTER (WHERE peso_huevo > 0)
          INTO r_peso_h, r_peso_m, r_unif, r_cv, r_peso_huevo
          FROM _seg WHERE sem_vida = s;
        IF r_peso_h IS NOT NULL THEN r_peso_h := CASE WHEN r_peso_h > 100 THEN r_peso_h/1000 ELSE r_peso_h END; END IF;
        IF r_peso_m IS NOT NULL THEN r_peso_m := CASE WHEN r_peso_m > 100 THEN r_peso_m/1000 ELSE r_peso_m END; END IF;

        -- %mortalidad / %selección: sobre el saldo REAL de inicio (avesActuales)
        r_porc_mort_h := CASE WHEN v_aves_h_act > 0 THEN r_mort_h::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_porc_mort_m := CASE WHEN v_aves_m_act > 0 THEN r_mort_m::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_porc_sel_h  := CASE WHEN v_aves_h_act > 0 THEN r_sel_h::double precision  / v_aves_h_act * 100 ELSE 0 END;

        -- Censo de inicio de semana (desviación preservada: sobrecuenta con las bajas de la propia semana)
        r_aves_h_inicio := v_aves_h_act + r_mort_h + r_sel_h;
        r_aves_m_inicio := v_aves_m_act + r_mort_m;

        -- ── Guía (una sola tabla) por Edad = semana de VIDA (s) ──
        g_found := false;
        SELECT true,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.uniformidad),'')::double precision,
               NULLIF(btrim(g.h_total_aa),'')::double precision,
               NULLIF(btrim(g.h_inc_aa),'')::double precision,
               NULLIF(btrim(g.prod_porcentaje),'')::double precision,
               NULLIF(btrim(g.peso_huevo),'')::double precision
          INTO g_found, g_cons_h, g_cons_m, g_mort_h, g_mort_m, g_peso_h, g_peso_m, g_unif,
               g_huevos_tot, g_huevos_inc, g_prod_pct, g_peso_huevo
          FROM guia_genetica_sanmarino_colombia g
         WHERE g.company_id = p_company_id
           AND g.deleted_at IS NULL
           AND btrim(lower(g.raza)) = btrim(lower(v_raza))
           AND btrim(g.anio_guia) = v_ano
           AND fn_parse_edad_numerica(g.edad) = s
         LIMIT 1;
        g_found := COALESCE(g_found, false);

        IF g_found THEN
            -- ParseDouble => 0 cuando el string es vacío/no numérico (no NULL). Las columnas de la
            -- guía ""obtenerGuiaGeneticaProduccion"" pasan por ParseDouble (0 si vacío); las del raw
            -- (huevos/%prod/pesoHuevo) por ParseDecimal (NULL si vacío). Se respeta esa diferencia:
            g_cons_h := COALESCE(g_cons_h, 0);
            g_cons_m := COALESCE(g_cons_m, 0);
            g_mort_h := COALESCE(g_mort_h, 0);
            g_mort_m := COALESCE(g_mort_m, 0);
            g_peso_h := COALESCE(g_peso_h, 0) / 1000;   -- peso_h/1000
            g_peso_m := COALESCE(g_peso_m, 0) / 1000;   -- peso_m/1000
            g_unif   := COALESCE(g_unif, 0);
            -- huevos/%prod/pesoHuevo: quedan NULL si vacíos (ParseDecimal), no 0.
        ELSE
            g_cons_h := NULL; g_cons_m := NULL; g_mort_h := NULL; g_mort_m := NULL;
            g_peso_h := NULL; g_peso_m := NULL; g_unif := NULL;
            g_huevos_tot := NULL; g_huevos_inc := NULL; g_prod_pct := NULL; g_peso_huevo := NULL;
        END IF;

        -- Consumo real (g/ave/día) — denominador = censo de inicio sobrecontado (desviación preservada)
        r_cons_real_h := CASE WHEN r_dias > 0 AND r_aves_h_inicio > 0
                              THEN r_cons_kg_h * 1000 / (r_dias * r_aves_h_inicio) ELSE NULL END;
        r_cons_real_m := CASE WHEN r_dias > 0 AND r_aves_m_inicio > 0
                              THEN r_cons_kg_m * 1000 / (r_dias * r_aves_m_inicio) ELSE NULL END;

        -- Decremento de aves (al final, == C#)
        v_aves_h_act := GREATEST(0, v_aves_h_act - r_mort_h - r_sel_h);
        v_aves_m_act := GREATEST(0, v_aves_m_act - r_mort_m);

        -- ── Emitir fila (respetando filtro semanaDesde/Hasta como en C#) ──
        IF (p_semana_desde IS NULL OR s >= p_semana_desde)
           AND (p_semana_hasta IS NULL OR s <= p_semana_hasta) THEN
            semana                           := s;
            fecha_inicio_semana              := v_enc_date + ((s - 1) * 7);
            fecha_fin_semana                 := v_enc_date + ((s - 1) * 7) + 6;
            total_registros                  := r_dias;
            mortalidad_hembras               := r_mort_h;
            mortalidad_machos                := r_mort_m;
            porcentaje_mortalidad_hembras    := r_porc_mort_h;
            porcentaje_mortalidad_machos     := r_porc_mort_m;
            mortalidad_guia_hembras          := g_mort_h;
            mortalidad_guia_machos           := g_mort_m;
            diferencia_mortalidad_hembras    := fn_dif_pct(r_porc_mort_h, g_mort_h);
            diferencia_mortalidad_machos     := fn_dif_pct(r_porc_mort_m, g_mort_m);
            seleccion_hembras                := r_sel_h;
            porcentaje_seleccion_hembras     := r_porc_sel_h;
            consumo_kg_hembras               := r_cons_kg_h;
            consumo_kg_machos                := r_cons_kg_m;
            consumo_total_kg                 := r_cons_kg_h + r_cons_kg_m;
            consumo_promedio_diario_kg       := CASE WHEN r_dias > 0 THEN (r_cons_kg_h + r_cons_kg_m)/r_dias ELSE 0 END;
            consumo_guia_hembras             := g_cons_h;
            consumo_guia_machos              := g_cons_m;
            diferencia_consumo_hembras       := fn_dif_pct(r_cons_real_h, g_cons_h);
            diferencia_consumo_machos        := fn_dif_pct(r_cons_real_m, g_cons_m);
            huevos_totales                   := r_huevos_tot;
            huevos_incubables                := r_huevos_inc;
            promedio_huevos_por_dia          := r_prom_huevos;
            eficiencia_produccion            := r_efic;
            huevos_totales_guia              := g_huevos_tot;
            huevos_incubables_guia           := g_huevos_inc;
            porcentaje_produccion_guia       := g_prod_pct;
            diferencia_huevos_totales        := fn_dif_pct(r_htaa, g_huevos_tot);
            diferencia_huevos_incubables     := fn_dif_pct(r_hiaa, g_huevos_inc);
            diferencia_porcentaje_produccion := fn_dif_pct(r_efic, g_prod_pct);
            peso_huevo_promedio              := r_peso_huevo;
            peso_huevo_guia                  := g_peso_huevo;
            diferencia_peso_huevo            := fn_dif_pct(r_peso_huevo, g_peso_huevo);
            peso_promedio_hembras            := r_peso_h;
            peso_promedio_machos             := r_peso_m;
            peso_guia_hembras                := g_peso_h;
            peso_guia_machos                 := g_peso_m;
            diferencia_peso_hembras          := fn_dif_pct(r_peso_h, g_peso_h);
            diferencia_peso_machos           := fn_dif_pct(r_peso_m, g_peso_m);
            uniformidad_promedio             := r_unif;
            uniformidad_guia                 := g_unif;
            diferencia_uniformidad           := fn_dif_pct(r_unif, g_unif);
            coeficiente_variacion_promedio   := r_cv;
            huevos_limpios                   := r_limpios;
            huevos_tratados                  := r_tratados;
            huevos_sucios                    := r_sucios;
            huevos_deformes                  := r_deformes;
            huevos_blancos                   := r_blancos;
            huevos_doble_yema                := r_doble_yema;
            huevos_piso                      := r_piso;
            huevos_pequenos                  := r_pequenos;
            huevos_rotos                     := r_rotos;
            huevos_desecho                   := r_desecho;
            huevos_otro                      := r_otro;
            aves_hembras_inicio_semana       := r_aves_h_inicio;
            aves_machos_inicio_semana        := r_aves_m_inicio;
            aves_hembras_fin_semana          := v_aves_h_act;
            aves_machos_fin_semana           := v_aves_m_act;
            htaa_real                        := r_htaa;
            hiaa_real                        := r_hiaa;
            RETURN NEXT;
        END IF;
    END LOOP;

    RETURN;
END;
$function$

";

        private const string SpLevanteOrig = @"CREATE OR REPLACE FUNCTION public.sp_recalcular_seguimiento_levante(l_lote_id text)
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
$function$

";

    }
}
