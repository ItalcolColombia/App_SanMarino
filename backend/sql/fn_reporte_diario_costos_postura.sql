-- =============================================================================
-- fn_reporte_diario_costos_postura(p_company_id, p_granja_ids, p_regional,
--                                  p_lote_postura_base_id, p_fase,
--                                  p_fecha_desde, p_fecha_hasta)
--
-- Reporte DIARIO del área de costos para POSTURA (levante + producción), a nivel
-- lote:galpón. Devuelve una fila por (fecha, lote, fase) con:
--   * bajas del día por sexo: mortalidad, selección, error de sexaje y venta de aves,
--   * alimento del día desglosado POR ÍTEM y por sexo (json),
--   * las 11 categorías crudas de huevo + los movimientos de huevo del día
--     (venta y traslado a planta). Las filas de LEVANTE traen los huevos en 0.
--
-- ⚠️ NO es el reporte de engorde. No comparte una sola línea de aritmética con
--    fn_reporte_diario_costos_engorde; solo se le copió la forma (fn que agrega en
--    la BD + service delgado).
--
-- v1 (2026-08-07) — Diseño:
--   * PRODUCCIÓN: no reimplementa nada — CROSS JOIN LATERAL
--     fn_seguimiento_diario_produccion(NULL, lote_id), la fn diaria CANÓNICA
--     (dedup por día Bogotá, universo seguimientos ∪ movimientos) ⇒ el reporte
--     cuadra 1:1 con la pantalla de seguimiento de producción.
--     Las dos columnas que esa fn NO expone (venta_aves_hembras/machos) se
--     recuperan con LEFT JOIN por seg_id a seguimiento_diario_produccion.
--     Motivo: la venta de aves de la carga masiva vive en esas columnas; el
--     mov_venta_h/m de la fn sale de movimiento_aves y es 0 para esos lotes.
--   * LEVANTE: NO existe fn diaria canónica (fn_indicadores_levante_postura es
--     SEMANAL) ⇒ se lee seguimiento_diario_levante acá adentro, con el MISMO
--     criterio de día que la fn de producción: DISTINCT ON del día calendario
--     America/Bogota, gana el timestamp más temprano. Este es el ÚNICO lugar con
--     ese criterio para levante; si algún día nace fn_seguimiento_diario_levante,
--     esta fn debe re-sourcearse sobre ella y verificarse byte a byte.
--   * NO clasifica el huevo: devuelve las 11 categorías crudas. La agrupación
--     fértil/comercial/inservible tiene UN SOLO dueño y es C# puro y testeado
--     (ReporteDiarioCostosPosturaCalculos.ClasificarHuevo). Calcularla también
--     acá sería la segunda implementación del mismo número.
--   * Alimento: explode de metadata->'itemsHembras'/'itemsMachos'
--     ([{nombre, cantidad, unidad, ...}]) ⇒ una entrada por ítem (decisión D4).
--     Fallback para filas sin metadata: 1 entrada por sexo con el nombre partido
--     de tipo_alimento ("H: x / M: y") y el total de consumo_kg_*. Nunca se
--     descarta consumo: si no hay ítems pero hay kg, sale la entrada de fallback.
--   * Corte de día SIEMPRE AT TIME ZONE 'America/Bogota' (date_trunc crudo sobre
--     timestamptz usa la zona de la SESIÓN: local Bogotá vs prod UTC ⇒ corrimiento).
--   * Alcance fail-closed: p_granja_ids lo arma el service con las granjas
--     asignadas al usuario. Array vacío ⇒ 0 filas (jamás "todas").
--     p_granja_ids NULL ⇒ todas las granjas de la empresa (uso interno/scripts).
--   * p_fase NULL ⇒ ambas fases. 'lev%' ⇒ Levante, 'produc%' ⇒ Produccion
--     (acepta 'produccion' y 'producción').
-- =============================================================================

