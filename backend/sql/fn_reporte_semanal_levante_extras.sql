-- ============================================================================
-- fn_reporte_semanal_levante_extras(p_lote_id)
-- COMPLEMENTO por SEXO para el "Reporte Técnico Semanal" de LEVANTE (Sanmarino).
--
-- HERMANA de fn_indicadores_levante_postura(p_lote_id): MISMO parámetro
-- (lotes.lote_id), MISMA fuente (seguimiento_diario_levante tipo='levante'),
-- MISMA fórmula de semana (floor((fecha_bogota - encaset)/7)+1, LEAST(25,…)),
-- MISMOS guards (encaset NULL/futuro ⇒ cero filas; exclusión de filas de PURO
-- traslado posteriores a la semana 25) y MISMO saldo Feature-13 por sexo
-- (aves_fin = aves_ini - mort - sel - err - tras_sal + tras_ing).
-- ⇒ la columna `semana` casa 1:1 con la fn base y el servicio puede hacer zip.
--
-- Qué agrega (la fn base emite % y promedios; el Excel del reporte necesita
-- CONTEOS/KG por sexo con base FIJA de aves iniciales):
--   * Conteos semanales por sexo: mortalidad / selección(descarte) / error sexaje
--     + traslados (las % y acumulados con base fija los computa el C# puro
--     ReporteTecnicoSemanalCalculos — Excel: F7 = E7/$C$7, base inicial fija).
--   * Consumo kg por sexo por semana (la base solo expone g/ave/día).
--   * Bases resueltas de aves por sexo (mismo fallback que la fn base:
--     hembras_l/machos_l → primer traslado_ingreso → 0) y saldos inicio/fin.
--   * Nutrición REAL hembras: kcal_al_h / prot_al_h promedio de la semana
--     (la tabla no tiene kcal/prot de machos; el Excel solo la pide en hembras).
--   * Uniformidad y C.V. POR SEXO del registro de pesaje de la semana (misma
--     regla de selección de fila que la fn base: último registro con peso>0,
--     si no, el último registro de la semana). Sin arrastre (la uniformidad no
--     se arrastra en la fn base).
--   * fecha_fin_semana = encaset + (semana-1)*7 + 6 (columna FECHA del Excel).
--
-- IMPORTANTE (mapeo EF): nombres de columnas = snake_case EXACTO de las props
-- del DTO ReporteSemanalLevanteExtrasRow (…HembrasSem → …_hembras_sem).
--
-- TEMP TABLE con nombre PROPIO (_seg_sem_rx) + DROP IF EXISTS para no chocar
-- con _seg_sem de la fn base si ambas corren en la misma transacción.
-- Zona horaria: America/Bogota (idéntico a la fn base).
-- ============================================================================
DROP FUNCTION IF EXISTS fn_reporte_semanal_levante_extras(integer);
CREATE OR REPLACE FUNCTION fn_reporte_semanal_levante_extras(p_lote_id integer)
RETURNS TABLE(
    semana                          integer,
    fecha_fin_semana                date,
    dias_con_registro               integer,
    base_hembras                    double precision,
    base_machos                     double precision,
    aves_hembras_inicio             double precision,
    aves_hembras_fin                double precision,
    aves_machos_inicio              double precision,
    aves_machos_fin                 double precision,
    mortalidad_hembras_sem          integer,
    mortalidad_machos_sem           integer,
    seleccion_hembras_sem           integer,
    seleccion_machos_sem            integer,
    error_hembras_sem               integer,
    error_machos_sem                integer,
    traslado_ingreso_hembras_sem    integer,
    traslado_ingreso_machos_sem     integer,
    traslado_salida_hembras_sem     integer,
    traslado_salida_machos_sem      integer,
    consumo_kg_hembras_sem          double precision,
    consumo_kg_machos_sem           double precision,
    kcal_alimento_hembras           double precision,
    prot_alimento_hembras           double precision,
    uniformidad_hembras             double precision,
    uniformidad_machos              double precision,
    cv_hembras                      double precision,
    cv_machos                       double precision,
    -- Peso prom por sexo del pesaje de la semana con ARRASTRE del último
    -- conocido (misma regla que peso_hembras/peso_machos de la fn base).
    -- NULL si el sexo nunca tuvo pesaje.
    peso_hembras_sem                double precision,
    peso_machos_sem                 double precision
)
LANGUAGE plpgsql VOLATILE AS $$
DECLARE
    v_hembras_l   integer;
    v_machos_l    integer;
    v_enc_date    date;
    v_min_reg     date;
    v_first_ing_h double precision;
    v_first_ing_m double precision;
    v_base_h      double precision;
    v_base_m      double precision;

    v_aves_acum_h double precision;
    v_aves_acum_m double precision;

    v_max_sem     integer;
    s             integer;

    r_mort_h      integer;
    r_mort_m      integer;
    r_sel_h       integer;
    r_sel_m       integer;
    r_err_h       integer;
    r_err_m       integer;
    r_tras_ing_h  integer;
    r_tras_ing_m  integer;
    r_tras_sal_h  integer;
    r_tras_sal_m  integer;
    r_cons_kg_h   double precision;
    r_cons_kg_m   double precision;
    r_dias        integer;
    r_kcal_h      double precision;
    r_prot_h      double precision;
    r_uh          double precision;
    r_um          double precision;
    r_cvh         double precision;
    r_cvm         double precision;
    r_ph          double precision;
    r_pm          double precision;
    v_peso_ant_h  double precision := NULL;
    v_peso_ant_m  double precision := NULL;
    r_peso_h      double precision;
    r_peso_m      double precision;
    r_fin_h       double precision;
    r_fin_m       double precision;
