-- ============================================================================
-- DIAGNOSTICO (solo lectura): filas de seguimiento engorde que violan la regla
-- de la HORA DE LLEGADA, y el estado de las estructuras que hay que cuidar al
-- remediarlas.
--
-- Contexto: fn_cruce_reproductora_a_engorde fechaba en `fecha_encaset + d` sin
-- mirar `hora_encasetamiento`. Corregido en la migracion
-- 20260828170000_FnCruceReproductoraEngordeHoraLlegada, que NO recalcula nada:
-- las filas ya escritas quedaron torcidas. Este script las cuenta y mide el
-- dano colateral de tocarlas.
-- Plan: fase_de_desarrollo/remediacion_cruce_engorde_hora_llegada_plan.md
--
-- ⚠️ La sesion se fuerza a UTC a proposito. `fecha` es timestamptz guardada a
--    00:00Z; con America/Bogota `fecha::date` resta un dia y este mismo script
--    reporta el DOBLE de violaciones, todas falsas.
--
-- SIN-MIGRACION: diagnostico de solo lectura, se corre a mano contra un dump
-- para medir. No crea ni modifica nada.
-- ============================================================================

SET TIME ZONE 'UTC';

\echo ''
\echo '=== 1. Lotes engorde con hora informada, por empresa ==='
SELECT c.name AS empresa,
       count(*)                                                              AS lotes_con_hora,
       count(*) FILTER (WHERE lae.hora_encasetamiento >= time '13:00')       AS hora_tardia,
       count(*) FILTER (WHERE EXISTS (SELECT 1 FROM seguimiento_diario_aves_engorde s
                                       WHERE s.lote_ave_engorde_id = lae.lote_ave_engorde_id
                                         AND s.origen_cruce))                AS usan_cruce
  FROM lote_ave_engorde lae
  JOIN farms f     ON f.id = lae.granja_id
  JOIN companies c ON c.id = f.company_id
 WHERE lae.hora_encasetamiento IS NOT NULL
 GROUP BY 1 ORDER BY 1;

\echo ''
\echo '=== 2. VIOLACIONES: filas con fecha anterior al primer dia con registro ==='
\echo '    (deberia dar 0 filas una vez remediado)'
SELECT c.name AS empresa, lae.lote_ave_engorde_id AS lote, lae.lote_nombre, lae.galpon_id,
       (lae.fecha_encaset AT TIME ZONE 'UTC')::date AS encaset,
       lae.hora_encasetamiento AS hora,
       lae.deleted_at IS NOT NULL                   AS lote_borrado,
       s.id                                         AS seguimiento_id,
       (s.fecha AT TIME ZONE 'UTC')::date           AS fecha_fila,
       s.origen_cruce, s.created_by_user_id,
       s.mortalidad_machos + s.mortalidad_hembras + s.sel_m + s.sel_h AS bajas,
       s.consumo_kg_machos + s.consumo_kg_hembras                     AS consumo_kg
  FROM lote_ave_engorde lae
  JOIN farms f     ON f.id = lae.granja_id
  JOIN companies c ON c.id = f.company_id
  JOIN seguimiento_diario_aves_engorde s ON s.lote_ave_engorde_id = lae.lote_ave_engorde_id
 WHERE lae.hora_encasetamiento >= time '13:00'
   AND (s.fecha AT TIME ZONE 'UTC')::date < (lae.fecha_encaset AT TIME ZONE 'UTC')::date + 1
 ORDER BY 1, 2, 9;

\echo ''
\echo '=== 3. Desfase encaset reproductora vs engorde (el cruce mapea por EDAD) ==='
\echo '    delta != 0 corre la serie entera; delta 0 es lo normal'
SELECT (lr.fecha_encasetamiento AT TIME ZONE 'UTC')::date
         - (lae.fecha_encaset AT TIME ZONE 'UTC')::date AS delta_dias,
       count(*)                                          AS lotes_reproductora,
       count(DISTINCT lae.lote_ave_engorde_id)           AS lotes_engorde
  FROM lote_reproductora_ave_engorde lr
  JOIN lote_ave_engorde lae ON lae.lote_ave_engorde_id = lr.lote_ave_engorde_id
 GROUP BY 1 ORDER BY 1;