CREATE OR REPLACE FUNCTION fn_reporte_diario_costos_postura(
    p_company_id           INT,
    p_granja_ids           INT[] DEFAULT NULL,
    p_regional             TEXT  DEFAULT NULL,
    p_lote_postura_base_id INT   DEFAULT NULL,
    p_fase                 TEXT  DEFAULT NULL,
    p_fecha_desde          DATE  DEFAULT NULL,
    p_fecha_hasta          DATE  DEFAULT NULL
)
RETURNS TABLE (
    fecha                  DATE,
    fase                   TEXT,
    lote_id                INT,
    lote_nombre            TEXT,
    galpon_id              TEXT,
    galpon_nombre          TEXT,
    nucleo_id              TEXT,
    granja_id              INT,
    granja_nombre          TEXT,
    regional               TEXT,
    lote_postura_base_id   INT,
    lote_base_nombre       TEXT,
    edad_dias              INT,
    semana                 INT,
    -- Pestaña 1 — Aves
    mortalidad_h           INT,
    mortalidad_m           INT,
    seleccion_h            INT,
    seleccion_m            INT,
    error_sexaje_h         INT,
    error_sexaje_m         INT,
    venta_aves_h           INT,
    venta_aves_m           INT,
    -- Pestaña 2 — Alimento
    consumo_kg_h           FLOAT8,
    consumo_kg_m           FLOAT8,
    alimentos              TEXT,   -- [{sexo:'H'|'M', nombre, cantidad_kg, origen:'metadata'|'tipo_alimento'}]
    -- Pestaña 3 — Huevos (0 en filas de levante)
    huevo_tot              INT,
    huevo_inc              INT,
    huevo_limpio           INT,
    huevo_tratado          INT,
    huevo_sucio            INT,
    huevo_deforme          INT,
    huevo_blanco           INT,
    huevo_doble_yema       INT,
    huevo_piso             INT,
    huevo_pequeno          INT,
    huevo_roto             INT,
    huevo_desecho          INT,
    huevo_otro             INT,
    huevo_venta            INT,
    huevo_traslado_planta  INT
) LANGUAGE sql STABLE AS $$

WITH
-- 0. Normalización de la fase pedida.
cfg AS (
    SELECT CASE
             WHEN NULLIF(TRIM(COALESCE(p_fase, '')), '') IS NULL THEN NULL
             WHEN lower(TRIM(p_fase)) LIKE 'lev%'    THEN 'Levante'
             WHEN lower(TRIM(p_fase)) LIKE 'produc%' THEN 'Produccion'
             ELSE NULL
           END AS fase_pedida
),

-- 1. Lotes del alcance. Fail-closed por empresa; regional/granja/lote base opcionales.
lotes_scope AS MATERIALIZED (
    SELECT
        l.lote_id                                                        AS lote_id,
        COALESCE(l.lote_nombre, '')::text                                AS lote_nombre,
        COALESCE(TRIM(l.galpon_id), '')::text                            AS galpon_id,
        COALESCE(
            NULLIF(TRIM(g.galpon_nombre), ''),
            NULLIF(TRIM(l.galpon_id), ''),
            'Sin galpón'
        )::text                                                          AS galpon_nombre,
        COALESCE(TRIM(l.nucleo_id), '')::text                            AS nucleo_id,
        l.granja_id                                                      AS granja_id,
        COALESCE(f.name, '')::text                                       AS granja_nombre,
        COALESCE(mlo.value, '')::text                                    AS regional,
        l.lote_postura_base_id                                           AS lote_postura_base_id,
        COALESCE(b.lote_nombre, '')::text                                AS lote_base_nombre,
        (l.fecha_encaset AT TIME ZONE 'America/Bogota')::date            AS fecha_encaset
    FROM lotes l
    JOIN farms f
      ON f.id = l.granja_id
     AND f.deleted_at IS NULL
    LEFT JOIN galpones g
      ON g.galpon_id = l.galpon_id
     AND g.deleted_at IS NULL
    LEFT JOIN master_list_options mlo
      ON mlo.id = f.regional_id
    LEFT JOIN lote_postura_base b
      ON b.lote_postura_base_id = l.lote_postura_base_id
     AND b.deleted_at IS NULL
    WHERE l.company_id  = p_company_id
      AND l.deleted_at IS NULL
      AND (p_granja_ids IS NULL OR l.granja_id = ANY (p_granja_ids))
      AND (NULLIF(TRIM(COALESCE(p_regional, '')), '') IS NULL
           OR TRIM(COALESCE(mlo.value, '')) = TRIM(p_regional))
      AND (p_lote_postura_base_id IS NULL
           OR l.lote_postura_base_id = p_lote_postura_base_id)
),

