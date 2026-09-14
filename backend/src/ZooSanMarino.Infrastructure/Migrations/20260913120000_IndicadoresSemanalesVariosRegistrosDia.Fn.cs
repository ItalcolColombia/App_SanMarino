// Partial de 20260913120000_IndicadoresSemanalesVariosRegistrosDia: SQL verbatim.
// Nueva / V4 = espejos en backend/sql/ (este commit); Prev / V3 = versiones anteriores para el Down.

namespace ZooSanMarino.Infrastructure.Migrations
{
    public partial class IndicadoresSemanalesVariosRegistrosDia
    {
        private const string FnIndicadoresLevanteNueva = """
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
--   * 13-sep-2026 — Varios registros por día (Santa Reyes, flag
--     permite_multiples_seguimientos_diarios): el PESAJE semanal se arma por DÍA —último día de la
--     semana con pesaje; peso por sexo = promedio de los registros de ese día que pesaron ese sexo;
--     uniformidad = la del último registro del día que la trae—. Con un registro por día da lo
--     mismo que antes («el último registro con peso>0»). Sumas y días ya eran correctos.
--     Plan: fase_de_desarrollo/indicadores_semanales_varios_registros_dia_plan.md.
--     Especificación ejecutable: Application/Calculos/PesajeSemanalLevanteCalculos.cs.
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

        -- Pesaje de la semana = el ÚLTIMO DÍA con pesaje (ph>0 o pm>0), agregado POR DÍA
        -- (13-sep-2026, varios registros por día):
        --   • peso por sexo = PROMEDIO de los registros de ese día que pesaron ESE sexo (>0);
        --   • uniformidad   = la del último registro (id) de ese día que la trae (>0).
        -- Antes se tomaba UN registro («el último con peso>0»): con 2 pesajes el mismo día no
        -- promediaba —la grilla diaria sí—, y si el último solo pesó hembras, los machos del otro
        -- registro se perdían y se arrastraba el peso de la semana anterior. Con UN registro con
        -- pesaje por día (toda empresa sin el flag) cada agregado devuelve el valor de esa fila ⇒
        -- idéntico a la versión anterior. Especificación ejecutable: PesajeSemanalLevanteCalculos.
        IF EXISTS (SELECT 1 FROM _seg_sem WHERE sem = s AND (ph > 0 OR pm > 0)) THEN
            SELECT COALESCE(AVG(ph) FILTER (WHERE ph > 0), 0),
                   COALESCE(AVG(pm) FILTER (WHERE pm > 0), 0),
                   COALESCE((array_agg(uh ORDER BY id DESC) FILTER (WHERE uh > 0))[1], 0),
                   COALESCE((array_agg(um ORDER BY id DESC) FILTER (WHERE um > 0))[1], 0)
              INTO r_pH, r_pM, r_uH, r_uM
              FROM _seg_sem
             WHERE sem = s AND (ph > 0 OR pm > 0)
               AND reg_date = (SELECT MAX(reg_date) FROM _seg_sem
                                WHERE sem = s AND (ph > 0 OR pm > 0));
        ELSE
            -- Semana sin pesaje: como siempre, el último registro (la uniformidad puede venir sola).
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
""";

        private const string FnIndicadoresLevantePrev = """
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
""";

