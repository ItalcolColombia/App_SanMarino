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
-- v3 (2026-07-29) — Flag `companies.reporte_costos_alimento_desde_fuentes_reales`:
--   OFF (default) → comportamiento historico intacto (todo del snapshot jsonb).
--   ON  → consumo del SEGUIMIENTO DIARIO + stock de INGRESOS del historico − consumo. El jsonb
--         solo reparte los dias con 2+ alimentos (tipo_alimento los concatena); nunca da un total.
--   Motivo: el jsonb esta INCOMPLETO (1.554.181,4 kg contra 1.706.089,8 kg de consumo real) y su
--   saldo_final solo cubre los alimentos consumidos ESE dia. Es flag y no cambio global porque el
--   desglose necesita que tipo_alimento sea el nombre del item: en Panama lo es, en Ecuador viene
--   con prefijo de sexo ("H: AV. SUPER POLLO ENGORDE") y no cruzaria con el inventario.
--   * consumo por alimento → SEGUIMIENTO DIARIO (tipo_alimento + consumo_dia_kg)
--   * stock por alimento    → INGRESOS del historico (lo que alimenta Gestion de inventario) − consumo
--   * Motivo: el jsonb esta INCOMPLETO — suma 1.554.181,4 kg contra los 1.706.089,8 kg de consumo
--     real del seguimiento. Con las dos fuentes reales el reporte cierra contra inventario y
--     contra la pantalla de seguimiento por lote.
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
--   * Regla del "segundo lote": si p_fecha_inicio es NULL, el reporte arranca
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
-- 1. Lotes del alcance (granja + lote base opcional). Galpón "" = sin galpón.
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

-- 9. Alimentos. Dos comportamientos, decididos por el flag de la empresa
--    `companies.reporte_costos_alimento_desde_fuentes_reales`:
--
--    OFF (default, comportamiento histórico) → todo sale del snapshot jsonb
--        `historico_consumo_alimento`: consumo por ítem y stock = último `saldo_final` del día.
--
--    ON  → las fuentes REALES:
--        * consumo → SEGUIMIENTO DIARIO (`consumo_dia_kg`, el mismo total que la pantalla)
--        * stock   → INGRESOS del histórico (lo que alimenta Gestión de inventario) − consumo
--        El jsonb solo se usa para REPARTIR los días con dos o más alimentos (`tipo_alimento`
--        los concatena con " / " y el reparto real únicamente está ahí); nunca decide un total.
--
--    Por qué es un flag y no un cambio global: el desglose depende de que `tipo_alimento` sea el
--    nombre del ítem. En Panamá lo es; en Ecuador viene con prefijo de sexo
--    ("H: AV. SUPER POLLO ENGORDE") y no cruzaría con el inventario.
--
--    Motivo del cambio: el snapshot está INCOMPLETO — suma 1.554.181,4 kg contra los 1.706.089,8 kg
--    de consumo real, y su `saldo_final` solo existe para los alimentos consumidos ESE día, así que
--    el stock era una fracción del real (G0464 al 22/07: 46.229,2 kg de un galpón con 66.565,8 en
--    tres ítems) y no se movía al recalcularse el saldo.
flag_alim AS (
    SELECT COALESCE((SELECT c.reporte_costos_alimento_desde_fuentes_reales
                       FROM companies c WHERE c.id = p_company_id), FALSE) AS fuentes_reales
),