BEGIN
    SELECT l.hembras_l, l.machos_l,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date
      INTO v_hembras_l, v_machos_l, v_enc_date
      FROM lotes l
     WHERE l.lote_id = p_lote_id AND l.deleted_at IS NULL;

    IF NOT FOUND THEN RETURN; END IF;

    SELECT COALESCE(sl.traslado_ingreso_hembras,0)::double precision,
           COALESCE(sl.traslado_ingreso_machos,0)::double precision
      INTO v_first_ing_h, v_first_ing_m
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
       AND (COALESCE(sl.traslado_ingreso_hembras,0) + COALESCE(sl.traslado_ingreso_machos,0)) > 0
     ORDER BY sl.fecha ASC, sl.id ASC
     LIMIT 1;
    v_first_ing_h := COALESCE(v_first_ing_h, 0);
    v_first_ing_m := COALESCE(v_first_ing_m, 0);

    SELECT MIN((sl.fecha AT TIME ZONE 'America/Bogota')::date)
      INTO v_min_reg
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text;

    IF v_min_reg IS NULL THEN RETURN; END IF;
    IF v_enc_date IS NULL OR v_enc_date > v_min_reg THEN RETURN; END IF;

    v_base_h := COALESCE(NULLIF(v_hembras_l, 0)::double precision, NULLIF(v_first_ing_h, 0), 0);
    v_base_m := COALESCE(NULLIF(v_machos_l, 0)::double precision, NULLIF(v_first_ing_m, 0), 0);

    v_aves_acum_h := v_base_h;
    v_aves_acum_m := v_base_m;

    DROP TABLE IF EXISTS _seg_sem_rx;
    CREATE TEMP TABLE _seg_sem_rx ON COMMIT DROP AS
    WITH base AS (
        SELECT
            (floor((( (sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date ) / 7.0))::int) + 1 AS real_sem,
            (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date,
            COALESCE(sl.mortalidad_hembras,0) AS mort_h,
            COALESCE(sl.mortalidad_machos,0)  AS mort_m,
            COALESCE(sl.sel_h,0)              AS sel_h,
            COALESCE(sl.sel_m,0)              AS sel_m,
            COALESCE(sl.error_sexaje_hembras,0) AS err_h,
            COALESCE(sl.error_sexaje_machos,0)  AS err_m,
            COALESCE(sl.consumo_kg_hembras,0) AS cons_kg_h_num,
            COALESCE(sl.consumo_kg_machos,0)  AS cons_kg_m_num,
            COALESCE(sl.traslado_salida_hembras,0) AS tras_sal_h,
            COALESCE(sl.traslado_salida_machos,0)  AS tras_sal_m,
            COALESCE(sl.traslado_ingreso_hembras,0) AS tras_ing_h,
            COALESCE(sl.traslado_ingreso_machos,0)  AS tras_ing_m,
            COALESCE(sl.peso_prom_hembras,0)  AS ph,
            COALESCE(sl.peso_prom_machos,0)   AS pm,
            sl.uniformidad_hembras            AS uh,
            sl.uniformidad_machos             AS um,
            sl.cv_hembras                     AS cvh,
            sl.cv_machos                      AS cvm,
            sl.kcal_al_h                      AS kcal_h,
            sl.prot_al_h                      AS prot_h,
            sl.id
          FROM seguimiento_diario_levante sl
         WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
    )
    SELECT
        LEAST(25, real_sem) AS sem,
        reg_date, mort_h, mort_m, sel_h, sel_m, err_h, err_m,
        cons_kg_h_num::double precision AS cons_kg_h,
        cons_kg_m_num::double precision AS cons_kg_m,
        tras_sal_h, tras_sal_m, tras_ing_h, tras_ing_m,
        ph, pm, uh, um, cvh, cvm, kcal_h, prot_h, id
      FROM base
     WHERE NOT (
            real_sem > 25
        AND mort_h = 0 AND mort_m = 0 AND sel_h = 0 AND sel_m = 0
        AND err_h = 0 AND err_m = 0
        AND cons_kg_h_num = 0 AND cons_kg_m_num = 0
        AND ph = 0 AND pm = 0
        AND (tras_sal_h + tras_sal_m + tras_ing_h + tras_ing_m) > 0
     );

    SELECT MAX(x.sem) INTO v_max_sem FROM _seg_sem_rx x;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    FOR s IN 1..v_max_sem LOOP
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem_rx x WHERE x.sem = s);

        SELECT COALESCE(SUM(x.mort_h),0)::int, COALESCE(SUM(x.mort_m),0)::int,
               COALESCE(SUM(x.sel_h),0)::int,  COALESCE(SUM(x.sel_m),0)::int,
               COALESCE(SUM(x.err_h),0)::int,  COALESCE(SUM(x.err_m),0)::int,
               COALESCE(SUM(x.tras_ing_h),0)::int, COALESCE(SUM(x.tras_ing_m),0)::int,
               COALESCE(SUM(x.tras_sal_h),0)::int, COALESCE(SUM(x.tras_sal_m),0)::int,
               COALESCE(SUM(x.cons_kg_h),0), COALESCE(SUM(x.cons_kg_m),0),
               COUNT(*)::int,
               AVG(x.kcal_h) FILTER (WHERE x.kcal_h IS NOT NULL AND x.kcal_h > 0),
               AVG(x.prot_h) FILTER (WHERE x.prot_h IS NOT NULL AND x.prot_h > 0)
          INTO r_mort_h, r_mort_m, r_sel_h, r_sel_m, r_err_h, r_err_m,
               r_tras_ing_h, r_tras_ing_m, r_tras_sal_h, r_tras_sal_m,
               r_cons_kg_h, r_cons_kg_m, r_dias, r_kcal_h, r_prot_h
          FROM _seg_sem_rx x WHERE x.sem = s;

        -- Pesaje de la semana: misma selección de fila que la fn base.
        SELECT x.uh, x.um, x.cvh, x.cvm, x.ph, x.pm INTO r_uh, r_um, r_cvh, r_cvm, r_ph, r_pm
          FROM _seg_sem_rx x
         WHERE x.sem = s AND (x.ph > 0 OR x.pm > 0)
         ORDER BY x.reg_date DESC, x.id DESC LIMIT 1;
        IF NOT FOUND THEN
            SELECT x.uh, x.um, x.cvh, x.cvm, x.ph, x.pm INTO r_uh, r_um, r_cvh, r_cvm, r_ph, r_pm
              FROM _seg_sem_rx x WHERE x.sem = s ORDER BY x.reg_date DESC, x.id DESC LIMIT 1;
        END IF;
        r_ph := COALESCE(r_ph, 0);
        r_pm := COALESCE(r_pm, 0);
        -- Arrastre por sexo (regla de la fn base): valor del pesaje si hay,
        -- si no el último conocido; NULL si nunca hubo pesaje del sexo.
        r_peso_h := CASE WHEN r_ph > 0 THEN r_ph ELSE v_peso_ant_h END;
        r_peso_m := CASE WHEN r_pm > 0 THEN r_pm ELSE v_peso_ant_m END;

        r_fin_h := v_aves_acum_h - r_mort_h - r_sel_h - r_err_h - r_tras_sal_h + r_tras_ing_h;
        r_fin_m := v_aves_acum_m - r_mort_m - r_sel_m - r_err_m - r_tras_sal_m + r_tras_ing_m;

        semana                       := s;
        fecha_fin_semana             := v_enc_date + ((s - 1) * 7) + 6;
        dias_con_registro            := r_dias;
        base_hembras                 := v_base_h;
        base_machos                  := v_base_m;
        aves_hembras_inicio          := v_aves_acum_h;
        aves_hembras_fin             := r_fin_h;
        aves_machos_inicio           := v_aves_acum_m;
        aves_machos_fin              := r_fin_m;
        mortalidad_hembras_sem       := r_mort_h;
        mortalidad_machos_sem        := r_mort_m;
        seleccion_hembras_sem        := r_sel_h;
        seleccion_machos_sem         := r_sel_m;
        error_hembras_sem            := r_err_h;
        error_machos_sem             := r_err_m;
        traslado_ingreso_hembras_sem := r_tras_ing_h;
        traslado_ingreso_machos_sem  := r_tras_ing_m;
        traslado_salida_hembras_sem  := r_tras_sal_h;
        traslado_salida_machos_sem   := r_tras_sal_m;
        consumo_kg_hembras_sem       := r_cons_kg_h;
        consumo_kg_machos_sem        := r_cons_kg_m;
        kcal_alimento_hembras        := r_kcal_h;
        prot_alimento_hembras        := r_prot_h;
        uniformidad_hembras          := NULLIF(COALESCE(r_uh, 0), 0);
        uniformidad_machos           := NULLIF(COALESCE(r_um, 0), 0);
        cv_hembras                   := NULLIF(COALESCE(r_cvh, 0), 0);
        cv_machos                    := NULLIF(COALESCE(r_cvm, 0), 0);
        peso_hembras_sem             := r_peso_h;
        peso_machos_sem              := r_peso_m;

        RETURN NEXT;

        v_aves_acum_h := r_fin_h;
        v_aves_acum_m := r_fin_m;
        v_peso_ant_h  := r_peso_h;
        v_peso_ant_m  := r_peso_m;
    END LOOP;

    RETURN;
END;
$$;