\echo ''
\echo '=== 4. Colision al correr la serie: dias destino ya ocupados por un MANUAL ==='
\echo '    cada fila = un dia de reproductora que el cruce NO podria escribir'
\echo '    (ON CONFLICT DO NOTHING + RAISE WARNING) => kilos que se pierden'
WITH tardios AS (
    SELECT lae.lote_ave_engorde_id AS lote,
           (lae.fecha_encaset AT TIME ZONE 'UTC')::date AS enc
      FROM lote_ave_engorde lae
     WHERE lae.hora_encasetamiento >= time '13:00'
       AND EXISTS (SELECT 1 FROM seguimiento_diario_aves_engorde s
                    WHERE s.lote_ave_engorde_id = lae.lote_ave_engorde_id AND s.origen_cruce)
), destinos AS (
    SELECT t.lote, t.enc + 1 + d AS fecha_dest, d AS edad
      FROM tardios t CROSS JOIN generate_series(0, 7) AS d
)
SELECT dst.lote, dst.edad, dst.fecha_dest,
       s.id AS ocupada_por, s.created_by_user_id,
       s.consumo_kg_machos + s.consumo_kg_hembras AS kg_del_manual
  FROM destinos dst
  JOIN seguimiento_diario_aves_engorde s
    ON s.lote_ave_engorde_id = dst.lote
   AND (s.fecha AT TIME ZONE 'UTC')::date = dst.fecha_dest
   AND NOT s.origen_cruce
 ORDER BY 1, 2;

\echo ''
\echo '=== 5. INVARIANTE: historico unificado vivo apuntando al seguimiento ==='
\echo '    Lo escribe C# (RetiroAvesEngordeAplicador.SincronizarCruceAsync), NO el SQL.'
\echo '    Borrar filas de cruce por SQL dejaria estas huerfanas y SIN anular.'
SELECT h.lote_ave_engorde_id AS lote, count(*) AS bajas_vivas,
       count(*) FILTER (WHERE NOT EXISTS (SELECT 1 FROM seguimiento_diario_aves_engorde s
                                           WHERE s.id = h.origen_id)) AS huerfanas,
       sum(COALESCE(h.cantidad_hembras,0) + COALESCE(h.cantidad_machos,0)) AS aves
  FROM lote_registro_historico_unificado h
 WHERE h.origen_tabla = 'seguimiento_diario_aves_engorde'
   AND h.tipo_evento  = 'BAJA_SEGUIMIENTO'
   AND NOT h.anulado
   AND h.lote_ave_engorde_id IN (
        SELECT lae.lote_ave_engorde_id FROM lote_ave_engorde lae
         WHERE lae.hora_encasetamiento >= time '13:00')
 GROUP BY 1 ORDER BY 1;

\echo ''
\echo '=== 6. HUERFANAS EN TODO EL UNIVERSO (debe dar 0; si no, ya se rompio) ==='
SELECT count(*) AS bajas_vivas_sin_seguimiento
  FROM lote_registro_historico_unificado h
 WHERE h.origen_tabla = 'seguimiento_diario_aves_engorde'
   AND h.tipo_evento  = 'BAJA_SEGUIMIENTO'
   AND NOT h.anulado
   AND NOT EXISTS (SELECT 1 FROM seguimiento_diario_aves_engorde s WHERE s.id = h.origen_id);

\echo ''
\echo '=== 7. LINEA BASE del cuadre de los galpones involucrados ==='
SELECT c.empresa, c.granja, c.galpon_id, c.lote_ave_engorde_id AS lote, c.lote_nombre,
       round(c.saldo_tabla_kg::numeric, 2) AS saldo_tabla_kg,
       round(c.stock_kg::numeric, 2)       AS stock_kg,
       round(c.descuadre_kg::numeric, 2)   AS descuadre_kg,
       c.filas_negativas
  FROM fn_cuadre_alimento_engorde(NULL) c
 WHERE c.galpon_id IN (SELECT lae.galpon_id FROM lote_ave_engorde lae
                        WHERE lae.hora_encasetamiento >= time '13:00')
 ORDER BY 1, 3, 4;
