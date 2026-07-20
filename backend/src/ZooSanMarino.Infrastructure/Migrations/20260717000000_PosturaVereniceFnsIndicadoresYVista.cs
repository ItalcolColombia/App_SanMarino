using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Actualiza las funciones/vista de indicadores POSTURA Colombia por los requerimientos
    /// de la lider funcional Verenice (rev 6-jul-26):
    ///   - vw_guia_genetica_por_lote_postura (+ f_safe_numeric): vista Power BI (REQ-005).
    ///   - fn_indicadores_levante_postura: consumo/peso/mortalidad/retiro H/M reales y guia
    ///     separados (REQ-002e, REQ-010b), acumulados sobre aves iniciales (REQ-002f),
    ///     defensas encaset futuro + DROP TEMP TABLE (REQ-002-B36).
    ///   - fn_indicadores_produccion_postura: %Retiro real + guia H/M (REQ-004) y semana 25 (REQ-012b).
    /// Ambas fn cambian la firma RETURNS TABLE (columnas nuevas) => se usa DROP FUNCTION + CREATE
    /// (Postgres no permite CREATE OR REPLACE al cambiar el tipo de retorno). Idempotente.
    /// Migracion hecha a mano (no altera el ModelSnapshot). Fuente/spec: backend/sql/*.sql.
    /// </summary>
    public partial class PosturaVereniceFnsIndicadoresYVista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // REQ-005 — Vista Power BI (+ helper f_safe_numeric). CREATE OR REPLACE (idempotente).
            migrationBuilder.Sql(@"-- ============================================================================
-- Vista para Power BI (REQ-005) — Comparativo Real vs Guía Genética por lote+semana
-- ----------------------------------------------------------------------------
-- Expone, para cada lote de postura (levante y producción) de San Marino Colombia,
-- la fila de guía genética (guia_genetica_sanmarino_colombia) que le corresponde por
-- Raza + Año genético + Edad(semana). Power BI cruza esto contra los datos reales
-- (seguimiento diario) por lote + semana para el comparativo por línea de producción.
--
-- Idempotente. No cambia schema. Semana = edad (semanas de vida).
-- La guía se almacena como texto (puede traer '', '-', o coma decimal) → se castea seguro.
--
-- DEPLOY (REQ-005): este .sql es la SPEC. NO se aplica corriéndolo a mano en prod.
-- Se aplica envolviendo su contenido tal cual en `migrationBuilder.Sql(@""..."")`
-- dentro de una migración EF nueva (mismo patrón que
-- 20260703120000_AddFnIndicadoresProduccionPostura.cs: Up() = CREATE OR REPLACE
-- FUNCTION + CREATE OR REPLACE VIEW; Down() = DROP VIEW/FUNCTION IF EXISTS). La
-- migración la crea el coordinador; este archivo queda como fuente de verdad para
-- copiar/pegar dentro de Up().
-- ============================================================================

-- Cast seguro a numeric: tolera vacío, '-', y coma decimal.
CREATE OR REPLACE FUNCTION public.f_safe_numeric(v text) RETURNS numeric AS $$
  SELECT CASE
           WHEN replace(trim(coalesce(v, '')), ',', '.') ~ '^-?[0-9]+(\.[0-9]+)?$'
           THEN replace(trim(v), ',', '.')::numeric
           ELSE NULL
         END;
$$ LANGUAGE sql IMMUTABLE;

CREATE OR REPLACE VIEW public.vw_guia_genetica_por_lote_postura AS
WITH guia AS (
    SELECT
        g.company_id,
        g.raza,
        g.anio_guia,
        NULLIF(regexp_replace(g.edad, '[^0-9]', '', 'g'), '')::int AS semana,
        public.f_safe_numeric(g.gr_ave_dia_h)   AS gr_ave_dia_h,
        public.f_safe_numeric(g.gr_ave_dia_m)   AS gr_ave_dia_m,
        public.f_safe_numeric(g.cons_ac_h)      AS cons_ac_h,
        public.f_safe_numeric(g.cons_ac_m)      AS cons_ac_m,
        public.f_safe_numeric(g.peso_h)         AS peso_h,
        public.f_safe_numeric(g.peso_m)         AS peso_m,
        public.f_safe_numeric(g.uniformidad)    AS uniformidad,
        public.f_safe_numeric(g.mort_sem_h)     AS mort_sem_h,
        public.f_safe_numeric(g.mort_sem_m)     AS mort_sem_m,
        public.f_safe_numeric(g.retiro_ac_h)    AS retiro_ac_h,
        public.f_safe_numeric(g.retiro_ac_m)    AS retiro_ac_m,
        public.f_safe_numeric(g.h_total_aa)     AS h_total_aa,
        public.f_safe_numeric(g.h_inc_aa)       AS h_inc_aa,
        public.f_safe_numeric(g.prod_porcentaje) AS prod_porcentaje,
        public.f_safe_numeric(g.aprov_sem)      AS aprov_sem,
        public.f_safe_numeric(g.peso_huevo)     AS peso_huevo,
        public.f_safe_numeric(g.apareo)         AS apareo
    FROM public.guia_genetica_sanmarino_colombia g
    WHERE g.deleted_at IS NULL
      AND g.edad ~ '^[0-9]'
)
SELECT
    'Levante'::text                    AS etapa,
    l.lote_postura_levante_id          AS lote_postura_id,
    l.lote_id                          AS lote_id,
    l.lote_nombre                      AS lote_nombre,
    l.company_id                       AS company_id,
    l.regional                         AS regional,
    l.raza                             AS raza,
    l.ano_tabla_genetica               AS ano_tabla_genetica,
    gu.semana                          AS semana,
    gu.gr_ave_dia_h, gu.gr_ave_dia_m, gu.cons_ac_h, gu.cons_ac_m,
    gu.peso_h, gu.peso_m, gu.uniformidad, gu.mort_sem_h, gu.mort_sem_m,
    gu.retiro_ac_h, gu.retiro_ac_m,
    NULL::numeric AS h_total_aa, NULL::numeric AS h_inc_aa, NULL::numeric AS prod_porcentaje,
    NULL::numeric AS aprov_sem, NULL::numeric AS peso_huevo, NULL::numeric AS apareo
FROM public.lote_postura_levante l
JOIN guia gu
  ON gu.company_id = l.company_id
 AND gu.raza = l.raza
 AND gu.anio_guia = l.ano_tabla_genetica::text
 AND gu.semana BETWEEN 1 AND 25
WHERE l.deleted_at IS NULL

UNION ALL

SELECT
    'Produccion'::text                 AS etapa,
    p.lote_postura_produccion_id       AS lote_postura_id,
    p.lote_id                          AS lote_id,
    p.lote_nombre                      AS lote_nombre,
    p.company_id                       AS company_id,
    p.regional                         AS regional,
    p.raza                             AS raza,
    p.ano_tabla_genetica               AS ano_tabla_genetica,
    gu.semana                          AS semana,
    gu.gr_ave_dia_h, gu.gr_ave_dia_m, gu.cons_ac_h, gu.cons_ac_m,
    gu.peso_h, gu.peso_m, gu.uniformidad, gu.mort_sem_h, gu.mort_sem_m,
    gu.retiro_ac_h, gu.retiro_ac_m,
    gu.h_total_aa, gu.h_inc_aa, gu.prod_porcentaje, gu.aprov_sem, gu.peso_huevo, gu.apareo
FROM public.lote_postura_produccion p
JOIN guia gu
  ON gu.company_id = p.company_id
 AND gu.raza = p.raza
 AND gu.anio_guia = p.ano_tabla_genetica::text
 AND gu.semana >= 26
WHERE p.deleted_at IS NULL;
");

            // REQ-002e/002f/002-B36/010b — Indicadores levante (firma nueva: DROP + CREATE).
            migrationBuilder.Sql(@"-- ============================================================================
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
--     semana 25; ya no se clampean con LEAST(25) generando una ""semana 25""
--     falsa con el salto de saldo del traslado post-levante.
--   * REQ-002B36 — Defensas:
--       - Base de aves con fallback: COALESCE(aves_encasetadas,
--         hembras_l+machos_l, primer traslado_ingreso, 0).
--       - Encaset futuro/ausente: si fecha_encaset es NULL o es POSTERIOR al
--         primer registro (encaset tecleado a futuro, p. ej. lote 116), se
--         devuelven CERO filas en lugar de colapsar 140+ días en una
--         ""semana 1"" absurda con base 0 y %pérdidas 100%. Se eligió devolver
--         cero filas (y no ""usar el primer registro como referencia"") porque
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
    retiro_pct_machos               numeric             -- % retiro machos  = (mort+sel+err)_m / aves_inicio_m * 100
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

    -- Primer traslado_ingreso del lote (fallback de base cuando el lote se
    -- pobló por traslado y no trae aves_encasetadas / hembras_l / machos_l).
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
        mort_h, mort_m, sel_h, sel_m, err_h, err_m,
        cons_kg_h_num::double precision           AS cons_kg_h,
        cons_kg_m_num::double precision           AS cons_kg_m,
        tras_sal_h, tras_sal_m, tras_ing_h, tras_ing_m,
        ph, pm, uh, um, id
      FROM base
     WHERE NOT (
            real_sem > 25
        AND mort_h = 0 AND mort_m = 0 AND sel_h = 0 AND sel_m = 0
        AND err_h = 0 AND err_m = 0
        AND cons_kg_h_num = 0 AND cons_kg_m_num = 0
        AND ph = 0 AND pm = 0
        AND (tras_sal_h + tras_sal_m + tras_ing_h + tras_ing_m) > 0
     );

    SELECT MAX(sem) INTO v_max_sem FROM _seg_sem;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    FOR s IN 1..v_max_sem LOOP
        -- ¿la semana tiene registros? (el front solo itera semanas presentes)
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg_sem WHERE sem = s);

        SELECT COALESCE(SUM(mort),0), COALESCE(SUM(sel),0), COALESCE(SUM(cons_kg),0),
               COALESCE(SUM(err),0), COALESCE(SUM(tras_sal),0), COALESCE(SUM(tras_ing),0), COUNT(*)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0),
               COALESCE(SUM(sel_h),0),  COALESCE(SUM(sel_m),0),
               COALESCE(SUM(err_h),0),  COALESCE(SUM(err_m),0),
               COALESCE(SUM(cons_kg_h),0), COALESCE(SUM(cons_kg_m),0),
               COALESCE(SUM(tras_sal_h),0), COALESCE(SUM(tras_sal_m),0),
               COALESCE(SUM(tras_ing_h),0), COALESCE(SUM(tras_ing_m),0)
          INTO r_mort_tot, r_sel_tot, r_cons_kg, r_err_tot, r_tras_sal, r_tras_ing, r_dias,
               r_mort_h, r_mort_m, r_sel_h, r_sel_m, r_err_h, r_err_m,
               r_cons_kg_h, r_cons_kg_m, r_tras_sal_h, r_tras_sal_m, r_tras_ing_h, r_tras_ing_m
          FROM _seg_sem WHERE sem = s;

        -- Saldo físico Feature-13: salidas = mort + sel + err + traslado_salida - traslado_ingreso.
        r_aves_fin := v_aves_acum - r_mort_tot - r_sel_tot - r_err_tot - r_tras_sal + r_tras_ing;
        -- Saldo por género (REQ-002e).
        r_aves_fin_h := v_aves_acum_h - r_mort_h - r_sel_h - r_err_h - r_tras_sal_h + r_tras_ing_h;
        r_aves_fin_m := v_aves_acum_m - r_mort_m - r_sel_m - r_err_m - r_tras_sal_m + r_tras_ing_m;

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

        -- Guía real (Colombia) para la semana. Mixto (compat) + por sexo SIN promediar (REQ-002e).
        SELECT (COALESCE(NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,0)
              + COALESCE(NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,0))/2,
               (COALESCE(NULLIF(btrim(g.peso_h),'')::double precision,0)
              + COALESCE(NULLIF(btrim(g.peso_m),'')::double precision,0))/2,
               COALESCE(NULLIF(btrim(g.uniformidad),'')::double precision,0),
               (COALESCE(NULLIF(btrim(g.mort_sem_h),'')::double precision,0)
              + COALESCE(NULLIF(btrim(g.mort_sem_m),'')::double precision,0))/2,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision
          INTO r_cons_tabla, r_peso_tabla, r_unif_tabla, r_mort_tabla, r_cons_tabla_h, r_cons_tabla_m,
               r_peso_tabla_h, r_peso_tabla_m, r_mort_tabla_h, r_mort_tabla_m
          FROM guia_genetica_sanmarino_colombia g
         WHERE g.raza = v_raza AND g.anio_guia = v_anio AND g.company_id = v_company
           AND btrim(g.edad) = s::text
         LIMIT 1;
        r_cons_tabla := COALESCE(r_cons_tabla,0);
        r_peso_tabla := COALESCE(r_peso_tabla,0);
        r_unif_tabla := COALESCE(r_unif_tabla,0);
        r_mort_tabla := COALESCE(r_mort_tabla,0);
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
");

            // REQ-004/012b — Indicadores produccion (firma nueva: DROP + CREATE).
            migrationBuilder.Sql(@"-- ============================================================================
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
--   REQ-004e  (Verenice rev 6-jul-26) La tabla ""% Retiro (Real vs Guía)"" del front mostraba el
--             REAL pero la GUÍA quedaba vacía: la fn calculaba retiro_ac_h/m REAL pero nunca
--             exponía la guía. Se agregan retiro_ac_h_guia/retiro_ac_m_guia leyendo
--             guia_genetica_sanmarino_colombia.retiro_ac_h/retiro_ac_m (mismo parseo NULLIF/btrim
--             que las demás columnas guía; NULL si no hay guía para la semana).
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

-- REQ-004 cambia la firma de RETURNS TABLE (agrega retiro_sem_h/m + retiro_ac_h/m). Postgres NO
-- permite CREATE OR REPLACE cuando cambia el row type de los parámetros OUT → DROP idempotente antes.
DROP FUNCTION IF EXISTS fn_indicadores_produccion_postura(integer, integer, integer, integer, integer, date, date);

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
    hiaa_real                           double precision,
    -- REQ-004: %Retiro REAL por sexo (mortalidad + selección). Semanal sobre saldo de inicio del
    --   sexo; acumulado sobre aves iniciales del sexo. Aritmética == ProduccionCalculos.PorcentajeRetiro*.
    retiro_sem_h                        double precision,
    retiro_sem_m                        double precision,
    retiro_ac_h                         double precision,
    retiro_ac_m                         double precision,
    -- REQ-004 (Verenice rev 6-jul-26): %Retiro acumulado de GUÍA por sexo, desde
    --   guia_genetica_sanmarino_colombia.retiro_ac_h/retiro_ac_m (texto, mismo parseo que las
    --   demás columnas guía: NULLIF(btrim(...),'')::double precision). NULL si no hay guía para
    --   esa semana (g_found=false); si hay guía pero el campo viene vacío, 0 (mismo criterio que
    --   g_mort_h/g_mort_m, no el de huevos/%prod que preservan NULL).
    retiro_ac_h_guia                    double precision,
    retiro_ac_m_guia                    double precision
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
    -- REQ-004: acumulados de retiro por sexo (mortalidad + selección)
    v_cum_mort_h     bigint := 0;
    v_cum_sel_h      bigint := 0;
    v_cum_mort_m     bigint := 0;

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
    -- REQ-004: %Retiro real por semana
    r_retiro_sem_h   double precision;
    r_retiro_sem_m   double precision;
    r_retiro_ac_h    double precision;
    r_retiro_ac_m    double precision;
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
    -- REQ-004 (Verenice): %Retiro acumulado de guía por sexo.
    g_retiro_ac_h    double precision;
    g_retiro_ac_m    double precision;
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
    -- REQ-012b: producción arranca en la semana 25 de vida (antes 26). La guía genética empieza en
    --   la semana 26, así que la 25 queda con columnas de guía en NULL (g_found=false ya lo soporta).
    DELETE FROM _seg WHERE sem_vida < 25;

    SELECT MAX(sem_vida) INTO v_max_sem FROM _seg;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 3) Iterar semanas presentes en orden (== foreach sobre grupos ordenados).
    --    OJO: itera SOLO las semanas con registros (>=25 tras REQ-012b) y en orden asc.
    --    Los acumuladores (aves actuales, htaa/hiaa, retiro) avanzan solo en esas semanas.
    -- ════════════════════════════════════════════════════════════════════
    v_aves_h_act := v_aves_h_ini;
    v_aves_m_act := v_aves_m_ini;

    FOR s IN 25..v_max_sem LOOP  -- REQ-012b: incluir semana 25 (antes 26)
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

        -- REQ-004: acumulados de retiro (mortalidad + selección) por sexo. Machos sin selección en
        --   esta fn (igual que el decremento de aves, que solo resta mort_m).
        v_cum_mort_h := v_cum_mort_h + r_mort_h;
        v_cum_sel_h  := v_cum_sel_h + r_sel_h;
        v_cum_mort_m := v_cum_mort_m + r_mort_m;
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

        -- REQ-004: %Retiro REAL (== ProduccionCalculos.PorcentajeRetiroSemanal/Acumulado).
        --   Semanal: (mort + sel de la semana) / saldo REAL de inicio del sexo (v_aves_*_act, pre-decremento) * 100.
        --   Acumulado: (mort + sel acumulados) / aves iniciales del sexo * 100.
        r_retiro_sem_h := CASE WHEN v_aves_h_act > 0 THEN (r_mort_h + r_sel_h)::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_retiro_sem_m := CASE WHEN v_aves_m_act > 0 THEN r_mort_m::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_retiro_ac_h  := CASE WHEN v_aves_h_ini > 0 THEN (v_cum_mort_h + v_cum_sel_h)::double precision / v_aves_h_ini * 100 ELSE 0 END;
        r_retiro_ac_m  := CASE WHEN v_aves_m_ini > 0 THEN v_cum_mort_m::double precision / v_aves_m_ini * 100 ELSE 0 END;

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
               NULLIF(btrim(g.peso_huevo),'')::double precision,
               NULLIF(btrim(g.retiro_ac_h),'')::double precision,
               NULLIF(btrim(g.retiro_ac_m),'')::double precision
          INTO g_found, g_cons_h, g_cons_m, g_mort_h, g_mort_m, g_peso_h, g_peso_m, g_unif,
               g_huevos_tot, g_huevos_inc, g_prod_pct, g_peso_huevo, g_retiro_ac_h, g_retiro_ac_m
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
            -- retiro_ac_h/m guía: mismo criterio que mort_h/mort_m (ParseDouble => 0 si vacío).
            g_retiro_ac_h := COALESCE(g_retiro_ac_h, 0);
            g_retiro_ac_m := COALESCE(g_retiro_ac_m, 0);
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
$fn$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP VIEW IF EXISTS public.vw_guia_genetica_por_lote_postura;
DROP FUNCTION IF EXISTS public.f_safe_numeric(text);
DROP FUNCTION IF EXISTS fn_indicadores_levante_postura(integer);
DROP FUNCTION IF EXISTS fn_indicadores_produccion_postura(integer, integer, integer, integer, integer, date, date);
");
        }
    }
}