        private const string FnSeguimientoDiarioProduccionV4 = """
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- fn_seguimiento_diario_produccion — grilla diaria CANÓNICA de producción (postura)
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- v4 (2026-09-13) — seg_dias_agrupado (solo flag ON): peso ave/huevo promedia los registros que
--   pesaron (> 0), uniformidad/CV = último registro que la trae, desempate del «último» por
--   seg_id y metadata.huevoItems concatenados de todos los registros del día. Flag OFF intacto.
--   Detalle junto al CTE. Espejo C#: SeguimientoDiarioProduccionCalculos.AgruparPorDia.
-- v3 (2026-09-05) — múltiples registros por día para empresas con el flag
--   companies.permite_multiples_seguimientos_diarios (plan
--   fase_de_desarrollo/seguimiento_produccion_multiples_registros_dia_plan.md). El CTE
--   seg_dias se bifurca: seg_dias_dedup (de siempre, INTACTO byte a byte) para el flag OFF,
--   seg_dias_agrupado (SUMA lo aditivo, PROMEDIA peso, último-registro-gana en
--   uniformidad/CV%/observaciones/metadata) para el flag ON. Decide ctx.permite_multiples,
--   un valor constante por llamada (la fn siempre corre para UN lote → UNA empresa). Espejo
--   C#: SeguimientoDiarioProduccionCalculos.AgruparPorDia.
-- v2 (2026-08-01) — filas TSD visibles en la rama LPP (migración 20260801110000)
--   Problema: las filas de traslado creadas por TrasladoAvesDesdeSegService nacen con
--   lote_postura_produccion_id NULL (matching por lote_id + fecha, documentado en
--   fn_migracion_seguimiento.sql) ⇒ la rama LPP (filtro por lpp_id) no las devolvía y el
--   traslado hecho desde la pantalla era INVISIBLE en la grilla del LPP.
--   Solución: la rama LPP suma las filas de la tabla canónica con lpp NULL y el MISMO
--   lote base (marcadas con la columna nueva fila_sin_lpp = true). Las 3 fns semanales
--   las EXCLUYEN explícitamente (AND NOT fila_sin_lpp) — su salida no cambia ni un byte:
--   un día solo-traslado no es un «día con registro» para los indicadores (paridad con el
--   comportamiento previo). El saldo tampoco cambia: esas filas traen mort/sel/err = 0 y
--   el movimiento ya entra por movimiento_aves.
-- v1 (2026-08-01) — creación (plan fase_de_desarrollo/seguimiento_produccion_fn_canonica_plan.md)
--
--   Patrón fn_seguimiento_diario_engorde (v13): LANGUAGE sql STABLE a PROPÓSITO — el
--   inlining en CROSS JOIN LATERAL es real (plpgsql = Function Scan, ×2.8 más lento medido
--   en engorde) y plpgsql RETURN QUERY no aplica assignment casts (SUM(int)→bigint vs INT
--   = error 42804). Por eso TODOS los agregados del SELECT final van casteados explícitos.
--
--   ÚNICA FÓRMULA de los números diarios de producción (regla del repo «una sola fórmula
--   por número»): fila diaria cruda + derivados (saldo de aves del día, acumulados de
--   huevos, % postura hen-day diario). El espejo C# de especificación ejecutable es
--   Application/Calculos/SeguimientoDiarioProduccionCalculos.cs (tests xUnit = contrato).
--
--   Decisiones de diseño (D1-D4 del plan, confirmadas):
--   • Universo = días con seguimiento (dedup) ∪ días con movimientos de aves (filas
--     movimiento-only con seg_id NULL, patrón engorde v7: una venta tardía genera su fila
--     y el saldo del lote la refleja).
--   • Fuente dual + dedup por día Bogotá con «gana el timestamp más temprano»: MISMO bloque
--     que fn_indicadores_produccion_postura / fn_clasificacion_huevo_items_produccion /
--     fn_resumen_semanal_ra_pesadas_produccion (el registro puede vivir en la tabla
--     canónica seguimiento_diario_produccion o en la legacy seguimiento_diario_levante
--     con tipo_seguimiento='produccion'; hoy la legacy tiene 0 filas de producción).
--   • SALDO DE AVES (D4): GREATEST(0, base − Σ(mort+sel+ERR) − Σ mov_out + Σ mov_in),
--     CON error de sexaje — semántica de los escritores incrementales
--     (SeguimientoProduccionService.AplicarDescuentoLppAsync y fn_migracion paso 3).
--     base = COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0) (misma prioridad
--     que ObtenerInformacionLoteAsync). mov_out = movimiento_aves Completado no borrado con
--     el lote como ORIGEN (Venta+Traslado+Retiro, cualquier tipo — igual que el GET);
--     mov_in = tipo Traslado con el lote como DESTINO. lote_postura_produccion.aves_*_actual
--     es DERIVADO verificable, jamás fuente.
--   • FILTRO DE FASE de los movimientos (divergencia DELIBERADA vs el GET viejo): solo se
--     cuentan movimientos con fecha >= lpp.fecha_inicio_produccion (día Bogotá). Los
--     anteriores pertenecen al LEVANTE y ya están reflejados en aves_h_inicial del LPP
--     (las aves iniciales de producción = aves vivas al cierre del levante): contarlos de
--     nuevo los duplicaba. Caso real: lote 130 — el GET viejo daba 8.646 H (restaba otra
--     vez la venta 100 + salida 500 − ingreso 200 del levante); el valor correcto validado
--     por el E2E de carga masiva es 9.039. Con fecha_inicio_produccion NULL no se filtra
--     (comportamiento del GET, conservador).
--   • Semana de vida CRUDA ((fecha − ref)/7)+1 SIN piso 26 ni corte 25: el corte es del
--     consumidor (los indicadores cortan en 25; la clasificación por ítems de Santa Reyes
--     deliberadamente NO corta). ref = COALESCE(lev.fecha_encaset, lpp.fecha_encaset,
--     lpp.fecha_inicio_produccion) en día Bogotá (idéntico a las 3 fns semanales).
--   • Rama LPP filtra por lote_postura_produccion_id (paridad con la grilla y las fns de
--     hoy) ⇒ las filas de traslado TSD con lpp NULL siguen fuera de esta rama (deuda
--     documentada; su efecto en el saldo entra por movimiento_aves, que sí las audita).
--   • Rama legacy (p_lote_id): el C# ya resuelve el lote hijo en fase Produccion; acá solo
--     se listan sus filas. Sin LPP no hay base de aves ⇒ saldos NULL (no 0: GREATEST
--     ignora NULLs, por eso el CASE explícito).
--   • Corte de día SIEMPRE AT TIME ZONE 'America/Bogota'; jamás date_trunc dependiente de
--     la TZ de sesión ni ::date directo sobre timestamptz.
--
--   Consumidores: grilla GET /api/Produccion/seguimiento (SqlQueryRaw, snake_case),
--   informacion-lote (saldo del último día), y las fns semanales re-sourced sobre esta.
--
--   Firma: exactamente UNO de los dos parámetros debe venir no-NULL.
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION fn_seguimiento_diario_produccion(
    p_lote_postura_produccion_id INT,
    p_lote_id                    INT
)
RETURNS TABLE (
    -- Identificación
    seg_id                      BIGINT,       -- NULL = fila movimiento-only (sin registro diario)
    fecha                       DATE,
    fecha_ts                    TIMESTAMPTZ,  -- timestamp original del registro (NULL en movimiento-only)
    fuente                      TEXT,         -- 'sdp' | 'sdl' (legacy) | 'mov'
    fila_sin_lpp                BOOLEAN,      -- v2: fila TSD del lote base con lpp NULL en rama LPP (las fns semanales la excluyen)
    lote_id                     INT,
    lote_postura_produccion_id  INT,
    company_id                  INT,
    -- Tiempo
    edad_dias                   INT,
    semana                      INT,
    -- Aves crudas del registro
    mortalidad_hembras          INT,
    mortalidad_machos           INT,
    sel_h                       INT,
    sel_m                       INT,
    error_sexaje_hembras        INT,
    error_sexaje_machos         INT,
    -- Consumo
    cons_kg_h                   DOUBLE PRECISION,
    cons_kg_m                   DOUBLE PRECISION,
    consumo_total_kg            DOUBLE PRECISION,
    tipo_alimento               TEXT,
    -- Huevos crudos
    huevo_tot                   INT,
    huevo_inc                   INT,
    huevo_limpio                INT,
    huevo_tratado               INT,
    huevo_sucio                 INT,
    huevo_deforme               INT,
    huevo_blanco                INT,
    huevo_doble_yema            INT,
    huevo_piso                  INT,
    huevo_pequeno               INT,
    huevo_roto                  INT,
    huevo_desecho               INT,
    huevo_otro                  INT,
    peso_huevo                  DOUBLE PRECISION,
    -- Derivados de huevos
    huevo_tot_acum              BIGINT,
    huevo_inc_acum              BIGINT,
    pct_postura_dia             DOUBLE PRECISION,  -- hen-day diario: huevo_tot / aves_h_inicio_dia * 100
    -- Movimientos de aves del día (desde movimiento_aves Completado)
    mov_venta_h                 INT,
    mov_venta_m                 INT,
    mov_retiro_h                INT,
    mov_retiro_m                INT,
    mov_traslado_in_h           INT,
    mov_traslado_in_m           INT,
    mov_traslado_out_h          INT,
    mov_traslado_out_m          INT,
    -- Saldo de aves (D4: con error de sexaje; NULL en rama legacy sin LPP)
    aves_h_inicio_dia           INT,
    aves_m_inicio_dia           INT,
    saldo_aves_h                INT,
    saldo_aves_m                INT,
    -- Traslado crudo de la fila diaria
    es_traslado                 BOOLEAN,
    traslado_direccion          TEXT,
    traslado_ingreso_hembras    INT,
    traslado_ingreso_machos     INT,
    traslado_salida_hembras     INT,
    traslado_salida_machos      INT,
    lote_destino_id             INT,
    granja_destino_id           INT,
    -- Pesaje (peso_h/m, uniformidad y CV del lote son NUMERIC — mismos tipos que la tabla,
    -- para que el C# los lea como decimal EXACTO, sin pasar por float8)
    peso_h                      NUMERIC,
    peso_m                      NUMERIC,
    uniformidad                 NUMERIC,
    coeficiente_variacion       NUMERIC,
    uniformidad_hembras         DOUBLE PRECISION,
    uniformidad_machos          DOUBLE PRECISION,
    cv_hembras                  DOUBLE PRECISION,
    cv_machos                   DOUBLE PRECISION,
    observaciones_pesaje        TEXT,
    -- Agua
    consumo_agua_diario         DOUBLE PRECISION,
    consumo_agua_ph             DOUBLE PRECISION,
    consumo_agua_orp            DOUBLE PRECISION,
    consumo_agua_temperatura    DOUBLE PRECISION,
    -- Otros
    etapa                       INT,
    ciclo                       TEXT,
    observaciones               TEXT,
    metadata                    JSONB,
    created_by_user_id          INT,
    created_at                  TIMESTAMPTZ,
    updated_at                  TIMESTAMPTZ
)
LANGUAGE sql STABLE
AS $$
WITH ctx AS (
    -- ── Rama LPP: base de aves + fecha de referencia (idéntica a fn_indicadores) ──
    SELECT lpp.lote_postura_produccion_id                            AS ctx_lpp_id,
           lpp.lote_id                                               AS ctx_lote_id,
           lpp.company_id                                            AS ctx_company,
           COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0) AS base_h,
           COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0)  AS base_m,
           (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
               AT TIME ZONE 'America/Bogota')::date                  AS ref_date,
           (lpp.fecha_inicio_produccion
               AT TIME ZONE 'America/Bogota')::date                  AS mov_desde,
           true                                                      AS es_lpp,
           -- v3: flag de la empresa — decide si seg_dias dedupea (de siempre) o agrupa (§ abajo)
           COALESCE(comp.permite_multiples_seguimientos_diarios, false) AS permite_multiples
      FROM lote_postura_produccion lpp
      LEFT JOIN lote_postura_levante lev
             ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
            AND lev.deleted_at IS NULL
      LEFT JOIN companies comp ON comp.id = lpp.company_id
     WHERE p_lote_postura_produccion_id IS NOT NULL
       AND lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
       AND lpp.deleted_at IS NULL
    UNION ALL
    -- ── Rama legacy: p_lote_id ya resuelto por el C# (lote hijo en fase Produccion o lote
    --    crudo). LEFT JOIN para no perder filas huérfanas cuyo lote no existe en lotes
    --    (paridad con la grilla actual, que igual las devuelve). Sin base de aves.
    SELECT NULL::int, p_lote_id, lo.company_id,
           NULL::int, NULL::int,
           (COALESCE(lo.fecha_inicio_produccion, pa.fecha_encaset, lo.fecha_encaset)
               AT TIME ZONE 'America/Bogota')::date,
           NULL::date,
           false,
           COALESCE(comp2.permite_multiples_seguimientos_diarios, false)
      FROM (SELECT 1) uno
      LEFT JOIN lotes lo ON lo.lote_id = p_lote_id AND lo.deleted_at IS NULL
      LEFT JOIN lotes pa ON pa.lote_id = lo.lote_padre_id AND pa.deleted_at IS NULL
      LEFT JOIN companies comp2 ON comp2.id = lo.company_id
     WHERE p_lote_postura_produccion_id IS NULL
       AND p_lote_id IS NOT NULL
),
-- ── Fuente dual + dedup por día Bogotá (bloque canónico de las fns semanales) ──
crudos AS (
    SELECT sd.id::bigint                                  AS c_seg_id,
           'sdl'::text                                    AS c_fuente,
           sd.fecha                                       AS c_ts,
           COALESCE(sd.mortalidad_hembras, 0)             AS c_mort_h,
           COALESCE(sd.mortalidad_machos, 0)              AS c_mort_m,
           COALESCE(sd.sel_h, 0)                          AS c_sel_h,
           COALESCE(sd.sel_m, 0)                          AS c_sel_m,
           COALESCE(sd.error_sexaje_hembras, 0)           AS c_err_h,
           COALESCE(sd.error_sexaje_machos, 0)            AS c_err_m,
           COALESCE(sd.consumo_kg_hembras, 0)::float8     AS c_cons_h,
           COALESCE(sd.consumo_kg_machos, 0)::float8      AS c_cons_m,
           sd.tipo_alimento::text                         AS c_tipo_alimento,
           COALESCE(sd.huevo_tot, 0)                      AS c_huevo_tot,
           COALESCE(sd.huevo_inc, 0)                      AS c_huevo_inc,
           COALESCE(sd.huevo_limpio, 0)                   AS c_h_limpio,
           COALESCE(sd.huevo_tratado, 0)                  AS c_h_tratado,
           COALESCE(sd.huevo_sucio, 0)                    AS c_h_sucio,
           COALESCE(sd.huevo_deforme, 0)                  AS c_h_deforme,
           COALESCE(sd.huevo_blanco, 0)                   AS c_h_blanco,
           COALESCE(sd.huevo_doble_yema, 0)               AS c_h_doble,
           COALESCE(sd.huevo_piso, 0)                     AS c_h_piso,
           COALESCE(sd.huevo_pequeno, 0)                  AS c_h_pequeno,
           COALESCE(sd.huevo_roto, 0)                     AS c_h_roto,
           COALESCE(sd.huevo_desecho, 0)                  AS c_h_desecho,
           COALESCE(sd.huevo_otro, 0)                     AS c_h_otro,
           sd.peso_huevo::float8                          AS c_peso_huevo,
           sd.es_traslado                                 AS c_es_traslado,
           sd.traslado_direccion::text                    AS c_tras_dir,
           COALESCE(sd.traslado_ingreso_hembras, 0)       AS c_tras_in_h,
           COALESCE(sd.traslado_ingreso_machos, 0)        AS c_tras_in_m,
           COALESCE(sd.traslado_salida_hembras, 0)        AS c_tras_out_h,
           COALESCE(sd.traslado_salida_machos, 0)         AS c_tras_out_m,
           NULL::int                                      AS c_lote_destino_id,
           NULL::int                                      AS c_granja_destino_id,
           sd.peso_h                                      AS c_peso_h,
           sd.peso_m                                      AS c_peso_m,
           sd.uniformidad                                 AS c_unif,
           sd.coeficiente_variacion                       AS c_cv,
           sd.uniformidad_hembras::float8                 AS c_unif_h,
           sd.uniformidad_machos::float8                  AS c_unif_m,
           sd.cv_hembras::float8                          AS c_cv_h,
           sd.cv_machos::float8                           AS c_cv_m,
           sd.observaciones_pesaje                        AS c_obs_pesaje,
           sd.consumo_agua_diario                         AS c_agua,
           sd.consumo_agua_ph                             AS c_agua_ph,
           sd.consumo_agua_orp                            AS c_agua_orp,
           sd.consumo_agua_temperatura                    AS c_agua_temp,
           sd.etapa                                       AS c_etapa,
           sd.ciclo::text                                 AS c_ciclo,
           sd.observaciones                               AS c_observaciones,
           sd.metadata                                    AS c_metadata,
           NULL::int                                      AS c_created_by,  -- legacy: varchar, no casteable
           sd.created_at                                  AS c_created_at,
           sd.updated_at                                  AS c_updated_at,
           NULL::int                                      AS c_company_id,
           sd.lote_postura_produccion_id                  AS c_lpp
      FROM seguimiento_diario_levante sd
     WHERE sd.tipo_seguimiento = 'produccion'
       AND ( (p_lote_postura_produccion_id IS NOT NULL
                AND sd.lote_postura_produccion_id = p_lote_postura_produccion_id)
          OR (p_lote_postura_produccion_id IS NULL
                AND sd.lote_id = p_lote_id::text) )
    UNION ALL
    SELECT sp.id::bigint,
           'sdp'::text,
           sp.fecha_registro,
           COALESCE(sp.mortalidad_hembras, 0),
           COALESCE(sp.mortalidad_machos, 0),
           COALESCE(sp.sel_h, 0),
           COALESCE(sp.sel_m, 0),
           COALESCE(sp.error_sexaje_hembras, 0),
           COALESCE(sp.error_sexaje_machos, 0),
           COALESCE(sp.cons_kg_h, 0)::float8,
           COALESCE(sp.cons_kg_m, 0)::float8,
           sp.tipo_alimento,
           COALESCE(sp.huevo_tot, 0),
           COALESCE(sp.huevo_inc, 0),
           COALESCE(sp.huevo_limpio, 0),
           COALESCE(sp.huevo_tratado, 0),
           COALESCE(sp.huevo_sucio, 0),
           COALESCE(sp.huevo_deforme, 0),
           COALESCE(sp.huevo_blanco, 0),
           COALESCE(sp.huevo_doble_yema, 0),
           COALESCE(sp.huevo_piso, 0),
           COALESCE(sp.huevo_pequeno, 0),
           COALESCE(sp.huevo_roto, 0),
           COALESCE(sp.huevo_desecho, 0),
           COALESCE(sp.huevo_otro, 0),
           sp.peso_huevo,
           sp.es_traslado,
           sp.traslado_direccion::text,
           COALESCE(sp.traslado_ingreso_hembras, 0),
           COALESCE(sp.traslado_ingreso_machos, 0),
           COALESCE(sp.traslado_salida_hembras, 0),
           COALESCE(sp.traslado_salida_machos, 0),
           sp.lote_destino_id,
           sp.granja_destino_id,
           sp.peso_h,
           sp.peso_m,
           sp.uniformidad,
           sp.coeficiente_variacion,
           sp.uniformidad_hembras,
           sp.uniformidad_machos,
           sp.cv_hembras,
           sp.cv_machos,
           sp.observaciones_pesaje,
           sp.consumo_agua_diario,
           sp.consumo_agua_ph,
           sp.consumo_agua_orp,
           sp.consumo_agua_temperatura,
           sp.etapa,
           sp.ciclo::text,
           sp.observaciones,
           sp.metadata,
           sp.created_by_user_id,
           sp.created_at,
           sp.updated_at,
           sp.company_id,
           sp.lote_postura_produccion_id
      FROM seguimiento_diario_produccion sp
     -- A5: la fila borrada no cuenta. La fn ya filtraba el `deleted_at` de `lotes`, `lpp`, `lpl` y
     -- `movimiento_aves`, pero NO el de la tabla de la que saca los seguimientos. La columna existe
     -- y está mapeada en EF; hoy nadie la escribe (los borrados son físicos), así que esto es un
     -- no-op sobre los datos actuales — y deja de haber una forma silenciosa de inflar el saldo el
     -- día que algo empiece a marcarla.
     WHERE sp.deleted_at IS NULL
       AND ( (p_lote_postura_produccion_id IS NOT NULL
                AND sp.lote_postura_produccion_id = p_lote_postura_produccion_id)
          -- v2: filas TSD del MISMO lote base con lpp NULL (traslados desde la pantalla de
          -- seguimiento no setean la FK; matching por lote crudo, igual que fn_migracion)
          OR (p_lote_postura_produccion_id IS NOT NULL
                AND sp.lote_postura_produccion_id IS NULL
                AND sp.lote_id IN (SELECT c2.ctx_lote_id FROM ctx c2
                                    WHERE c2.es_lpp AND c2.ctx_lote_id IS NOT NULL))
          OR (p_lote_postura_produccion_id IS NULL
                AND sp.lote_id = p_lote_id) )
),
-- v3: DOS estrategias en paralelo — cuál alimenta seg_dias lo decide el flag de la empresa
-- (ctx.permite_multiples), UN valor constante para toda la llamada (la fn siempre se invoca
-- para UN lote). El camino de siempre (seg_dias_dedup) queda BYTE A BYTE intacto: ninguna
-- empresa sin el flag pasa jamás por seg_dias_agrupado.
seg_dias_dedup AS (
    SELECT DISTINCT ON ((c.c_ts AT TIME ZONE 'America/Bogota')::date)
           c.*,
           (c.c_ts AT TIME ZONE 'America/Bogota')::date AS reg_date
      FROM crudos c
     ORDER BY (c.c_ts AT TIME ZONE 'America/Bogota')::date, c.c_ts
),
-- v3 — flag ON: agrupa TODOS los registros del mismo día calendario Bogotá en una sola fila.
-- Regla de agregación por campo (plan seguimiento_produccion_multiples_registros_dia, §3):
--   • ADITIVOS (mortalidad, selección, error de sexaje, consumo, huevos, traslados) → SUMA.
--   • Peso promedio (ave y huevo) → PROMEDIO simple. "Ponderado por aves vivas" se simplifica a
--     esto porque las aves vivas son un valor de DÍA (constante entre los registros del mismo
--     día), así que ponderar por una constante equivale a promediar sin más.
--   • Uniformidad, CV%, observaciones, metadata, ciclo, etapa, tipo de alimento, traslado
--     (dirección/lote/granja destino) → gana el ÚLTIMO registro del día (mayor c_ts).
--   • es_traslado → TRUE si CUALQUIER registro del día fue de traslado (bool_or).
--   • Identificación (seg_id, company_id, lpp) → el primer valor NO NULO (MIN), para que un
--     registro real (con lpp/company) no quede tapado por una fila-stub de traslado sin FK.
--   • created_at = el más temprano del día; updated_at = el más tardío; created_by = el del
--     último registro (simplificación: no hay noción de "autor del día").
-- Con UN solo registro (caso normal de cualquier empresa sin duplicados ese día) cada fórmula
-- de arriba devuelve exactamente el valor de esa fila — el mismo resultado que el dedup.
-- v4 (2026-09-13) — lo NO aditivo se agrupaba mal con 2+ registros el mismo día y lo heredaban
-- los indicadores semanales (plan fase_de_desarrollo/indicadores_semanales_varios_registros_dia_plan.md):
--   • peso_h / peso_m / peso_huevo → promedio de los registros que PESARON (> 0). peso_huevo se
--     guarda en 0 cuando no se pesa: el AVG de v3 daba 30 g para un pesaje de 60 g + un registro
--     sin pesaje. Si ninguno pesó, el AVG de siempre (0 o NULL, como en v3).
--   • uniformidad / CV → el último registro que la TRAE: un NULL ya no tapa la medición del día.
--   • «gana el último» se desempata por c_seg_id: los forms graban todos los registros del día al
--     mediodía y el timestamp solo no decidía (orden no determinista).
--   • metadata → la del último registro con `huevoItems` = los de TODOS los registros del día.
--     En v3 Primera/Pnc de la semana perdían los ítems de los demás registros y huevo_tot no.
--   Con UN registro el día cada expresión devuelve el valor de esa fila (idéntico a v3).
seg_dias_agrupado AS (
    SELECT
        MIN(c.c_seg_id)                                              AS c_seg_id,
        (array_agg(c.c_fuente ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]              AS c_fuente,
        MIN(c.c_ts)                                                  AS c_ts,
        SUM(c.c_mort_h)::int                                         AS c_mort_h,
        SUM(c.c_mort_m)::int                                         AS c_mort_m,
        SUM(c.c_sel_h)::int                                          AS c_sel_h,
        SUM(c.c_sel_m)::int                                          AS c_sel_m,
        SUM(c.c_err_h)::int                                          AS c_err_h,
        SUM(c.c_err_m)::int                                          AS c_err_m,
        SUM(c.c_cons_h)::float8                                      AS c_cons_h,
        SUM(c.c_cons_m)::float8                                      AS c_cons_m,
        (array_agg(c.c_tipo_alimento ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]       AS c_tipo_alimento,
        SUM(c.c_huevo_tot)::int                                      AS c_huevo_tot,
        SUM(c.c_huevo_inc)::int                                      AS c_huevo_inc,
        SUM(c.c_h_limpio)::int                                       AS c_h_limpio,
        SUM(c.c_h_tratado)::int                                      AS c_h_tratado,
        SUM(c.c_h_sucio)::int                                        AS c_h_sucio,
        SUM(c.c_h_deforme)::int                                      AS c_h_deforme,
        SUM(c.c_h_blanco)::int                                       AS c_h_blanco,
        SUM(c.c_h_doble)::int                                        AS c_h_doble,
        SUM(c.c_h_piso)::int                                         AS c_h_piso,
        SUM(c.c_h_pequeno)::int                                      AS c_h_pequeno,
        SUM(c.c_h_roto)::int                                         AS c_h_roto,
        SUM(c.c_h_desecho)::int                                      AS c_h_desecho,
        SUM(c.c_h_otro)::int                                         AS c_h_otro,
        COALESCE(AVG(c.c_peso_huevo) FILTER (WHERE c.c_peso_huevo > 0), AVG(c.c_peso_huevo))::float8 AS c_peso_huevo,
        bool_or(c.c_es_traslado)                                     AS c_es_traslado,
        (array_agg(c.c_tras_dir ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]            AS c_tras_dir,
        SUM(c.c_tras_in_h)::int                                      AS c_tras_in_h,
        SUM(c.c_tras_in_m)::int                                      AS c_tras_in_m,
        SUM(c.c_tras_out_h)::int                                     AS c_tras_out_h,
        SUM(c.c_tras_out_m)::int                                     AS c_tras_out_m,
        (array_agg(c.c_lote_destino_id ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]     AS c_lote_destino_id,
        (array_agg(c.c_granja_destino_id ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]   AS c_granja_destino_id,
        COALESCE(AVG(c.c_peso_h) FILTER (WHERE c.c_peso_h > 0), AVG(c.c_peso_h)) AS c_peso_h,
        COALESCE(AVG(c.c_peso_m) FILTER (WHERE c.c_peso_m > 0), AVG(c.c_peso_m)) AS c_peso_m,
        (array_agg(c.c_unif ORDER BY c.c_ts DESC, c.c_seg_id DESC) FILTER (WHERE c.c_unif IS NOT NULL))[1]                AS c_unif,
        (array_agg(c.c_cv ORDER BY c.c_ts DESC, c.c_seg_id DESC) FILTER (WHERE c.c_cv IS NOT NULL))[1]                  AS c_cv,
        (array_agg(c.c_unif_h ORDER BY c.c_ts DESC, c.c_seg_id DESC) FILTER (WHERE c.c_unif_h IS NOT NULL))[1]              AS c_unif_h,
        (array_agg(c.c_unif_m ORDER BY c.c_ts DESC, c.c_seg_id DESC) FILTER (WHERE c.c_unif_m IS NOT NULL))[1]              AS c_unif_m,
        (array_agg(c.c_cv_h ORDER BY c.c_ts DESC, c.c_seg_id DESC) FILTER (WHERE c.c_cv_h IS NOT NULL))[1]                AS c_cv_h,
        (array_agg(c.c_cv_m ORDER BY c.c_ts DESC, c.c_seg_id DESC) FILTER (WHERE c.c_cv_m IS NOT NULL))[1]                AS c_cv_m,
        (array_agg(c.c_obs_pesaje ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]          AS c_obs_pesaje,
        SUM(c.c_agua)                                                AS c_agua,
        AVG(c.c_agua_ph)                                             AS c_agua_ph,
        AVG(c.c_agua_orp)                                            AS c_agua_orp,
        AVG(c.c_agua_temp)                                           AS c_agua_temp,
        (array_agg(c.c_etapa ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]               AS c_etapa,
        (array_agg(c.c_ciclo ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]               AS c_ciclo,
        (array_agg(c.c_observaciones ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]       AS c_observaciones,
        -- v4: metadata del ÚLTIMO registro, pero `huevoItems` = los de TODOS los registros del día
        -- concatenados en orden de carga (son cantidades, como huevo_tot, que sí se suma).
        CASE
            WHEN bool_or(jsonb_typeof(c.c_metadata -> 'huevoItems') = 'array')
            THEN (CASE WHEN jsonb_typeof((array_agg(c.c_metadata ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]) = 'object'
                       THEN (array_agg(c.c_metadata ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]
                       ELSE '{}'::jsonb END)
                 || jsonb_build_object('huevoItems',
                        (SELECT COALESCE(jsonb_agg(it.item ORDER BY arr.ord, it.pos), '[]'::jsonb)
                           FROM unnest(array_agg(c.c_metadata -> 'huevoItems' ORDER BY c.c_ts, c.c_seg_id)
                                         FILTER (WHERE jsonb_typeof(c.c_metadata -> 'huevoItems') = 'array'))
                                WITH ORDINALITY AS arr(items, ord)
                          CROSS JOIN LATERAL jsonb_array_elements(arr.items) WITH ORDINALITY AS it(item, pos)))
            ELSE (array_agg(c.c_metadata ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]
        END                                                          AS c_metadata,
        (array_agg(c.c_created_by ORDER BY c.c_ts DESC, c.c_seg_id DESC))[1]          AS c_created_by,
        MIN(c.c_created_at)                                          AS c_created_at,
        MAX(c.c_updated_at)                                          AS c_updated_at,
        MIN(c.c_company_id)                                          AS c_company_id,
        MIN(c.c_lpp)                                                 AS c_lpp,
        (c.c_ts AT TIME ZONE 'America/Bogota')::date                 AS reg_date
      FROM crudos c
     GROUP BY (c.c_ts AT TIME ZONE 'America/Bogota')::date
),
seg_dias AS (
    SELECT * FROM seg_dias_dedup    WHERE NOT (SELECT bool_or(ctx.permite_multiples) FROM ctx)
    UNION ALL
    SELECT * FROM seg_dias_agrupado WHERE     (SELECT bool_or(ctx.permite_multiples) FROM ctx)
),
-- ── Movimientos de aves (solo rama LPP con lote base) — misma población que el GET
--    informacion-lote: Completado, no borrado, misma empresa; salidas = CUALQUIER tipo con
--    el lote como origen; entradas = tipo Traslado con el lote como destino. ──
movs AS (
    SELECT (m.fecha_movimiento AT TIME ZONE 'America/Bogota')::date AS mov_date,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS out_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS out_m,
           CASE WHEN m.tipo_movimiento = 'Traslado' AND m.lote_destino_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS in_h,
           CASE WHEN m.tipo_movimiento = 'Traslado' AND m.lote_destino_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS in_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Venta'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS venta_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Venta'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS venta_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Retiro'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS retiro_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Retiro'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS retiro_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Traslado'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS tout_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Traslado'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS tout_m
      FROM movimiento_aves m
      JOIN ctx c ON c.es_lpp AND c.ctx_lote_id IS NOT NULL
     WHERE m.estado = 'Completado'
       AND m.deleted_at IS NULL
       AND m.company_id = c.ctx_company
       AND (m.lote_origen_id = c.ctx_lote_id OR m.lote_destino_id = c.ctx_lote_id)
       -- Filtro de FASE: los movimientos previos al inicio de producción son del levante y
       -- ya viven en aves_h_inicial (ver changelog v1)
       AND (c.mov_desde IS NULL
            OR (m.fecha_movimiento AT TIME ZONE 'America/Bogota')::date >= c.mov_desde)
),
movs_dia AS (
    SELECT mv.mov_date,
           SUM(mv.out_h)::int    AS out_h,
           SUM(mv.out_m)::int    AS out_m,
           SUM(mv.in_h)::int     AS in_h,
           SUM(mv.in_m)::int     AS in_m,
           SUM(mv.venta_h)::int  AS venta_h,
           SUM(mv.venta_m)::int  AS venta_m,
           SUM(mv.retiro_h)::int AS retiro_h,
           SUM(mv.retiro_m)::int AS retiro_m,
           SUM(mv.tout_h)::int   AS tout_h,
           SUM(mv.tout_m)::int   AS tout_m
      FROM movs mv
     GROUP BY mv.mov_date
),
-- ── Universo: días con seguimiento ∪ días solo-movimiento (FULL JOIN por día) ──
universo AS (
    SELECT COALESCE(s.reg_date, md.mov_date) AS u_fecha,
           s.*,
           md.out_h    AS m_out_h,
           md.out_m    AS m_out_m,
           md.in_h     AS m_in_h,
           md.in_m     AS m_in_m,
           md.venta_h  AS m_venta_h,
           md.venta_m  AS m_venta_m,
           md.retiro_h AS m_retiro_h,
           md.retiro_m AS m_retiro_m,
           md.tout_h   AS m_tout_h,
           md.tout_m   AS m_tout_m
      FROM seg_dias s
      FULL OUTER JOIN movs_dia md ON md.mov_date = s.reg_date
)
SELECT
    u.c_seg_id                                                        AS seg_id,
    u.u_fecha                                                         AS fecha,
    u.c_ts                                                            AS fecha_ts,
    COALESCE(u.c_fuente, 'mov')                                       AS fuente,
    (u.c_seg_id IS NOT NULL AND u.c_lpp IS NULL AND c.es_lpp)         AS fila_sin_lpp,
    c.ctx_lote_id                                                     AS lote_id,
    COALESCE(u.c_lpp, c.ctx_lpp_id)                                   AS lote_postura_produccion_id,
    COALESCE(u.c_company_id, c.ctx_company)                           AS company_id,
    CASE WHEN c.ref_date IS NULL THEN NULL
         ELSE GREATEST(0, u.u_fecha - c.ref_date) END::int            AS edad_dias,
    CASE WHEN c.ref_date IS NULL THEN NULL
         ELSE ((u.u_fecha - c.ref_date) / 7) + 1 END::int             AS semana,
    u.c_mort_h                                                        AS mortalidad_hembras,
    u.c_mort_m                                                        AS mortalidad_machos,
    u.c_sel_h                                                         AS sel_h,
    u.c_sel_m                                                         AS sel_m,
    u.c_err_h                                                         AS error_sexaje_hembras,
    u.c_err_m                                                         AS error_sexaje_machos,
    u.c_cons_h                                                        AS cons_kg_h,
    u.c_cons_m                                                        AS cons_kg_m,
    (COALESCE(u.c_cons_h, 0) + COALESCE(u.c_cons_m, 0))::float8       AS consumo_total_kg,
    u.c_tipo_alimento                                                 AS tipo_alimento,
    u.c_huevo_tot                                                     AS huevo_tot,
    u.c_huevo_inc                                                     AS huevo_inc,
    u.c_h_limpio                                                      AS huevo_limpio,
    u.c_h_tratado                                                     AS huevo_tratado,
    u.c_h_sucio                                                       AS huevo_sucio,
    u.c_h_deforme                                                     AS huevo_deforme,
    u.c_h_blanco                                                      AS huevo_blanco,
    u.c_h_doble                                                       AS huevo_doble_yema,
    u.c_h_piso                                                        AS huevo_piso,
    u.c_h_pequeno                                                     AS huevo_pequeno,
    u.c_h_roto                                                        AS huevo_roto,
    u.c_h_desecho                                                     AS huevo_desecho,
    u.c_h_otro                                                        AS huevo_otro,
    u.c_peso_huevo                                                    AS peso_huevo,
    SUM(COALESCE(u.c_huevo_tot, 0)) OVER w_ord::bigint                AS huevo_tot_acum,
    SUM(COALESCE(u.c_huevo_inc, 0)) OVER w_ord::bigint                AS huevo_inc_acum,
    CASE
        WHEN c.base_h IS NULL THEN NULL
        WHEN GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)) > 0
            THEN (100.0 * COALESCE(u.c_huevo_tot, 0)
                / GREATEST(0, c.base_h
                    - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                    - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                    + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)))
        ELSE 0
    END::float8                                                       AS pct_postura_dia,
    COALESCE(u.m_venta_h, 0)                                          AS mov_venta_h,
    COALESCE(u.m_venta_m, 0)                                          AS mov_venta_m,
    COALESCE(u.m_retiro_h, 0)                                         AS mov_retiro_h,
    COALESCE(u.m_retiro_m, 0)                                         AS mov_retiro_m,
    COALESCE(u.m_in_h, 0)                                             AS mov_traslado_in_h,
    COALESCE(u.m_in_m, 0)                                             AS mov_traslado_in_m,
    COALESCE(u.m_tout_h, 0)                                           AS mov_traslado_out_h,
    COALESCE(u.m_tout_m, 0)                                           AS mov_traslado_out_m,
    CASE WHEN c.base_h IS NULL THEN NULL
         ELSE GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)) END::int AS aves_h_inicio_dia,
    CASE WHEN c.base_m IS NULL THEN NULL
         ELSE GREATEST(0, c.base_m
                - COALESCE(SUM(u.c_mort_m + u.c_sel_m + u.c_err_m) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_m, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_m, 0)) OVER w_prev, 0)) END::int AS aves_m_inicio_dia,
    CASE WHEN c.base_h IS NULL THEN NULL
         ELSE GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_ord, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_ord, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_ord, 0)) END::int  AS saldo_aves_h,
    CASE WHEN c.base_m IS NULL THEN NULL
         ELSE GREATEST(0, c.base_m
                - COALESCE(SUM(u.c_mort_m + u.c_sel_m + u.c_err_m) OVER w_ord, 0)
                - COALESCE(SUM(COALESCE(u.m_out_m, 0)) OVER w_ord, 0)
                + COALESCE(SUM(COALESCE(u.m_in_m, 0)) OVER w_ord, 0)) END::int  AS saldo_aves_m,
    COALESCE(u.c_es_traslado, false)                                  AS es_traslado,
    u.c_tras_dir                                                      AS traslado_direccion,
    u.c_tras_in_h                                                     AS traslado_ingreso_hembras,
    u.c_tras_in_m                                                     AS traslado_ingreso_machos,
    u.c_tras_out_h                                                    AS traslado_salida_hembras,
    u.c_tras_out_m                                                    AS traslado_salida_machos,
    u.c_lote_destino_id                                               AS lote_destino_id,
    u.c_granja_destino_id                                             AS granja_destino_id,
    u.c_peso_h                                                        AS peso_h,
    u.c_peso_m                                                        AS peso_m,
    u.c_unif                                                          AS uniformidad,
    u.c_cv                                                            AS coeficiente_variacion,
    u.c_unif_h                                                        AS uniformidad_hembras,
    u.c_unif_m                                                        AS uniformidad_machos,
    u.c_cv_h                                                          AS cv_hembras,
    u.c_cv_m                                                          AS cv_machos,
    u.c_obs_pesaje                                                    AS observaciones_pesaje,
    u.c_agua                                                          AS consumo_agua_diario,
    u.c_agua_ph                                                       AS consumo_agua_ph,
    u.c_agua_orp                                                      AS consumo_agua_orp,
    u.c_agua_temp                                                     AS consumo_agua_temperatura,
    u.c_etapa                                                         AS etapa,
    u.c_ciclo                                                         AS ciclo,
    u.c_observaciones                                                 AS observaciones,
    u.c_metadata                                                      AS metadata,
    u.c_created_by                                                    AS created_by_user_id,
    u.c_created_at                                                    AS created_at,
    u.c_updated_at                                                    AS updated_at
FROM universo u
CROSS JOIN ctx c
WINDOW
    w_ord  AS (ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0)
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    w_prev AS (ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0)
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0);
$$;
""";

