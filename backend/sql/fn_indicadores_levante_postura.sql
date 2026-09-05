-- ============================================================================
-- fn_indicadores_levante_postura(lote_id)
-- Indicadores semanales de LEVANTE (postura Colombia) calculados en la BD.
-- Reemplaza el cómputo del front (lote-levante/tabla-lista-indicadores +
-- graficas-principal): el front solo debe pintar.
--
-- Replica EXACTO el algoritmo del front (double precision, mismo orden) e
-- incorpora las correcciones ya acordadas:
--   * Peso/uniformidad del PESAJE semanal: último registro de la semana con
--     peso>0 (no el último día, que suele venir en 0) + arrastre del último
--     peso conocido cuando la semana no tiene pesaje (evita ganancia negativa
--     y dif -100%).  [bug histórico corregido]
--   * Guía genética REAL desde guia_genetica_sanmarino_colombia por
--     raza + año + company + semana (no valores hardcodeados / no Ecuador).
--
-- Correcciones matriz Verenice rev 6-jul-26:
--   * REQ-002e — Consumo por sexo: además del consumo mixto (compatibilidad),
--     se exponen consumo_diario_hembras / consumo_diario_machos (g/ave/día reales
--     por sexo = consumo_kg_sexo*1000 / saldo_prom_sexo / días) y
--     consumo_tabla_hembras / consumo_tabla_machos (gr_ave_dia_h/_m de la guía, SIN
--     promediar). Requiere llevar el saldo de aves POR GÉNERO dentro de la fn.
--     (Columnas renombradas de _h/_m a _hembras/_machos por el mapeo EF, ver nota abajo.)
--   * REQ-002f — Acumulados reales: mortalidad/selección acumuladas =
--     bajas_acumuladas / aves_encasetadas * 100 (acumulado real sobre aves
--     iniciales), no la suma de % semanales sobre base decreciente.
--   * REQ-002f/B36 — Semana fantasma: se EXCLUYEN las filas de PURO traslado
--     (sin mortalidad/selección/error/consumo/pesaje) posteriores a la
--     semana 25; ya no se clampean con LEAST(25) generando una "semana 25"
--     falsa con el salto de saldo del traslado post-levante.
--   * REQ-002B36 — Defensas:
--       - Base de aves con fallback: COALESCE(aves_encasetadas,
--         hembras_l+machos_l, primer traslado_ingreso, 0).
--       - Encaset futuro/ausente: si fecha_encaset es NULL o es POSTERIOR al
--         primer registro (encaset tecleado a futuro, p. ej. lote 116), se
--         devuelven CERO filas en lugar de colapsar 140+ días en una
--         "semana 1" absurda con base 0 y %pérdidas 100%. Se eligió devolver
--         cero filas (y no "usar el primer registro como referencia") porque
--         con un encaset inconsistente NINGÚN indicador es confiable: es más
--         seguro que el front muestre su empty-state a mostrar cifras
--         engañosas. Al devolver cero filas ya no hace falta GREATEST(1,…)
--         (no quedan semanas negativas que clampear).
--       - Idempotencia intra-transacción: DROP TABLE IF EXISTS _seg_sem antes
--         del CREATE TEMP TABLE (permite llamar la fn 2+ veces en la misma
--         transacción sin 'relation _seg_sem already exists').
--
-- Fuente de verdad del algoritmo: tabla-lista-indicadores.component.ts
-- Zona horaria: America/Bogota para el corte de semanas (calendario local).
--
-- Fase 3 (convergencia levante a Feature-13): lee la tabla CANÓNICA
-- seguimiento_diario_levante (tipo_seguimiento='levante') y las
-- salidas de la semana incluyen error de sexaje y traslados dedicados:
--   out = mort + sel + err + traslado_salida - traslado_ingreso;  aves_fin = aves - out.
-- ============================================================================
--   * REQ-010b — Series POR SEXO para el selector Hembras/Machos/Ambos de la
--     pestaña Gráfica: además del consumo por sexo, se exponen peso (real +
--     guía), mortalidad % (real + guía) y retiro % (real; la guía por sexo no
--     existe ⇒ NULL) por sexo, para que el control cambie las series Real/Guía.
--     Aritmética por sexo consistente con la mixta (mismo denominador = aves al
--     inicio de la semana del sexo; NULL cuando el sexo no tiene saldo/pesaje).
--
--   * TK-2026-000022 — TODOS los parametros por sexo en la TABLA de indicadores.
--     El usuario reporto que «los parametros aparecen solo para un grupo de aves y
--     no identifica si se refieren a hembras o machos». Peor: varias columnas
--     mixtas son un PROMEDIO ARITMETICO simple de los dos sexos (peso_cierre y
--     unif_real: (H+M)/2, sin ponderar por cantidad de aves), o sea un valor que
--     no le corresponde a ninguna ave del galpon —en reproductoras la hembra y el
--     macho tienen pesos muy distintos—. Se exponen aves inicio/fin, consumo total,
--     uniformidad, ganancia, dif % de peso vs guia, seleccion % y error de sexaje %
--     por sexo. NO se agrega aritmetica nueva: son las mismas variables internas
--     con las que ya se arman las columnas mixtas, publicadas sin promediar.
--
-- IMPORTANTE (mapeo EF): los nombres de las columnas por sexo son el snake_case
-- EXACTO de las props del DTO (…Hembras→…_hembras, …Machos→…_machos). EF Core
-- (SqlQueryRaw<IndicadorSemanalLevanteDto> con convención snake_case) mapea
-- ConsumoDiarioHembras↔consumo_diario_hembras, PesoHembras↔peso_hembras, etc.
-- Un nombre abreviado (_h/_m) NO mapearía a props …Hembras/…Machos (mismo patrón
-- probado en fn_indicadores_produccion_postura: porcentaje_mortalidad_hembras…).
-- Por eso las columnas de consumo por sexo se renombran de _h/_m a _hembras/_machos.
--
-- DROP previo: la firma cambió (se renombraron/agregaron columnas OUT por sexo),
-- y CREATE OR REPLACE no puede alterar el tipo de retorno.
DROP FUNCTION IF EXISTS fn_indicadores_levante_postura(integer);
CREATE OR REPLACE FUNCTION fn_indicadores_levante_postura(p_lote_id integer)
RETURNS TABLE(
    semana                          integer,
    aves_inicio_semana              double precision,
    aves_fin_semana                 double precision,
    consumo_diario                  double precision,   -- g/ave/día real (mixto H+M)
    consumo_tabla                   double precision,   -- g/ave/día guía (promedio H,M)
    consumo_total_semana            double precision,   -- gramos
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
    dias_con_registro               integer,
    -- REQ-002e / REQ-010b: series POR SEXO (reales y guía SIN promediar). numeric → decimal? en el DTO.
    -- Nombres = snake_case EXACTO de las props del DTO para que EF las mapee (ver nota de cabecera).
    consumo_diario_hembras          numeric,            -- g/ave/día real hembras
    consumo_diario_machos           numeric,            -- g/ave/día real machos
    consumo_tabla_hembras           numeric,            -- gr_ave_dia_h de la guía
    consumo_tabla_machos            numeric,            -- gr_ave_dia_m de la guía
    peso_hembras                    numeric,            -- peso prom hembras (arrastre si semana sin pesaje)
    peso_machos                     numeric,            -- peso prom machos  (arrastre si semana sin pesaje)
    peso_tabla_hembras              numeric,            -- guía peso_h
    peso_tabla_machos               numeric,            -- guía peso_m
    mort_pct_hembras                numeric,            -- % mort semana hembras = mort_h / aves_inicio_h * 100
    mort_pct_machos                 numeric,            -- % mort semana machos  = mort_m / aves_inicio_m * 100
    mort_tabla_hembras              numeric,            -- guía mort_sem_h
    mort_tabla_machos               numeric,            -- guía mort_sem_m
    retiro_pct_hembras              numeric,            -- % retiro hembras = (mort+sel+err)_h / aves_inicio_h * 100
    retiro_pct_machos               numeric,            -- % retiro machos  = (mort+sel+err)_m / aves_inicio_m * 100
    -- TK-2026-000022: el resto de los parametros POR SEXO. La tabla de indicadores mostraba una
    -- sola serie sin decir de que sexo era —y varias de esas columnas mixtas son un PROMEDIO
    -- ARITMETICO de hembras y machos (peso, uniformidad), o sea un numero que no le corresponde a
    -- ninguna ave real. Todo esto ya se calculaba dentro de la funcion; solo faltaba exponerlo.
    -- Convencion identica a las de arriba: NULL cuando el sexo no existe en el lote o no hay dato,
    -- nunca 0 sintetico.
    aves_inicio_hembras             numeric,            -- saldo hembras al inicio de la semana
    aves_fin_hembras                numeric,            -- saldo hembras al cierre de la semana
    aves_inicio_machos              numeric,            -- saldo machos al inicio de la semana
    aves_fin_machos                 numeric,            -- saldo machos al cierre de la semana
    consumo_total_semana_hembras    numeric,            -- gramos consumidos por las hembras en la semana
    consumo_total_semana_machos     numeric,            -- gramos consumidos por los machos en la semana
    unif_hembras                    numeric,            -- % uniformidad hembras del pesaje de la semana
    unif_machos                     numeric,            -- % uniformidad machos  del pesaje de la semana
    ganancia_hembras                numeric,            -- g ganados por las hembras respecto de la semana previa
    ganancia_machos                 numeric,            -- g ganados por los machos  respecto de la semana previa
    dif_peso_pct_hembras            numeric,            -- (peso_h - guia peso_h) / guia peso_h * 100
    dif_peso_pct_machos             numeric,            -- (peso_m - guia peso_m) / guia peso_m * 100
    seleccion_pct_hembras           numeric,            -- % seleccion semana hembras = sel_h / aves_inicio_h * 100
    seleccion_pct_machos            numeric,            -- % seleccion semana machos  = sel_m / aves_inicio_m * 100
    error_sexaje_pct_hembras        numeric,            -- % error sexaje hembras = err_h / aves_inicio_h * 100
    error_sexaje_pct_machos         numeric             -- % error sexaje machos  = err_m / aves_inicio_m * 100
)
LANGUAGE plpgsql VOLATILE AS $$
DECLARE
    v_raza        text;
    v_anio        text;
    v_company     integer;
    v_aves_enc_col integer;   -- lotes.aves_encasetadas (crudo)
    v_hembras_l   integer;    -- lotes.hembras_l (crudo)
    v_machos_l    integer;    -- lotes.machos_l (crudo)
    v_aves_enc    double precision;   -- base total resuelta (con fallback)
    v_aves_enc_h  double precision;   -- base hembras resuelta
    v_aves_enc_m  double precision;   -- base machos resuelta
    v_peso_ini    double precision;
    v_enc_date    date;
    v_min_reg     date;
    v_first_ing_h double precision;   -- primer traslado_ingreso (fallback base)
    v_first_ing_m double precision;

    -- acumuladores (mismos nombres que el front)
    v_aves_acum       double precision;
    v_aves_acum_h     double precision;
    v_aves_acum_m     double precision;
    v_mort_bajas_acum double precision := 0;   -- bajas acumuladas (unidades) REQ-002f
    v_sel_bajas_acum  double precision := 0;   -- selección acumulada (unidades) REQ-002f
    v_peso_anterior   double precision;
    v_peso_tabla_ant  double precision := 0;

    v_max_sem     integer;
    s             integer;

    -- por semana
    r_mort_tot    double precision;
    r_sel_tot     double precision;
    r_cons_kg     double precision;
    r_err_tot     double precision;
    r_tras_sal    double precision;
    r_tras_ing    double precision;
    r_venta_tot   double precision;   -- venta de aves: sale del lote y no llega a ningún otro
    r_dias        integer;
    r_aves_fin    double precision;
    -- por semana / por género
    r_mort_h      double precision;
    r_mort_m      double precision;
    r_sel_h       double precision;
    r_sel_m       double precision;
    r_err_h       double precision;
    r_err_m       double precision;
    r_cons_kg_h   double precision;
    r_cons_kg_m   double precision;
    r_tras_sal_h  double precision;
    r_tras_sal_m  double precision;
    r_tras_ing_h  double precision;
    r_tras_ing_m  double precision;
    r_venta_h     double precision;
    r_venta_m     double precision;
    r_aves_fin_h  double precision;
    r_aves_fin_m  double precision;
    r_aves_prom_h double precision;
    r_aves_prom_m double precision;
    r_cons_dia_h  double precision;
    r_cons_dia_m  double precision;
    r_cons_tabla_h double precision;
    r_cons_tabla_m double precision;
    -- REQ-010b: peso / mortalidad / retiro POR SEXO + guía por sexo.
    v_peso_ant_h   double precision;   -- arrastre peso hembras
    v_peso_ant_m   double precision;   -- arrastre peso machos
    r_peso_h       double precision;
    r_peso_m       double precision;
    r_peso_tabla_h double precision;
    r_peso_tabla_m double precision;
    r_mort_tabla_h double precision;
    r_mort_tabla_m double precision;
    -- De que tabla salio la fila de guia: 'compartida' (guia_genetica_sanmarino_colombia,
    -- >40 columnas) o 'propia' (guia_genetica_santa_reyes, 3 metricas y solo hembras).
    -- Ver el bloque de la guia mas abajo: gobierna si se coalescea a 0 o se deja NULL.
    v_origen_guia  text;
    -- ¿La EMPRESA del lote tiene guia propia (tabla reducida)? Distinto de v_origen_guia, que
    -- dice de donde salio LA FILA de esta semana y queda NULL cuando no hubo ninguna. Se necesita
    -- separado para el caso «empresa con guia propia + semana sin fila»: ahi un 0 seria un
    -- objetivo inventado (su guia arranca en la semana 18 y no cubre todo el levante).
    v_guia_propia_empresa boolean := false;
    r_mort_pct_h   double precision;
    r_mort_pct_m   double precision;
    r_retiro_pct_h double precision;
    r_retiro_pct_m double precision;

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
           l.aves_encasetadas, l.hembras_l, l.machos_l,
           COALESCE(l.peso_inicial_h,0)::double precision,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date
      INTO v_raza, v_anio, v_company, v_aves_enc_col, v_hembras_l, v_machos_l, v_peso_ini, v_enc_date
      FROM lotes l
     WHERE l.lote_id = p_lote_id AND l.deleted_at IS NULL;

    IF NOT FOUND THEN RETURN; END IF;

    -- Una sola vez por lote: ¿esta empresa tiene guia propia? Gobierna el COALESCE a 0 de mas
    -- abajo. Para las cuatro empresas que leen la guia compartida da FALSE, y la expresion que
    -- se ejecuta queda identica a la de siempre.
    SELECT EXISTS (SELECT 1 FROM guia_genetica_santa_reyes gp
                    WHERE gp.company_id = v_company AND gp.deleted_at IS NULL)
      INTO v_guia_propia_empresa;

    -- Aves entradas por traslado en filas que el armado de la serie DESCARTA (puro traslado
    -- > sem 25): fallback de base cuando el lote se pobló por traslado y no trae
    -- aves_encasetadas / hembras_l / machos_l. Nadie más suma esas aves — la ventana las tira.
    --
    -- ⚠️ El predicado debe ser el MISMO que el WHERE NOT (...) del armado de la serie. Si acá
    --    entrara una fila que sí se procesa, sus aves contarían DOS veces (base + ingreso).
    -- SUM por sexo, no una sola fila: los sexos pueden llegar en traslados de días distintos,
    -- y con LIMIT 1 el sexo ausente de la fila más antigua quedaba con base 0 ⇒ saldo negativo.
    SELECT COALESCE(SUM(COALESCE(sl.traslado_ingreso_hembras,0)),0)::double precision,
           COALESCE(SUM(COALESCE(sl.traslado_ingreso_machos,0)),0)::double precision
      INTO v_first_ing_h, v_first_ing_m
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
       AND (floor(((( sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date) / 7.0))::int) + 1 > 25
       AND COALESCE(sl.mortalidad_hembras,0) = 0 AND COALESCE(sl.mortalidad_machos,0) = 0
       AND COALESCE(sl.sel_h,0) = 0 AND COALESCE(sl.sel_m,0) = 0
       AND COALESCE(sl.error_sexaje_hembras,0) = 0 AND COALESCE(sl.error_sexaje_machos,0) = 0
       AND COALESCE(sl.consumo_kg_hembras,0) = 0 AND COALESCE(sl.consumo_kg_machos,0) = 0
       AND COALESCE(sl.peso_prom_hembras,0) = 0 AND COALESCE(sl.peso_prom_machos,0) = 0
       AND COALESCE(sl.venta_aves_hembras,0) = 0 AND COALESCE(sl.venta_aves_machos,0) = 0
       AND (COALESCE(sl.traslado_salida_hembras,0) + COALESCE(sl.traslado_salida_machos,0)
          + COALESCE(sl.traslado_ingreso_hembras,0) + COALESCE(sl.traslado_ingreso_machos,0)) > 0;
    v_first_ing_h := COALESCE(v_first_ing_h, 0);
    v_first_ing_m := COALESCE(v_first_ing_m, 0);

    -- Primer registro (calendario Bogotá) para validar el encaset.
    SELECT MIN((sl.fecha AT TIME ZONE 'America/Bogota')::date)
      INTO v_min_reg
      FROM seguimiento_diario_levante sl
     WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text;

    IF v_min_reg IS NULL THEN RETURN; END IF;   -- sin registros

    -- REQ-002B36: encaset ausente o POSTERIOR al primer registro (futuro) ⇒
    -- datos inconsistentes ⇒ cero filas (el front muestra su empty-state).
    IF v_enc_date IS NULL OR v_enc_date > v_min_reg THEN RETURN; END IF;

    -- Base de aves con fallback (REQ-002B36).
    v_aves_enc := COALESCE(
        NULLIF(v_aves_enc_col, 0)::double precision,
        NULLIF(COALESCE(v_hembras_l,0) + COALESCE(v_machos_l,0), 0)::double precision,
        NULLIF(v_first_ing_h + v_first_ing_m, 0),
        0);
    v_aves_enc_h := COALESCE(
        NULLIF(v_hembras_l, 0)::double precision,
        NULLIF(v_first_ing_h, 0),
        0);
    v_aves_enc_m := COALESCE(
        NULLIF(v_machos_l, 0)::double precision,
        NULLIF(v_first_ing_m, 0),
        0);

    v_aves_acum     := v_aves_enc;
    v_aves_acum_h   := v_aves_enc_h;
    v_aves_acum_m   := v_aves_enc_m;
    v_peso_anterior := v_peso_ini;
    v_peso_ant_h    := NULLIF(v_peso_ini, 0);   -- peso_inicial_h como base hembras (NULL si 0)
    v_peso_ant_m    := NULL;                     -- no hay peso_inicial_m ⇒ arranca NULL

    -- Semana de cada registro (calendario local Bogotá). real_sem = semana real
    -- (sin clamp inferior: el guard de encaset ya garantiza real_sem >= 1).
    -- LEAST(25,…) sólo topa por arriba filas de DATOS legítimos > 25 (no existen
    -- en levante); las filas de PURO traslado > 25 se EXCLUYEN (REQ-002f).
    DROP TABLE IF EXISTS _seg_sem;
    CREATE TEMP TABLE _seg_sem ON COMMIT DROP AS
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
            COALESCE(sl.consumo_kg_hembras,0) AS cons_kg_h_num,   -- numeric
            COALESCE(sl.consumo_kg_machos,0)  AS cons_kg_m_num,   -- numeric
            COALESCE(sl.traslado_salida_hembras,0) AS tras_sal_h,
            COALESCE(sl.traslado_salida_machos,0)  AS tras_sal_m,
            COALESCE(sl.traslado_ingreso_hembras,0) AS tras_ing_h,
            COALESCE(sl.traslado_ingreso_machos,0)  AS tras_ing_m,
            -- Venta de aves (2026-08-17): salen del lote igual que un traslado de salida, pero no
            -- llegan a ningún otro lote. Se usan los splits por sexo —no `venta_aves_cantidad`—
            -- porque el saldo también se lleva por sexo; es el mismo criterio de
            -- `fn_resumen_semanal_ra_pesadas_levante`, y el mixto se arma como h+m igual que
            -- mort/sel/err/traslados.
            COALESCE(sl.venta_aves_hembras,0)       AS venta_h,
            COALESCE(sl.venta_aves_machos,0)        AS venta_m,
            COALESCE(sl.peso_prom_hembras,0)  AS ph,
            COALESCE(sl.peso_prom_machos,0)   AS pm,
            COALESCE(sl.uniformidad_hembras,0) AS uh,
            COALESCE(sl.uniformidad_machos,0)  AS um,
            sl.id
          FROM seguimiento_diario_levante sl
         WHERE sl.tipo_seguimiento = 'levante' AND sl.lote_id = p_lote_id::text
    )
    SELECT
        LEAST(25, real_sem)                       AS sem,
        reg_date,
        (mort_h + mort_m)                         AS mort,
        (sel_h + sel_m)                           AS sel,
        (cons_kg_h_num + cons_kg_m_num)           AS cons_kg,   -- numeric (idéntico al original)
        (err_h + err_m)                           AS err,
        (tras_sal_h + tras_sal_m)                 AS tras_sal,
        (tras_ing_h + tras_ing_m)                 AS tras_ing,
        (venta_h + venta_m)                       AS venta,
        mort_h, mort_m, sel_h, sel_m, err_h, err_m,
        cons_kg_h_num::double precision           AS cons_kg_h,
        cons_kg_m_num::double precision           AS cons_kg_m,
        tras_sal_h, tras_sal_m, tras_ing_h, tras_ing_m,
        venta_h, venta_m,
        ph, pm, uh, um, id
      FROM base
     WHERE NOT (
            real_sem > 25
        AND mort_h = 0 AND mort_m = 0 AND sel_h = 0 AND sel_m = 0
        AND err_h = 0 AND err_m = 0
        AND cons_kg_h_num = 0 AND cons_kg_m_num = 0
        AND ph = 0 AND pm = 0
        -- Una fila que trae VENTA no es «puro traslado»: descartarla perdería esas aves, que es el
        -- defecto que este cambio viene a cerrar. El mismo término se agrega al predicado gemelo de
        -- `v_first_ing_*` — los dos tienen que seguir siendo idénticos o las aves cuentan dos veces.
        AND venta_h = 0 AND venta_m = 0
        AND (tras_sal_h + tras_sal_m + tras_ing_h + tras_ing_m) > 0
     );

    SELECT MAX(sem) INTO v_max_sem FROM _seg_sem;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    FOR s IN 1..v_max_sem LOOP
        -- ¿la semana tiene registros? (el front solo itera semanas presentes)
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem WHERE sem = s);

        SELECT COALESCE(SUM(mort),0), COALESCE(SUM(sel),0), COALESCE(SUM(cons_kg),0),
               COALESCE(SUM(err),0), COALESCE(SUM(tras_sal),0), COALESCE(SUM(tras_ing),0), COUNT(DISTINCT reg_date)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0),
               COALESCE(SUM(sel_h),0),  COALESCE(SUM(sel_m),0),
               COALESCE(SUM(err_h),0),  COALESCE(SUM(err_m),0),
               COALESCE(SUM(cons_kg_h),0), COALESCE(SUM(cons_kg_m),0),
               COALESCE(SUM(tras_sal_h),0), COALESCE(SUM(tras_sal_m),0),
               COALESCE(SUM(tras_ing_h),0), COALESCE(SUM(tras_ing_m),0),
               COALESCE(SUM(venta),0), COALESCE(SUM(venta_h),0), COALESCE(SUM(venta_m),0)
          INTO r_mort_tot, r_sel_tot, r_cons_kg, r_err_tot, r_tras_sal, r_tras_ing, r_dias,
               r_mort_h, r_mort_m, r_sel_h, r_sel_m, r_err_h, r_err_m,
               r_cons_kg_h, r_cons_kg_m, r_tras_sal_h, r_tras_sal_m, r_tras_ing_h, r_tras_ing_m,
               r_venta_tot, r_venta_h, r_venta_m
          FROM _seg_sem WHERE sem = s;

        -- Saldo físico Feature-13: salidas = mort + sel + err + traslado_salida + VENTA - traslado_ingreso.
        --
        -- ⭐ 2026-08-17: la VENTA entró acá. Antes esta fn era el único lector del saldo de levante
        -- que no la descontaba, así que el mismo lote y la misma semana mostraban dos conteos según
        -- la pantalla (lote 143 sem 24: 10.619 acá contra 10.329 en `fn_reporte_semanal_levante_extras`,
        -- diferencia = la venta acumulada). Una ave vendida sale del lote: no contarla infla el saldo
        -- y, en cascada, subestima el consumo por ave — el mismo mecanismo por el que en su momento
        -- hubo que sumar el error de sexaje. La especificación ejecutable es
        -- `SaldoAvesLevanteCalculos.BajasNetas`, que ya la incluía.
        r_aves_fin := v_aves_acum - r_mort_tot - r_sel_tot - r_err_tot - r_tras_sal - r_venta_tot + r_tras_ing;
        -- Saldo por género (REQ-002e). Por sexo se usan los splits dedicados, no `venta_aves_cantidad`.
        r_aves_fin_h := v_aves_acum_h - r_mort_h - r_sel_h - r_err_h - r_tras_sal_h - r_venta_h + r_tras_ing_h;
        r_aves_fin_m := v_aves_acum_m - r_mort_m - r_sel_m - r_err_m - r_tras_sal_m - r_venta_m + r_tras_ing_m;

        -- Pesaje: último registro (por fecha, luego id) de la semana con peso>0.
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

        -- Peso por sexo (REQ-010b): valor del pesaje del sexo; arrastre del último conocido
        -- cuando la semana no tiene pesaje del sexo (mismo criterio que el peso mixto, que
        -- también arrastra). NULL si nunca hubo pesaje del sexo (p.ej. machos sin pesaje ⇒
        -- serie vacía en el chart, degrada con spanGaps).
        r_peso_h := CASE WHEN r_pH > 0 THEN r_pH ELSE v_peso_ant_h END;
        r_peso_m := CASE WHEN r_pM > 0 THEN r_pM ELSE v_peso_ant_m END;

        r_cons_g    := r_cons_kg * 1000;
        r_aves_prom := (v_aves_acum + r_aves_fin)/2;
        r_cons_dia  := CASE WHEN r_aves_prom > 0 AND r_dias > 0 THEN r_cons_g/(r_aves_prom*r_dias) ELSE 0 END;

        -- Consumo real por sexo (g/ave/día): consumo_kg_sexo*1000 / saldo_prom_sexo / días.
        r_aves_prom_h := (v_aves_acum_h + r_aves_fin_h)/2;
        r_aves_prom_m := (v_aves_acum_m + r_aves_fin_m)/2;
        r_cons_dia_h  := CASE WHEN r_aves_prom_h > 0 AND r_dias > 0
                              THEN (r_cons_kg_h*1000)/(r_aves_prom_h*r_dias) ELSE NULL END;
        r_cons_dia_m  := CASE WHEN r_aves_prom_m > 0 AND r_dias > 0
                              THEN (r_cons_kg_m*1000)/(r_aves_prom_m*r_dias) ELSE NULL END;

        -- Guía real para la semana. Mixto (compat) + por sexo SIN promediar (REQ-002e).
        --
        -- 🔴 EL PROMEDIO MIXTO NO SE PUEDE APLICAR A UNA GUÍA DE SOLO HEMBRAS.
        -- Las tres expresiones mixtas hacen COALESCE de cada término y dividen por 2 FIJO.
        -- Con la guía reducida —que trae hembras y NO machos— eso da (95.00 + 0)/2 = 47,5
        -- donde el cliente dice 95,00: no es NULL, no es 0, no revienta. Es un número
        -- plausible y equivocado por un factor de 2, que nadie detecta mirando la pantalla.
        -- Por eso el promedio se aplica SOLO cuando la fila viene de la guía compartida;
        -- para la propia se usa el valor de hembras tal cual, que es el único que existe.
        -- La rama 'compartida' es LITERALMENTE la expresión de siempre ⇒ delta cero por
        -- construcción para Sanmarino, Demo, Ecuador y Panamá, no «verificado después».
        SELECT CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.gr_ave_dia_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,0))/2 END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.peso_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.peso_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.peso_m),'')::double precision,0))/2 END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.uniformidad),'')::double precision
                    ELSE COALESCE(NULLIF(btrim(g.uniformidad),'')::double precision,0) END,
               CASE WHEN g.origen = 'propia'
                    THEN NULLIF(btrim(g.mort_sem_h),'')::double precision
                    ELSE (COALESCE(NULLIF(btrim(g.mort_sem_h),'')::double precision,0)
                        + COALESCE(NULLIF(btrim(g.mort_sem_m),'')::double precision,0))/2 END,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision,
               g.origen
          INTO r_cons_tabla, r_peso_tabla, r_unif_tabla, r_mort_tabla, r_cons_tabla_h, r_cons_tabla_m,
               r_peso_tabla_h, r_peso_tabla_m, r_mort_tabla_h, r_mort_tabla_m, v_origen_guia
          FROM vw_guia_genetica_postura g
         WHERE g.company_id = v_company
           -- ⚠️ La comparacion de raza de la rama COMPARTIDA queda EXACTA y case-sensitive, como
           -- siempre: aflojarla haria matchear filas que hoy no matchean para Sanmarino, Demo,
           -- Ecuador y Panama, o sea el refactor cambiaria resultados por si solo. La rama PROPIA
           -- —inalcanzable para esas cuatro— si compara normalizado, porque produccion ya lo hace
           -- y tenerlo de un solo lado era la causa medida de que `CRIOLLA` cruzara en produccion
           -- y no en levante (30-ago-2026). La grafia del ERP la resuelve la vista, con su alias.
           AND (CASE WHEN g.origen = 'propia'
                     THEN btrim(lower(g.raza)) = btrim(lower(v_raza))
                     ELSE g.raza = v_raza END)
           AND g.anio_guia = v_anio
           AND btrim(g.edad) = s::text
         LIMIT 1;
        -- El COALESCE a 0 también es exclusivo de la guía compartida: ahí la columna existe en
        -- toda la curva y el 0 se lee como «la guía dice 0». En la propia la métrica NO EXISTE
        -- (no trae peso, ni uniformidad, ni mortalidad semanal — su retiro_ac_h es ACUMULADO),
        -- y un 0 ahí se leería como un objetivo real. NULL es la única lectura honesta, y el
        -- front ya lo sabe pintar: las series por sexo llegan NULL desde siempre.
        -- `AND NOT v_guia_propia_empresa`: sin eso, una semana SIN fila de guia (v_origen_guia
        -- NULL) caia igual en el COALESCE. Para una empresa con guia propia eso pinta 0,00 en las
        -- cuatro columnas de guia —un objetivo inventado— justo donde su guia no llega: la de
        -- Santa Reyes arranca en la semana 18 y el levante empieza en la 1. Medido el 30-ago-2026.
        -- Para las cuatro empresas sin guia propia la condicion nueva es siempre TRUE ⇒ la misma
        -- expresion de hoy, incluido el 0 legitimo cuando la guia compartida trae la columna vacia.
        IF v_origen_guia IS DISTINCT FROM 'propia' AND NOT v_guia_propia_empresa THEN
            r_cons_tabla := COALESCE(r_cons_tabla,0);
            r_peso_tabla := COALESCE(r_peso_tabla,0);
            r_unif_tabla := COALESCE(r_unif_tabla,0);
            r_mort_tabla := COALESCE(r_mort_tabla,0);
        END IF;
        -- r_cons_tabla_h/_m, r_peso_tabla_h/_m, r_mort_tabla_h/_m se dejan NULL si la guía
        -- no trae el dato del sexo (series de guía degradan a NULL, sin promediar).

        r_gan_sem   := r_peso_prom - v_peso_anterior;
        r_cons_ave  := CASE WHEN r_aves_prom > 0 THEN r_cons_g/r_aves_prom ELSE 0 END;
        r_conv      := CASE WHEN r_gan_sem > 0 THEN r_cons_ave/r_gan_sem ELSE 0 END;
        r_gan_dia_ac := r_gan_sem/7;
        r_gan_tabla := CASE WHEN r_peso_tabla > 0 AND v_peso_tabla_ant > 0 THEN r_peso_tabla - v_peso_tabla_ant ELSE 0 END;

        r_mort_sem  := CASE WHEN v_aves_acum > 0 THEN (r_mort_tot/v_aves_acum)*100 ELSE 0 END;
        r_sel_sem   := CASE WHEN v_aves_acum > 0 THEN (r_sel_tot/v_aves_acum)*100 ELSE 0 END;
        r_err_sem   := CASE WHEN v_aves_acum > 0 THEN (r_err_tot/v_aves_acum)*100 ELSE 0 END;
        r_mort_mas_sel := r_mort_sem + r_sel_sem;

        -- REQ-010b: mortalidad y retiro POR SEXO. Mismo denominador que el mixto (aves al inicio
        -- de la semana del sexo). El retiro replica el mixto retiroSem = mort+sel+errSex del sexo.
        -- NULL (no 0 sintético) cuando el sexo no tiene saldo ⇒ la serie degrada con spanGaps.
        r_mort_pct_h   := CASE WHEN v_aves_acum_h > 0 THEN (r_mort_h / v_aves_acum_h) * 100 ELSE NULL END;
        r_mort_pct_m   := CASE WHEN v_aves_acum_m > 0 THEN (r_mort_m / v_aves_acum_m) * 100 ELSE NULL END;
        r_retiro_pct_h := CASE WHEN v_aves_acum_h > 0 THEN ((r_mort_h + r_sel_h + r_err_h) / v_aves_acum_h) * 100 ELSE NULL END;
        r_retiro_pct_m := CASE WHEN v_aves_acum_m > 0 THEN ((r_mort_m + r_sel_m + r_err_m) / v_aves_acum_m) * 100 ELSE NULL END;

        r_efic   := CASE WHEN r_cons_ave > 0 THEN r_gan_sem/r_cons_ave ELSE 0 END;
        r_superv := CASE WHEN v_aves_acum > 0 THEN r_aves_fin/v_aves_acum ELSE 0 END;
        r_ip     := r_efic * r_superv;

        -- REQ-002f: acumulados reales = bajas_acumuladas / aves_encasetadas * 100.
        v_mort_bajas_acum := v_mort_bajas_acum + r_mort_tot;
        v_sel_bajas_acum  := v_sel_bajas_acum + r_sel_tot;

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
        vpi                           := r_ip;   -- front: vpi = supervivencia*eficiencia = ip
        saldo_aves_semanal            := r_aves_fin;
        mortalidad_acum               := CASE WHEN v_aves_enc > 0 THEN (v_mort_bajas_acum/v_aves_enc)*100 ELSE 0 END;
        seleccion_acum                := CASE WHEN v_aves_enc > 0 THEN (v_sel_bajas_acum/v_aves_enc)*100 ELSE 0 END;
        mortalidad_mas_seleccion_acum := CASE WHEN v_aves_enc > 0 THEN ((v_mort_bajas_acum + v_sel_bajas_acum)/v_aves_enc)*100 ELSE 0 END;
        piso_termico_visible          := false;  -- la guía no expone el flag; front daba false
        peso_inicial                  := v_peso_anterior;
        peso_cierre                   := r_peso_prom;
        dias_con_registro             := r_dias;
        consumo_diario_hembras        := r_cons_dia_h;
        consumo_diario_machos         := r_cons_dia_m;
        consumo_tabla_hembras         := r_cons_tabla_h;
        consumo_tabla_machos          := r_cons_tabla_m;
        peso_hembras                  := r_peso_h;
        peso_machos                   := r_peso_m;
        peso_tabla_hembras            := r_peso_tabla_h;
        peso_tabla_machos             := r_peso_tabla_m;
        mort_pct_hembras              := r_mort_pct_h;
        mort_pct_machos               := r_mort_pct_m;
        mort_tabla_hembras            := r_mort_tabla_h;
        mort_tabla_machos             := r_mort_tabla_m;
        retiro_pct_hembras            := r_retiro_pct_h;
        retiro_pct_machos             := r_retiro_pct_m;

        -- TK-2026-000022 — el resto de los parametros por sexo. Ninguno introduce aritmetica
        -- nueva: son las MISMAS variables con las que ya se arman las columnas mixtas, expuestas
        -- sin promediar. El criterio de NULL es el de las series por sexo de arriba: si el sexo no
        -- tiene saldo (o la semana no tuvo pesaje / la guia no trae el dato) va NULL, para que la
        -- pantalla muestre un guion en vez de un cero que se leeria como dato real.
        aves_inicio_hembras           := CASE WHEN v_aves_enc_h > 0 THEN v_aves_acum_h ELSE NULL END;
        aves_fin_hembras              := CASE WHEN v_aves_enc_h > 0 THEN r_aves_fin_h  ELSE NULL END;
        aves_inicio_machos            := CASE WHEN v_aves_enc_m > 0 THEN v_aves_acum_m ELSE NULL END;
        aves_fin_machos               := CASE WHEN v_aves_enc_m > 0 THEN r_aves_fin_m  ELSE NULL END;
        consumo_total_semana_hembras  := CASE WHEN v_aves_enc_h > 0 THEN r_cons_kg_h * 1000 ELSE NULL END;
        consumo_total_semana_machos   := CASE WHEN v_aves_enc_m > 0 THEN r_cons_kg_m * 1000 ELSE NULL END;
        -- Uniformidad: 0 significa "no hubo pesaje esta semana", no "0 % de uniformidad".
        unif_hembras                  := CASE WHEN r_uH > 0 THEN r_uH ELSE NULL END;
        unif_machos                   := CASE WHEN r_uM > 0 THEN r_uM ELSE NULL END;
        ganancia_hembras              := CASE WHEN r_peso_h IS NOT NULL AND v_peso_ant_h IS NOT NULL
                                              THEN r_peso_h - v_peso_ant_h ELSE NULL END;
        ganancia_machos               := CASE WHEN r_peso_m IS NOT NULL AND v_peso_ant_m IS NOT NULL
                                              THEN r_peso_m - v_peso_ant_m ELSE NULL END;
        dif_peso_pct_hembras          := CASE WHEN r_peso_tabla_h > 0 AND r_peso_h IS NOT NULL
                                              THEN ((r_peso_h - r_peso_tabla_h)/r_peso_tabla_h)*100 ELSE NULL END;
        dif_peso_pct_machos           := CASE WHEN r_peso_tabla_m > 0 AND r_peso_m IS NOT NULL
                                              THEN ((r_peso_m - r_peso_tabla_m)/r_peso_tabla_m)*100 ELSE NULL END;
        seleccion_pct_hembras         := CASE WHEN v_aves_acum_h > 0 THEN (r_sel_h / v_aves_acum_h) * 100 ELSE NULL END;
        seleccion_pct_machos          := CASE WHEN v_aves_acum_m > 0 THEN (r_sel_m / v_aves_acum_m) * 100 ELSE NULL END;
        error_sexaje_pct_hembras      := CASE WHEN v_aves_acum_h > 0 THEN (r_err_h / v_aves_acum_h) * 100 ELSE NULL END;
        error_sexaje_pct_machos       := CASE WHEN v_aves_acum_m > 0 THEN (r_err_m / v_aves_acum_m) * 100 ELSE NULL END;

        RETURN NEXT;

        -- avanzar acumuladores (idéntico al front) + saldo por género.
        v_aves_acum      := r_aves_fin;
        v_aves_acum_h    := r_aves_fin_h;
        v_aves_acum_m    := r_aves_fin_m;
        v_peso_anterior  := r_peso_prom;
        v_peso_tabla_ant := r_peso_tabla;
        v_peso_ant_h     := r_peso_h;   -- arrastre peso por sexo (REQ-010b)
        v_peso_ant_m     := r_peso_m;
    END LOOP;

    RETURN;
END;
$$;