-- 2. LEVANTE — un registro por lote y día calendario Bogotá (gana el más temprano).
--    Mismo criterio de dedup que fn_seguimiento_diario_produccion.
lev_dedup AS (
    SELECT DISTINCT ON (s.lote_id_int, (s.fecha AT TIME ZONE 'America/Bogota')::date)
           (s.fecha AT TIME ZONE 'America/Bogota')::date  AS d,
           s.lote_id_int                                  AS lote_id,
           s.mortalidad_hembras, s.mortalidad_machos,
           s.sel_h, s.sel_m,
           s.error_sexaje_hembras, s.error_sexaje_machos,
           s.venta_aves_hembras, s.venta_aves_machos,
           s.consumo_kg_hembras, s.consumo_kg_machos,
           s.tipo_alimento,
           s.metadata
    FROM seguimiento_diario_levante s
    WHERE s.lote_id_int IS NOT NULL
      AND EXISTS (SELECT 1 FROM lotes_scope ls WHERE ls.lote_id = s.lote_id_int)
      AND EXISTS (SELECT 1 FROM cfg WHERE fase_pedida IS NULL OR fase_pedida = 'Levante')
    ORDER BY s.lote_id_int,
             (s.fecha AT TIME ZONE 'America/Bogota')::date,
             s.fecha,
             s.id
),
lev AS (
    SELECT
        'Levante'::text                          AS fase,
        v.d                                      AS d,
        ls.*,
        COALESCE(v.mortalidad_hembras, 0)        AS mortalidad_h,
        COALESCE(v.mortalidad_machos, 0)         AS mortalidad_m,
        COALESCE(v.sel_h, 0)                     AS seleccion_h,
        COALESCE(v.sel_m, 0)                     AS seleccion_m,
        COALESCE(v.error_sexaje_hembras, 0)      AS error_sexaje_h,
        COALESCE(v.error_sexaje_machos, 0)       AS error_sexaje_m,
        COALESCE(v.venta_aves_hembras, 0)        AS venta_aves_h,
        COALESCE(v.venta_aves_machos, 0)         AS venta_aves_m,
        COALESCE(v.consumo_kg_hembras, 0)::float8 AS consumo_kg_h,
        COALESCE(v.consumo_kg_machos, 0)::float8  AS consumo_kg_m,
        v.tipo_alimento::text                    AS tipo_alimento,
        v.metadata                               AS metadata,
        0 AS huevo_tot, 0 AS huevo_inc, 0 AS huevo_limpio, 0 AS huevo_tratado,
        0 AS huevo_sucio, 0 AS huevo_deforme, 0 AS huevo_blanco, 0 AS huevo_doble_yema,
        0 AS huevo_piso, 0 AS huevo_pequeno, 0 AS huevo_roto, 0 AS huevo_desecho,
        0 AS huevo_otro
    FROM lev_dedup v
    JOIN lotes_scope ls ON ls.lote_id = v.lote_id
),

