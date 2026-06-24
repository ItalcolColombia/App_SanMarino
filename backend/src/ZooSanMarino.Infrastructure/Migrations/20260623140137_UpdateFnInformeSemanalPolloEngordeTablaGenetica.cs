using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Actualiza fn_informe_semanal_pollo_engorde (v2): conecta las columnas "Tabla" a
    /// guia_genetica_ecuador_detalle (sexo='mixto', SUMA de los 7 días de cada semana de vida).
    /// Lotes sin raza/ano_tabla_genetica o sin header activo siguen devolviendo NULL en esas columnas.
    /// Fuente canónica: backend/sql/fn_informe_semanal_pollo_engorde.sql
    /// </summary>
    public partial class UpdateFnInformeSemanalPolloEngordeTablaGenetica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FN_SQL_V2, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FN_SQL_V1, suppressTransaction: true);
        }

        private const string FN_SQL_V2 = @"
DROP FUNCTION IF EXISTS public.fn_informe_semanal_pollo_engorde(INT, INT[], TEXT, TEXT, INT, DATE, DATE);

CREATE OR REPLACE FUNCTION public.fn_informe_semanal_pollo_engorde(
    p_company_id  INT,
    p_granja_ids  INT[] DEFAULT NULL,
    p_nucleo_id   TEXT  DEFAULT NULL,
    p_galpon_id   TEXT  DEFAULT NULL,
    p_lote_id     INT   DEFAULT NULL,
    p_fecha_desde DATE  DEFAULT NULL,
    p_fecha_hasta DATE  DEFAULT NULL
)
RETURNS TABLE (
    company_id            INT,
    granja_id             INT,
    granja_nombre         TEXT,
    nucleo_id             TEXT,
    galpon_id             TEXT,
    galpon_nombre         TEXT,
    lote_ave_engorde_id   INT,
    lote_nombre           TEXT,
    fecha_encaset         DATE,
    semana                INT,
    edad_dia_fin          INT,
    fecha_inicio_semana   DATE,
    fecha_fin_semana      DATE,
    aves_encasetadas      INT,
    saldo_inicio_semana   INT,
    saldo_fin_semana      INT,
    mort_natural_unid     INT,
    seleccion_unid        INT,
    ventas_unid           INT,
    mort_natural_pct      NUMERIC,
    seleccion_pct         NUMERIC,
    mortalidad_total_pct  NUMERIC,
    consumo_semana_kg     NUMERIC,
    consumo_acum_kg       NUMERIC,
    consumo_real_g_ave    NUMERIC,
    peso_real_g           NUMERIC,
    peso_anterior_g       NUMERIC,
    peso_llegada_g        NUMERIC,
    ganancia_real_g       NUMERIC,
    conversion_real       NUMERIC,
    ventas_kg             NUMERIC,
    agua_ml               NUMERIC,
    relacion_agua         NUMERIC,
    consumo_tabla_g       NUMERIC,
    peso_tabla_g          NUMERIC,
    ganancia_tabla_g      NUMERIC,
    conversion_tabla      NUMERIC,
    mortalidad_tabla_pct  NUMERIC,
    pct_consumo           NUMERIC,
    pct_peso              NUMERIC,
    pct_conversion        NUMERIC
)
LANGUAGE sql STABLE AS $$
WITH lotes AS (
    SELECT
        l.lote_ave_engorde_id                                   AS lote_id,
        l.company_id,
        l.granja_id,
        f.name                                                  AS granja_nombre,
        l.nucleo_id,
        l.galpon_id,
        g.galpon_nombre,
        l.lote_nombre,
        l.fecha_encaset::date                                   AS fecha_encaset,
        COALESCE(l.aves_encasetadas,
                 COALESCE(l.hembras_l,0)+COALESCE(l.machos_l,0)+COALESCE(l.mixtas,0)) AS aves_encasetadas,
        COALESCE(NULLIF(l.peso_inicial_h,0), NULLIF(l.peso_inicial_m,0), NULLIF(l.peso_mixto,0))::numeric AS peso_llegada_g,
        l.raza,
        l.ano_tabla_genetica
    FROM public.lote_ave_engorde l
    JOIN public.farms f         ON f.id = l.granja_id
    LEFT JOIN public.galpones g ON g.galpon_id = l.galpon_id AND g.granja_id = l.granja_id
    WHERE l.deleted_at IS NULL
      AND l.company_id = p_company_id
      AND (p_granja_ids IS NULL OR cardinality(p_granja_ids) = 0 OR l.granja_id = ANY(p_granja_ids))
      AND (p_nucleo_id  IS NULL OR l.nucleo_id = p_nucleo_id)
      AND (p_galpon_id  IS NULL OR l.galpon_id = p_galpon_id)
      AND (p_lote_id    IS NULL OR l.lote_ave_engorde_id = p_lote_id)
),
diario AS (
    SELECT
        lo.lote_id,
        d.semana,
        d.fecha,
        d.edad_dia,
        (COALESCE(d.mortalidad_hembras,0)+COALESCE(d.mortalidad_machos,0))      AS mort,
        (COALESCE(d.sel_h,0)+COALESCE(d.sel_m,0))                              AS sel,
        COALESCE(d.consumo_dia_kg,0)                                          AS consumo_dia_kg,
        COALESCE(d.acum_consumo_kg,0)                                         AS acum_consumo_kg,
        d.saldo_aves,
        (COALESCE(d.despacho_hembras,0)+COALESCE(d.despacho_machos,0)+COALESCE(d.despacho_mixtas,0)) AS ventas,
        COALESCE(d.despacho_peso_neto,0)                                      AS ventas_kg,
        CASE
            WHEN COALESCE(d.peso_prom_hembras,0) > 0 AND COALESCE(d.peso_prom_machos,0) > 0
                THEN (d.peso_prom_hembras + d.peso_prom_machos) / 2.0
            ELSE NULLIF(COALESCE(NULLIF(d.peso_prom_hembras,0), NULLIF(d.peso_prom_machos,0)), 0)
        END                                                                   AS peso_g,
        COALESCE(d.consumo_agua_diario,0)                                     AS agua
    FROM lotes lo
    CROSS JOIN LATERAL public.fn_seguimiento_diario_engorde(lo.lote_id) d
),
por_semana AS (
    SELECT
        lote_id,
        semana::int                                            AS semana,
        MAX(edad_dia)::int                                     AS edad_dia_fin,
        SUM(mort)::int                                         AS mort_unid,
        SUM(sel)::int                                          AS sel_unid,
        SUM(ventas)::int                                       AS ventas_unid,
        SUM(consumo_dia_kg)::numeric                           AS consumo_semana_kg,
        MAX(acum_consumo_kg)::numeric                          AS consumo_acum_kg,
        SUM(ventas_kg)::numeric                                AS ventas_kg,
        SUM(agua)::numeric                                     AS agua_ml,
        (array_agg(saldo_aves ORDER BY fecha DESC, edad_dia DESC))[1]               AS saldo_fin_semana,
        (array_agg(peso_g     ORDER BY fecha DESC) FILTER (WHERE peso_g IS NOT NULL))[1] AS peso_real_g
    FROM diario
    GROUP BY lote_id, semana
),
win AS (
    SELECT
        ps.*,
        COALESCE(SUM(ps.mort_unid + ps.sel_unid + ps.ventas_unid)
            OVER (PARTITION BY ps.lote_id ORDER BY ps.semana
                  ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0)         AS perdidas_previas,
        LAG(ps.peso_real_g) OVER (PARTITION BY ps.lote_id ORDER BY ps.semana)   AS peso_anterior_g
    FROM por_semana ps
),
tabla_gen AS (
    SELECT
        lo.lote_id,
        ((d.dia - 1) / 7 + 1)::int                        AS semana,
        SUM(d.cantidad_alimento_diario_g)::numeric          AS consumo_tabla_g,
        SUM(d.peso_corporal_g)::numeric                     AS peso_tabla_g,
        SUM(d.ganancia_diaria_g)::numeric                   AS ganancia_tabla_g,
        SUM(d.ca)::numeric                                  AS conversion_tabla,
        SUM(d.mortalidad_seleccion_diaria)::numeric         AS mortalidad_tabla_pct
    FROM lotes lo
    JOIN public.guia_genetica_ecuador_header h
        ON h.company_id  = p_company_id
       AND h.raza        = lo.raza
       AND h.anio_guia   = lo.ano_tabla_genetica
       AND h.deleted_at  IS NULL
       AND h.estado      = 'active'
    JOIN public.guia_genetica_ecuador_detalle d
        ON d.guia_genetica_ecuador_header_id = h.id
       AND d.sexo        = 'mixto'
       AND d.deleted_at  IS NULL
    WHERE lo.raza IS NOT NULL
      AND lo.ano_tabla_genetica IS NOT NULL
    GROUP BY lo.lote_id, ((d.dia - 1) / 7 + 1)
)
SELECT
    lo.company_id,
    lo.granja_id,
    lo.granja_nombre::text,
    lo.nucleo_id::text,
    lo.galpon_id::text,
    COALESCE(lo.galpon_nombre,'')::text                                        AS galpon_nombre,
    lo.lote_id                                                                 AS lote_ave_engorde_id,
    lo.lote_nombre::text,
    lo.fecha_encaset,
    w.semana,
    w.edad_dia_fin,
    (lo.fecha_encaset + (7 * (w.semana - 1)))::date                            AS fecha_inicio_semana,
    (lo.fecha_encaset + (7 *  w.semana - 1))::date                             AS fecha_fin_semana,
    lo.aves_encasetadas,
    GREATEST(0, lo.aves_encasetadas - w.perdidas_previas)::int                 AS saldo_inicio_semana,
    w.saldo_fin_semana::int,
    w.mort_unid                                                                AS mort_natural_unid,
    w.sel_unid                                                                 AS seleccion_unid,
    w.ventas_unid,
    CASE WHEN GREATEST(0, lo.aves_encasetadas - w.perdidas_previas) > 0
         THEN w.mort_unid::numeric / GREATEST(1, lo.aves_encasetadas - w.perdidas_previas) * 100
         ELSE 0 END                                                            AS mort_natural_pct,
    CASE WHEN GREATEST(0, lo.aves_encasetadas - w.perdidas_previas) > 0
         THEN w.sel_unid::numeric  / GREATEST(1, lo.aves_encasetadas - w.perdidas_previas) * 100
         ELSE 0 END                                                            AS seleccion_pct,
    CASE WHEN GREATEST(0, lo.aves_encasetadas - w.perdidas_previas) > 0
         THEN (w.mort_unid + w.sel_unid)::numeric / GREATEST(1, lo.aves_encasetadas - w.perdidas_previas) * 100
         ELSE 0 END                                                            AS mortalidad_total_pct,
    w.consumo_semana_kg,
    w.consumo_acum_kg,
    CASE WHEN lo.aves_encasetadas > 0 THEN w.consumo_acum_kg / lo.aves_encasetadas * 1000 ELSE 0 END AS consumo_real_g_ave,
    w.peso_real_g,
    w.peso_anterior_g,
    lo.peso_llegada_g,
    CASE
        WHEN w.peso_real_g IS NULL THEN NULL
        WHEN w.peso_anterior_g IS NOT NULL THEN w.peso_real_g - w.peso_anterior_g
        ELSE w.peso_real_g - COALESCE(lo.peso_llegada_g, 0)
    END                                                                        AS ganancia_real_g,
    CASE WHEN COALESCE(w.peso_real_g,0) > 0 AND lo.aves_encasetadas > 0
         THEN (w.consumo_acum_kg / lo.aves_encasetadas * 1000) / w.peso_real_g
         ELSE NULL END                                                         AS conversion_real,
    w.ventas_kg,
    w.agua_ml,
    CASE WHEN w.consumo_acum_kg > 0 THEN w.agua_ml / w.consumo_acum_kg ELSE NULL END AS relacion_agua,
    tg.consumo_tabla_g,
    tg.peso_tabla_g,
    tg.ganancia_tabla_g,
    tg.conversion_tabla,
    tg.mortalidad_tabla_pct,
    CASE WHEN NULLIF(tg.consumo_tabla_g, 0) IS NOT NULL AND lo.aves_encasetadas > 0
         THEN ROUND(((w.consumo_semana_kg * 1000.0 / lo.aves_encasetadas) / tg.consumo_tabla_g * 100)::numeric, 2)
         ELSE NULL END                                                         AS pct_consumo,
    CASE WHEN NULLIF(tg.peso_tabla_g, 0) IS NOT NULL AND w.peso_real_g IS NOT NULL
         THEN ROUND((w.peso_real_g / tg.peso_tabla_g * 100)::numeric, 2)
         ELSE NULL END                                                         AS pct_peso,
    CASE WHEN NULLIF(tg.conversion_tabla, 0) IS NOT NULL
              AND COALESCE(w.peso_real_g, 0) > 0 AND lo.aves_encasetadas > 0
         THEN ROUND((((w.consumo_acum_kg * 1000.0 / lo.aves_encasetadas) / w.peso_real_g) / tg.conversion_tabla * 100)::numeric, 2)
         ELSE NULL END                                                         AS pct_conversion
FROM win w
JOIN lotes lo   ON lo.lote_id   = w.lote_id
LEFT JOIN tabla_gen tg ON tg.lote_id = w.lote_id AND tg.semana = w.semana
WHERE (p_fecha_desde IS NULL OR (lo.fecha_encaset + (7 *  w.semana - 1))      >= p_fecha_desde)
  AND (p_fecha_hasta IS NULL OR (lo.fecha_encaset + (7 * (w.semana - 1)))     <= p_fecha_hasta)
ORDER BY w.semana, lo.granja_nombre, lo.lote_nombre;
$$;

COMMENT ON FUNCTION public.fn_informe_semanal_pollo_engorde(INT, INT[], TEXT, TEXT, INT, DATE, DATE) IS
  'Informe Semanal Pollo Engorde (Panamá): 1 fila por (lote, semana de vida). Reales desde seguimiento_diario_aves_engorde + movimiento_pollo_engorde (vía fn_seguimiento_diario_engorde). Tabla genética = guia_genetica_ecuador_detalle (sexo=mixto, SUMA 7 días). Filtrar por company_id (oblig.), granja_ids[], nucleo_id, galpon_id, lote_id, fechas.';
";

        private const string FN_SQL_V1 = @"
DROP FUNCTION IF EXISTS public.fn_informe_semanal_pollo_engorde(INT, INT[], TEXT, TEXT, INT, DATE, DATE);

CREATE OR REPLACE FUNCTION public.fn_informe_semanal_pollo_engorde(
    p_company_id  INT,
    p_granja_ids  INT[] DEFAULT NULL,
    p_nucleo_id   TEXT  DEFAULT NULL,
    p_galpon_id   TEXT  DEFAULT NULL,
    p_lote_id     INT   DEFAULT NULL,
    p_fecha_desde DATE  DEFAULT NULL,
    p_fecha_hasta DATE  DEFAULT NULL
)
RETURNS TABLE (
    company_id            INT,
    granja_id             INT,
    granja_nombre         TEXT,
    nucleo_id             TEXT,
    galpon_id             TEXT,
    galpon_nombre         TEXT,
    lote_ave_engorde_id   INT,
    lote_nombre           TEXT,
    fecha_encaset         DATE,
    semana                INT,
    edad_dia_fin          INT,
    fecha_inicio_semana   DATE,
    fecha_fin_semana      DATE,
    aves_encasetadas      INT,
    saldo_inicio_semana   INT,
    saldo_fin_semana      INT,
    mort_natural_unid     INT,
    seleccion_unid        INT,
    ventas_unid           INT,
    mort_natural_pct      NUMERIC,
    seleccion_pct         NUMERIC,
    mortalidad_total_pct  NUMERIC,
    consumo_semana_kg     NUMERIC,
    consumo_acum_kg       NUMERIC,
    consumo_real_g_ave    NUMERIC,
    peso_real_g           NUMERIC,
    peso_anterior_g       NUMERIC,
    peso_llegada_g        NUMERIC,
    ganancia_real_g       NUMERIC,
    conversion_real       NUMERIC,
    ventas_kg             NUMERIC,
    agua_ml               NUMERIC,
    relacion_agua         NUMERIC,
    consumo_tabla_g       NUMERIC,
    peso_tabla_g          NUMERIC,
    ganancia_tabla_g      NUMERIC,
    conversion_tabla      NUMERIC,
    mortalidad_tabla_pct  NUMERIC,
    pct_consumo           NUMERIC,
    pct_peso              NUMERIC,
    pct_conversion        NUMERIC
)
LANGUAGE sql STABLE AS $$
WITH lotes AS (
    SELECT
        l.lote_ave_engorde_id                                   AS lote_id,
        l.company_id,
        l.granja_id,
        f.name                                                  AS granja_nombre,
        l.nucleo_id,
        l.galpon_id,
        g.galpon_nombre,
        l.lote_nombre,
        l.fecha_encaset::date                                   AS fecha_encaset,
        COALESCE(l.aves_encasetadas,
                 COALESCE(l.hembras_l,0)+COALESCE(l.machos_l,0)+COALESCE(l.mixtas,0)) AS aves_encasetadas,
        COALESCE(NULLIF(l.peso_inicial_h,0), NULLIF(l.peso_inicial_m,0), NULLIF(l.peso_mixto,0))::numeric AS peso_llegada_g
    FROM public.lote_ave_engorde l
    JOIN public.farms f         ON f.id = l.granja_id
    LEFT JOIN public.galpones g ON g.galpon_id = l.galpon_id AND g.granja_id = l.granja_id
    WHERE l.deleted_at IS NULL
      AND l.company_id = p_company_id
      AND (p_granja_ids IS NULL OR cardinality(p_granja_ids) = 0 OR l.granja_id = ANY(p_granja_ids))
      AND (p_nucleo_id  IS NULL OR l.nucleo_id = p_nucleo_id)
      AND (p_galpon_id  IS NULL OR l.galpon_id = p_galpon_id)
      AND (p_lote_id    IS NULL OR l.lote_ave_engorde_id = p_lote_id)
),
diario AS (
    SELECT
        lo.lote_id,
        d.semana,
        d.fecha,
        d.edad_dia,
        (COALESCE(d.mortalidad_hembras,0)+COALESCE(d.mortalidad_machos,0))      AS mort,
        (COALESCE(d.sel_h,0)+COALESCE(d.sel_m,0))                              AS sel,
        COALESCE(d.consumo_dia_kg,0)                                          AS consumo_dia_kg,
        COALESCE(d.acum_consumo_kg,0)                                         AS acum_consumo_kg,
        d.saldo_aves,
        (COALESCE(d.despacho_hembras,0)+COALESCE(d.despacho_machos,0)+COALESCE(d.despacho_mixtas,0)) AS ventas,
        COALESCE(d.despacho_peso_neto,0)                                      AS ventas_kg,
        CASE
            WHEN COALESCE(d.peso_prom_hembras,0) > 0 AND COALESCE(d.peso_prom_machos,0) > 0
                THEN (d.peso_prom_hembras + d.peso_prom_machos) / 2.0
            ELSE NULLIF(COALESCE(NULLIF(d.peso_prom_hembras,0), NULLIF(d.peso_prom_machos,0)), 0)
        END                                                                   AS peso_g,
        COALESCE(d.consumo_agua_diario,0)                                     AS agua
    FROM lotes lo
    CROSS JOIN LATERAL public.fn_seguimiento_diario_engorde(lo.lote_id) d
),
por_semana AS (
    SELECT
        lote_id,
        semana::int                                            AS semana,
        MAX(edad_dia)::int                                     AS edad_dia_fin,
        SUM(mort)::int                                         AS mort_unid,
        SUM(sel)::int                                          AS sel_unid,
        SUM(ventas)::int                                       AS ventas_unid,
        SUM(consumo_dia_kg)::numeric                           AS consumo_semana_kg,
        MAX(acum_consumo_kg)::numeric                          AS consumo_acum_kg,
        SUM(ventas_kg)::numeric                                AS ventas_kg,
        SUM(agua)::numeric                                     AS agua_ml,
        (array_agg(saldo_aves ORDER BY fecha DESC, edad_dia DESC))[1]               AS saldo_fin_semana,
        (array_agg(peso_g     ORDER BY fecha DESC) FILTER (WHERE peso_g IS NOT NULL))[1] AS peso_real_g
    FROM diario
    GROUP BY lote_id, semana
),
win AS (
    SELECT
        ps.*,
        COALESCE(SUM(ps.mort_unid + ps.sel_unid + ps.ventas_unid)
            OVER (PARTITION BY ps.lote_id ORDER BY ps.semana
                  ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0)         AS perdidas_previas,
        LAG(ps.peso_real_g) OVER (PARTITION BY ps.lote_id ORDER BY ps.semana)   AS peso_anterior_g
    FROM por_semana ps
)
SELECT
    lo.company_id,
    lo.granja_id,
    lo.granja_nombre::text,
    lo.nucleo_id::text,
    lo.galpon_id::text,
    COALESCE(lo.galpon_nombre,'')::text                                        AS galpon_nombre,
    lo.lote_id                                                                 AS lote_ave_engorde_id,
    lo.lote_nombre::text,
    lo.fecha_encaset,
    w.semana,
    w.edad_dia_fin,
    (lo.fecha_encaset + (7 * (w.semana - 1)))::date                            AS fecha_inicio_semana,
    (lo.fecha_encaset + (7 *  w.semana - 1))::date                             AS fecha_fin_semana,
    lo.aves_encasetadas,
    GREATEST(0, lo.aves_encasetadas - w.perdidas_previas)::int                 AS saldo_inicio_semana,
    w.saldo_fin_semana::int,
    w.mort_unid                                                                AS mort_natural_unid,
    w.sel_unid                                                                 AS seleccion_unid,
    w.ventas_unid,
    CASE WHEN GREATEST(0, lo.aves_encasetadas - w.perdidas_previas) > 0
         THEN w.mort_unid::numeric / GREATEST(1, lo.aves_encasetadas - w.perdidas_previas) * 100
         ELSE 0 END                                                            AS mort_natural_pct,
    CASE WHEN GREATEST(0, lo.aves_encasetadas - w.perdidas_previas) > 0
         THEN w.sel_unid::numeric  / GREATEST(1, lo.aves_encasetadas - w.perdidas_previas) * 100
         ELSE 0 END                                                            AS seleccion_pct,
    CASE WHEN GREATEST(0, lo.aves_encasetadas - w.perdidas_previas) > 0
         THEN (w.mort_unid + w.sel_unid)::numeric / GREATEST(1, lo.aves_encasetadas - w.perdidas_previas) * 100
         ELSE 0 END                                                            AS mortalidad_total_pct,
    w.consumo_semana_kg,
    w.consumo_acum_kg,
    CASE WHEN lo.aves_encasetadas > 0 THEN w.consumo_acum_kg / lo.aves_encasetadas * 1000 ELSE 0 END AS consumo_real_g_ave,
    w.peso_real_g,
    w.peso_anterior_g,
    lo.peso_llegada_g,
    CASE
        WHEN w.peso_real_g IS NULL THEN NULL
        WHEN w.peso_anterior_g IS NOT NULL THEN w.peso_real_g - w.peso_anterior_g
        ELSE w.peso_real_g - COALESCE(lo.peso_llegada_g, 0)
    END                                                                        AS ganancia_real_g,
    CASE WHEN COALESCE(w.peso_real_g,0) > 0 AND lo.aves_encasetadas > 0
         THEN (w.consumo_acum_kg / lo.aves_encasetadas * 1000) / w.peso_real_g
         ELSE NULL END                                                         AS conversion_real,
    w.ventas_kg,
    w.agua_ml,
    CASE WHEN w.consumo_acum_kg > 0 THEN w.agua_ml / w.consumo_acum_kg ELSE NULL END AS relacion_agua,
    NULL::numeric AS consumo_tabla_g,
    NULL::numeric AS peso_tabla_g,
    NULL::numeric AS ganancia_tabla_g,
    NULL::numeric AS conversion_tabla,
    NULL::numeric AS mortalidad_tabla_pct,
    NULL::numeric AS pct_consumo,
    NULL::numeric AS pct_peso,
    NULL::numeric AS pct_conversion
FROM win w
JOIN lotes lo ON lo.lote_id = w.lote_id
WHERE (p_fecha_desde IS NULL OR (lo.fecha_encaset + (7 *  w.semana - 1))      >= p_fecha_desde)
  AND (p_fecha_hasta IS NULL OR (lo.fecha_encaset + (7 * (w.semana - 1)))     <= p_fecha_hasta)
ORDER BY w.semana, lo.granja_nombre, lo.lote_nombre;
$$;
";
    }
}
