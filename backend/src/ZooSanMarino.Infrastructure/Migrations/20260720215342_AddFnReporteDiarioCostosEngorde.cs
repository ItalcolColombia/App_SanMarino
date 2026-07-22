using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Crea fn_reporte_diario_costos_engorde(p_company_id, p_granja_id, p_lote_base_id,
    /// p_fecha_inicio, p_fecha_fin): Reporte Diario Costos de pollo engorde a nivel granja.
    /// Unifica POR FECHA los lotes del alcance (granja + lote base global opcional) reusando
    /// LATERAL fn_seguimiento_diario_engorde por lote (misma aritmética que la pantalla de
    /// seguimiento). Devuelve por día: alimento (stock/consumo por tipo, jsonb),
    /// mortalidad+selección y aves vivas por galpón (jsonb). Regla del segundo lote:
    /// sin p_fecha_inicio arranca en MAX(fecha_encaset) del alcance.
    /// Idempotente: CREATE OR REPLACE. SQL sincronizado con backend/sql/fn_reporte_diario_costos_engorde.sql.
    /// </summary>
    public partial class AddFnReporteDiarioCostosEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FN_SQL, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_reporte_diario_costos_engorde(INT, INT, INT, DATE, DATE);");
        }

        private const string FN_SQL = @"
CREATE OR REPLACE FUNCTION fn_reporte_diario_costos_engorde(
    p_company_id   INT,
    p_granja_id    INT,
    p_lote_base_id INT  DEFAULT NULL,
    p_fecha_inicio DATE DEFAULT NULL,
    p_fecha_fin    DATE DEFAULT NULL
)
RETURNS TABLE (
    fecha            DATE,
    consumo_total_kg FLOAT8,
    mort_sel_total   INT,
    aves_vivas_total INT,
    alimentos        TEXT,   -- [{nombre_alimento, stock_kg, consumo_kg}]
    galpones         TEXT    -- [{galpon_id, galpon_nombre, mortalidad, seleccion, err_sexaje, mort_sel, consumo_kg, aves_vivas}]
) LANGUAGE sql STABLE AS $$

WITH
-- 1. Lotes del alcance (granja + lote base opcional). Galpón '' = sin galpón.
lotes_scope AS (
    SELECT
        l.lote_ave_engorde_id                       AS lote_id,
        l.lote_nombre,
        COALESCE(TRIM(l.galpon_id), '')             AS galpon_id,
        COALESCE(
            NULLIF(TRIM(g.galpon_nombre), ''),
            NULLIF(TRIM(l.galpon_id), ''),
            'Sin galpón')                           AS galpon_nombre,
        l.fecha_encaset
    FROM lote_ave_engorde l
    LEFT JOIN galpones g ON g.galpon_id = TRIM(l.galpon_id)
    WHERE l.company_id = p_company_id
      AND l.granja_id  = p_granja_id
      AND l.deleted_at IS NULL
      AND (p_lote_base_id IS NULL OR l.lote_base_engorde_id = p_lote_base_id)
),

-- 2. Rango efectivo. Inicio por defecto = encaset del lote MÁS RECIENTE del alcance.
rango AS (
    SELECT
        COALESCE(p_fecha_inicio, (SELECT MAX(ls.fecha_encaset)::DATE FROM lotes_scope ls)) AS f_ini,
        COALESCE(p_fecha_fin, CURRENT_DATE)                                                AS f_fin
),

-- 3. Serie diaria completa por lote (SIN recorte inferior: el saldo de aves es
--    acumulado y necesita el histórico previo al rango). MATERIALIZED: la fn
--    LATERAL se evalúa una sola vez por lote.
diario_full AS MATERIALIZED (
    SELECT
        ls.lote_id,
        ls.lote_nombre,
        ls.galpon_id,
        ls.galpon_nombre,
        f.fecha,
        f.seg_id,
        COALESCE(f.mortalidad_hembras, 0) + COALESCE(f.mortalidad_machos, 0)         AS mortalidad,
        COALESCE(f.sel_h, 0) + COALESCE(f.sel_m, 0)                                  AS seleccion,
        COALESCE(f.error_sexaje_hembras, 0) + COALESCE(f.error_sexaje_machos, 0)     AS err_sexaje,
        COALESCE(f.consumo_dia_kg, 0)::FLOAT8                                        AS consumo_kg,
        COALESCE(f.saldo_aves, 0)                                                    AS saldo_aves,
        f.historico_consumo_alimento,
        f.tipo_alimento
    FROM lotes_scope ls
    CROSS JOIN LATERAL fn_seguimiento_diario_engorde(ls.lote_id) f
    CROSS JOIN rango r
    WHERE f.fecha <= r.f_fin
),