-- 3. PRODUCCIÓN — se delega en la fn diaria canónica (una llamada por lote).
--    Solo se llama para lotes que tienen seguimiento de producción.
lotes_con_produccion AS (
    SELECT ls.lote_id
    FROM lotes_scope ls
    WHERE EXISTS (SELECT 1 FROM cfg WHERE fase_pedida IS NULL OR fase_pedida = 'Produccion')
      AND EXISTS (
            SELECT 1 FROM seguimiento_diario_produccion sp
            WHERE sp.lote_id = ls.lote_id AND sp.deleted_at IS NULL)
),
prod AS (
    SELECT
        'Produccion'::text                       AS fase,
        fn.fecha                                 AS d,
        ls.*,
        COALESCE(fn.mortalidad_hembras, 0)       AS mortalidad_h,
        COALESCE(fn.mortalidad_machos, 0)        AS mortalidad_m,
        COALESCE(fn.sel_h, 0)                    AS seleccion_h,
        COALESCE(fn.sel_m, 0)                    AS seleccion_m,
        COALESCE(fn.error_sexaje_hembras, 0)     AS error_sexaje_h,
        COALESCE(fn.error_sexaje_machos, 0)      AS error_sexaje_m,
        -- La fn no expone estas dos (su mov_venta_* sale de movimiento_aves).
        COALESCE(sp.venta_aves_hembras, 0)       AS venta_aves_h,
        COALESCE(sp.venta_aves_machos, 0)        AS venta_aves_m,
        COALESCE(fn.cons_kg_h, 0)::float8        AS consumo_kg_h,
        COALESCE(fn.cons_kg_m, 0)::float8        AS consumo_kg_m,
        fn.tipo_alimento                         AS tipo_alimento,
        fn.metadata                              AS metadata,
        COALESCE(fn.huevo_tot, 0)        AS huevo_tot,
        COALESCE(fn.huevo_inc, 0)        AS huevo_inc,
        COALESCE(fn.huevo_limpio, 0)     AS huevo_limpio,
        COALESCE(fn.huevo_tratado, 0)    AS huevo_tratado,
        COALESCE(fn.huevo_sucio, 0)      AS huevo_sucio,
        COALESCE(fn.huevo_deforme, 0)    AS huevo_deforme,
        COALESCE(fn.huevo_blanco, 0)     AS huevo_blanco,
        COALESCE(fn.huevo_doble_yema, 0) AS huevo_doble_yema,
        COALESCE(fn.huevo_piso, 0)       AS huevo_piso,
        COALESCE(fn.huevo_pequeno, 0)    AS huevo_pequeno,
        COALESCE(fn.huevo_roto, 0)       AS huevo_roto,
        COALESCE(fn.huevo_desecho, 0)    AS huevo_desecho,
        COALESCE(fn.huevo_otro, 0)       AS huevo_otro
    FROM lotes_con_produccion lcp
    JOIN lotes_scope ls ON ls.lote_id = lcp.lote_id
    CROSS JOIN LATERAL fn_seguimiento_diario_produccion(NULL, lcp.lote_id) fn
    LEFT JOIN seguimiento_diario_produccion sp ON sp.id = fn.seg_id
),

-- 4. Universo unificado, ya recortado al rango pedido.
base AS (
    SELECT * FROM lev
    UNION ALL
    SELECT * FROM prod
),
base_rango AS (
    SELECT * FROM base
    WHERE (p_fecha_desde IS NULL OR d >= p_fecha_desde)
      AND (p_fecha_hasta IS NULL OR d <= p_fecha_hasta)
),