-- ── Rama OFF: snapshot jsonb (v1, intacta) ───────────────────────────────────
alim_items AS (
    SELECT
        d.fecha,
        d.galpon_id,
        COALESCE(d.seg_id, 0)                                                   AS seg_id,
        COALESCE(NULLIF(TRIM(item->>'nombre_alimento'), ''), 'Sin especificar') AS nombre,
        COALESCE(NULLIF(item->>'consumo', '')::NUMERIC, 0)::FLOAT8              AS consumo,
        NULLIF(item->>'saldo_final', '')::NUMERIC::FLOAT8                       AS saldo_final
    FROM diario d, flag_alim fa
    CROSS JOIN LATERAL jsonb_array_elements(
        CASE WHEN NOT fa.fuentes_reales
              AND jsonb_typeof(d.historico_consumo_alimento) = 'array'
             THEN d.historico_consumo_alimento
             ELSE '[]'::jsonb END
    ) AS item
),
alim_stock_legacy AS (
    SELECT DISTINCT ON (ai.fecha, ai.galpon_id, ai.nombre)
        ai.fecha, ai.galpon_id, ai.nombre, ai.saldo_final
    FROM alim_items ai
    ORDER BY ai.fecha, ai.galpon_id, ai.nombre, ai.seg_id DESC
),
alim_stock_dia_legacy AS (
    SELECT ast.fecha, ast.nombre, SUM(ast.saldo_final)::FLOAT8 AS stock_kg
    FROM alim_stock_legacy ast
    GROUP BY ast.fecha, ast.nombre
),
alim_consumo_legacy AS (
    SELECT ai.fecha, ai.nombre, SUM(ai.consumo)::FLOAT8 AS consumo_kg
    FROM alim_items ai
    GROUP BY ai.fecha, ai.nombre
),
-- Fallback de la rama OFF: filas con consumo pero sin histórico por ítem → tipo_alimento, stock NULL.
alim_fallback_legacy AS (
    SELECT
        d.fecha,
        COALESCE(NULLIF(TRIM(d.tipo_alimento), ''), 'Sin especificar') AS nombre,
        SUM(d.consumo_kg)::FLOAT8                                      AS consumo_kg
    FROM diario d, flag_alim fa
    WHERE NOT fa.fuentes_reales
      AND d.consumo_kg > 0
      AND (CASE
               WHEN d.historico_consumo_alimento IS NULL                    THEN TRUE
               WHEN jsonb_typeof(d.historico_consumo_alimento) <> 'array'   THEN TRUE
               ELSE jsonb_array_length(d.historico_consumo_alimento) = 0
           END)
    GROUP BY d.fecha, COALESCE(NULLIF(TRIM(d.tipo_alimento), ''), 'Sin especificar')
),
alim_dia_legacy AS (
    SELECT u.fecha, u.nombre,
           SUM(u.consumo_kg)::FLOAT8 AS consumo_kg,
           SUM(u.stock_kg)::FLOAT8   AS stock_kg     -- SUM ignora NULL; todo NULL → NULL
    FROM (
        SELECT ac.fecha, ac.nombre, ac.consumo_kg, asd.stock_kg
        FROM alim_consumo_legacy ac
        LEFT JOIN alim_stock_dia_legacy asd ON asd.fecha = ac.fecha AND asd.nombre = ac.nombre
        UNION ALL
        SELECT af.fecha, af.nombre, af.consumo_kg, NULL::FLOAT8
        FROM alim_fallback_legacy af
    ) u
    GROUP BY u.fecha, u.nombre
),

