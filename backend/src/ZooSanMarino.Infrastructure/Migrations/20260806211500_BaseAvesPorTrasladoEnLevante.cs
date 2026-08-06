using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrige la resolución de la BASE DE AVES de los lotes de levante que se poblaron por
    /// TRASLADO en vez de por encaset — un caso legítimo del negocio: hay lotes que reciben aves
    /// de otros lotes y nunca tuvieron aves encasetadas propias.
    ///
    /// Las tres funciones de levante resolvían esa base con el MISMO fallback defectuoso:
    /// leían la fila de traslado más antigua con `LIMIT 1` y sacaban de ahí LOS DOS SEXOS.
    /// De ahí salían dos defectos independientes, los dos reproducidos contra datos reales:
    ///
    ///  1. SALDO NEGATIVO. Si los sexos llegaron en traslados de días distintos, el sexo que no
    ///     estaba en la fila más antigua quedaba con base 0 y el reporte le restaba igual la
    ///     mortalidad. Caso real: un lote recibió 1.010 machos el 08-jun y 7.617 hembras el
    ///     11-jun; la base de hembras quedó en 0 y el saldo se reportaba en -212 durante 14
    ///     semanas, mientras el maestro decía 7.405.
    ///
    ///  2. SALDO INFLADO AL DOBLE. La fila usada como base también la suma la acumulación como
    ///     ingreso, salvo que la ventana de 25 semanas la descarte. Cuando el traslado caía
    ///     dentro de la ventana, las aves contaban DOS veces: un lote de 5.100 hembras
    ///     reportaba 10.200. Afectaba a 3 lotes.
    ///
    /// El arreglo, idéntico en las tres: la base por traslado pasa a ser la SUMA POR SEXO de los
    /// ingresos de las filas que la ventana DESCARTA (puro traslado más allá de la semana 25),
    /// que son justamente las aves que no suma nadie. Las que la ventana sí procesa se dejan a
    /// la acumulación, así no se cuentan dos veces.
    ///
    /// Sigue siendo un COALESCE, NO una suma: un lote CON encaset conserva exactamente su número
    /// de siempre y el fallback solo entra cuando el encaset es 0/NULL. Por eso el cambio es
    /// cerrado por construcción sobre los lotes rotos.
    ///
    /// GATE MULTIPAÍS (BD local, versiones previas desplegadas en paralelo con sufijo _V0 y
    /// comparadas fila a fila): las tres funciones cambian ÚNICAMENTE en los mismos 4 lotes
    /// (116 A374A de Agroavicola Sanmarino; 124, 128 y 129 de Demo) y en las 3 el saldo final
    /// pasa a coincidir EXACTO con lote_postura_levante.aves_h_actual / aves_m_actual, que es el
    /// testigo independiente. Ningún otro lote de ninguna empresa se mueve: 0 filas de diferencia
    /// fuera de esos 4.
    ///
    /// Espejos .sql: backend/sql/fn_resumen_semanal_ra_pesadas_levante.sql,
    /// fn_reporte_semanal_levante_extras.sql, fn_indicadores_levante_postura.sql (idénticos).
    /// Data-only (Designer clonado, ModelSnapshot intacto). Idempotente
    /// (DROP FUNCTION IF EXISTS + CREATE OR REPLACE).
    /// </summary>
    public partial class BaseAvesPorTrasladoEnLevante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── fn_resumen_semanal_ra_pesadas_levante ─────────────────────────────────────────────
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS fn_resumen_semanal_ra_pesadas_levante(integer, integer, integer, integer[], text, boolean);