-- 5. Alimento por ÍTEM (decisión D4). Explode del metadata; fallback por sexo.
--
--    ⚠️ El metadata tiene DOS formas según el camino de captura del consumo:
--      · camino 2 (inventario):  {"nombre": "...", "itemInventarioEcuadorId": 180}  → trae nombre
--      · camino 1 (catálogo):    {"catalogItemId": 70}                             → NO trae nombre
--    Los lotes viejos (K345) son camino 1: sin resolver el id contra el catálogo, la columna
--    "tipo alimento" salía «Sin especificar» y el reporte quedaba inútil para costear.
items_metadata AS (
    SELECT b.fase, b.lote_id, b.d, 'H'::text AS sexo,
           COALESCE(
               NULLIF(TRIM(COALESCE(it->>'nombre', '')), ''),
               NULLIF(TRIM(COALESCE(iie.nombre, '')), ''),
               NULLIF(TRIM(COALESCE(ci.nombre, '')), '')
           )                                                      AS nombre,
           COALESCE((it->>'cantidad')::float8, 0)                 AS cantidad_kg
    FROM base_rango b
    CROSS JOIN LATERAL jsonb_array_elements(
        CASE WHEN jsonb_typeof(b.metadata -> 'itemsHembras') = 'array'
             THEN b.metadata -> 'itemsHembras' ELSE '[]'::jsonb END) it
    LEFT JOIN item_inventario_ecuador iie
           ON iie.id = NULLIF(it->>'itemInventarioEcuadorId', '')::int
    LEFT JOIN catalogo_items ci
           ON ci.id = NULLIF(it->>'catalogItemId', '')::int
    UNION ALL
    SELECT b.fase, b.lote_id, b.d, 'M'::text,
           COALESCE(
               NULLIF(TRIM(COALESCE(it->>'nombre', '')), ''),
               NULLIF(TRIM(COALESCE(iie.nombre, '')), ''),
               NULLIF(TRIM(COALESCE(ci.nombre, '')), '')
           ),
           COALESCE((it->>'cantidad')::float8, 0)
    FROM base_rango b
    CROSS JOIN LATERAL jsonb_array_elements(
        CASE WHEN jsonb_typeof(b.metadata -> 'itemsMachos') = 'array'
             THEN b.metadata -> 'itemsMachos' ELSE '[]'::jsonb END) it
    LEFT JOIN item_inventario_ecuador iie
           ON iie.id = NULLIF(it->>'itemInventarioEcuadorId', '')::int
    LEFT JOIN catalogo_items ci
           ON ci.id = NULLIF(it->>'catalogItemId', '')::int
),
items_fallback AS (
    -- Sin ítems en metadata pero CON kg consumidos: se parte tipo_alimento, que tiene DOS
    -- formatos vivos: "H: x + y / M: z" (levante/producción nuevos) y "x / y" (lotes viejos,
    -- sin prefijo de sexo). Si no matchea ninguno, el nombre completo va a los dos sexos.
    SELECT b.fase, b.lote_id, b.d, 'H'::text AS sexo,
           NULLIF(TRIM(CASE
               WHEN COALESCE(b.tipo_alimento, '') LIKE '%/ M:%'
                   THEN regexp_replace(split_part(b.tipo_alimento, '/ M:', 1), '^\s*H\s*:\s*', '')
               WHEN COALESCE(b.tipo_alimento, '') LIKE '% / %'
                   THEN split_part(b.tipo_alimento, ' / ', 1)
               ELSE COALESCE(b.tipo_alimento, '')
           END), '')                                              AS nombre,
           b.consumo_kg_h                                          AS cantidad_kg
    FROM base_rango b
    WHERE b.consumo_kg_h <> 0
      AND NOT EXISTS (SELECT 1 FROM items_metadata i
                      WHERE i.fase = b.fase AND i.lote_id = b.lote_id
                        AND i.d = b.d AND i.sexo = 'H')
    UNION ALL
    SELECT b.fase, b.lote_id, b.d, 'M'::text,
           NULLIF(TRIM(CASE
               WHEN COALESCE(b.tipo_alimento, '') LIKE '%/ M:%'
                   THEN split_part(b.tipo_alimento, '/ M:', 2)
               WHEN COALESCE(b.tipo_alimento, '') LIKE '% / %'
                   THEN split_part(b.tipo_alimento, ' / ', 2)
               ELSE COALESCE(b.tipo_alimento, '')
           END), ''),
           b.consumo_kg_m
    FROM base_rango b
    WHERE b.consumo_kg_m <> 0
      AND NOT EXISTS (SELECT 1 FROM items_metadata i
                      WHERE i.fase = b.fase AND i.lote_id = b.lote_id
                        AND i.d = b.d AND i.sexo = 'M')
),
items_all AS (
    SELECT fase, lote_id, d, sexo, nombre, cantidad_kg, 'metadata'::text AS origen
    FROM items_metadata
    UNION ALL
    SELECT fase, lote_id, d, sexo, nombre, cantidad_kg, 'tipo_alimento'::text
    FROM items_fallback
),
alimentos_json AS (
    SELECT i.fase, i.lote_id, i.d,
           json_agg(
               json_build_object(
                   'sexo',        i.sexo,
                   'nombre',      COALESCE(i.nombre, 'Sin especificar'),
                   'cantidad_kg', i.cantidad_kg,
                   'origen',      i.origen)
               ORDER BY i.sexo, COALESCE(i.nombre, 'Sin especificar')
           )::text AS alimentos
    FROM items_all i
    GROUP BY i.fase, i.lote_id, i.d
),

