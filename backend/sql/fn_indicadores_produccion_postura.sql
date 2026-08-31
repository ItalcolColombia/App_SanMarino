-- ============================================================================
-- fn_indicadores_produccion_postura(company, lote_produccion, lote, semanas, fechas)
-- Indicadores semanales de PRODUCCION (postura Colombia).
--
-- ⚠️ Este archivo se REGENERO el 14ago26 desde la funcion DESPLEGADA
--    (pg_get_functiondef), porque la version anterior del espejo estaba
--    DESINCRONIZADA: le faltaba la columna de salida `porcentaje_seleccion_machos`,
--    que si existe en la base. Aplicarlo tal cual habria fallado con
--    «42P13: cannot change return type of existing function» — y de hecho fallo,
--    que es como se detecto. Antes de tocar este archivo, comparalo contra
--    pg_get_functiondef; el espejo NO es automaticamente lo desplegado.
--
-- Cambio de esta version (TK-2026-000023): `diferencia_mortalidad_hembras/machos`
-- pasan de fn_dif_pct (porcentaje relativo) a fn_dif_pp (diferencia directa en
-- puntos porcentuales). El resto de las diferencias no se toca.
-- ============================================================================

CREATE OR REPLACE FUNCTION public.fn_indicadores_produccion_postura(p_company_id integer, p_lote_postura_produccion_id integer DEFAULT NULL::integer, p_lote_id integer DEFAULT NULL::integer, p_semana_desde integer DEFAULT NULL::integer, p_semana_hasta integer DEFAULT NULL::integer, p_fecha_desde date DEFAULT NULL::date, p_fecha_hasta date DEFAULT NULL::date)
 RETURNS TABLE(semana integer, fecha_inicio_semana date, fecha_fin_semana date, total_registros integer, mortalidad_hembras integer, mortalidad_machos integer, porcentaje_mortalidad_hembras double precision, porcentaje_mortalidad_machos double precision, mortalidad_guia_hembras double precision, mortalidad_guia_machos double precision, diferencia_mortalidad_hembras double precision, diferencia_mortalidad_machos double precision, seleccion_hembras integer, porcentaje_seleccion_hembras double precision, seleccion_machos integer, porcentaje_seleccion_machos double precision, consumo_kg_hembras double precision, consumo_kg_machos double precision, consumo_total_kg double precision, consumo_promedio_diario_kg double precision, consumo_guia_hembras double precision, consumo_guia_machos double precision, diferencia_consumo_hembras double precision, diferencia_consumo_machos double precision, huevos_totales integer, huevos_incubables integer, promedio_huevos_por_dia double precision, eficiencia_produccion double precision, huevos_totales_guia double precision, huevos_incubables_guia double precision, porcentaje_produccion_guia double precision, diferencia_huevos_totales double precision, diferencia_huevos_incubables double precision, diferencia_porcentaje_produccion double precision, peso_huevo_promedio double precision, peso_huevo_guia double precision, diferencia_peso_huevo double precision, peso_promedio_hembras double precision, peso_promedio_machos double precision, peso_guia_hembras double precision, peso_guia_machos double precision, diferencia_peso_hembras double precision, diferencia_peso_machos double precision, uniformidad_promedio double precision, uniformidad_guia double precision, diferencia_uniformidad double precision, coeficiente_variacion_promedio double precision, huevos_limpios integer, huevos_tratados integer, huevos_sucios integer, huevos_deformes integer, huevos_blancos integer, huevos_doble_yema integer, huevos_piso integer, huevos_pequenos integer, huevos_rotos integer, huevos_desecho integer, huevos_otro integer, aves_hembras_inicio_semana integer, aves_machos_inicio_semana integer, aves_hembras_fin_semana integer, aves_machos_fin_semana integer, htaa_real double precision, hiaa_real double precision, retiro_sem_h double precision, retiro_sem_m double precision, retiro_ac_h double precision, retiro_ac_m double precision, retiro_ac_h_guia double precision, retiro_ac_m_guia double precision)
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
    v_lote_id_int    integer;         -- flujo legacy: lote resuelto, para fn_seguimiento_diario_produccion
    v_has_lote       boolean := false;
    -- Semana de VIDA desde la que esta empresa muestra produccion (companies.
    -- semana_inicio_indicadores_produccion, DEFAULT 25 = el valor que estuvo hardcodeado hasta el
    -- 30-ago-2026). Existe porque no todas las empresas empiezan a poner en la misma semana: la
    -- postura comercial de Santa Reyes arranca en la 18 —es la primera edad de su guia propia y es
    -- coherente con su huevo_primera_postura_hasta_semana = 22—, y con el 25 fijo sus semanas
    -- 18-24 no aparecian en ningun indicador. Con el DEFAULT 25 las otras cuatro empresas
    -- ejecutan exactamente lo mismo que antes.
    v_sem_inicio     integer;

    -- ── acumuladores iterativos (mismos que el C#) ──
    v_aves_h_act     integer;
    v_aves_m_act     integer;
    v_cum_h_tot      bigint := 0;
    v_cum_h_inc      bigint := 0;
    -- REQ-004: acumulados de retiro por sexo (mortalidad + selección)
    v_cum_mort_h     bigint := 0;
    v_cum_sel_h      bigint := 0;
    v_cum_mort_m     bigint := 0;
    v_cum_sel_m      bigint := 0;

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
    r_porc_sel_m     double precision;
    -- REQ-004: %Retiro real por semana
    r_retiro_sem_h   double precision;
    r_retiro_sem_m   double precision;
    r_retiro_ac_h    double precision;
    r_retiro_ac_m    double precision;
    r_aves_h_inicio  integer;
    r_aves_m_inicio  integer;
    -- Movimientos de aves de la semana (ventas, retiros y traslados). Antes el saldo
    --   solo restaba mortalidad y selección, así que una venta de producción —que no deja
    --   columna numérica en la fila diaria, solo nota— quedaba fuera y el saldo del
    --   reporte terminaba por encima del real en exactamente el total vendido.
    r_sel_m          integer;   -- la fn nunca llevó la selección de machos: ni al saldo ni a la salida
    r_venta_h        integer;
    r_venta_m        integer;
    r_retiro_h       integer;
    r_retiro_m       integer;
    r_tras_out_h     integer;
    r_tras_out_m     integer;
    r_tras_in_h      integer;
    r_tras_in_m      integer;
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
    -- REQ-004 (Verenice): %Retiro acumulado de guía por sexo.
    g_retiro_ac_h    double precision;
    g_retiro_ac_m    double precision;
    g_found          boolean;
    -- De que tabla salio la fila: 'compartida' (guia_genetica_sanmarino_colombia) o 'propia'
    -- (guia_genetica_santa_reyes, 3 metricas y solo hembras). Gobierna los COALESCE de abajo.
    g_origen         text;
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

        -- Seguimientos: desde fn_seguimiento_diario_produccion (la fn diaria canónica ya hace el
        -- UNION dual-fuente + dedup por día Bogotá «gana el más temprano»); solo días con registro
        -- (seg_id IS NOT NULL — sin días movimiento-only).
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        SELECT f.fecha_ts AS ts,
               COALESCE(f.mortalidad_hembras,0) AS mort_h, COALESCE(f.mortalidad_machos,0) AS mort_m,
               COALESCE(f.sel_h,0) AS sel_h, COALESCE(f.sel_m,0) AS sel_m,
               COALESCE(f.cons_kg_h,0)::double precision AS cons_h,
               COALESCE(f.cons_kg_m,0)::double precision AS cons_m,
               COALESCE(f.huevo_tot,0) AS huevo_tot, COALESCE(f.huevo_inc,0) AS huevo_inc,
               COALESCE(f.huevo_limpio,0) AS h_limpio, COALESCE(f.huevo_tratado,0) AS h_tratado,
               COALESCE(f.huevo_sucio,0) AS h_sucio, COALESCE(f.huevo_deforme,0) AS h_deforme,
               COALESCE(f.huevo_blanco,0) AS h_blanco, COALESCE(f.huevo_doble_yema,0) AS h_doble,
               COALESCE(f.huevo_piso,0) AS h_piso, COALESCE(f.huevo_pequeno,0) AS h_pequeno,
               COALESCE(f.huevo_roto,0) AS h_roto, COALESCE(f.huevo_desecho,0) AS h_desecho,
               COALESCE(f.huevo_otro,0) AS h_otro,
               f.peso_huevo::double precision AS peso_huevo,
               f.peso_h::double precision AS peso_h, f.peso_m::double precision AS peso_m,
               f.uniformidad::double precision AS unif, f.coeficiente_variacion::double precision AS cv,
               COALESCE(f.mov_venta_h,0) AS mv_venta_h, COALESCE(f.mov_venta_m,0) AS mv_venta_m,
               COALESCE(f.mov_retiro_h,0) AS mv_retiro_h, COALESCE(f.mov_retiro_m,0) AS mv_retiro_m,
               COALESCE(f.mov_traslado_out_h,0) AS mv_out_h, COALESCE(f.mov_traslado_out_m,0) AS mv_out_m,
               COALESCE(f.mov_traslado_in_h,0) AS mv_in_h, COALESCE(f.mov_traslado_in_m,0) AS mv_in_m
          FROM fn_seguimiento_diario_produccion(p_lote_postura_produccion_id, NULL) f
         WHERE f.seg_id IS NOT NULL
           AND NOT f.fila_sin_lpp;   -- v2 fn diaria: los dias solo-traslado TSD no son "dia con registro"

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
            v_lote_id_int := v_lp_lote_id;

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

        -- Seguimientos legacy: desde fn_seguimiento_diario_produccion (dedup dual-fuente ya
        -- resuelto por la fn diaria); solo días con registro.
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        SELECT f.fecha_ts AS ts,
               COALESCE(f.mortalidad_hembras,0) AS mort_h, COALESCE(f.mortalidad_machos,0) AS mort_m,
               COALESCE(f.sel_h,0) AS sel_h, COALESCE(f.sel_m,0) AS sel_m,
               COALESCE(f.cons_kg_h,0)::double precision AS cons_h,
               COALESCE(f.cons_kg_m,0)::double precision AS cons_m,
               COALESCE(f.huevo_tot,0) AS huevo_tot, COALESCE(f.huevo_inc,0) AS huevo_inc,
               COALESCE(f.huevo_limpio,0) AS h_limpio, COALESCE(f.huevo_tratado,0) AS h_tratado,
               COALESCE(f.huevo_sucio,0) AS h_sucio, COALESCE(f.huevo_deforme,0) AS h_deforme,
               COALESCE(f.huevo_blanco,0) AS h_blanco, COALESCE(f.huevo_doble_yema,0) AS h_doble,
               COALESCE(f.huevo_piso,0) AS h_piso, COALESCE(f.huevo_pequeno,0) AS h_pequeno,
               COALESCE(f.huevo_roto,0) AS h_roto, COALESCE(f.huevo_desecho,0) AS h_desecho,
               COALESCE(f.huevo_otro,0) AS h_otro,
               f.peso_huevo::double precision AS peso_huevo,
               f.peso_h::double precision AS peso_h, f.peso_m::double precision AS peso_m,
               f.uniformidad::double precision AS unif, f.coeficiente_variacion::double precision AS cv,
               COALESCE(f.mov_venta_h,0) AS mv_venta_h, COALESCE(f.mov_venta_m,0) AS mv_venta_m,
               COALESCE(f.mov_retiro_h,0) AS mv_retiro_h, COALESCE(f.mov_retiro_m,0) AS mv_retiro_m,
               COALESCE(f.mov_traslado_out_h,0) AS mv_out_h, COALESCE(f.mov_traslado_out_m,0) AS mv_out_m,
               COALESCE(f.mov_traslado_in_h,0) AS mv_in_h, COALESCE(f.mov_traslado_in_m,0) AS mv_in_m
          FROM fn_seguimiento_diario_produccion(NULL, v_lote_id_int) f
         WHERE f.seg_id IS NOT NULL
           AND NOT f.fila_sin_lpp;   -- v2 fn diaria: los dias solo-traslado TSD no son "dia con registro"

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
    -- REQ-012b: producción arranca en la semana 25 de vida (antes 26). La guía genética empieza en
    --   la semana 26, así que la 25 queda con columnas de guía en NULL (g_found=false ya lo soporta).
    -- 30-ago-2026: ese 25 pasa a ser el DEFAULT de la columna por empresa, no una constante. El
    --   COALESCE cubre tanto la empresa inexistente como la columna en NULL: sin fila, el valor es
    --   el de siempre.
    SELECT COALESCE(c.semana_inicio_indicadores_produccion, 25)
      INTO v_sem_inicio
      FROM companies c
     WHERE c.id = p_company_id;
    v_sem_inicio := COALESCE(v_sem_inicio, 25);

    DELETE FROM _seg WHERE sem_vida < v_sem_inicio;

    SELECT MAX(sem_vida) INTO v_max_sem FROM _seg;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 3) Iterar semanas presentes en orden (== foreach sobre grupos ordenados).
    --    OJO: itera SOLO las semanas con registros (>=25 tras REQ-012b) y en orden asc.
    --    Los acumuladores (aves actuales, htaa/hiaa, retiro) avanzan solo en esas semanas.
    -- ════════════════════════════════════════════════════════════════════
    v_aves_h_act := v_aves_h_ini;
    v_aves_m_act := v_aves_m_ini;

    FOR s IN v_sem_inicio..v_max_sem LOOP  -- REQ-012b: incluir semana 25 (antes 26); hoy, la de la empresa
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg WHERE sem_vida = s);

        SELECT COUNT(*)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0), COALESCE(SUM(sel_h),0),
               COALESCE(SUM(cons_h),0), COALESCE(SUM(cons_m),0),
               COALESCE(SUM(huevo_tot),0), COALESCE(SUM(huevo_inc),0),
               COALESCE(SUM(h_limpio),0), COALESCE(SUM(h_tratado),0), COALESCE(SUM(h_sucio),0),
               COALESCE(SUM(h_deforme),0), COALESCE(SUM(h_blanco),0), COALESCE(SUM(h_doble),0),
               COALESCE(SUM(h_piso),0), COALESCE(SUM(h_pequeno),0), COALESCE(SUM(h_roto),0),
               COALESCE(SUM(h_desecho),0), COALESCE(SUM(h_otro),0),
               COALESCE(SUM(mv_venta_h),0), COALESCE(SUM(mv_venta_m),0),
               COALESCE(SUM(mv_retiro_h),0), COALESCE(SUM(mv_retiro_m),0),
               COALESCE(SUM(mv_out_h),0), COALESCE(SUM(mv_out_m),0),
               COALESCE(SUM(mv_in_h),0), COALESCE(SUM(mv_in_m),0), COALESCE(SUM(sel_m),0)
          INTO r_dias, r_mort_h, r_mort_m, r_sel_h, r_cons_kg_h, r_cons_kg_m,
               r_huevos_tot, r_huevos_inc,
               r_limpios, r_tratados, r_sucios, r_deformes, r_blancos, r_doble_yema,
               r_piso, r_pequenos, r_rotos, r_desecho, r_otro,
               r_venta_h, r_venta_m, r_retiro_h, r_retiro_m,
               r_tras_out_h, r_tras_out_m, r_tras_in_h, r_tras_in_m, r_sel_m
          FROM _seg WHERE sem_vida = s;

        r_prom_huevos := CASE WHEN r_dias > 0 THEN r_huevos_tot::double precision / r_dias ELSE 0 END;

        -- REQ-004a: %Producción hen-day = huevos/día / HEMBRAS vivas (solo hembras) * 100
        r_efic := CASE WHEN v_aves_h_act > 0 THEN r_prom_huevos / v_aves_h_act * 100 ELSE 0 END;

        -- Acumulados por ave alojada (REQ-004c)
        v_cum_h_tot := v_cum_h_tot + r_huevos_tot;
        v_cum_h_inc := v_cum_h_inc + r_huevos_inc;

        -- REQ-004: acumulados de retiro (mortalidad + selección) por sexo. Desde
        --   20260806093256 los MACHOS también acumulan selección, igual que las hembras.
        v_cum_mort_h := v_cum_mort_h + r_mort_h;
        v_cum_sel_h  := v_cum_sel_h + r_sel_h;
        v_cum_mort_m := v_cum_mort_m + r_mort_m;
        v_cum_sel_m  := v_cum_sel_m + r_sel_m;
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
        r_porc_sel_m  := CASE WHEN v_aves_m_act > 0 THEN r_sel_m::double precision  / v_aves_m_act * 100 ELSE 0 END;

        -- REQ-004: %Retiro REAL (== ProduccionCalculos.PorcentajeRetiroSemanal/Acumulado).
        --   Semanal: (mort + sel de la semana) / saldo REAL de inicio del sexo (v_aves_*_act, pre-decremento) * 100.
        --   Acumulado: (mort + sel acumulados) / aves iniciales del sexo * 100.
        r_retiro_sem_h := CASE WHEN v_aves_h_act > 0 THEN (r_mort_h + r_sel_h)::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_retiro_sem_m := CASE WHEN v_aves_m_act > 0 THEN (r_mort_m + r_sel_m)::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_retiro_ac_h  := CASE WHEN v_aves_h_ini > 0 THEN (v_cum_mort_h + v_cum_sel_h)::double precision / v_aves_h_ini * 100 ELSE 0 END;
        r_retiro_ac_m  := CASE WHEN v_aves_m_ini > 0 THEN (v_cum_mort_m + v_cum_sel_m)::double precision / v_aves_m_ini * 100 ELSE 0 END;

        -- Censo de inicio de semana (desviación preservada: sobrecuenta con las bajas de la propia semana)
        r_aves_h_inicio := v_aves_h_act + r_mort_h + r_sel_h;
        r_aves_m_inicio := v_aves_m_act + r_mort_m + r_sel_m;

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
               NULLIF(btrim(g.peso_huevo),'')::double precision,
               NULLIF(btrim(g.retiro_ac_h),'')::double precision,
               NULLIF(btrim(g.retiro_ac_m),'')::double precision,
               g.origen
          INTO g_found, g_cons_h, g_cons_m, g_mort_h, g_mort_m, g_peso_h, g_peso_m, g_unif,
               g_huevos_tot, g_huevos_inc, g_prod_pct, g_peso_huevo, g_retiro_ac_h, g_retiro_ac_m,
               g_origen
          FROM vw_guia_genetica_postura g
         WHERE g.company_id = p_company_id
           AND g.deleted_at IS NULL
           AND btrim(lower(g.raza)) = btrim(lower(v_raza))
           AND btrim(g.anio_guia) = v_ano
           AND fn_parse_edad_numerica(g.edad) = s
         -- La semana 25 tiene DOS filas que parsean a 25: '25' (cierre de
         -- levante) y '25P' (arranque de producción), con valores muy distintos
         -- (retiro_ac_h 4,03 vs 0,10). Sin ORDER BY la que gana depende del
         -- plan y del orden físico de la tabla: hoy sale '25P' por el ctid, no
         -- por contrato. Se fija el desempate en la variante con sufijo —la de
         -- producción, que es la correcta acá y la que ya venía devolviendo—
         -- para que un VACUUM o un re-seed no cambien el reporte en silencio.
         ORDER BY (CASE WHEN btrim(g.edad) = s::text THEN 1 ELSE 0 END), g.id
         LIMIT 1;
        g_found := COALESCE(g_found, false);

        IF g_found THEN
            -- ParseDouble => 0 cuando el string es vacío/no numérico (no NULL). Las columnas de la
            -- guía "obtenerGuiaGeneticaProduccion" pasan por ParseDouble (0 si vacío); las del raw
            -- (huevos/%prod/pesoHuevo) por ParseDecimal (NULL si vacío). Se respeta esa diferencia:
            -- 🔴 Los COALESCE a 0 son EXCLUSIVOS de la guía compartida.
            -- Ahí la columna existe en toda la curva y el 0 se lee como «la guía dice 0»
            -- (y quitarlos NO sería delta cero: en el rango de producción, company 1 tiene
            -- entre 6 y 14 filas en blanco por columna). En la guía propia esas métricas
            -- NO EXISTEN —no trae peso, ni consumo de machos, ni mortalidad semanal— y el 0
            -- ahí no es «sin dato»: es un objetivo falso. Peor todavía, `fn_dif_pp` documenta
            -- que con guía = 0 NO devuelve NULL, así que la columna «diferencia vs guía» de
            -- mortalidad pintaría la mortalidad REAL del lote como si fuera la desviación.
            -- Con NULL, `fn_dif_pct`/`fn_dif_pp` degradan solas y el front pinta un guion.
            IF g_origen IS DISTINCT FROM 'propia' THEN
                g_cons_h := COALESCE(g_cons_h, 0);
                g_cons_m := COALESCE(g_cons_m, 0);
                g_mort_h := COALESCE(g_mort_h, 0);
                g_mort_m := COALESCE(g_mort_m, 0);
            END IF;
            -- El /1000 sí se aplica siempre (la guía viene en gramos y la salida en kg);
            -- lo condicional es el COALESCE, porque NULL/1000 = NULL y eso es lo correcto.
            g_peso_h := CASE WHEN g_origen = 'propia' THEN g_peso_h / 1000
                             ELSE COALESCE(g_peso_h, 0) / 1000 END;   -- peso_h/1000
            g_peso_m := CASE WHEN g_origen = 'propia' THEN g_peso_m / 1000
                             ELSE COALESCE(g_peso_m, 0) / 1000 END;   -- peso_m/1000
            -- ⚠️ EXCEPCIÓN DELIBERADA a la regla ParseDouble=>0 de sus vecinas: g_unif NO se
            --   coalescea. La guía genética no define uniformidad para las edades de PRODUCCIÓN
            --   (solo 25 de sus 98 filas la traen, todas de levante) ⇒ el 0 se pintaba en TODAS
            --   las semanas y se lee como «la guía exige 0 %» en vez de «sin dato», además de
            --   calcular la diferencia contra ese 0. Un 0 real tampoco existe como objetivo de
            --   uniformidad, así que NULL es la única lectura honesta.
            --   `diferencia_uniformidad` no se mueve: fn_dif_pct ya devolvía NULL con guía = 0.
            --   Los demás (cons/mort/peso/retiro_ac) SÍ conservan el 0: la guía los trae en toda
            --   la curva y cambiarlos movería números sin necesidad.
            -- huevos/%prod/pesoHuevo: quedan NULL si vacíos (ParseDecimal), no 0.
            -- retiro_ac_h/m guía: mismo criterio que mort_h/mort_m (ParseDouble => 0 si vacío).
            -- retiro_ac_h SÍ lo trae la guía propia (es su métrica de mortalidad, acumulada);
            -- retiro_ac_m no, y por eso el COALESCE queda condicionado igual que los de arriba.
            IF g_origen IS DISTINCT FROM 'propia' THEN
                g_retiro_ac_h := COALESCE(g_retiro_ac_h, 0);
                g_retiro_ac_m := COALESCE(g_retiro_ac_m, 0);
            END IF;
        ELSE
            g_cons_h := NULL; g_cons_m := NULL; g_mort_h := NULL; g_mort_m := NULL;
            g_peso_h := NULL; g_peso_m := NULL; g_unif := NULL;
            g_huevos_tot := NULL; g_huevos_inc := NULL; g_prod_pct := NULL; g_peso_huevo := NULL;
            g_retiro_ac_h := NULL; g_retiro_ac_m := NULL;
        END IF;

        -- Consumo real (g/ave/día) — denominador = censo de inicio sobrecontado (desviación preservada)
        r_cons_real_h := CASE WHEN r_dias > 0 AND r_aves_h_inicio > 0
                              THEN r_cons_kg_h * 1000 / (r_dias * r_aves_h_inicio) ELSE NULL END;
        r_cons_real_m := CASE WHEN r_dias > 0 AND r_aves_m_inicio > 0
                              THEN r_cons_kg_m * 1000 / (r_dias * r_aves_m_inicio) ELSE NULL END;

        -- Decremento de aves. Además de mortalidad y selección descuenta VENTAS, retiros
        --   y salidas por traslado, y suma los ingresos: son aves que dejan (o entran a)
        --   el lote igual que las bajas. Misma composición que SaldoAvesLevanteCalculos.
        v_aves_h_act := GREATEST(0, v_aves_h_act - r_mort_h - r_sel_h
                                    - r_venta_h - r_retiro_h - r_tras_out_h + r_tras_in_h);
        v_aves_m_act := GREATEST(0, v_aves_m_act - r_mort_m - r_sel_m
                                    - r_venta_m - r_retiro_m - r_tras_out_m + r_tras_in_m);

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
            -- TK-2026-000023: la diferencia de MORTALIDAD es DIRECTA (puntos porcentuales),
            -- no porcentaje diferencial. Real y guia ya son porcentajes: restarlos da la
            -- distancia real (0,07 % vs 0,33 % => -0,26 pp). El porcentaje relativo
            -- ((real-guia)/guia*100) sobre numeros tan chicos explota: la pantalla llegaba a
            -- mostrar +2.212,10 % para 0,26 % contra 0,01 % de guia.
            -- Las demas diferencias (consumo, peso, huevos) SIGUEN relativas: ahi real y guia
            -- son magnitudes (kg, g, unidades), no porcentajes.
            diferencia_mortalidad_hembras    := fn_dif_pp(r_porc_mort_h, g_mort_h);
            diferencia_mortalidad_machos     := fn_dif_pp(r_porc_mort_m, g_mort_m);
            seleccion_hembras                := r_sel_h;
            seleccion_machos                 := r_sel_m;
            porcentaje_seleccion_hembras     := r_porc_sel_h;
            porcentaje_seleccion_machos      := r_porc_sel_m;
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
            retiro_sem_h                     := r_retiro_sem_h;
            retiro_sem_m                     := r_retiro_sem_m;
            retiro_ac_h                      := r_retiro_ac_h;
            retiro_ac_m                      := r_retiro_ac_m;
            retiro_ac_h_guia                 := g_retiro_ac_h;
            retiro_ac_m_guia                 := g_retiro_ac_m;
            RETURN NEXT;
        END IF;
    END LOOP;

    RETURN;
END;
$function$
