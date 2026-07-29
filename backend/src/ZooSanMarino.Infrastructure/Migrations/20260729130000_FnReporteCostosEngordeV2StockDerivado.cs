using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <c>fn_reporte_diario_costos_engorde</c> v2: el <c>stock_kg</c> por alimento se DERIVA de
    /// ingresos − consumo, en vez de leer el <c>saldo_final</c> del snapshot jsonb
    /// <c>historico_consumo_alimento</c>.
    /// <para>
    /// El snapshot solo existe para los alimentos que se consumieron ESE dia, asi que el reporte
    /// mostraba una fraccion del stock real y no se movia cuando el saldo se recalculaba. Caso
    /// testigo G0464 (DAYLAND) al 22/07: el reporte daba 46.229,2 kg —solo AV. SUPER POLLO
    /// ENGORDE— cuando el galpon tenia 66.565,8 repartidos en tres items.
    /// </para>
    /// <para>
    /// La divergencia era estructural y previa a este trabajo: 738 de 2.103 registros en Ecuador
    /// y 451 de 470 en Panama tienen el jsonb desalineado del <c>saldo_alimento_kg</c>.
    /// </para>
    /// <para>
    /// Ahora el stock es <c>ingresos(≤fecha) − consumo(≤fecha)</c> por alimento, con los mismos
    /// filtros que <c>fn_seguimiento_diario_engorde</c> (se excluyen los INV_INGRESO del propio
    /// seguimiento y las devoluciones por eliminacion), acumulado sobre TODO el historico porque
    /// el stock no se recorta al rango. Un alimento con stock aparece aunque ese dia no se consuma.
    /// </para>
    /// <para>
    /// Verificado: la suma de <c>stock_kg</c> coincide EXACTO (0,0) con
    /// <c>ingresos − consumo</c> del alcance en las 12 granjas de las dos empresas, y en Panama
    /// cuadra ademas contra Gestion de inventario (DAYLAND, MENDOZA y TROFARELLO en 0,0).
    /// <c>consumo_total_kg</c>, <c>mort_sel_total</c> y <c>aves_vivas_total</c> NO cambian.
    /// </para>
    /// Idempotente (CREATE OR REPLACE). SQL sincronizado con
    /// backend/sql/fn_reporte_diario_costos_engorde.sql.
    /// Plan: fase_de_desarrollo/cuadre_engorde_panama_aves_alimento_plan.md
    /// </summary>
    public partial class FnReporteCostosEngordeV2StockDerivado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- =============================================================================
