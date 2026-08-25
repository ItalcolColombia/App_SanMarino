-- =============================================================================
-- fn_cuadre_alimento_engorde(p_company_id INT DEFAULT NULL)
--
-- El INVARIANTE del alimento de pollo engorde, por galpón:
--
--     saldo del ciclo activo  ==  stock físico − movimientos posteriores al último seguimiento
--
-- Es la misma verificación con la que se validaron los fixes de jul-2026 (v11, v12, enganche del
-- recálculo y cierre de los huecos del histórico). En ese momento dio Ecuador 35/35 y Panamá 25/25
-- con error 0,0.
--
-- POR QUÉ EXISTE
-- El descuadre que originó todo ese trabajo lo detectó un humano de operación, semanas después de que
-- se produjo. Nada en el sistema lo verificaba. Con esta función el mismo defecto se ve el mismo día,
-- sin depender de que alguien abra un ticket.
--
-- POR QUÉ SE RESTAN LOS MOVIMIENTOS POSTERIORES
-- `saldo_alimento_kg` tiene una fila por día de seguimiento. El alimento que entra DESPUÉS del último
-- día cargado no tiene fila donde reflejarse: la tabla diaria lo muestra como fila de movimiento
-- suelta, pero la última fila de seguimiento no lo incluye. Restarlos es lo que hace comparable el
-- saldo contra el stock de hoy; sin eso, todo galpón con alimento recién recibido daría falso positivo.
-- =============================================================================

DROP FUNCTION IF EXISTS fn_cuadre_alimento_engorde(INT);

CREATE OR REPLACE FUNCTION fn_cuadre_alimento_engorde(p_company_id INT DEFAULT NULL)
RETURNS TABLE (
    company_id            INT,
    empresa               TEXT,
    granja_id             INT,
    granja                TEXT,
    nucleo_id             TEXT,
    galpon_id             TEXT,
    lote_ave_engorde_id   INT,
    lote_nombre           TEXT,
    estado_operativo_lote TEXT,
    ultimo_seguimiento    DATE,
    saldo_tabla_kg        DOUBLE PRECISION,
    mov_post_kg           DOUBLE PRECISION,
    stock_kg              DOUBLE PRECISION,
    esperado_kg           DOUBLE PRECISION,
    descuadre_kg          DOUBLE PRECISION,
    filas_negativas       INT
) LANGUAGE sql STABLE AS $$

WITH ciclos AS (
    SELECT l.lote_ave_engorde_id                    AS lote_id,
           l.company_id,
           l.lote_nombre,
           l.granja_id,
           COALESCE(TRIM(l.nucleo_id), '')          AS nuc,
           COALESCE(TRIM(l.galpon_id), '')          AS gal,
           COALESCE(l.estado_operativo_lote, '')    AS estado,
           sg.seg_max,
           ROW_NUMBER() OVER (PARTITION BY l.granja_id,
                                           COALESCE(TRIM(l.nucleo_id), ''),
                                           COALESCE(TRIM(l.galpon_id), '')
                              ORDER BY sg.seg_max DESC, l.lote_ave_engorde_id DESC) AS rn
    FROM lote_ave_engorde l
    JOIN LATERAL (
        SELECT MAX(s.fecha)::DATE AS seg_max
        FROM seguimiento_diario_aves_engorde s
        WHERE s.lote_ave_engorde_id = l.lote_ave_engorde_id
    ) sg ON TRUE
    WHERE l.deleted_at IS NULL
      AND sg.seg_max IS NOT NULL
      AND COALESCE(TRIM(l.galpon_id), '') <> ''
      AND (p_company_id IS NULL OR l.company_id = p_company_id)
),
activos AS (SELECT * FROM ciclos WHERE rn = 1),

-- Saldo al ÚLTIMO DÍA DE SEGUIMIENTO y cuántas filas quedan negativas, en una sola pasada por lote.
-- ⚠️ Tiene que ser el saldo en `seg_max`, NO el de la última fila que devuelve la fn: cuando hay
-- movimientos posteriores al último seguimiento, la fn agrega filas propias para ellos y ese saldo YA
-- los incluye. Tomarlo de ahí y encima restar `mov_post` los contaría dos veces y daría falsos
-- descuadres (se vio al estrenar esta función: 24/35 en Ecuador donde en realidad es 35/35).
tabla AS (
    SELECT a.lote_id, t.saldo_ultimo, t.negativas
    FROM activos a
    CROSS JOIN LATERAL (
        SELECT (ARRAY_AGG(f.saldo_alimento_kg ORDER BY f.fecha DESC)
                    FILTER (WHERE f.fecha <= a.seg_max))[1]                        AS saldo_ultimo,
               COUNT(*) FILTER (WHERE f.saldo_alimento_kg < -0.001)::INT           AS negativas
        FROM fn_seguimiento_diario_engorde(a.lote_id) f
    ) t
),

