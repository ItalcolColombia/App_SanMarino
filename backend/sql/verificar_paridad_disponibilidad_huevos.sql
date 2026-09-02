-- SIN-MIGRACION: diagnóstico de solo lectura. No crea ni modifica ningún objeto; se corre a mano
-- contra una copia para comparar dos formas de calcular el mismo número.
--
-- Paridad del cálculo de huevos disponibles (DisponibilidadLoteService).
--
-- Antes, el service traía TODAS las filas del lote y sumaba en memoria con `Sum(x => x.Campo ?? 0)`
-- —o sea, NULL contaba como 0 y después se sumaba. Ahora suma en la BD con `SUM(campo)`, que
-- IGNORA los NULL, y envuelve el resultado en COALESCE(...,0) para el caso de cero filas.
--
-- Este script compara las dos formulaciones LOTE POR LOTE sobre los datos reales. La condición de
-- aceptación es que la última columna diga 0 en todas las filas: cualquier otra cosa significa que
-- el refactor movió un número y no se mergea.

\echo === 1) seguimiento_diario_levante: memoria vs BD, lote por lote ===
-- Nota: el service filtra tipo_seguimiento='produccion' sobre esta tabla, que solo contiene
-- 'levante' => hoy ese filtro no devuelve ninguna fila. Se compara SIN el filtro para que la
-- verificacion corra sobre datos reales y pruebe la aritmetica del refactor.
WITH por_lote AS (
  SELECT
    lote_id,
    -- formulación VIEJA (semántica en memoria): COALESCE por fila y después sumar
    SUM(COALESCE(huevo_limpio,0))     AS viejo_limpio,
    SUM(COALESCE(huevo_tratado,0))    AS viejo_tratado,
    SUM(COALESCE(huevo_sucio,0))      AS viejo_sucio,
    SUM(COALESCE(huevo_deforme,0))    AS viejo_deforme,
    SUM(COALESCE(huevo_blanco,0))     AS viejo_blanco,
    SUM(COALESCE(huevo_doble_yema,0)) AS viejo_doble_yema,
    SUM(COALESCE(huevo_piso,0))       AS viejo_piso,
    SUM(COALESCE(huevo_pequeno,0))    AS viejo_pequeno,
    SUM(COALESCE(huevo_roto,0))       AS viejo_roto,
    SUM(COALESCE(huevo_desecho,0))    AS viejo_desecho,
    SUM(COALESCE(huevo_otro,0))       AS viejo_otro,
    -- formulación NUEVA (la que emite EF): SUM que ignora NULL, con COALESCE al final
    COALESCE(SUM(huevo_limpio),0)     AS nuevo_limpio,
    COALESCE(SUM(huevo_tratado),0)    AS nuevo_tratado,
    COALESCE(SUM(huevo_sucio),0)      AS nuevo_sucio,
    COALESCE(SUM(huevo_deforme),0)    AS nuevo_deforme,
    COALESCE(SUM(huevo_blanco),0)     AS nuevo_blanco,
    COALESCE(SUM(huevo_doble_yema),0) AS nuevo_doble_yema,
    COALESCE(SUM(huevo_piso),0)       AS nuevo_piso,
    COALESCE(SUM(huevo_pequeno),0)    AS nuevo_pequeno,
    COALESCE(SUM(huevo_roto),0)       AS nuevo_roto,
    COALESCE(SUM(huevo_desecho),0)    AS nuevo_desecho,
    COALESCE(SUM(huevo_otro),0)       AS nuevo_otro,
    COUNT(*)                          AS filas,
    MAX(fecha)                        AS ultima_fecha
  FROM seguimiento_diario_levante   -- la entidad SeguimientoDiario mapea ACA (ToTable)
  GROUP BY lote_id
)
SELECT
  COUNT(*)                                    AS lotes_comparados,
  SUM(filas)                                  AS filas_agregadas,
  COUNT(*) FILTER (WHERE ultima_fecha IS NULL AND filas > 0) AS max_fecha_nula_con_filas,
  COUNT(*) FILTER (WHERE
        viejo_limpio     IS DISTINCT FROM nuevo_limpio
     OR viejo_tratado    IS DISTINCT FROM nuevo_tratado
     OR viejo_sucio      IS DISTINCT FROM nuevo_sucio
     OR viejo_deforme    IS DISTINCT FROM nuevo_deforme
     OR viejo_blanco     IS DISTINCT FROM nuevo_blanco
     OR viejo_doble_yema IS DISTINCT FROM nuevo_doble_yema
     OR viejo_piso       IS DISTINCT FROM nuevo_piso
     OR viejo_pequeno    IS DISTINCT FROM nuevo_pequeno
     OR viejo_roto       IS DISTINCT FROM nuevo_roto
     OR viejo_desecho    IS DISTINCT FROM nuevo_desecho
     OR viejo_otro       IS DISTINCT FROM nuevo_otro
  ) AS lotes_con_diferencia
