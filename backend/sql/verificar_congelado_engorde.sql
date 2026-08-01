-- =============================================================================
-- verificar_congelado_engorde.sql — Salud de la liquidación CONGELADA de engorde
--
-- Verifica el invariante «estado Cerrado ⟺ copia congelada vigente» y la
-- integridad de las copias. Los chequeos 1-5 deben dar 0; el 6 es la auditoría
-- copia-vs-vivo (informativo: un checksum distinto acá NO es un error — dice
-- que el cálculo vivo cambió DESPUÉS de congelar, que es exactamente lo que la
-- copia existe para retener; sirve para medir cuánto se habría movido el lote
-- y decidir si amerita un re-congelado admin).
--
-- Uso:  psql ... -f backend/sql/verificar_congelado_engorde.sql
-- Solo lectura efectiva: el chequeo 6 anula copias DENTRO de una transacción
-- que termina en ROLLBACK (así la fn recalcula en vivo sin duplicar la fórmula).
-- =============================================================================

\echo '=== 1) Lotes Cerrado SIN copia vigente (debe dar 0) ==='
SELECT count(*) AS cerrados_sin_copia
FROM lote_ave_engorde l
WHERE LOWER(COALESCE(l.estado_operativo_lote, '')) = 'cerrado'
  AND l.deleted_at IS NULL
  AND NOT EXISTS (SELECT 1 FROM liquidacion_lote_engorde_congelada c
                   WHERE c.lote_ave_engorde_id = l.lote_ave_engorde_id
                     AND c.anulada_at IS NULL);

\echo '=== 2) Copias vigentes con lote NO Cerrado o eliminado (debe dar 0) ==='
SELECT count(*) AS copias_vigentes_con_lote_abierto
FROM liquidacion_lote_engorde_congelada c
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = c.lote_ave_engorde_id
WHERE c.anulada_at IS NULL
  AND (LOWER(COALESCE(l.estado_operativo_lote, '')) <> 'cerrado' OR l.deleted_at IS NOT NULL);

\echo '=== 3) Lotes con MAS de una copia vigente (debe dar 0; lo impide el UNIQUE parcial) ==='
SELECT count(*) AS lotes_con_doble_vigente
FROM (SELECT c.lote_ave_engorde_id
        FROM liquidacion_lote_engorde_congelada c
       WHERE c.anulada_at IS NULL
       GROUP BY c.lote_ave_engorde_id
      HAVING count(*) > 1) x;

\echo '=== 4) Filas de detalle sin cabecera (debe dar 0; lo impide la FK) ==='
SELECT count(*) AS filas_huerfanas
FROM liquidacion_lote_engorde_congelada_fila f
WHERE NOT EXISTS (SELECT 1 FROM liquidacion_lote_engorde_congelada c WHERE c.id = f.liquidacion_id);

\echo '=== 5) Cabeceras cuyo contador `filas` no coincide con el detalle (debe dar 0) ==='
SELECT count(*) AS cabeceras_descuadradas
FROM liquidacion_lote_engorde_congelada c
WHERE c.filas <> (SELECT count(*) FROM liquidacion_lote_engorde_congelada_fila f
                   WHERE f.liquidacion_id = c.id);

\echo '=== 6) AUDITORIA copia vs VIVO: ¿cuánto se moveria cada lote congelado si recalculara hoy? ==='
-- Anulación SIMULADA dentro de la transacción: la fn deja de ver la copia y calcula en vivo con
-- la fórmula vigente (una sola fórmula — no se duplica acá). Se compara el checksum almacenado
-- (md5 sobre (orden, 47 columnas), el mismo de fn_congelar) contra el del recálculo de HOY.
-- ROLLBACK al final: nada queda modificado.
BEGIN;
UPDATE liquidacion_lote_engorde_congelada SET anulada_at = now() WHERE anulada_at IS NULL;

SELECT c.lote_ave_engorde_id AS lote,
       c.lote_nombre         AS corrida,
       c.origen,
       c.filas               AS filas_copia,
       c.congelada_at::date  AS congelada,
       CASE WHEN vivo.checksum_vivo = c.checksum
            THEN 'OK: el vivo no se ha movido'
            ELSE 'DIFIERE: el recalculo de hoy ya no coincide con la copia' END AS estado,
       vivo.filas_vivo
FROM liquidacion_lote_engorde_congelada c
CROSS JOIN LATERAL (
    SELECT md5(string_agg(x::text, '|' ORDER BY x.orden)) AS checksum_vivo,
           count(*)                                        AS filas_vivo
    FROM (SELECT row_number() OVER (ORDER BY f.fecha, COALESCE(f.seg_id, 0)) AS orden, f.*
            FROM fn_seguimiento_diario_engorde(c.lote_ave_engorde_id) f) x
) vivo
WHERE c.anulada_at = now()   -- solo las anuladas por ESTA simulación (mismo now() de la tx)
ORDER BY c.lote_ave_engorde_id;

ROLLBACK;