CREATE OR REPLACE FUNCTION fn_resumen_semanal_ra_pesadas_levante(
    p_company_id           integer,
    p_anio                 integer,
    p_sem_anio             integer,   -- NULL = todas las semanas del año
    p_granja_ids           integer[] DEFAULT NULL,
    p_regional             text      DEFAULT NULL,
    p_excluir_trasladados  boolean   DEFAULT false
)
RETURNS TABLE(
    lote_id                   integer,
    lote_nombre               text,
    granja_id                 integer,
    granja_nombre             text,
    nucleo_nombre             text,
    regional                  text,
    raza                      text,
    anio_guia                 integer,
    edad_semana               integer,
    fecha_fin_semana          date,
    dias_con_registro         integer,
    tuvo_traslado             boolean,
    part                      double precision,
    saldo_hembras             double precision,
    saldo_machos              double precision,
    mort_hembras_pct          double precision,
    retiro_acum_hembras_pct   double precision,
    retiro_acum_hembras_guia  double precision,
    dif_consumo_hembras_pct   double precision,
    dif_peso_hembras_pct      double precision,
    uniformidad_hembras       double precision,
    cv_hembras                double precision,
    mort_machos_pct           double precision,
    retiro_acum_machos_pct    double precision,
    retiro_acum_machos_guia   double precision,
    dif_consumo_machos_pct    double precision,
    dif_peso_machos_pct       double precision,
    uniformidad_machos        double precision,
    cv_machos                 double precision
)
LANGUAGE sql STABLE AS $$
WITH
-- ── 1) Lotes candidatos de la empresa (+ ubicación y datos de guía) ──────────
lote_base AS (
    SELECT l.lote_id,
           l.lote_nombre::text                                        AS lote_nombre,
           l.granja_id,
           f.name::text                                               AS granja_nombre,
           n.nucleo_nombre::text                                      AS nucleo_nombre,
           COALESCE(NULLIF(mo.value, ''), NULLIF(l.regional, ''))::text AS regional,
           l.raza::text                                               AS raza,
           l.ano_tabla_genetica                                       AS anio_guia,
           (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date       AS enc_date
      FROM lotes l
      JOIN farms f
        ON f.id = l.granja_id
      LEFT JOIN nucleos n
        ON n.granja_id = l.granja_id
       AND n.nucleo_id = l.nucleo_id
       AND n.deleted_at IS NULL
      LEFT JOIN master_list_options mo
        ON mo.id = f.regional_id
     WHERE l.company_id = p_company_id
       AND l.deleted_at IS NULL
       AND (p_granja_ids IS NULL OR l.granja_id = ANY (p_granja_ids))
),
-- ── 2) Registros diarios de levante, con la semana de edad ya resuelta ───────
--    Mismo WHERE, mismo COALESCE y misma exclusión de «puro traslado > 25»
--    que fn_reporte_semanal_levante_extras.
reg AS (
    SELECT lb.lote_id,
           (floor(((sl.fecha AT TIME ZONE 'America/Bogota')::date - lb.enc_date) / 7.0)::int) + 1 AS real_sem,
           (sl.fecha AT TIME ZONE 'America/Bogota')::date AS reg_date,
           COALESCE(sl.mortalidad_hembras, 0)      AS mort_h,
           COALESCE(sl.mortalidad_machos, 0)       AS mort_m,
           COALESCE(sl.sel_h, 0)                   AS sel_h,
           COALESCE(sl.sel_m, 0)                   AS sel_m,
           COALESCE(sl.error_sexaje_hembras, 0)    AS err_h,
           COALESCE(sl.error_sexaje_machos, 0)     AS err_m,
           COALESCE(sl.consumo_kg_hembras, 0)::double precision AS cons_kg_h,
           COALESCE(sl.consumo_kg_machos, 0)::double precision  AS cons_kg_m,
           COALESCE(sl.traslado_salida_hembras, 0) AS tras_sal_h,
           COALESCE(sl.traslado_salida_machos, 0)  AS tras_sal_m,
           COALESCE(sl.traslado_ingreso_hembras, 0) AS tras_ing_h,
           COALESCE(sl.traslado_ingreso_machos, 0)  AS tras_ing_m,
           COALESCE(sl.peso_prom_hembras, 0)::double precision AS ph,
           COALESCE(sl.peso_prom_machos, 0)::double precision  AS pm,
           sl.uniformidad_hembras::double precision AS uh,
           sl.uniformidad_machos::double precision  AS um,
           sl.cv_hembras::double precision          AS cvh,
           sl.cv_machos::double precision           AS cvm,
           sl.id
      FROM lote_base lb
      JOIN seguimiento_diario_levante sl
        ON sl.lote_id = lb.lote_id::text
       AND sl.tipo_seguimiento = 'levante'
),
-- ── 3) Guards por lote: primer registro y validez del encaset ────────────────
lote_ok AS (
    SELECT lb.*,
           g.min_reg,
           -- base por sexo con el mismo fallback del Detalle. Sigue siendo COALESCE, no suma:
           -- un lote CON encaset conserva exactamente su número de siempre. El fallback solo
           -- entra cuando el encaset es 0/NULL, que es el lote poblado únicamente por traslado.
           COALESCE(
               NULLIF(l.hembras_l, 0)::double precision,
               NULLIF(fi.ing_desc_h, 0),
               0)                                    AS base_h,
           COALESCE(
               NULLIF(l.machos_l, 0)::double precision,
               NULLIF(fi.ing_desc_m, 0),
               0)                                    AS base_m,
           COALESCE(tr.tuvo_traslado, false)         AS tuvo_traslado
      FROM lote_base lb
      JOIN lotes l
        ON l.lote_id = lb.lote_id
      JOIN LATERAL (
            SELECT MIN(r.reg_date) AS min_reg
              FROM reg r
             WHERE r.lote_id = lb.lote_id
      ) g ON true
      -- Aves que entraron por traslado en filas que reg_ok DESCARTA (puro traslado > sem 25).
      -- Esas aves no las suma nadie: la ventana las tira, así que si el lote no trae encaset
      -- quedan fuera del saldo. Se rescatan acá como base.
      --
      -- ⚠️ El predicado tiene que ser el MISMO que el de reg_ok (más abajo). Si cambia uno,
      --    cambia el otro: si acá entrara una fila que reg_ok SÍ cuenta, sus aves se sumarían
      --    dos veces (una como base y otra como ingreso) y el saldo saldría inflado.
      -- SUM por sexo, no una sola fila: los sexos pueden llegar en traslados de DÍAS DISTINTOS.
      -- Con `LIMIT 1` se leían los dos sexos de la fila más antigua, así que el sexo que no
      -- venía en esa fila quedaba con base 0 y el reporte lo mostraba NEGATIVO tras restarle
      -- la mortalidad (caso real: machos el 08-jun y hembras el 11-jun ⇒ hembras en -212).
      LEFT JOIN LATERAL (
            SELECT COALESCE(SUM(r.tras_ing_h), 0)::double precision AS ing_desc_h,
                   COALESCE(SUM(r.tras_ing_m), 0)::double precision AS ing_desc_m
              FROM reg r
             WHERE r.lote_id = lb.lote_id
               AND r.real_sem > 25
               AND r.mort_h = 0 AND r.mort_m = 0
               AND r.sel_h = 0  AND r.sel_m = 0
               AND r.err_h = 0  AND r.err_m = 0
               AND r.cons_kg_h = 0 AND r.cons_kg_m = 0
               AND r.ph = 0 AND r.pm = 0
               AND (r.tras_sal_h + r.tras_sal_m + r.tras_ing_h + r.tras_ing_m) > 0
      ) fi ON true
      LEFT JOIN LATERAL (
            SELECT true AS tuvo_traslado
              FROM reg r
             WHERE r.lote_id = lb.lote_id
               AND (r.tras_ing_h + r.tras_ing_m + r.tras_sal_h + r.tras_sal_m) > 0
             LIMIT 1
      ) tr ON true
     WHERE g.min_reg IS NOT NULL
       AND lb.enc_date IS NOT NULL
       AND lb.enc_date <= g.min_reg
),
-- ── 4) Registros válidos (topados a 25, sin filas de puro traslado > 25) ─────
reg_ok AS (
    SELECT r.*,
           LEAST(25, r.real_sem) AS sem
      FROM reg r
      JOIN lote_ok lo ON lo.lote_id = r.lote_id
     WHERE NOT (
               r.real_sem > 25
           AND r.mort_h = 0 AND r.mort_m = 0
           AND r.sel_h = 0  AND r.sel_m = 0
           AND r.err_h = 0  AND r.err_m = 0
           AND r.cons_kg_h = 0 AND r.cons_kg_m = 0
           AND r.ph = 0 AND r.pm = 0
           AND (r.tras_sal_h + r.tras_sal_m + r.tras_ing_h + r.tras_ing_m) > 0
       )
),
-- ── 5) Agregado semanal por lote ────────────────────────────────────────────
sem AS (
    SELECT lote_id,
           sem,
           COUNT(*)::int                       AS dias,
           SUM(mort_h)::double precision       AS mort_h,
           SUM(mort_m)::double precision       AS mort_m,
           SUM(sel_h)::double precision        AS sel_h,
           SUM(sel_m)::double precision        AS sel_m,
           SUM(err_h)::double precision        AS err_h,
           SUM(err_m)::double precision        AS err_m,
           SUM(tras_sal_h)::double precision   AS tras_sal_h,
           SUM(tras_sal_m)::double precision   AS tras_sal_m,
           SUM(tras_ing_h)::double precision   AS tras_ing_h,
           SUM(tras_ing_m)::double precision   AS tras_ing_m,
           SUM(cons_kg_h)                      AS cons_kg_h,
           SUM(cons_kg_m)                      AS cons_kg_m
      FROM reg_ok
     GROUP BY lote_id, sem
),
-- ── 6) Fila de pesaje de la semana (misma regla de selección del Detalle) ────
pesaje AS (
    SELECT s.lote_id,
           s.sem,
           p.ph, p.pm, p.uh, p.um, p.cvh, p.cvm
      FROM sem s
      LEFT JOIN LATERAL (
            SELECT r.ph, r.pm, r.uh, r.um, r.cvh, r.cvm
              FROM reg_ok r
             WHERE r.lote_id = s.lote_id
               AND r.sem = s.sem
             ORDER BY (CASE WHEN r.ph > 0 OR r.pm > 0 THEN 0 ELSE 1 END),
                      r.reg_date DESC, r.id DESC
             LIMIT 1
      ) p ON true
),
-- ── 7) Acumulados por ventana + arrastre (LOCF) del peso por sexo ───────────
--    El ""grupo"" de LOCF es el conteo de pesajes no nulos hasta la semana:
--    dentro de cada grupo, el primer valor es el último peso conocido.
acum AS (
    SELECT s.lote_id,
           s.sem,
           s.dias,
           s.mort_h, s.mort_m, s.sel_h, s.sel_m, s.err_h, s.err_m,
           s.tras_sal_h, s.tras_sal_m, s.tras_ing_h, s.tras_ing_m,
           s.cons_kg_h, s.cons_kg_m,
           NULLIF(p.ph, 0) AS peso_h_raw,
           NULLIF(p.pm, 0) AS peso_m_raw,
           NULLIF(COALESCE(p.uh, 0), 0)  AS unif_h,
           NULLIF(COALESCE(p.um, 0), 0)  AS unif_m,
           NULLIF(COALESCE(p.cvh, 0), 0) AS cv_h,
           NULLIF(COALESCE(p.cvm, 0), 0) AS cv_m,
           -- salidas netas acumuladas hasta ESTA semana (inclusive)
           SUM(s.mort_h + s.sel_h + s.err_h + s.tras_sal_h - s.tras_ing_h)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS neto_out_h,
           SUM(s.mort_m + s.sel_m + s.err_m + s.tras_sal_m - s.tras_ing_m)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS neto_out_m,
           -- retiro acumulado (mort + sel + err), SIN traslados: es lo que el
           -- Excel llama RetAcH/RetAcM y va sobre base FIJA
           SUM(s.mort_h + s.sel_h + s.err_h)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS retiro_ac_h,
           SUM(s.mort_m + s.sel_m + s.err_m)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS retiro_ac_m,
           COUNT(NULLIF(p.ph, 0))
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS grp_h,
           COUNT(NULLIF(p.pm, 0))
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS grp_m
      FROM sem s
      JOIN pesaje p
        ON p.lote_id = s.lote_id AND p.sem = s.sem
),
locf AS (
    SELECT a.*,
           FIRST_VALUE(a.peso_h_raw) OVER (
               PARTITION BY a.lote_id, a.grp_h ORDER BY a.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS peso_h,
           FIRST_VALUE(a.peso_m_raw) OVER (
               PARTITION BY a.lote_id, a.grp_m ORDER BY a.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS peso_m
      FROM acum a
),
-- ── 8) Solo la semana calendario pedida (WEEKNUM estilo Excel) ──────────────
sem_objetivo AS (
    SELECT lo.lote_id, lo.lote_nombre, lo.granja_id, lo.granja_nombre,
           lo.nucleo_nombre, lo.regional, lo.raza, lo.anio_guia,
           lo.base_h, lo.base_m, lo.tuvo_traslado,
           x.sem, x.dias,
           x.mort_h, x.mort_m, x.sel_h, x.sel_m, x.err_h, x.err_m,
           x.tras_sal_h, x.tras_sal_m, x.tras_ing_h, x.tras_ing_m,
           x.cons_kg_h, x.cons_kg_m,
           x.unif_h, x.unif_m, x.cv_h, x.cv_m,
           x.neto_out_h, x.neto_out_m, x.retiro_ac_h, x.retiro_ac_m,
           x.peso_h, x.peso_m,
           (lo.enc_date + ((x.sem - 1) * 7) + 6) AS fin_sem,
           -- Semana CALENDARIO (WEEKNUM estilo Excel) del cierre de la semana de edad.
           -- Se materializa acá porque la usan DOS cosas: el filtro de abajo y la
           -- partición de `part`. OJO: NO es lo mismo que fin_sem — fin_sem depende del
           -- encaset de CADA lote, así que dos sublotes del mismo lote padre con fechas
           -- de llegada distintas caen en la misma semana calendario con fin_sem DISTINTO.
           floor(
             ( (lo.enc_date + ((x.sem - 1) * 7) + 6)
               - date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp)::date
               + EXTRACT(DOW FROM date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp))::int
             ) / 7.0
           )::int + 1                            AS sem_cal
      FROM locf x
      JOIN lote_ok lo ON lo.lote_id = x.lote_id
     WHERE EXTRACT(YEAR FROM (lo.enc_date + ((x.sem - 1) * 7) + 6))::int = p_anio
       -- p_sem_anio NULL = TODAS las semanas del año (curva del año completo);
       -- con valor, una sola semana calendario.
       AND (p_sem_anio IS NULL OR (
             floor(
               ( (lo.enc_date + ((x.sem - 1) * 7) + 6)
                 - date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp)::date
                 + EXTRACT(DOW FROM date_trunc('year', (lo.enc_date + ((x.sem - 1) * 7) + 6)::timestamp))::int
               ) / 7.0
             )::int + 1
           ) = p_sem_anio)
       AND (p_regional IS NULL OR lo.regional = p_regional)
       AND (NOT p_excluir_trasladados OR NOT lo.tuvo_traslado)
),
-- ── 9) Guía del lote para esa edad ──────────────────────────────────────────
con_guia AS (
    SELECT so.*,
           f_safe_numeric(g.retiro_ac_h)  AS g_retiro_ac_h,
           f_safe_numeric(g.retiro_ac_m)  AS g_retiro_ac_m,
           f_safe_numeric(g.gr_ave_dia_h) AS g_gr_ave_dia_h,
           f_safe_numeric(g.gr_ave_dia_m) AS g_gr_ave_dia_m,
           f_safe_numeric(g.peso_h)       AS g_peso_h,
           f_safe_numeric(g.peso_m)       AS g_peso_m
      FROM sem_objetivo so
      LEFT JOIN LATERAL (
            SELECT gg.*
              FROM guia_genetica_sanmarino_colombia gg
             WHERE gg.company_id = p_company_id
               AND gg.deleted_at IS NULL
               AND lower(trim(gg.raza)) = lower(trim(COALESCE(so.raza, '')))
               AND trim(gg.anio_guia) = so.anio_guia::text
               -- ⚠️ Comparación de edad como TEXTO EXACTO, igual que
               --    fn_indicadores_levante_postura (`btrim(g.edad) = s::text`).
               --    NO parsear a número: la guía tiene DOS filas para la semana 25
               --    ('25' de levante y '25P' de producción) y el parseo numérico
               --    haría match con las dos, devolviendo la fila equivocada.
               AND btrim(gg.edad) = so.sem::text
             ORDER BY gg.id
             LIMIT 1
      ) g ON true
),
-- ── 10) Saldos y derivadas ──────────────────────────────────────────────────
calc AS (
    SELECT cg.*,
           (cg.base_h - cg.neto_out_h)                                   AS saldo_h,
           (cg.base_m - cg.neto_out_m)                                   AS saldo_m,
           -- aves al INICIO de la semana = saldo final + salidas netas de la semana
           (cg.base_h - cg.neto_out_h
              + (cg.mort_h + cg.sel_h + cg.err_h + cg.tras_sal_h - cg.tras_ing_h)) AS ini_h,
           (cg.base_m - cg.neto_out_m
              + (cg.mort_m + cg.sel_m + cg.err_m + cg.tras_sal_m - cg.tras_ing_m)) AS ini_m
      FROM con_guia cg
),
final AS (
    SELECT c.*,
           -- g/ave/día real por sexo: kg*1000 / promedio(inicio, fin) / días
           CASE WHEN c.dias > 0 AND ((c.ini_h + (c.base_h - c.neto_out_h)) / 2.0) > 0
                THEN (c.cons_kg_h * 1000.0)
                     / ((c.ini_h + (c.base_h - c.neto_out_h)) / 2.0) / c.dias
           END AS gr_ave_dia_h,
           CASE WHEN c.dias > 0 AND ((c.ini_m + (c.base_m - c.neto_out_m)) / 2.0) > 0
                THEN (c.cons_kg_m * 1000.0)
                     / ((c.ini_m + (c.base_m - c.neto_out_m)) / 2.0) / c.dias
           END AS gr_ave_dia_m
      FROM calc c
)
SELECT
    f.lote_id,
    f.lote_nombre,
    f.granja_id,
    f.granja_nombre,
    f.nucleo_nombre,
    f.regional,
    f.raza,
    f.anio_guia,
    f.sem                                                        AS edad_semana,
    f.fin_sem                                                    AS fecha_fin_semana,
    f.dias                                                       AS dias_con_registro,
    f.tuvo_traslado,
    -- Participación SIEMPRE dentro de su propia semana CALENDARIO: con p_sem_anio NULL
    -- la ventana global mezclaría las 52 semanas del año. Particiona por sem_cal, NO por
    -- fin_sem: fin_sem sale del encaset de cada lote, así que un lote padre con sublotes
    -- de fechas de llegada distintas dejaba a cada sublote SOLO en su partición y todos
    -- daban part = 1 (deberían repartirse ~0,50 y ~0,50). Con p_sem_anio concreto todas
    -- las filas comparten sem_cal, así que esto equivale al OVER () original.
    CASE WHEN SUM(f.saldo_h) OVER (PARTITION BY f.sem_cal) > 0
         THEN f.saldo_h / SUM(f.saldo_h) OVER (PARTITION BY f.sem_cal)
    END                                                          AS part,
    f.saldo_h                                                    AS saldo_hembras,
    f.saldo_m                                                    AS saldo_machos,
    -- ── hembras ──
    CASE WHEN f.ini_h > 0 THEN f.mort_h / f.ini_h * 100.0 END    AS mort_hembras_pct,
    CASE WHEN f.base_h > 0 THEN f.retiro_ac_h / f.base_h * 100.0 END AS retiro_acum_hembras_pct,
    f.g_retiro_ac_h::double precision                            AS retiro_acum_hembras_guia,
    CASE WHEN COALESCE(f.g_gr_ave_dia_h, 0) <> 0 AND f.gr_ave_dia_h IS NOT NULL
         THEN (f.gr_ave_dia_h / f.g_gr_ave_dia_h::double precision - 1) * 100.0 END
                                                                 AS dif_consumo_hembras_pct,
    CASE WHEN COALESCE(f.g_peso_h, 0) <> 0 AND f.peso_h IS NOT NULL
         THEN (f.peso_h / f.g_peso_h::double precision - 1) * 100.0 END
                                                                 AS dif_peso_hembras_pct,
    f.unif_h                                                     AS uniformidad_hembras,
    f.cv_h                                                       AS cv_hembras,
    -- ── machos ──
    CASE WHEN f.ini_m > 0 THEN f.mort_m / f.ini_m * 100.0 END    AS mort_machos_pct,
    CASE WHEN f.base_m > 0 THEN f.retiro_ac_m / f.base_m * 100.0 END AS retiro_acum_machos_pct,
    f.g_retiro_ac_m::double precision                            AS retiro_acum_machos_guia,
    CASE WHEN COALESCE(f.g_gr_ave_dia_m, 0) <> 0 AND f.gr_ave_dia_m IS NOT NULL
         THEN (f.gr_ave_dia_m / f.g_gr_ave_dia_m::double precision - 1) * 100.0 END
                                                                 AS dif_consumo_machos_pct,
    CASE WHEN COALESCE(f.g_peso_m, 0) <> 0 AND f.peso_m IS NOT NULL
         THEN (f.peso_m / f.g_peso_m::double precision - 1) * 100.0 END
                                                                 AS dif_peso_machos_pct,
    f.unif_m                                                     AS uniformidad_machos,
    f.cv_m                                                       AS cv_machos
  FROM final f
 ORDER BY f.sem DESC, f.lote_nombre;