FROM por_lote;

\echo === 2) traslado_huevos (Completado): memoria vs BD, lote por lote ===
WITH por_lote AS (
  SELECT
    lote_id,
    SUM(COALESCE(cantidad_limpio,0))     AS viejo_limpio,
    SUM(COALESCE(cantidad_tratado,0))    AS viejo_tratado,
    SUM(COALESCE(cantidad_sucio,0))      AS viejo_sucio,
    SUM(COALESCE(cantidad_deforme,0))    AS viejo_deforme,
    SUM(COALESCE(cantidad_blanco,0))     AS viejo_blanco,
    SUM(COALESCE(cantidad_doble_yema,0)) AS viejo_doble_yema,
    SUM(COALESCE(cantidad_piso,0))       AS viejo_piso,
    SUM(COALESCE(cantidad_pequeno,0))    AS viejo_pequeno,
    SUM(COALESCE(cantidad_roto,0))       AS viejo_roto,
    SUM(COALESCE(cantidad_desecho,0))    AS viejo_desecho,
    SUM(COALESCE(cantidad_otro,0))       AS viejo_otro,
    COALESCE(SUM(cantidad_limpio),0)     AS nuevo_limpio,
    COALESCE(SUM(cantidad_tratado),0)    AS nuevo_tratado,
    COALESCE(SUM(cantidad_sucio),0)      AS nuevo_sucio,
    COALESCE(SUM(cantidad_deforme),0)    AS nuevo_deforme,
    COALESCE(SUM(cantidad_blanco),0)     AS nuevo_blanco,
    COALESCE(SUM(cantidad_doble_yema),0) AS nuevo_doble_yema,
    COALESCE(SUM(cantidad_piso),0)       AS nuevo_piso,
    COALESCE(SUM(cantidad_pequeno),0)    AS nuevo_pequeno,
    COALESCE(SUM(cantidad_roto),0)       AS nuevo_roto,
    COALESCE(SUM(cantidad_desecho),0)    AS nuevo_desecho,
    COALESCE(SUM(cantidad_otro),0)       AS nuevo_otro,
    COUNT(*)                             AS filas
  FROM traslado_huevos
  WHERE estado = 'Completado'
  GROUP BY lote_id
)
SELECT
  COUNT(*)   AS lotes_comparados,
  SUM(filas) AS filas_agregadas,
  COUNT(*) FILTER (WHERE
        viejo_limpio     IS DISTINCT FROM nuevo_limpio
     OR viejo_tratado    IS DISTINCT FROM nuevo_tratado
     OR viejo_sucio      IS DISTINCT FROM nuevo_sucio
     OR viejo_deforme    IS DISTINCT FROM nuevo_deforme
     OR viejo_blanco     IS DISTINCT FROM nuevo_blanco
     OR viejo_doble_yema IS DISTINCT FROM nuevo_doble_yema
     OR viejo_piso       IS DISTINCT FROM nuevo_piso
     OR viejo_pequeno    IS DISTINCT FROM nuevo_pequeno
     OR viejo_roto       IS DISTINCT FROM nuevo_roto
     OR viejo_desecho    IS DISTINCT FROM nuevo_desecho
     OR viejo_otro       IS DISTINCT FROM nuevo_otro
  ) AS lotes_con_diferencia
FROM por_lote;