        private const string FnSeguimientoDiarioProduccionV3 = """
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- fn_seguimiento_diario_produccion — grilla diaria CANÓNICA de producción (postura)
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- v3 (2026-09-05) — múltiples registros por día para empresas con el flag
--   companies.permite_multiples_seguimientos_diarios (plan
--   fase_de_desarrollo/seguimiento_produccion_multiples_registros_dia_plan.md). El CTE
--   seg_dias se bifurca: seg_dias_dedup (de siempre, INTACTO byte a byte) para el flag OFF,
--   seg_dias_agrupado (SUMA lo aditivo, PROMEDIA peso, último-registro-gana en
--   uniformidad/CV%/observaciones/metadata) para el flag ON. Decide ctx.permite_multiples,
--   un valor constante por llamada (la fn siempre corre para UN lote → UNA empresa). Espejo
--   C#: SeguimientoDiarioProduccionCalculos.AgruparPorDia.
-- v2 (2026-08-01) — filas TSD visibles en la rama LPP (migración 20260801110000)
--   Problema: las filas de traslado creadas por TrasladoAvesDesdeSegService nacen con
--   lote_postura_produccion_id NULL (matching por lote_id + fecha, documentado en
--   fn_migracion_seguimiento.sql) ⇒ la rama LPP (filtro por lpp_id) no las devolvía y el
--   traslado hecho desde la pantalla era INVISIBLE en la grilla del LPP.
--   Solución: la rama LPP suma las filas de la tabla canónica con lpp NULL y el MISMO
--   lote base (marcadas con la columna nueva fila_sin_lpp = true). Las 3 fns semanales
--   las EXCLUYEN explícitamente (AND NOT fila_sin_lpp) — su salida no cambia ni un byte:
--   un día solo-traslado no es un «día con registro» para los indicadores (paridad con el
--   comportamiento previo). El saldo tampoco cambia: esas filas traen mort/sel/err = 0 y
--   el movimiento ya entra por movimiento_aves.
-- v1 (2026-08-01) — creación (plan fase_de_desarrollo/seguimiento_produccion_fn_canonica_plan.md)
--
--   Patrón fn_seguimiento_diario_engorde (v13): LANGUAGE sql STABLE a PROPÓSITO — el
--   inlining en CROSS JOIN LATERAL es real (plpgsql = Function Scan, ×2.8 más lento medido
--   en engorde) y plpgsql RETURN QUERY no aplica assignment casts (SUM(int)→bigint vs INT
--   = error 42804). Por eso TODOS los agregados del SELECT final van casteados explícitos.
--
--   ÚNICA FÓRMULA de los números diarios de producción (regla del repo «una sola fórmula
--   por número»): fila diaria cruda + derivados (saldo de aves del día, acumulados de
--   huevos, % postura hen-day diario). El espejo C# de especificación ejecutable es
--   Application/Calculos/SeguimientoDiarioProduccionCalculos.cs (tests xUnit = contrato).
--
--   Decisiones de diseño (D1-D4 del plan, confirmadas):
--   • Universo = días con seguimiento (dedup) ∪ días con movimientos de aves (filas
--     movimiento-only con seg_id NULL, patrón engorde v7: una venta tardía genera su fila
--     y el saldo del lote la refleja).
--   • Fuente dual + dedup por día Bogotá con «gana el timestamp más temprano»: MISMO bloque
--     que fn_indicadores_produccion_postura / fn_clasificacion_huevo_items_produccion /
--     fn_resumen_semanal_ra_pesadas_produccion (el registro puede vivir en la tabla
--     canónica seguimiento_diario_produccion o en la legacy seguimiento_diario_levante
--     con tipo_seguimiento='produccion'; hoy la legacy tiene 0 filas de producción).
--   • SALDO DE AVES (D4): GREATEST(0, base − Σ(mort+sel+ERR) − Σ mov_out + Σ mov_in),
--     CON error de sexaje — semántica de los escritores incrementales
--     (SeguimientoProduccionService.AplicarDescuentoLppAsync y fn_migracion paso 3).
--     base = COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0) (misma prioridad
--     que ObtenerInformacionLoteAsync). mov_out = movimiento_aves Completado no borrado con
--     el lote como ORIGEN (Venta+Traslado+Retiro, cualquier tipo — igual que el GET);
--     mov_in = tipo Traslado con el lote como DESTINO. lote_postura_produccion.aves_*_actual
--     es DERIVADO verificable, jamás fuente.
--   • FILTRO DE FASE de los movimientos (divergencia DELIBERADA vs el GET viejo): solo se
--     cuentan movimientos con fecha >= lpp.fecha_inicio_produccion (día Bogotá). Los
--     anteriores pertenecen al LEVANTE y ya están reflejados en aves_h_inicial del LPP
--     (las aves iniciales de producción = aves vivas al cierre del levante): contarlos de
--     nuevo los duplicaba. Caso real: lote 130 — el GET viejo daba 8.646 H (restaba otra
--     vez la venta 100 + salida 500 − ingreso 200 del levante); el valor correcto validado
--     por el E2E de carga masiva es 9.039. Con fecha_inicio_produccion NULL no se filtra
--     (comportamiento del GET, conservador).
--   • Semana de vida CRUDA ((fecha − ref)/7)+1 SIN piso 26 ni corte 25: el corte es del
--     consumidor (los indicadores cortan en 25; la clasificación por ítems de Santa Reyes
--     deliberadamente NO corta). ref = COALESCE(lev.fecha_encaset, lpp.fecha_encaset,
--     lpp.fecha_inicio_produccion) en día Bogotá (idéntico a las 3 fns semanales).
--   • Rama LPP filtra por lote_postura_produccion_id (paridad con la grilla y las fns de
--     hoy) ⇒ las filas de traslado TSD con lpp NULL siguen fuera de esta rama (deuda
--     documentada; su efecto en el saldo entra por movimiento_aves, que sí las audita).
--   • Rama legacy (p_lote_id): el C# ya resuelve el lote hijo en fase Produccion; acá solo
--     se listan sus filas. Sin LPP no hay base de aves ⇒ saldos NULL (no 0: GREATEST
--     ignora NULLs, por eso el CASE explícito).
--   • Corte de día SIEMPRE AT TIME ZONE 'America/Bogota'; jamás date_trunc dependiente de
--     la TZ de sesión ni ::date directo sobre timestamptz.
--
--   Consumidores: grilla GET /api/Produccion/seguimiento (SqlQueryRaw, snake_case),
--   informacion-lote (saldo del último día), y las fns semanales re-sourced sobre esta.
--
--   Firma: exactamente UNO de los dos parámetros debe venir no-NULL.
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION fn_seguimiento_diario_produccion(
    p_lote_postura_produccion_id INT,
    p_lote_id                    INT
)
RETURNS TABLE (
    -- Identificación
    seg_id                      BIGINT,       -- NULL = fila movimiento-only (sin registro diario)
    fecha                       DATE,
    fecha_ts                    TIMESTAMPTZ,  -- timestamp original del registro (NULL en movimiento-only)
    fuente                      TEXT,         -- 'sdp' | 'sdl' (legacy) | 'mov'
    fila_sin_lpp                BOOLEAN,      -- v2: fila TSD del lote base con lpp NULL en rama LPP (las fns semanales la excluyen)
    lote_id                     INT,
    lote_postura_produccion_id  INT,
    company_id                  INT,
    -- Tiempo
    edad_dias                   INT,
    semana                      INT,
    -- Aves crudas del registro
    mortalidad_hembras          INT,
    mortalidad_machos           INT,
    sel_h                       INT,
    sel_m                       INT,
    error_sexaje_hembras        INT,
    error_sexaje_machos         INT,
    -- Consumo
    cons_kg_h                   DOUBLE PRECISION,
    cons_kg_m                   DOUBLE PRECISION,
    consumo_total_kg            DOUBLE PRECISION,
    tipo_alimento               TEXT,
    -- Huevos crudos
    huevo_tot                   INT,
    huevo_inc                   INT,
    huevo_limpio                INT,
    huevo_tratado               INT,
    huevo_sucio                 INT,
    huevo_deforme               INT,
    huevo_blanco                INT,
    huevo_doble_yema            INT,
    huevo_piso                  INT,
    huevo_pequeno               INT,
    huevo_roto                  INT,
    huevo_desecho               INT,
    huevo_otro                  INT,
    peso_huevo                  DOUBLE PRECISION,
    -- Derivados de huevos
    huevo_tot_acum              BIGINT,
    huevo_inc_acum              BIGINT,
    pct_postura_dia             DOUBLE PRECISION,  -- hen-day diario: huevo_tot / aves_h_inicio_dia * 100
    -- Movimientos de aves del día (desde movimiento_aves Completado)
    mov_venta_h                 INT,
    mov_venta_m                 INT,
    mov_retiro_h                INT,
    mov_retiro_m                INT,
    mov_traslado_in_h           INT,
    mov_traslado_in_m           INT,
    mov_traslado_out_h          INT,
    mov_traslado_out_m          INT,
    -- Saldo de aves (D4: con error de sexaje; NULL en rama legacy sin LPP)
    aves_h_inicio_dia           INT,
    aves_m_inicio_dia           INT,
    saldo_aves_h                INT,
    saldo_aves_m                INT,
    -- Traslado crudo de la fila diaria
    es_traslado                 BOOLEAN,
    traslado_direccion          TEXT,
    traslado_ingreso_hembras    INT,
    traslado_ingreso_machos     INT,
    traslado_salida_hembras     INT,
    traslado_salida_machos      INT,
    lote_destino_id             INT,
    granja_destino_id           INT,
    -- Pesaje (peso_h/m, uniformidad y CV del lote son NUMERIC — mismos tipos que la tabla,
    -- para que el C# los lea como decimal EXACTO, sin pasar por float8)
    peso_h                      NUMERIC,
    peso_m                      NUMERIC,
    uniformidad                 NUMERIC,
    coeficiente_variacion       NUMERIC,
    uniformidad_hembras         DOUBLE PRECISION,
    uniformidad_machos          DOUBLE PRECISION,
    cv_hembras                  DOUBLE PRECISION,
    cv_machos                   DOUBLE PRECISION,
    observaciones_pesaje        TEXT,
    -- Agua
    consumo_agua_diario         DOUBLE PRECISION,
    consumo_agua_ph             DOUBLE PRECISION,
    consumo_agua_orp            DOUBLE PRECISION,
    consumo_agua_temperatura    DOUBLE PRECISION,
    -- Otros
    etapa                       INT,
    ciclo                       TEXT,
    observaciones               TEXT,
    metadata                    JSONB,
    created_by_user_id          INT,
    created_at                  TIMESTAMPTZ,
    updated_at                  TIMESTAMPTZ
)
LANGUAGE sql STABLE
AS $$
WITH ctx AS (
    -- ── Rama LPP: base de aves + fecha de referencia (idéntica a fn_indicadores) ──
    SELECT lpp.lote_postura_produccion_id                            AS ctx_lpp_id,
           lpp.lote_id                                               AS ctx_lote_id,
           lpp.company_id                                            AS ctx_company,
           COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0) AS base_h,
           COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0)  AS base_m,
           (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
               AT TIME ZONE 'America/Bogota')::date                  AS ref_date,
           (lpp.fecha_inicio_produccion
               AT TIME ZONE 'America/Bogota')::date                  AS mov_desde,
           true                                                      AS es_lpp,
           -- v3: flag de la empresa — decide si seg_dias dedupea (de siempre) o agrupa (§ abajo)
           COALESCE(comp.permite_multiples_seguimientos_diarios, false) AS permite_multiples
      FROM lote_postura_produccion lpp
      LEFT JOIN lote_postura_levante lev
             ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
            AND lev.deleted_at IS NULL
      LEFT JOIN companies comp ON comp.id = lpp.company_id
     WHERE p_lote_postura_produccion_id IS NOT NULL
       AND lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
       AND lpp.deleted_at IS NULL
    UNION ALL
    -- ── Rama legacy: p_lote_id ya resuelto por el C# (lote hijo en fase Produccion o lote
    --    crudo). LEFT JOIN para no perder filas huérfanas cuyo lote no existe en lotes
    --    (paridad con la grilla actual, que igual las devuelve). Sin base de aves.
    SELECT NULL::int, p_lote_id, lo.company_id,
           NULL::int, NULL::int,
           (COALESCE(lo.fecha_inicio_produccion, pa.fecha_encaset, lo.fecha_encaset)
               AT TIME ZONE 'America/Bogota')::date,
           NULL::date,
           false,
           COALESCE(comp2.permite_multiples_seguimientos_diarios, false)
      FROM (SELECT 1) uno
      LEFT JOIN lotes lo ON lo.lote_id = p_lote_id AND lo.deleted_at IS NULL
      LEFT JOIN lotes pa ON pa.lote_id = lo.lote_padre_id AND pa.deleted_at IS NULL
      LEFT JOIN companies comp2 ON comp2.id = lo.company_id
     WHERE p_lote_postura_produccion_id IS NULL
       AND p_lote_id IS NOT NULL
),
-- ── Fuente dual + dedup por día Bogotá (bloque canónico de las fns semanales) ──
crudos AS (
    SELECT sd.id::bigint                                  AS c_seg_id,
           'sdl'::text                                    AS c_fuente,
           sd.fecha                                       AS c_ts,
           COALESCE(sd.mortalidad_hembras, 0)             AS c_mort_h,
           COALESCE(sd.mortalidad_machos, 0)              AS c_mort_m,
           COALESCE(sd.sel_h, 0)                          AS c_sel_h,
           COALESCE(sd.sel_m, 0)                          AS c_sel_m,
           COALESCE(sd.error_sexaje_hembras, 0)           AS c_err_h,
           COALESCE(sd.error_sexaje_machos, 0)            AS c_err_m,
           COALESCE(sd.consumo_kg_hembras, 0)::float8     AS c_cons_h,
           COALESCE(sd.consumo_kg_machos, 0)::float8      AS c_cons_m,
           sd.tipo_alimento::text                         AS c_tipo_alimento,
           COALESCE(sd.huevo_tot, 0)                      AS c_huevo_tot,
           COALESCE(sd.huevo_inc, 0)                      AS c_huevo_inc,
           COALESCE(sd.huevo_limpio, 0)                   AS c_h_limpio,
           COALESCE(sd.huevo_tratado, 0)                  AS c_h_tratado,
           COALESCE(sd.huevo_sucio, 0)                    AS c_h_sucio,
           COALESCE(sd.huevo_deforme, 0)                  AS c_h_deforme,
           COALESCE(sd.huevo_blanco, 0)                   AS c_h_blanco,
           COALESCE(sd.huevo_doble_yema, 0)               AS c_h_doble,
           COALESCE(sd.huevo_piso, 0)                     AS c_h_piso,
           COALESCE(sd.huevo_pequeno, 0)                  AS c_h_pequeno,
           COALESCE(sd.huevo_roto, 0)                     AS c_h_roto,
           COALESCE(sd.huevo_desecho, 0)                  AS c_h_desecho,
           COALESCE(sd.huevo_otro, 0)                     AS c_h_otro,
           sd.peso_huevo::float8                          AS c_peso_huevo,
           sd.es_traslado                                 AS c_es_traslado,
           sd.traslado_direccion::text                    AS c_tras_dir,
           COALESCE(sd.traslado_ingreso_hembras, 0)       AS c_tras_in_h,
           COALESCE(sd.traslado_ingreso_machos, 0)        AS c_tras_in_m,
           COALESCE(sd.traslado_salida_hembras, 0)        AS c_tras_out_h,
           COALESCE(sd.traslado_salida_machos, 0)         AS c_tras_out_m,
           NULL::int                                      AS c_lote_destino_id,
           NULL::int                                      AS c_granja_destino_id,
           sd.peso_h                                      AS c_peso_h,
           sd.peso_m                                      AS c_peso_m,
           sd.uniformidad                                 AS c_unif,
           sd.coeficiente_variacion                       AS c_cv,
           sd.uniformidad_hembras::float8                 AS c_unif_h,
           sd.uniformidad_machos::float8                  AS c_unif_m,
           sd.cv_hembras::float8                          AS c_cv_h,
           sd.cv_machos::float8                           AS c_cv_m,
           sd.observaciones_pesaje                        AS c_obs_pesaje,
           sd.consumo_agua_diario                         AS c_agua,
           sd.consumo_agua_ph                             AS c_agua_ph,
           sd.consumo_agua_orp                            AS c_agua_orp,
           sd.consumo_agua_temperatura                    AS c_agua_temp,
           sd.etapa                                       AS c_etapa,
           sd.ciclo::text                                 AS c_ciclo,
           sd.observaciones                               AS c_observaciones,
           sd.metadata                                    AS c_metadata,
           NULL::int                                      AS c_created_by,  -- legacy: varchar, no casteable
           sd.created_at                                  AS c_created_at,
           sd.updated_at                                  AS c_updated_at,
           NULL::int                                      AS c_company_id,
           sd.lote_postura_produccion_id                  AS c_lpp
      FROM seguimiento_diario_levante sd
     WHERE sd.tipo_seguimiento = 'produccion'
       AND ( (p_lote_postura_produccion_id IS NOT NULL
                AND sd.lote_postura_produccion_id = p_lote_postura_produccion_id)
          OR (p_lote_postura_produccion_id IS NULL
                AND sd.lote_id = p_lote_id::text) )
    UNION ALL
    SELECT sp.id::bigint,
           'sdp'::text,
           sp.fecha_registro,
           COALESCE(sp.mortalidad_hembras, 0),
           COALESCE(sp.mortalidad_machos, 0),
           COALESCE(sp.sel_h, 0),
           COALESCE(sp.sel_m, 0),
           COALESCE(sp.error_sexaje_hembras, 0),
           COALESCE(sp.error_sexaje_machos, 0),
           COALESCE(sp.cons_kg_h, 0)::float8,
           COALESCE(sp.cons_kg_m, 0)::float8,
           sp.tipo_alimento,
           COALESCE(sp.huevo_tot, 0),
           COALESCE(sp.huevo_inc, 0),
           COALESCE(sp.huevo_limpio, 0),
           COALESCE(sp.huevo_tratado, 0),
           COALESCE(sp.huevo_sucio, 0),
           COALESCE(sp.huevo_deforme, 0),
           COALESCE(sp.huevo_blanco, 0),
           COALESCE(sp.huevo_doble_yema, 0),
           COALESCE(sp.huevo_piso, 0),
           COALESCE(sp.huevo_pequeno, 0),
           COALESCE(sp.huevo_roto, 0),
           COALESCE(sp.huevo_desecho, 0),
           COALESCE(sp.huevo_otro, 0),
           sp.peso_huevo,
           sp.es_traslado,
           sp.traslado_direccion::text,
           COALESCE(sp.traslado_ingreso_hembras, 0),
           COALESCE(sp.traslado_ingreso_machos, 0),
           COALESCE(sp.traslado_salida_hembras, 0),
           COALESCE(sp.traslado_salida_machos, 0),
           sp.lote_destino_id,
           sp.granja_destino_id,
           sp.peso_h,
           sp.peso_m,
           sp.uniformidad,
           sp.coeficiente_variacion,
           sp.uniformidad_hembras,
           sp.uniformidad_machos,
           sp.cv_hembras,
           sp.cv_machos,
           sp.observaciones_pesaje,
           sp.consumo_agua_diario,
           sp.consumo_agua_ph,
           sp.consumo_agua_orp,
           sp.consumo_agua_temperatura,
           sp.etapa,
           sp.ciclo::text,
           sp.observaciones,
           sp.metadata,
           sp.created_by_user_id,
           sp.created_at,
           sp.updated_at,
           sp.company_id,
           sp.lote_postura_produccion_id
      FROM seguimiento_diario_produccion sp
     -- A5: la fila borrada no cuenta. La fn ya filtraba el `deleted_at` de `lotes`, `lpp`, `lpl` y
     -- `movimiento_aves`, pero NO el de la tabla de la que saca los seguimientos. La columna existe
     -- y está mapeada en EF; hoy nadie la escribe (los borrados son físicos), así que esto es un
     -- no-op sobre los datos actuales — y deja de haber una forma silenciosa de inflar el saldo el
     -- día que algo empiece a marcarla.
     WHERE sp.deleted_at IS NULL
       AND ( (p_lote_postura_produccion_id IS NOT NULL
                AND sp.lote_postura_produccion_id = p_lote_postura_produccion_id)
          -- v2: filas TSD del MISMO lote base con lpp NULL (traslados desde la pantalla de
          -- seguimiento no setean la FK; matching por lote crudo, igual que fn_migracion)
          OR (p_lote_postura_produccion_id IS NOT NULL
                AND sp.lote_postura_produccion_id IS NULL
                AND sp.lote_id IN (SELECT c2.ctx_lote_id FROM ctx c2
                                    WHERE c2.es_lpp AND c2.ctx_lote_id IS NOT NULL))
          OR (p_lote_postura_produccion_id IS NULL
                AND sp.lote_id = p_lote_id) )
),
-- v3: DOS estrategias en paralelo — cuál alimenta seg_dias lo decide el flag de la empresa
-- (ctx.permite_multiples), UN valor constante para toda la llamada (la fn siempre se invoca
-- para UN lote). El camino de siempre (seg_dias_dedup) queda BYTE A BYTE intacto: ninguna
-- empresa sin el flag pasa jamás por seg_dias_agrupado.
seg_dias_dedup AS (
    SELECT DISTINCT ON ((c.c_ts AT TIME ZONE 'America/Bogota')::date)
           c.*,
           (c.c_ts AT TIME ZONE 'America/Bogota')::date AS reg_date
      FROM crudos c
     ORDER BY (c.c_ts AT TIME ZONE 'America/Bogota')::date, c.c_ts
),
-- v3 — flag ON: agrupa TODOS los registros del mismo día calendario Bogotá en una sola fila.
-- Regla de agregación por campo (plan seguimiento_produccion_multiples_registros_dia, §3):
--   • ADITIVOS (mortalidad, selección, error de sexaje, consumo, huevos, traslados) → SUMA.
--   • Peso promedio (ave y huevo) → PROMEDIO simple. "Ponderado por aves vivas" se simplifica a
--     esto porque las aves vivas son un valor de DÍA (constante entre los registros del mismo
--     día), así que ponderar por una constante equivale a promediar sin más.
--   • Uniformidad, CV%, observaciones, metadata, ciclo, etapa, tipo de alimento, traslado
--     (dirección/lote/granja destino) → gana el ÚLTIMO registro del día (mayor c_ts).
--   • es_traslado → TRUE si CUALQUIER registro del día fue de traslado (bool_or).
--   • Identificación (seg_id, company_id, lpp) → el primer valor NO NULO (MIN), para que un
--     registro real (con lpp/company) no quede tapado por una fila-stub de traslado sin FK.
--   • created_at = el más temprano del día; updated_at = el más tardío; created_by = el del
--     último registro (simplificación: no hay noción de "autor del día").
-- Con UN solo registro (caso normal de cualquier empresa sin duplicados ese día) cada fórmula
-- de arriba devuelve exactamente el valor de esa fila — el mismo resultado que el dedup.
seg_dias_agrupado AS (
    SELECT
        MIN(c.c_seg_id)                                              AS c_seg_id,
        (array_agg(c.c_fuente ORDER BY c.c_ts DESC))[1]              AS c_fuente,
        MIN(c.c_ts)                                                  AS c_ts,
        SUM(c.c_mort_h)::int                                         AS c_mort_h,
        SUM(c.c_mort_m)::int                                         AS c_mort_m,
        SUM(c.c_sel_h)::int                                          AS c_sel_h,
        SUM(c.c_sel_m)::int                                          AS c_sel_m,
        SUM(c.c_err_h)::int                                          AS c_err_h,
        SUM(c.c_err_m)::int                                          AS c_err_m,
        SUM(c.c_cons_h)::float8                                      AS c_cons_h,
        SUM(c.c_cons_m)::float8                                      AS c_cons_m,
        (array_agg(c.c_tipo_alimento ORDER BY c.c_ts DESC))[1]       AS c_tipo_alimento,
        SUM(c.c_huevo_tot)::int                                      AS c_huevo_tot,
        SUM(c.c_huevo_inc)::int                                      AS c_huevo_inc,
        SUM(c.c_h_limpio)::int                                       AS c_h_limpio,
        SUM(c.c_h_tratado)::int                                      AS c_h_tratado,
        SUM(c.c_h_sucio)::int                                        AS c_h_sucio,
        SUM(c.c_h_deforme)::int                                      AS c_h_deforme,
        SUM(c.c_h_blanco)::int                                       AS c_h_blanco,
        SUM(c.c_h_doble)::int                                        AS c_h_doble,
        SUM(c.c_h_piso)::int                                         AS c_h_piso,
        SUM(c.c_h_pequeno)::int                                      AS c_h_pequeno,
        SUM(c.c_h_roto)::int                                         AS c_h_roto,
        SUM(c.c_h_desecho)::int                                      AS c_h_desecho,
        SUM(c.c_h_otro)::int                                         AS c_h_otro,
        AVG(c.c_peso_huevo)::float8                                  AS c_peso_huevo,
        bool_or(c.c_es_traslado)                                     AS c_es_traslado,
        (array_agg(c.c_tras_dir ORDER BY c.c_ts DESC))[1]            AS c_tras_dir,
        SUM(c.c_tras_in_h)::int                                      AS c_tras_in_h,
        SUM(c.c_tras_in_m)::int                                      AS c_tras_in_m,
        SUM(c.c_tras_out_h)::int                                     AS c_tras_out_h,
        SUM(c.c_tras_out_m)::int                                     AS c_tras_out_m,
        (array_agg(c.c_lote_destino_id ORDER BY c.c_ts DESC))[1]     AS c_lote_destino_id,
        (array_agg(c.c_granja_destino_id ORDER BY c.c_ts DESC))[1]   AS c_granja_destino_id,
        AVG(c.c_peso_h)                                              AS c_peso_h,
        AVG(c.c_peso_m)                                              AS c_peso_m,
        (array_agg(c.c_unif ORDER BY c.c_ts DESC))[1]                AS c_unif,
        (array_agg(c.c_cv ORDER BY c.c_ts DESC))[1]                  AS c_cv,
        (array_agg(c.c_unif_h ORDER BY c.c_ts DESC))[1]              AS c_unif_h,
        (array_agg(c.c_unif_m ORDER BY c.c_ts DESC))[1]              AS c_unif_m,
        (array_agg(c.c_cv_h ORDER BY c.c_ts DESC))[1]                AS c_cv_h,
        (array_agg(c.c_cv_m ORDER BY c.c_ts DESC))[1]                AS c_cv_m,
        (array_agg(c.c_obs_pesaje ORDER BY c.c_ts DESC))[1]          AS c_obs_pesaje,
        SUM(c.c_agua)                                                AS c_agua,
        AVG(c.c_agua_ph)                                             AS c_agua_ph,
        AVG(c.c_agua_orp)                                            AS c_agua_orp,
        AVG(c.c_agua_temp)                                           AS c_agua_temp,
        (array_agg(c.c_etapa ORDER BY c.c_ts DESC))[1]               AS c_etapa,
        (array_agg(c.c_ciclo ORDER BY c.c_ts DESC))[1]               AS c_ciclo,
        (array_agg(c.c_observaciones ORDER BY c.c_ts DESC))[1]       AS c_observaciones,
        (array_agg(c.c_metadata ORDER BY c.c_ts DESC))[1]            AS c_metadata,
        (array_agg(c.c_created_by ORDER BY c.c_ts DESC))[1]          AS c_created_by,
        MIN(c.c_created_at)                                          AS c_created_at,
        MAX(c.c_updated_at)                                          AS c_updated_at,
        MIN(c.c_company_id)                                          AS c_company_id,
        MIN(c.c_lpp)                                                 AS c_lpp,
        (c.c_ts AT TIME ZONE 'America/Bogota')::date                 AS reg_date
      FROM crudos c
     GROUP BY (c.c_ts AT TIME ZONE 'America/Bogota')::date
),
seg_dias AS (
    SELECT * FROM seg_dias_dedup    WHERE NOT (SELECT bool_or(ctx.permite_multiples) FROM ctx)
    UNION ALL
    SELECT * FROM seg_dias_agrupado WHERE     (SELECT bool_or(ctx.permite_multiples) FROM ctx)
),
-- ── Movimientos de aves (solo rama LPP con lote base) — misma población que el GET
--    informacion-lote: Completado, no borrado, misma empresa; salidas = CUALQUIER tipo con
--    el lote como origen; entradas = tipo Traslado con el lote como destino. ──
movs AS (
    SELECT (m.fecha_movimiento AT TIME ZONE 'America/Bogota')::date AS mov_date,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS out_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS out_m,
           CASE WHEN m.tipo_movimiento = 'Traslado' AND m.lote_destino_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS in_h,
           CASE WHEN m.tipo_movimiento = 'Traslado' AND m.lote_destino_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS in_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Venta'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS venta_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Venta'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS venta_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Retiro'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS retiro_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Retiro'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS retiro_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Traslado'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS tout_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Traslado'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS tout_m
      FROM movimiento_aves m
      JOIN ctx c ON c.es_lpp AND c.ctx_lote_id IS NOT NULL
     WHERE m.estado = 'Completado'
       AND m.deleted_at IS NULL
       AND m.company_id = c.ctx_company
       AND (m.lote_origen_id = c.ctx_lote_id OR m.lote_destino_id = c.ctx_lote_id)
       -- Filtro de FASE: los movimientos previos al inicio de producción son del levante y
       -- ya viven en aves_h_inicial (ver changelog v1)
       AND (c.mov_desde IS NULL
            OR (m.fecha_movimiento AT TIME ZONE 'America/Bogota')::date >= c.mov_desde)
),
movs_dia AS (
    SELECT mv.mov_date,
           SUM(mv.out_h)::int    AS out_h,
           SUM(mv.out_m)::int    AS out_m,
           SUM(mv.in_h)::int     AS in_h,
           SUM(mv.in_m)::int     AS in_m,
           SUM(mv.venta_h)::int  AS venta_h,
           SUM(mv.venta_m)::int  AS venta_m,
           SUM(mv.retiro_h)::int AS retiro_h,
           SUM(mv.retiro_m)::int AS retiro_m,
           SUM(mv.tout_h)::int   AS tout_h,
           SUM(mv.tout_m)::int   AS tout_m
      FROM movs mv
     GROUP BY mv.mov_date
),
-- ── Universo: días con seguimiento ∪ días solo-movimiento (FULL JOIN por día) ──
universo AS (
    SELECT COALESCE(s.reg_date, md.mov_date) AS u_fecha,
           s.*,
           md.out_h    AS m_out_h,
           md.out_m    AS m_out_m,
           md.in_h     AS m_in_h,
           md.in_m     AS m_in_m,
           md.venta_h  AS m_venta_h,
           md.venta_m  AS m_venta_m,
           md.retiro_h AS m_retiro_h,
           md.retiro_m AS m_retiro_m,
           md.tout_h   AS m_tout_h,
           md.tout_m   AS m_tout_m
      FROM seg_dias s
      FULL OUTER JOIN movs_dia md ON md.mov_date = s.reg_date
)
SELECT
    u.c_seg_id                                                        AS seg_id,
    u.u_fecha                                                         AS fecha,
    u.c_ts                                                            AS fecha_ts,
    COALESCE(u.c_fuente, 'mov')                                       AS fuente,
    (u.c_seg_id IS NOT NULL AND u.c_lpp IS NULL AND c.es_lpp)         AS fila_sin_lpp,
    c.ctx_lote_id                                                     AS lote_id,
    COALESCE(u.c_lpp, c.ctx_lpp_id)                                   AS lote_postura_produccion_id,
    COALESCE(u.c_company_id, c.ctx_company)                           AS company_id,
    CASE WHEN c.ref_date IS NULL THEN NULL
         ELSE GREATEST(0, u.u_fecha - c.ref_date) END::int            AS edad_dias,
    CASE WHEN c.ref_date IS NULL THEN NULL
         ELSE ((u.u_fecha - c.ref_date) / 7) + 1 END::int             AS semana,
    u.c_mort_h                                                        AS mortalidad_hembras,
    u.c_mort_m                                                        AS mortalidad_machos,
    u.c_sel_h                                                         AS sel_h,
    u.c_sel_m                                                         AS sel_m,
    u.c_err_h                                                         AS error_sexaje_hembras,
    u.c_err_m                                                         AS error_sexaje_machos,
    u.c_cons_h                                                        AS cons_kg_h,
    u.c_cons_m                                                        AS cons_kg_m,
    (COALESCE(u.c_cons_h, 0) + COALESCE(u.c_cons_m, 0))::float8       AS consumo_total_kg,
    u.c_tipo_alimento                                                 AS tipo_alimento,
    u.c_huevo_tot                                                     AS huevo_tot,
    u.c_huevo_inc                                                     AS huevo_inc,
    u.c_h_limpio                                                      AS huevo_limpio,
    u.c_h_tratado                                                     AS huevo_tratado,
    u.c_h_sucio                                                       AS huevo_sucio,
    u.c_h_deforme                                                     AS huevo_deforme,
    u.c_h_blanco                                                      AS huevo_blanco,
    u.c_h_doble                                                       AS huevo_doble_yema,
    u.c_h_piso                                                        AS huevo_piso,
    u.c_h_pequeno                                                     AS huevo_pequeno,
    u.c_h_roto                                                        AS huevo_roto,
    u.c_h_desecho                                                     AS huevo_desecho,
    u.c_h_otro                                                        AS huevo_otro,
    u.c_peso_huevo                                                    AS peso_huevo,
    SUM(COALESCE(u.c_huevo_tot, 0)) OVER w_ord::bigint                AS huevo_tot_acum,
    SUM(COALESCE(u.c_huevo_inc, 0)) OVER w_ord::bigint                AS huevo_inc_acum,
    CASE
        WHEN c.base_h IS NULL THEN NULL
        WHEN GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)) > 0
            THEN (100.0 * COALESCE(u.c_huevo_tot, 0)
                / GREATEST(0, c.base_h
                    - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                    - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                    + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)))
        ELSE 0
    END::float8                                                       AS pct_postura_dia,
    COALESCE(u.m_venta_h, 0)                                          AS mov_venta_h,
    COALESCE(u.m_venta_m, 0)                                          AS mov_venta_m,
    COALESCE(u.m_retiro_h, 0)                                         AS mov_retiro_h,
    COALESCE(u.m_retiro_m, 0)                                         AS mov_retiro_m,
    COALESCE(u.m_in_h, 0)                                             AS mov_traslado_in_h,
    COALESCE(u.m_in_m, 0)                                             AS mov_traslado_in_m,
    COALESCE(u.m_tout_h, 0)                                           AS mov_traslado_out_h,
    COALESCE(u.m_tout_m, 0)                                           AS mov_traslado_out_m,
    CASE WHEN c.base_h IS NULL THEN NULL
         ELSE GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)) END::int AS aves_h_inicio_dia,
    CASE WHEN c.base_m IS NULL THEN NULL
         ELSE GREATEST(0, c.base_m
                - COALESCE(SUM(u.c_mort_m + u.c_sel_m + u.c_err_m) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_m, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_m, 0)) OVER w_prev, 0)) END::int AS aves_m_inicio_dia,
    CASE WHEN c.base_h IS NULL THEN NULL
         ELSE GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_ord, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_ord, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_ord, 0)) END::int  AS saldo_aves_h,
    CASE WHEN c.base_m IS NULL THEN NULL
         ELSE GREATEST(0, c.base_m
                - COALESCE(SUM(u.c_mort_m + u.c_sel_m + u.c_err_m) OVER w_ord, 0)
                - COALESCE(SUM(COALESCE(u.m_out_m, 0)) OVER w_ord, 0)
                + COALESCE(SUM(COALESCE(u.m_in_m, 0)) OVER w_ord, 0)) END::int  AS saldo_aves_m,
    COALESCE(u.c_es_traslado, false)                                  AS es_traslado,
    u.c_tras_dir                                                      AS traslado_direccion,
    u.c_tras_in_h                                                     AS traslado_ingreso_hembras,
    u.c_tras_in_m                                                     AS traslado_ingreso_machos,
    u.c_tras_out_h                                                    AS traslado_salida_hembras,
    u.c_tras_out_m                                                    AS traslado_salida_machos,
    u.c_lote_destino_id                                               AS lote_destino_id,
    u.c_granja_destino_id                                             AS granja_destino_id,
    u.c_peso_h                                                        AS peso_h,
    u.c_peso_m                                                        AS peso_m,
    u.c_unif                                                          AS uniformidad,
    u.c_cv                                                            AS coeficiente_variacion,
    u.c_unif_h                                                        AS uniformidad_hembras,
    u.c_unif_m                                                        AS uniformidad_machos,
    u.c_cv_h                                                          AS cv_hembras,
    u.c_cv_m                                                          AS cv_machos,
    u.c_obs_pesaje                                                    AS observaciones_pesaje,
    u.c_agua                                                          AS consumo_agua_diario,
    u.c_agua_ph                                                       AS consumo_agua_ph,
    u.c_agua_orp                                                      AS consumo_agua_orp,
    u.c_agua_temp                                                     AS consumo_agua_temperatura,
    u.c_etapa                                                         AS etapa,
    u.c_ciclo                                                         AS ciclo,
    u.c_observaciones                                                 AS observaciones,
    u.c_metadata                                                      AS metadata,
    u.c_created_by                                                    AS created_by_user_id,
    u.c_created_at                                                    AS created_at,
    u.c_updated_at                                                    AS updated_at
FROM universo u
CROSS JOIN ctx c
WINDOW
    w_ord  AS (ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0)
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    w_prev AS (ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0)
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0);
$$;
""";
    }
}