-- fn_reporte_diario_costos_engorde(p_company_id, p_granja_id, p_lote_base_id,
--                                  p_fecha_inicio, p_fecha_fin)
--
-- Reporte Diario Costos de pollo engorde a nivel GRANJA: unifica POR FECHA todos
-- los lotes del alcance (granja + lote base opcional) y devuelve, por día:
--   * resumen global de alimento (kg): consumo y stock por tipo de alimento,
--   * mortalidad + selección por galpón,
--   * aves vivas por galpón (carry-forward del último saldo conocido por lote).
--
-- v2 (2026-07-29) — Fix: `stock_kg` se DERIVA de ingresos − consumo por alimento.
--   * Antes salía del `saldo_final` del snapshot jsonb `historico_consumo_alimento`, que solo
--     existe para los alimentos consumidos ESE día: el reporte mostraba una fracción del stock
--     real (G0464 al 22/07: 46.229,2 kg de un galpón que tenía 66.565,8 en tres ítems) y no se
--     movía al recalcularse el saldo. Ahora la suma de `stock_kg` cierra contra el galpón.
--   * Un alimento con stock aparece aunque ese día no se consuma (FULL JOIN).
--
-- v1 (2026-07-20) — Diseño:
--   * NO reimplementa aritmética: cada lote se resuelve con LATERAL
--     fn_seguimiento_diario_engorde(lote_id) (v8: apertura, cierre efectivo,
--     ventas, mort caja, saldo Lindley) → los números cuadran 1:1 con la
--     pantalla de seguimiento por lote.
--   * Alcance: company + granja + deleted_at IS NULL + (p_lote_base_id NULL =
--     TODOS los lotes | valor = solo lotes amarrados a ese lote base global).
--   * Regla del ""segundo lote"": si p_fecha_inicio es NULL, el reporte arranca
--     en MAX(fecha_encaset) del alcance (la llegada del lote más reciente).
--     p_fecha_fin NULL → hoy.
--   * Alimentos del día: explode de historico_consumo_alimento jsonb
--     ([{nombre_alimento, saldo_inicial, consumo, saldo_final, unidad_medida}]).
--     consumo = SUM por alimento; stock = SUM por galpón del ÚLTIMO saldo_final
--     de esa fecha (último snapshot por galpón+alimento, evita doble conteo de
--     lotes que comparten bodega). Fallback filas sin histórico: tipo_alimento
--     + consumo_dia_kg con stock NULL (no se inventa dato).
--   * Aves vivas por fecha = estado (no evento): último saldo_aves del lote con
--     fecha <= d SIN recortar por p_fecha_inicio (el saldo es acumulado), y 0 si
--     el lote aún no tiene filas. Los eventos del día (mort/sel/consumo) sí se
--     recortan al rango.
--   * jsonb agregado se devuelve como TEXT para mapeo directo con SqlQueryRaw.
-- =============================================================================

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
-- 1. Lotes del alcance (granja + lote base opcional). Galpón """" = sin galpón.
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
alim_consumo AS (
    SELECT ai.fecha, ai.nombre, SUM(ai.consumo)::FLOAT8 AS consumo_kg
    FROM alim_items ai
    GROUP BY ai.fecha, ai.nombre
),

-- ⭐ v2 (2026-07-29): el stock por alimento se DERIVA de ingresos − consumo, igual que el saldo
--    del galpón, en vez de leer el `saldo_final` del snapshot jsonb.
--    El snapshot solo existe para los alimentos consumidos ESE día, así que el reporte mostraba
--    una fracción del stock real: G0464 al 22/07 daba 46.229,2 kg (solo SUPER POLLO ENGORDE)
--    cuando el galpón tenía 66.565,8 repartidos en tres ítems. Además, al ser un valor escrito
--    al cargar el registro, no se movía cuando el saldo se recalculaba.
--    Ahora la suma de `stock_kg` de todos los alimentos cierra contra el saldo del galpón.

-- 9a. Consumo por alimento sobre TODO el histórico (sin recorte de rango): el stock es acumulado.
alim_consumo_full AS (
    SELECT df.fecha,
           COALESCE(NULLIF(TRIM(item->>'nombre_alimento'), ''), 'Sin especificar') AS nombre,
           SUM(COALESCE(NULLIF(item->>'consumo', '')::NUMERIC, 0))::FLOAT8         AS kg
    FROM diario_full df
    CROSS JOIN LATERAL jsonb_array_elements(
        CASE WHEN jsonb_typeof(df.historico_consumo_alimento) = 'array'
             THEN df.historico_consumo_alimento
             ELSE '[]'::jsonb END
    ) AS item
    GROUP BY 1, 2
    UNION ALL
    -- Fallback: días con consumo pero sin desglose jsonb → todo al tipo_alimento del día.
    SELECT df.fecha,
           COALESCE(NULLIF(TRIM(df.tipo_alimento), ''), 'Sin especificar') AS nombre,
           SUM(df.consumo_kg)::FLOAT8                                      AS kg
    FROM diario_full df
    WHERE df.consumo_kg > 0
      AND (CASE
               WHEN df.historico_consumo_alimento IS NULL                   THEN TRUE
               WHEN jsonb_typeof(df.historico_consumo_alimento) <> 'array'  THEN TRUE
               ELSE jsonb_array_length(df.historico_consumo_alimento) = 0
           END)
    GROUP BY 1, 2
),

-- 9b. Ingresos/traslados de alimento por fecha × alimento, en los galpones del alcance.
--     Mismos filtros que fn_seguimiento_diario_engorde para que los totales cierren:
--     se excluyen los INV_INGRESO del propio seguimiento y las devoluciones por eliminación.
alim_ingresos AS (
    SELECT DATE(h.fecha_operacion) AS fecha,
           COALESCE(NULLIF(TRIM(ii.nombre), ''), 'Sin especificar') AS nombre,
           SUM(CASE
                 WHEN h.tipo_evento = 'INV_INGRESO'          THEN COALESCE(h.cantidad_kg, 0)
                 WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA' THEN COALESCE(h.cantidad_kg, 0)
                 WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
                 ELSE 0 END)::FLOAT8 AS kg
    FROM lote_registro_historico_unificado h
    JOIN galpones_scope gs ON gs.galpon_id = COALESCE(TRIM(h.galpon_id), '')
    LEFT JOIN item_inventario_ecuador ii ON ii.id = h.item_inventario_ecuador_id
    WHERE h.company_id = p_company_id
      AND h.farm_id    = p_granja_id
      AND NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND NOT (h.tipo_evento = 'INV_INGRESO'
               AND h.referencia IS NOT NULL
               AND h.referencia LIKE 'Seguimiento aves engorde #%')
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
    GROUP BY 1, 2
),

-- 9c. Stock acumulado a la fecha por alimento = ingresos(≤f) − consumo(≤f).
alim_nombres AS (
    SELECT nombre FROM alim_consumo_full
    UNION
    SELECT nombre FROM alim_ingresos
),
alim_stock_dia AS (
    SELECT fx.fecha, n.nombre,
           (COALESCE((SELECT SUM(ai.kg) FROM alim_ingresos ai
                       WHERE ai.nombre = n.nombre AND ai.fecha <= fx.fecha), 0)
          - COALESCE((SELECT SUM(ac.kg) FROM alim_consumo_full ac
                       WHERE ac.nombre = n.nombre AND ac.fecha <= fx.fecha), 0))::FLOAT8 AS stock_kg
    FROM fechas fx
    CROSS JOIN alim_nombres n
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
-- Consumo del día dentro del rango (jsonb + fallback), unificado por alimento.
alim_consumo_dia AS (
    SELECT u.fecha, u.nombre, SUM(u.consumo_kg)::FLOAT8 AS consumo_kg
    FROM (
        SELECT ac.fecha, ac.nombre, ac.consumo_kg FROM alim_consumo  ac
        UNION ALL
        SELECT af.fecha, af.nombre, af.consumo_kg FROM alim_fallback af
    ) u
    GROUP BY u.fecha, u.nombre
),
-- ⭐ v2: FULL JOIN — un alimento con stock en bodega aparece aunque ese día no se haya consumido,
--    que es justamente lo que hacía falta para que el total del reporte cierre con el galpón.
alim_dia AS (
    SELECT COALESCE(c.fecha, s.fecha)   AS fecha,
           COALESCE(c.nombre, s.nombre) AS nombre,
           COALESCE(c.consumo_kg, 0)::FLOAT8 AS consumo_kg,
           COALESCE(s.stock_kg, 0)::FLOAT8   AS stock_kg
    FROM alim_consumo_dia c
    FULL OUTER JOIN alim_stock_dia s ON s.fecha = c.fecha AND s.nombre = c.nombre
    WHERE COALESCE(c.consumo_kg, 0) <> 0
       OR ROUND(COALESCE(s.stock_kg, 0)::NUMERIC, 3) <> 0
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
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin Down: volver a la v1 reintroduciria el stock parcial leido del snapshot jsonb.
            // La funcion es CREATE OR REPLACE, asi que una migracion posterior la reemplaza limpio.
        }
    }
}
