-- ═══════════════════════════════════════════════════════════════════════════════════════
-- fn_resumen_semanal_ra_pesadas_levante — Informe RA Pesadas (multi-lote/multi-granja)
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- Fix 2026-09-05 (plan seguimiento_produccion_multiples_registros_dia_plan.md, §5/S6):
--   'dias' contaba FILAS (COUNT(*)), no DIAS calendario, en el agregado semanal por lote.
--   Mismo razonamiento que fn_reporte_semanal_levante_extras: las SUMAS son asociativas y
--   no cambian, solo el conteo de dias necesitaba COUNT(DISTINCT reg_date).
-- Espejo exacto de pg_get_functiondef (ground truth) + este fix, no reformateado.
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION public.fn_resumen_semanal_ra_pesadas_levante(p_company_id integer, p_anio integer, p_sem_anio integer, p_granja_ids integer[] DEFAULT NULL::integer[], p_regional text DEFAULT NULL::text, p_excluir_trasladados boolean DEFAULT false)
 RETURNS TABLE(lote_id integer, lote_nombre text, granja_id integer, granja_nombre text, nucleo_nombre text, regional text, raza text, anio_guia integer, edad_semana integer, fecha_fin_semana date, dias_con_registro integer, tuvo_traslado boolean, part double precision, saldo_hembras double precision, saldo_machos double precision, mort_hembras_pct double precision, retiro_acum_hembras_pct double precision, retiro_acum_hembras_guia double precision, dif_consumo_hembras_pct double precision, dif_peso_hembras_pct double precision, uniformidad_hembras double precision, cv_hembras double precision, mort_machos_pct double precision, retiro_acum_machos_pct double precision, retiro_acum_machos_guia double precision, dif_consumo_machos_pct double precision, dif_peso_machos_pct double precision, uniformidad_machos double precision, cv_machos double precision)
 LANGUAGE sql
 STABLE
AS $function$
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
           -- Venta de aves: el saldo tiene que descontarla o el reporte sobrestima el lote. El total
           -- (venta_aves_cantidad) no sirve acá porque el saldo va POR SEXO; se usan los splits
           -- dedicados venta_aves_hembras/machos, espejo de movimiento_aves (que sigue siendo el
           -- dueño del número). Sin esto S-369B reportaba 1.281 machos con el maestro en 991.
           COALESCE(sl.venta_aves_hembras, 0)       AS venta_h,
           COALESCE(sl.venta_aves_machos, 0)        AS venta_m,
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
               AND r.venta_h = 0 AND r.venta_m = 0
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
           AND r.venta_h = 0 AND r.venta_m = 0
           AND (r.tras_sal_h + r.tras_sal_m + r.tras_ing_h + r.tras_ing_m) > 0
       )
),
-- ── 5) Agregado semanal por lote ────────────────────────────────────────────
sem AS (
    SELECT lote_id,
           sem,
           COUNT(DISTINCT reg_date)::int        AS dias,
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
           SUM(venta_h)::double precision      AS venta_h,
           SUM(venta_m)::double precision      AS venta_m,
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
--    El "grupo" de LOCF es el conteo de pesajes no nulos hasta la semana:
--    dentro de cada grupo, el primer valor es el último peso conocido.
acum AS (
    SELECT s.lote_id,
           s.sem,
           s.dias,
           s.mort_h, s.mort_m, s.sel_h, s.sel_m, s.err_h, s.err_m,
           s.tras_sal_h, s.tras_sal_m, s.tras_ing_h, s.tras_ing_m,
           s.venta_h, s.venta_m,
           s.cons_kg_h, s.cons_kg_m,
           NULLIF(p.ph, 0) AS peso_h_raw,
           NULLIF(p.pm, 0) AS peso_m_raw,
           NULLIF(COALESCE(p.uh, 0), 0)  AS unif_h,
           NULLIF(COALESCE(p.um, 0), 0)  AS unif_m,
           NULLIF(COALESCE(p.cvh, 0), 0) AS cv_h,
           NULLIF(COALESCE(p.cvm, 0), 0) AS cv_m,
           -- salidas netas acumuladas hasta ESTA semana (inclusive)
           SUM(s.mort_h + s.sel_h + s.err_h + s.tras_sal_h + s.venta_h - s.tras_ing_h)
               OVER (PARTITION BY s.lote_id ORDER BY s.sem
                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS neto_out_h,
           SUM(s.mort_m + s.sel_m + s.err_m + s.tras_sal_m + s.venta_m - s.tras_ing_m)
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
              -- Fuente unificada: la compartida + la reducida proyectada al mismo shape.
              -- Aca NO hace falta leer `origen` como en fn_indicadores_*: estas columnas
              -- pasan por f_safe_numeric(), que ya devuelve NULL ante NULL o texto no
              -- numerico ⇒ no fabrica el 0 falso que alla habia que condicionar.
              FROM vw_guia_genetica_postura gg
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
$function$

