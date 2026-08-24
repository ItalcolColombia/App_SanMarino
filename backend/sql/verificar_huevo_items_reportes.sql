-- backend/sql/verificar_huevo_items_reportes.sql
-- SIN-MIGRACION: diagnostico de SOLO LECTURA. No crea ni modifica ningun objeto; se corre a mano
-- contra un dump para verificar que la clasificacion de huevo por items cuadra con los reportes.
--
-- POR QUE EXISTE. La clasificacion por items NO tiene un numero propio en los reportes: el
-- contable, el tecnico de produccion, el diario de costos y el tecnico semanal NO leen
-- metadata->huevoItems — consumen huevo_tot y las 11 columnas fijas. El guardado por items pone
-- huevo_tot = suma del desglose y las 11 en 0. O sea que la coherencia de TODOS los reportes se
-- reduce a un solo invariante: huevo_tot == suma(metadata->huevoItems).
--
-- Verificado el 21-ago-2026 contra la BD local: 0 descuadres, 0 columnas legacy sucias, 0 items
-- fuera de la lista blanca del lote, y el espejo cuadrando exacto con la suma de seguimientos
-- (3.632.634 en Agroavicola Sanmarino).
--
-- Uso: psql ... -f backend/sql/verificar_huevo_items_reportes.sql
--      Todo en 0 / vacio = OK. Cualquier fila devuelta por (1) o (3) es una regresion.

-- Verificacion pedida por el usuario: "que se valide en el reporte contable y otros reportes sean iguales".
-- La clasificacion por items NO tiene un numero propio en los reportes: escribe huevo_tot y deja
-- las 11 columnas en 0. Entonces el invariante a verificar es que huevo_tot == suma del desglose.
\echo '=== 1) INVARIANTE: huevo_tot debe ser igual a la suma de metadata->huevoItems ==='
SELECT
    c.name                                        AS empresa,
    count(*)                                      AS registros_con_items,
    count(*) FILTER (WHERE sd.huevo_tot <> s.suma) AS descuadrados,
    COALESCE(sum(sd.huevo_tot), 0)                AS total_columna,
    COALESCE(sum(s.suma), 0)                      AS total_desglose
FROM seguimiento_diario_produccion sd
JOIN companies c ON c.id = sd.company_id
CROSS JOIN LATERAL (
    SELECT COALESCE(sum((e->>'cantidad')::numeric), 0) AS suma
    FROM jsonb_array_elements(sd.metadata->'huevoItems') e
) s
WHERE sd.metadata ? 'huevoItems'
GROUP BY c.name
ORDER BY c.name;

\echo ''
\echo '=== 2) Las 11 columnas legacy tienen que estar en CERO en los registros por items ==='
SELECT count(*) AS filas_con_columnas_legacy_no_cero
FROM seguimiento_diario_produccion sd
WHERE sd.metadata ? 'huevoItems'
  AND (COALESCE(sd.huevo_limpio,0) + COALESCE(sd.huevo_tratado,0) + COALESCE(sd.huevo_sucio,0)
     + COALESCE(sd.huevo_deforme,0) + COALESCE(sd.huevo_blanco,0) + COALESCE(sd.huevo_doble_yema,0)
     + COALESCE(sd.huevo_piso,0) + COALESCE(sd.huevo_pequeno,0) + COALESCE(sd.huevo_roto,0)
     + COALESCE(sd.huevo_desecho,0) + COALESCE(sd.huevo_otro,0)) <> 0;

\echo ''
\echo '=== 3) F7.3: todo item cargado debe estar declarado por su lote (0 filas = OK) ==='
SELECT sd.id AS seguimiento_id, sd.lote_id, (e->>'catalogItemId')::int AS item_no_declarado
FROM seguimiento_diario_produccion sd
CROSS JOIN LATERAL jsonb_array_elements(sd.metadata->'huevoItems') e
WHERE sd.metadata ? 'huevoItems'
  AND NOT EXISTS (
      SELECT 1 FROM lote_huevo_items lhi
      WHERE lhi.lote_id = sd.lote_id AND lhi.activo
        AND lhi.catalog_item_id = (e->>'catalogItemId')::int)
ORDER BY sd.id
LIMIT 50;

\echo ''
\echo '=== 4) El espejo de huevo debe cuadrar con la suma de los seguimientos ==='
SELECT c.name AS empresa,
       COALESCE(sum(eh.huevo_tot_historico), 0) AS espejo_historico,
       (SELECT COALESCE(sum(sd2.huevo_tot),0) FROM seguimiento_diario_produccion sd2
         WHERE sd2.company_id = c.id)           AS suma_seguimientos
FROM espejo_huevo_produccion eh
JOIN companies c ON c.id = eh.company_id
GROUP BY c.id, c.name
ORDER BY c.name;