-- ── Rama ON: fuentes reales ──────────────────────────────────────────────────
-- Cada seguimiento aporta su consumo REAL (`consumo_dia_kg`). El nombre sale de `tipo_alimento`;
-- si es compuesto ("A / B") y hay jsonb con reparto utilizable, se reparte con esas proporciones.
alim_base AS (
    SELECT df.fecha, COALESCE(df.seg_id, 0) AS seg_id, df.consumo_kg, df.tipo_alimento,
           CASE WHEN jsonb_typeof(df.historico_consumo_alimento) = 'array'
                THEN df.historico_consumo_alimento ELSE '[]'::jsonb END AS hist,
           (POSITION('/' IN COALESCE(df.tipo_alimento, '')) > 0)        AS compuesto
    FROM diario_full df, flag_alim fa
    WHERE fa.fuentes_reales AND df.consumo_kg <> 0
),
-- Total del jsonb por seguimiento: si es 0 o no hay filas, el reparto no sirve y se trata simple.
alim_base_tot AS (
    SELECT b.*,
           (SELECT SUM(COALESCE(NULLIF(e->>'consumo', '')::NUMERIC, 0))
              FROM jsonb_array_elements(b.hist) e) AS tot_json
    FROM alim_base b
),
alim_consumo_full AS (
    -- Simple, o compuesto sin reparto usable: todo el consumo del día a un solo nombre.
    SELECT bt.fecha,
           COALESCE(NULLIF(TRIM(bt.tipo_alimento), ''), 'Sin especificar') AS nombre,
           SUM(bt.consumo_kg)::FLOAT8                                      AS kg
    FROM alim_base_tot bt
    WHERE NOT bt.compuesto OR COALESCE(bt.tot_json, 0) <= 0
    GROUP BY 1, 2
    UNION ALL
    -- Compuesto con reparto: el TOTAL es el del seguimiento; el jsonb solo da las proporciones.
    SELECT bt.fecha,
           COALESCE(NULLIF(TRIM(e->>'nombre_alimento'), ''), 'Sin especificar') AS nombre,
           SUM(bt.consumo_kg
               * COALESCE(NULLIF(e->>'consumo', '')::NUMERIC, 0)
               / bt.tot_json)::FLOAT8                                          AS kg
    FROM alim_base_tot bt
    CROSS JOIN LATERAL jsonb_array_elements(bt.hist) e
    WHERE bt.compuesto AND COALESCE(bt.tot_json, 0) > 0
    GROUP BY 1, 2
),
-- Consumo del día ya recortado al rango del reporte.
alim_consumo_real AS (
    SELECT acf.fecha, acf.nombre, SUM(acf.kg)::FLOAT8 AS consumo_kg
    FROM alim_consumo_full acf
    CROSS JOIN rango r
    WHERE r.f_ini IS NULL OR acf.fecha >= r.f_ini
    GROUP BY 1, 2
),
-- Ingresos/traslados por fecha × alimento en los galpones del alcance. Mismos filtros que
-- fn_seguimiento_diario_engorde para que los totales cierren: se excluyen los INV_INGRESO del
-- propio seguimiento y las devoluciones por eliminación.
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
    CROSS JOIN flag_alim fa
    LEFT JOIN item_inventario ii ON ii.id = h.item_inventario_id
    WHERE fa.fuentes_reales
      AND h.company_id = p_company_id
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
-- Stock acumulado a la fecha = ingresos(≤f) − consumo(≤f). El consumo se toma SIN recortar por
-- f_ini: el stock es un estado acumulado, no un evento del día.
alim_nombres AS (
    SELECT nombre FROM alim_consumo_full
    UNION
    SELECT nombre FROM alim_ingresos
),
alim_stock_real AS (
    SELECT fx.fecha, n.nombre,
           (COALESCE((SELECT SUM(ai.kg) FROM alim_ingresos ai
                       WHERE ai.nombre = n.nombre AND ai.fecha <= fx.fecha), 0)
          - COALESCE((SELECT SUM(ac.kg) FROM alim_consumo_full ac
                       WHERE ac.nombre = n.nombre AND ac.fecha <= fx.fecha), 0))::FLOAT8 AS stock_kg
    FROM fechas fx
    CROSS JOIN alim_nombres n
),
-- FULL JOIN: un alimento con stock en bodega aparece aunque ese día no se consuma. Sin eso el
-- total del reporte no podría cerrar contra el galpón.
alim_dia_real AS (
    SELECT COALESCE(c.fecha, s.fecha)   AS fecha,
           COALESCE(c.nombre, s.nombre) AS nombre,
           COALESCE(c.consumo_kg, 0)::FLOAT8 AS consumo_kg,
           COALESCE(s.stock_kg, 0)::FLOAT8   AS stock_kg
    FROM alim_consumo_real c
    FULL OUTER JOIN alim_stock_real s ON s.fecha = c.fecha AND s.nombre = c.nombre
    WHERE COALESCE(c.consumo_kg, 0) <> 0
       OR ROUND(COALESCE(s.stock_kg, 0)::NUMERIC, 3) <> 0
),

-- Una sola de las dos ramas trae filas (la otra queda vacía por el flag).
alim_dia AS (
    SELECT fecha, nombre, consumo_kg, stock_kg FROM alim_dia_legacy
    UNION ALL
    SELECT fecha, nombre, consumo_kg, stock_kg FROM alim_dia_real
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
