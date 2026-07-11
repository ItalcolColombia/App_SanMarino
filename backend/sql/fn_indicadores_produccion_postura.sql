-- ============================================================================
-- fn_indicadores_produccion_postura(...)
-- Indicadores semanales de PRODUCCIÓN (postura) calculados en la BD.
-- Reemplaza el cómputo en memoria de IndicadoresProduccionService.CalcularIndicadoresAsync
-- (C#, 727 líneas): el servicio ahora resuelve company/lote y DELEGA el cálculo aquí.
--
-- Replica EXACTO el algoritmo C# actual (que ya incorpora las correcciones REQ-004 de
-- comparación vs guía) usando double precision y el mismo orden de operaciones.
--
-- Correcciones de guía YA presentes en el C# y replicadas aquí (documentadas):
--   REQ-004a  %Producción (hen-day) = promedioHuevos/día / HEMBRAS vivas * 100
--             (solo hembras en el denominador; los machos no ponen).
--   REQ-004b  Peso de aves normalizado a kg: >100 ? /1000 (los pesajes vienen en gramos)
--             para casar con la guía (peso_h/1000).
--   REQ-004c  H.T.A.A / H.I.A.A reales (acumulados por ave alojada) se comparan contra
--             h_total_aa / h_inc_aa de la guía (que son acumulados), no contra huevos/día.
--   REQ-004d  Mortalidad de guía es % (decimal), no entero (no se trunca a 0).
--   Guía = tabla real guia_genetica_sanmarino_colombia filtrada por company + raza + año
--          (misma tabla que ProduccionAvicolaRaw); indexada por Edad = SEMANA DE VIDA.
--
-- Desviaciones preservadas (NO son bugs de guía → se replican tal cual, ver spec §3):
--   * aves_hembras_inicio_semana = avesHActuales + mortH + selH (sobrecuenta el censo de
--     inicio respecto al saldo real de arranque). Campo informativo; NO afecta comparación.
--   * consumo_real_h/m divide por ese aves_*_inicio_semana sobrecontado.
--   * %mortalidad / %selección usan avesHActuales (saldo real de inicio), no el sobrecontado.
--
-- Timezone: America/Bogota para el corte de semanas. Con Npgsql.EnableLegacyTimestampBehavior
--   =true el back lee timestamptz como hora local del proceso; en dev/local el TZ es UTC-5
--   (= America/Bogota sin DST) → .Date del C# = fecha Bogotá. Se normaliza a Bogotá aquí.
--
-- Fuente de verdad: IndicadoresProduccionService.cs (ObtenerIndicadoresSemanalesAsync/CalcularIndicadoresAsync).
-- ============================================================================

-- ── Helper: diferencia porcentual (== CalcularDiferenciaPorcentual del C#).
--    NULL si falta real/guía o guía = 0.
CREATE OR REPLACE FUNCTION fn_dif_pct(p_real double precision, p_guia double precision)
RETURNS double precision LANGUAGE sql IMMUTABLE AS $$
    SELECT CASE
        WHEN p_real IS NULL OR p_guia IS NULL OR p_guia = 0 THEN NULL
        ELSE ((p_real - p_guia) / p_guia) * 100
    END;
$$;

-- ── Helper: parseo de edad numérica de la guía (== TryParseEdadNumerica del C#).
--    Intenta parsear a entero (coma->punto); si no, extrae el primer grupo de dígitos.
--    Devuelve NULL si no hay dígitos. (Edades de producción son enteros: 26, 27, …)
CREATE OR REPLACE FUNCTION fn_parse_edad_numerica(p_edad text)
RETURNS integer LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE
    v_clean text;
    v_match text;
BEGIN
    IF p_edad IS NULL OR btrim(p_edad) = '' THEN RETURN NULL; END IF;
    v_clean := replace(btrim(p_edad), ',', '.');
    IF v_clean ~ '^[+-]?\d+$' THEN
        RETURN v_clean::integer;
    END IF;
    v_match := (regexp_match(v_clean, '(\d+)'))[1];
    IF v_match IS NULL THEN RETURN NULL; END IF;
    RETURN v_match::integer;
END;
$$;

CREATE OR REPLACE FUNCTION fn_indicadores_produccion_postura(
    p_company_id                  integer,
    p_lote_postura_produccion_id  integer  DEFAULT NULL,
    p_lote_id                     integer  DEFAULT NULL,
    p_semana_desde                integer  DEFAULT NULL,
    p_semana_hasta                integer  DEFAULT NULL,
    p_fecha_desde                 date     DEFAULT NULL,
    p_fecha_hasta                 date     DEFAULT NULL
)
RETURNS TABLE(
    semana                              integer,
    fecha_inicio_semana                 date,
    fecha_fin_semana                    date,
    total_registros                     integer,
    mortalidad_hembras                  integer,
    mortalidad_machos                   integer,
    porcentaje_mortalidad_hembras       double precision,
    porcentaje_mortalidad_machos        double precision,
    mortalidad_guia_hembras             double precision,
    mortalidad_guia_machos              double precision,
    diferencia_mortalidad_hembras       double precision,
    diferencia_mortalidad_machos        double precision,
    seleccion_hembras                   integer,
    porcentaje_seleccion_hembras        double precision,
    consumo_kg_hembras                  double precision,
    consumo_kg_machos                   double precision,
    consumo_total_kg                    double precision,
    consumo_promedio_diario_kg          double precision,
    consumo_guia_hembras                double precision,
    consumo_guia_machos                 double precision,
    diferencia_consumo_hembras          double precision,
    diferencia_consumo_machos           double precision,
    huevos_totales                      integer,
    huevos_incubables                   integer,
    promedio_huevos_por_dia             double precision,
    eficiencia_produccion               double precision,
    huevos_totales_guia                 double precision,
    huevos_incubables_guia              double precision,
    porcentaje_produccion_guia          double precision,
    diferencia_huevos_totales           double precision,
    diferencia_huevos_incubables        double precision,
    diferencia_porcentaje_produccion    double precision,
    peso_huevo_promedio                 double precision,
    peso_huevo_guia                     double precision,
    diferencia_peso_huevo               double precision,
    peso_promedio_hembras               double precision,
    peso_promedio_machos                double precision,
    peso_guia_hembras                   double precision,
    peso_guia_machos                    double precision,
    diferencia_peso_hembras             double precision,
    diferencia_peso_machos              double precision,
    uniformidad_promedio                double precision,
    uniformidad_guia                    double precision,
    diferencia_uniformidad              double precision,
    coeficiente_variacion_promedio      double precision,
    huevos_limpios                      integer,
    huevos_tratados                     integer,
    huevos_sucios                       integer,
    huevos_deformes                     integer,
    huevos_blancos                      integer,
    huevos_doble_yema                   integer,
    huevos_piso                         integer,
    huevos_pequenos                     integer,
    huevos_rotos                        integer,
    huevos_desecho                      integer,
    huevos_otro                         integer,
    aves_hembras_inicio_semana          integer,
    aves_machos_inicio_semana           integer,
    aves_hembras_fin_semana             integer,
    aves_machos_fin_semana              integer,
    htaa_real                           double precision,
    hiaa_real                           double precision
)
LANGUAGE plpgsql VOLATILE AS $fn$
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
            -- guía "obtenerGuiaGeneticaProduccion" pasan por ParseDouble (0 si vacío); las del raw
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
$fn$;