-- 4. Eventos del día dentro del rango (mort/sel/consumo/alimentos).
diario AS (
    SELECT df.*
    FROM diario_full df
    CROSS JOIN rango r
    WHERE r.f_ini IS NULL OR df.fecha >= r.f_ini
),

-- 5. Universo de fechas del reporte = fechas con actividad dentro del rango.
fechas AS (
    SELECT DISTINCT d.fecha FROM diario d
),

-- 6. Galpones del alcance (columnas dinámicas del front).
galpones_scope AS (
    SELECT ls.galpon_id, MAX(ls.galpon_nombre) AS galpon_nombre
    FROM lotes_scope ls
    GROUP BY ls.galpon_id
),

-- 7. Eventos agregados por fecha × galpón.
galpon_fecha AS (
    SELECT
        d.fecha,
        d.galpon_id,
        SUM(d.mortalidad)         AS mortalidad,
        SUM(d.seleccion)          AS seleccion,
        SUM(d.err_sexaje)         AS err_sexaje,
        SUM(d.consumo_kg)::FLOAT8 AS consumo_kg
    FROM diario d
    GROUP BY d.fecha, d.galpon_id
),

-- 8. Aves vivas por fecha × lote: último saldo conocido (<= fecha) del lote.
aves_lote_fecha AS (
    SELECT
        fx.fecha,
        ls.lote_id,
        ls.galpon_id,
        COALESCE((
            SELECT df.saldo_aves
            FROM diario_full df
            WHERE df.lote_id = ls.lote_id
              AND df.fecha  <= fx.fecha
            ORDER BY df.fecha DESC, COALESCE(df.seg_id, 0) DESC
            LIMIT 1
        ), 0) AS aves_vivas
    FROM fechas fx
    CROSS JOIN lotes_scope ls
),
aves_galpon_fecha AS (
    SELECT alf.fecha, alf.galpon_id, SUM(alf.aves_vivas)::INT AS aves_vivas
    FROM aves_lote_fecha alf
    GROUP BY alf.fecha, alf.galpon_id
),

-- 9. Alimentos: explode del histórico jsonb por ítem. El CASE dentro del LATERAL
--    protege contra históricos no-array (NULL/objeto): jsonb_array_elements sobre
--    '[]' no emite filas y nunca lanza error (el WHERE no garantiza el orden de
--    evaluación frente al LATERAL).
alim_items AS (
    SELECT
        d.fecha,
        d.galpon_id,
        COALESCE(d.seg_id, 0)                                          AS seg_id,
        COALESCE(NULLIF(TRIM(item->>'nombre_alimento'), ''), 'Sin especificar') AS nombre,
        COALESCE(NULLIF(item->>'consumo', '')::NUMERIC, 0)::FLOAT8     AS consumo,
        NULLIF(item->>'saldo_final', '')::NUMERIC::FLOAT8              AS saldo_final
    FROM diario d
    CROSS JOIN LATERAL jsonb_array_elements(
        CASE WHEN jsonb_typeof(d.historico_consumo_alimento) = 'array'
             THEN d.historico_consumo_alimento
             ELSE '[]'::jsonb END
    ) AS item
),
-- Stock del día por galpón+alimento = saldo_final del ÚLTIMO snapshot de esa fecha.
alim_stock AS (
    SELECT DISTINCT ON (ai.fecha, ai.galpon_id, ai.nombre)
        ai.fecha, ai.galpon_id, ai.nombre, ai.saldo_final
    FROM alim_items ai
    ORDER BY ai.fecha, ai.galpon_id, ai.nombre, ai.seg_id DESC
),
alim_consumo AS (
    SELECT ai.fecha, ai.nombre, SUM(ai.consumo)::FLOAT8 AS consumo_kg
    FROM alim_items ai
    GROUP BY ai.fecha, ai.nombre
),
alim_stock_dia AS (
    SELECT ast.fecha, ast.nombre, SUM(ast.saldo_final)::FLOAT8 AS stock_kg
    FROM alim_stock ast
    GROUP BY ast.fecha, ast.nombre
),
-- Fallback: filas con consumo pero sin histórico por ítem → tipo_alimento, stock NULL.
alim_fallback AS (
    SELECT
        d.fecha,
        COALESCE(NULLIF(TRIM(d.tipo_alimento), ''), 'Sin especificar') AS nombre,
        SUM(d.consumo_kg)::FLOAT8                                      AS consumo_kg
    FROM diario d
    WHERE d.consumo_kg > 0
      AND (CASE
               WHEN d.historico_consumo_alimento IS NULL                    THEN TRUE
               WHEN jsonb_typeof(d.historico_consumo_alimento) <> 'array'  THEN TRUE
               ELSE jsonb_array_length(d.historico_consumo_alimento) = 0
           END)
    GROUP BY d.fecha, COALESCE(NULLIF(TRIM(d.tipo_alimento), ''), 'Sin especificar')
),
alim_dia AS (
    SELECT u.fecha, u.nombre,
           SUM(u.consumo_kg)::FLOAT8 AS consumo_kg,
           SUM(u.stock_kg)::FLOAT8   AS stock_kg     -- SUM ignora NULL; todo NULL → NULL
    FROM (
        SELECT ac.fecha, ac.nombre, ac.consumo_kg, asd.stock_kg
        FROM alim_consumo ac
        LEFT JOIN alim_stock_dia asd ON asd.fecha = ac.fecha AND asd.nombre = ac.nombre
        UNION ALL
        SELECT af.fecha, af.nombre, af.consumo_kg, NULL::FLOAT8
        FROM alim_fallback af
    ) u
    GROUP BY u.fecha, u.nombre
),