$$;

COMMENT ON FUNCTION fn_resumen_semanal_ra_pesadas_levante(integer, integer, integer, integer[], text, boolean)
IS 'Informe RA Pesadas — hoja RESUMEN SEMANAL, bloque Levante: una fila por lote para una semana calendario (WEEKNUM Excel). Set-based; equivalente 1:1 al Detalle de fn_reporte_semanal_levante_extras.';");

            // ── fn_reporte_semanal_levante_extras ─────────────────────────────────────────────
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS fn_reporte_semanal_levante_extras(integer);
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

    -- Aves entradas por traslado en filas que el armado de _seg_sem_rx DESCARTA (puro traslado
    -- > sem 25). Nadie las suma: la ventana las tira. Se rescatan como base cuando el lote no
    -- trae encaset.
    --
    -- ⚠️ El predicado debe ser el MISMO que el WHERE NOT (...) de _seg_sem_rx más abajo. Si acá
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
       AND (COALESCE(sl.traslado_salida_hembras,0) + COALESCE(sl.traslado_salida_machos,0)
          + COALESCE(sl.traslado_ingreso_hembras,0) + COALESCE(sl.traslado_ingreso_machos,0)) > 0;
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
$$;");

            // ── fn_indicadores_levante_postura ─────────────────────────────────────────────
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS fn_indicadores_levante_postura(integer);
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
$$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin Down: revertir sería reponer las versiones que reportan saldos negativos y
            // saldos al doble. Las tres funciones son idempotentes y el comportamiento de los
            // lotes con encaset propio es idéntico en ambas versiones.
        }
    }
}