-- 6. Movimientos de huevo del día (solo Completado — criterio del Reporte Contable).
mov_huevo AS (
    SELECT th.lote_id::text                       AS lote_txt,
           th.fecha_traslado::date                AS d,
           SUM(CASE WHEN th.tipo_operacion = 'Venta' THEN
                   th.cantidad_limpio + th.cantidad_tratado + th.cantidad_sucio
                 + th.cantidad_deforme + th.cantidad_blanco + th.cantidad_doble_yema
                 + th.cantidad_piso + th.cantidad_pequeno + th.cantidad_roto
                 + th.cantidad_desecho + th.cantidad_otro
               ELSE 0 END)::int                   AS huevo_venta,
           SUM(CASE WHEN th.tipo_operacion = 'Traslado' AND th.tipo_destino = 'Planta' THEN
                   th.cantidad_limpio + th.cantidad_tratado + th.cantidad_sucio
                 + th.cantidad_deforme + th.cantidad_blanco + th.cantidad_doble_yema
                 + th.cantidad_piso + th.cantidad_pequeno + th.cantidad_roto
                 + th.cantidad_desecho + th.cantidad_otro
               ELSE 0 END)::int                   AS huevo_traslado_planta
    FROM traslado_huevos th
    WHERE th.company_id = p_company_id
      AND th.deleted_at IS NULL
      AND th.estado = 'Completado'
      AND (p_fecha_desde IS NULL OR th.fecha_traslado::date >= p_fecha_desde)
      AND (p_fecha_hasta IS NULL OR th.fecha_traslado::date <= p_fecha_hasta)
    GROUP BY th.lote_id::text, th.fecha_traslado::date
)

SELECT
    b.d                                                   AS fecha,
    b.fase,
    b.lote_id,
    b.lote_nombre,
    b.galpon_id,
    b.galpon_nombre,
    b.nucleo_id,
    b.granja_id,
    b.granja_nombre,
    b.regional,
    b.lote_postura_base_id,
    b.lote_base_nombre,
    CASE WHEN b.fecha_encaset IS NULL THEN NULL
         ELSE (b.d - b.fecha_encaset)::int END            AS edad_dias,
    CASE WHEN b.fecha_encaset IS NULL THEN NULL
         ELSE (((b.d - b.fecha_encaset) / 7) + 1)::int END AS semana,
    b.mortalidad_h, b.mortalidad_m,
    b.seleccion_h,  b.seleccion_m,
    b.error_sexaje_h, b.error_sexaje_m,
    b.venta_aves_h, b.venta_aves_m,
    b.consumo_kg_h, b.consumo_kg_m,
    aj.alimentos,
    b.huevo_tot, b.huevo_inc, b.huevo_limpio, b.huevo_tratado,
    b.huevo_sucio, b.huevo_deforme, b.huevo_blanco, b.huevo_doble_yema,
    b.huevo_piso, b.huevo_pequeno, b.huevo_roto, b.huevo_desecho, b.huevo_otro,
    -- Los movimientos de huevo son del LOTE, no de la fase: solo se imputan a
    -- las filas de producción (en levante la pestaña de huevos no se muestra).
    CASE WHEN b.fase = 'Produccion' THEN COALESCE(mh.huevo_venta, 0) ELSE 0 END,
    CASE WHEN b.fase = 'Produccion' THEN COALESCE(mh.huevo_traslado_planta, 0) ELSE 0 END
FROM base_rango b
LEFT JOIN alimentos_json aj
       ON aj.fase = b.fase AND aj.lote_id = b.lote_id AND aj.d = b.d
LEFT JOIN mov_huevo mh
       ON mh.lote_txt = b.lote_id::text AND mh.d = b.d
ORDER BY b.d, b.lote_nombre, b.galpon_id, b.fase;

$$;

COMMENT ON FUNCTION fn_reporte_diario_costos_postura(INT, INT[], TEXT, INT, TEXT, DATE, DATE)
IS 'Reporte diario del área de costos para postura (levante + producción) por lote:galpón. '
   'Producción delega en fn_seguimiento_diario_produccion; levante lee seguimiento_diario_levante '
   'con el mismo corte de día America/Bogota. Devuelve el huevo CRUDO: la agrupación '
   'fértil/comercial/inservible vive en ReporteDiarioCostosPosturaCalculos (C# puro y testeado).';
