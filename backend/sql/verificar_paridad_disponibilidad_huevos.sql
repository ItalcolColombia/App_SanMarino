-- SIN-MIGRACION: diagnóstico de solo lectura. No crea ni modifica ningún objeto; se corre a mano
-- contra una copia para comprobar que el número que informa el endpoint es el correcto.
--
-- Disponibilidad de huevos (`DisponibilidadLoteService`).
--
-- Contexto: hasta el 2-sep-2026 el camino POR LOTE sumaba sobre la entidad `SeguimientoDiario`, que
-- por `ToTable` apunta a `seguimiento_diario_levante`, filtrando `tipo_seguimiento='produccion'` —
-- condición imposible en esa tabla, así que informaba CERO siempre. Ahora resuelve el LPP del lote y
-- lee `espejo_huevo_produccion`, el mismo origen que ya usaba el camino por LPP.
--
-- Este script comprueba tres cosas:
--   1) que el espejo cuadre con la fórmula directa (producción − traslados Completados);
--   2) que la resolución lote → LPP sea unívoca (si no, el endpoint elegiría un ciclo al azar);
--   3) cuánto cambia lo que ve el usuario respecto del cero que devolvía antes.

\echo === 1) espejo *_dinamico vs formula directa (produccion - traslados Completados) ===
WITH prod AS (
  SELECT lote_postura_produccion_id AS lpp,
         SUM(huevo_tot)        AS tot,   SUM(huevo_inc)        AS inc,
         SUM(huevo_limpio)     AS limpio, SUM(huevo_tratado)   AS tratado,
         SUM(huevo_sucio)      AS sucio,  SUM(huevo_deforme)   AS deforme,
         SUM(huevo_blanco)     AS blanco, SUM(huevo_doble_yema) AS doble_yema,
         SUM(huevo_piso)       AS piso,   SUM(huevo_pequeno)   AS pequeno,
         SUM(huevo_roto)       AS roto,   SUM(huevo_desecho)   AS desecho,
         SUM(huevo_otro)       AS otro
  FROM seguimiento_diario_produccion
  WHERE lote_postura_produccion_id IS NOT NULL
  GROUP BY lote_postura_produccion_id
),
mov AS (
  -- El total se descuenta SIEMPRE desde total_huevos: un traslado por ítems deja el desglose
  -- de las 11 columnas en 0 a propósito, y usarlas para el total dejaría corto el descuento.
  SELECT lote_postura_produccion_id AS lpp,
         SUM(total_huevos)      AS tot,
         SUM(cantidad_limpio)   AS limpio, SUM(cantidad_tratado)    AS tratado,
         SUM(cantidad_sucio)    AS sucio,  SUM(cantidad_deforme)    AS deforme,
         SUM(cantidad_blanco)   AS blanco, SUM(cantidad_doble_yema) AS doble_yema,
         SUM(cantidad_piso)     AS piso,   SUM(cantidad_pequeno)    AS pequeno,
         SUM(cantidad_roto)     AS roto,   SUM(cantidad_desecho)    AS desecho,
         SUM(cantidad_otro)     AS otro
  FROM traslado_huevos
  WHERE estado = 'Completado' AND deleted_at IS NULL AND lote_postura_produccion_id IS NOT NULL
  GROUP BY lote_postura_produccion_id
)
SELECT
  e.lote_postura_produccion_id                                   AS lpp,
  e.huevo_tot_historico                                          AS espejo_historico,
  COALESCE(p.tot,0)                                              AS calc_historico,
  e.huevo_tot_dinamico                                           AS espejo_disponible,
  COALESCE(p.tot,0) - COALESCE(m.tot,0)                          AS calc_disponible,
  (e.huevo_tot_historico IS DISTINCT FROM COALESCE(p.tot,0))     AS difiere_historico,
  (e.huevo_tot_dinamico  IS DISTINCT FROM COALESCE(p.tot,0) - COALESCE(m.tot,0)) AS difiere_disponible
FROM espejo_huevo_produccion e
LEFT JOIN prod p ON p.lpp = e.lote_postura_produccion_id
LEFT JOIN mov  m ON m.lpp = e.lote_postura_produccion_id
ORDER BY e.lote_postura_produccion_id;

\echo === 2) resolucion lote -> LPP: tiene que ser 1:1 (cero filas aca) ===
SELECT lote_id, COUNT(*) AS lpps_vivos
FROM lote_postura_produccion
WHERE deleted_at IS NULL AND lote_id IS NOT NULL
GROUP BY lote_id
HAVING COUNT(*) > 1
ORDER BY 2 DESC;

\echo === 3) que ve el usuario ahora vs el cero de antes, por lote ===
SELECT
  l.lote_id,
  l.lote_nombre,
  p.lote_postura_produccion_id AS lpp,
  0                            AS antes_total,
  e.huevo_tot_dinamico         AS ahora_total,
  e.huevo_tot_historico        AS historico
FROM lotes l
JOIN lote_postura_produccion p ON p.lote_id = l.lote_id AND p.deleted_at IS NULL
LEFT JOIN espejo_huevo_produccion e ON e.lote_postura_produccion_id = p.lote_postura_produccion_id
WHERE l.deleted_at IS NULL AND l.fase = 'Produccion'
ORDER BY l.lote_id;