-- Alimento que se movió DESPUÉS del último seguimiento: no cabe en la tabla diaria.
post AS (
    SELECT a.lote_id,
           COALESCE((
               SELECT SUM(CASE h.tipo_evento
                          WHEN 'INV_INGRESO'          THEN COALESCE(h.cantidad_kg, 0)
                          WHEN 'INV_TRASLADO_ENTRADA' THEN COALESCE(h.cantidad_kg, 0)
                          WHEN 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
                          -- 25-ago-2026: el ajuste de cuadre también puede caer después del último
                          -- seguimiento, y ahí no cabe en la tabla diaria igual que un ingreso.
                          WHEN 'INV_AJUSTE_CUADRE_ENTRADA' THEN COALESCE(h.cantidad_kg, 0)
                          WHEN 'INV_AJUSTE_CUADRE_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
                          ELSE 0 END)
               FROM lote_registro_historico_unificado h
               WHERE NOT h.anulado
                 AND h.farm_id = a.granja_id
                 AND COALESCE(TRIM(h.nucleo_id), '') = a.nuc
                 AND COALESCE(TRIM(h.galpon_id), '') = a.gal
                 AND DATE(h.fecha_operacion) > a.seg_max
                 AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA',
                                       'INV_AJUSTE_CUADRE_ENTRADA', 'INV_AJUSTE_CUADRE_SALIDA')
                 AND NOT (h.tipo_evento = 'INV_INGRESO'
                          AND h.referencia IS NOT NULL
                          AND h.referencia LIKE 'Seguimiento aves engorde #%')
                 AND NOT (h.referencia IS NOT NULL AND (
                          h.referencia LIKE '%devolución por eliminación%'
                       OR h.referencia LIKE '%devolucion por eliminacion%'))
           ), 0)::FLOAT8 AS mov_post
    FROM activos a
),

stock AS (
    SELECT s.farm_id,
           COALESCE(TRIM(s.nucleo_id), '') AS nuc,
           COALESCE(TRIM(s.galpon_id), '') AS gal,
           SUM(s.quantity)::FLOAT8         AS stock_kg
    FROM inventario_gestion_stock s
    GROUP BY 1, 2, 3
)

SELECT a.company_id,
       co.name::TEXT,
       a.granja_id,
       f.name::TEXT,
       a.nuc::TEXT,
       a.gal::TEXT,
       a.lote_id,
       a.lote_nombre::TEXT,
       a.estado::TEXT,
       a.seg_max,
       COALESCE(t.saldo_ultimo, 0)::FLOAT8                                        AS saldo_tabla_kg,
       p.mov_post                                                                 AS mov_post_kg,
       COALESCE(st.stock_kg, 0)                                                   AS stock_kg,
       (COALESCE(st.stock_kg, 0) - p.mov_post)                                    AS esperado_kg,
       (COALESCE(t.saldo_ultimo, 0) - (COALESCE(st.stock_kg, 0) - p.mov_post))    AS descuadre_kg,
       COALESCE(t.negativas, 0)                                                   AS filas_negativas
FROM activos a
JOIN companies co ON co.id = a.company_id
JOIN farms     f  ON f.id  = a.granja_id
JOIN tabla     t  ON t.lote_id = a.lote_id
JOIN post      p  ON p.lote_id = a.lote_id
LEFT JOIN stock st ON st.farm_id = a.granja_id AND st.nuc = a.nuc AND st.gal = a.gal
ORDER BY ABS(COALESCE(t.saldo_ultimo, 0) - (COALESCE(st.stock_kg, 0) - p.mov_post)) DESC,
         co.name, f.name, a.gal;
$$;

COMMENT ON FUNCTION fn_cuadre_alimento_engorde(INT) IS
'Invariante del alimento de engorde por galpón: saldo del ciclo activo == stock físico − movimientos '
'posteriores al último seguimiento. Un descuadre distinto de 0 señala que la tabla diaria y el '
'inventario dejaron de contar lo mismo.';