-- 10. JSON por fecha.
alim_json AS (
    SELECT ad.fecha,
           jsonb_agg(jsonb_build_object(
               'nombre_alimento', ad.nombre,
               'stock_kg',        ad.stock_kg,
               'consumo_kg',      ad.consumo_kg
           ) ORDER BY ad.nombre)::TEXT AS alimentos_json
    FROM alim_dia ad
    GROUP BY ad.fecha
),
galp_json AS (
    SELECT
        fx.fecha,
        jsonb_agg(jsonb_build_object(
            'galpon_id',     gs.galpon_id,
            'galpon_nombre', gs.galpon_nombre,
            'mortalidad',    COALESCE(gf.mortalidad, 0),
            'seleccion',     COALESCE(gf.seleccion, 0),
            'err_sexaje',    COALESCE(gf.err_sexaje, 0),
            'mort_sel',      COALESCE(gf.mortalidad, 0) + COALESCE(gf.seleccion, 0),
            'consumo_kg',    COALESCE(gf.consumo_kg, 0),
            'aves_vivas',    COALESCE(agf.aves_vivas, 0)
        ) ORDER BY gs.galpon_nombre, gs.galpon_id)::TEXT             AS galpones_json,
        SUM(COALESCE(gf.mortalidad, 0) + COALESCE(gf.seleccion, 0))  AS mort_sel_total,
        SUM(COALESCE(agf.aves_vivas, 0))                             AS aves_vivas_total
    FROM fechas fx
    CROSS JOIN galpones_scope gs
    LEFT JOIN galpon_fecha      gf  ON gf.fecha  = fx.fecha AND gf.galpon_id  = gs.galpon_id
    LEFT JOIN aves_galpon_fecha agf ON agf.fecha = fx.fecha AND agf.galpon_id = gs.galpon_id
    GROUP BY fx.fecha
)

-- 11. Salida final: una fila por fecha.
SELECT
    fx.fecha,
    COALESCE(tot.consumo_total_kg, 0)::FLOAT8 AS consumo_total_kg,
    COALESCE(gj.mort_sel_total, 0)::INT       AS mort_sel_total,
    COALESCE(gj.aves_vivas_total, 0)::INT     AS aves_vivas_total,
    COALESCE(aj.alimentos_json, '[]')         AS alimentos,
    COALESCE(gj.galpones_json, '[]')          AS galpones
FROM fechas fx
LEFT JOIN (
    SELECT d.fecha, SUM(d.consumo_kg)::FLOAT8 AS consumo_total_kg
    FROM diario d
    GROUP BY d.fecha
) tot ON tot.fecha = fx.fecha
LEFT JOIN alim_json aj ON aj.fecha = fx.fecha
LEFT JOIN galp_json gj ON gj.fecha = fx.fecha
ORDER BY fx.fecha;
$$;
";
    }
}
