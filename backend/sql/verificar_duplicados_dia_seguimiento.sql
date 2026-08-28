-- ============================================================================
-- Duplicados de DIA CALENDARIO en las tablas de seguimiento diario
--
-- Los indices unicos de seguimiento son sobre `(lote, fecha)` con `fecha` de
-- tipo `timestamptz` ⇒ comparan el INSTANTE completo, no el dia. Como los
-- escritores no usan la misma hora, dos filas del mismo dia conviven sin que el
-- indice se entere:
--
--   17:00:00Z  mediodia LOCAL   -- convencion manual vieja (hasta jul-2026)
--   12:00:00Z  mediodia UTC     -- convencion manual actual
--   00:00:00Z  medianoche UTC   -- trigger del cruce de reproductora
--
-- Las dos manuales caen en el mismo dia calendario en cualquier lente; la del
-- cruce es la unica ambigua, y es la que produce las colisiones reales.
--
-- Todo se mide con `AT TIME ZONE 'UTC'` EXPLICITO: es la lente que usa el
-- backend (`DateOnly.FromDateTime` sobre un timestamptz da el dia UTC) y la que
-- ya usa el indice de produccion `ux_seguimiento_diario_produccion_lote_dia_utc`.
-- Sin el `AT TIME ZONE` el resultado depende del `TimeZone` de la sesion y una
-- corrida local (America/Bogota) no coincide con una de produccion (UTC).
--
-- SOLO LECTURA. No crea ni modifica nada.
-- SIN-MIGRACION: diagnostico de solo lectura, se corre a mano contra un dump.
--
-- Uso:
--   psql ... -f backend/sql/verificar_duplicados_dia_seguimiento.sql
-- ============================================================================

\echo ''
\echo '=== 1. RESUMEN: cuantos dias duplicados hay por tabla ==='

WITH eng AS (
    SELECT lote_ave_engorde_id AS k,
           (fecha AT TIME ZONE 'UTC')::date AS d,
           count(*) AS n
      FROM seguimiento_diario_aves_engorde
     GROUP BY 1, 2 HAVING count(*) > 1
), rep AS (
    SELECT lote_reproductora_ave_engorde_id AS k,
           (fecha AT TIME ZONE 'UTC')::date AS d,
           count(*) AS n
      FROM seguimiento_diario_lote_reproductora_aves_engorde
     GROUP BY 1, 2 HAVING count(*) > 1
), lev AS (
    SELECT tipo_seguimiento || '|' || lote_id || '|' || COALESCE(reproductora_id, '') AS k,
           (fecha AT TIME ZONE 'UTC')::date AS d,
           count(*) AS n
      FROM seguimiento_diario_levante
     GROUP BY 1, 2 HAVING count(*) > 1
), prod AS (
    -- Produccion YA tiene el indice funcional por dia UTC, asi que esto deberia
    -- dar 0 siempre. Si alguna vez da > 0, el indice se cayo.
    SELECT lote_id::text AS k,
           (fecha_registro AT TIME ZONE 'UTC')::date AS d,
           count(*) AS n
      FROM seguimiento_diario_produccion
     WHERE deleted_at IS NULL
     GROUP BY 1, 2 HAVING count(*) > 1
)
SELECT 'engorde'      AS tabla, count(*) AS dias_duplicados, COALESCE(sum(n - 1), 0) AS filas_sobrantes FROM eng
UNION ALL SELECT 'reproductora', count(*), COALESCE(sum(n - 1), 0) FROM rep
UNION ALL SELECT 'levante',      count(*), COALESCE(sum(n - 1), 0) FROM lev
UNION ALL SELECT 'produccion',   count(*), COALESCE(sum(n - 1), 0) FROM prod
ORDER BY 3 DESC, 1;

\echo ''
\echo '=== 2. ENGORDE: detalle de cada dia duplicado ==='
\echo '    Se espera el patron cruce(00:00Z) + manual(12:00Z). Otro patron es un caso nuevo.'

WITH dup AS (
    SELECT lote_ave_engorde_id AS k, (fecha AT TIME ZONE 'UTC')::date AS d
      FROM seguimiento_diario_aves_engorde
     GROUP BY 1, 2 HAVING count(*) > 1
)
SELECT c.name                                   AS empresa,
       f.name                                   AS granja,
       s.lote_ave_engorde_id                    AS lote,
       dup.d                                    AS dia_utc,
       s.id,
       (s.fecha AT TIME ZONE 'UTC')::time       AS hora_guardada,
       s.origen_cruce                           AS del_cruce,
       s.validado,
       s.mortalidad_hembras                     AS mort_h,
       s.mortalidad_machos                      AS mort_m,
       s.consumo_kg_hembras                     AS kg_h,
       s.consumo_kg_machos                      AS kg_m,
       -- Una reserva ACTIVA significa que este registro todavia NO aplico su
       -- efecto: borrarlo es barato. Sin reserva y validado, el efecto ya se
       -- aplico y hay que revisarlo antes de tocar la fila.
       EXISTS (SELECT 1 FROM seguimiento_reserva_alimento r
                WHERE r.origen_modulo = 'ENGORDE'
                  AND r.origen_seguimiento_id = s.id
                  AND r.estado = 'ACTIVA')      AS reserva_activa
  FROM dup
  JOIN seguimiento_diario_aves_engorde s
    ON s.lote_ave_engorde_id = dup.k
   AND (s.fecha AT TIME ZONE 'UTC')::date = dup.d
  JOIN lote_ave_engorde e ON e.lote_ave_engorde_id = s.lote_ave_engorde_id
  JOIN companies c        ON c.id = e.company_id
  LEFT JOIN farms f       ON f.id = e.granja_id
 ORDER BY empresa, lote, dia_utc, s.id;

\echo ''
\echo '=== 3. LEVANTE: detalle de cada dia duplicado ==='

WITH dup AS (
    SELECT tipo_seguimiento AS t, lote_id AS l,
           COALESCE(reproductora_id, '') AS r,
           (fecha AT TIME ZONE 'UTC')::date AS d
      FROM seguimiento_diario_levante
     GROUP BY 1, 2, 3, 4 HAVING count(*) > 1
)
SELECT c.name                                AS empresa,
       s.tipo_seguimiento,
       s.lote_id,
       s.reproductora_id,
       dup.d                                 AS dia_utc,
       s.id,
       (s.fecha AT TIME ZONE 'UTC')::time    AS hora_guardada,
       s.validado
  FROM dup
  JOIN seguimiento_diario_levante s
    ON s.tipo_seguimiento = dup.t AND s.lote_id = dup.l
   AND COALESCE(s.reproductora_id, '') = dup.r
   AND (s.fecha AT TIME ZONE 'UTC')::date = dup.d
  LEFT JOIN lote_postura_levante l ON l.lote_postura_levante_id = s.lote_postura_levante_id
  LEFT JOIN companies c            ON c.id = l.company_id
 ORDER BY empresa, s.lote_id, dia_utc, s.id;

\echo ''
\echo '=== 4. CONVENCIONES DE HORA que conviven en cada tabla ==='
\echo '    Mientras haya mas de una, un indice sobre el timestamp crudo no protege.'

SELECT 'engorde' AS tabla,
       CASE WHEN origen_cruce THEN 'cruce' ELSE 'manual' END AS origen,
       (fecha AT TIME ZONE 'UTC')::time AS hora_utc,
       count(*) AS filas,
       min((fecha AT TIME ZONE 'UTC')::date) AS desde,
       max((fecha AT TIME ZONE 'UTC')::date) AS hasta
  FROM seguimiento_diario_aves_engorde
 GROUP BY 1, 2, 3
UNION ALL
SELECT 'levante', 'todas', (fecha AT TIME ZONE 'UTC')::time, count(*),
       min((fecha AT TIME ZONE 'UTC')::date), max((fecha AT TIME ZONE 'UTC')::date)
  FROM seguimiento_diario_levante
 GROUP BY 1, 2, 3
UNION ALL
SELECT 'reproductora', 'todas', (fecha AT TIME ZONE 'UTC')::time, count(*),
       min((fecha AT TIME ZONE 'UTC')::date), max((fecha AT TIME ZONE 'UTC')::date)
  FROM seguimiento_diario_lote_reproductora_aves_engorde
 GROUP BY 1, 2, 3
 ORDER BY 1, 4 DESC;

\echo ''
\echo '=== 5. IMPACTO: los dias duplicados de engorde, vistos por la fn diaria ==='
\echo '    Si el mismo dia sale dos veces, la mortalidad y el consumo se cuentan dos'
\echo '    veces y el saldo de alimento puede irse a negativo.'

SET TIME ZONE 'UTC';   -- la fn hace `fecha::date`: sin esto el resultado depende de la sesion

WITH lotes AS (
    SELECT DISTINCT lote_ave_engorde_id AS id
      FROM (SELECT lote_ave_engorde_id, (fecha AT TIME ZONE 'UTC')::date
              FROM seguimiento_diario_aves_engorde
             GROUP BY 1, 2 HAVING count(*) > 1) x
), diario AS (
    SELECT l.id AS lote, d.*
      FROM lotes l, LATERAL fn_seguimiento_diario_engorde(l.id) d
)
SELECT lote, fecha, count(*) AS veces,
       sum(mortalidad_hembras + mortalidad_machos) AS mort_sumada,
       round(sum(consumo_dia_kg)::numeric, 2)      AS kg_sumados,
       min(saldo_alimento_kg)                      AS saldo_alimento_kg
  FROM diario
 GROUP BY lote, fecha HAVING count(*) > 1
 ORDER BY lote, fecha;

\echo ''
\echo '=== 6. INDICES UNICOS vigentes sobre (lote, fecha) ==='
\echo '    El que dice `::date` protege el DIA; el que nombra la columna cruda, no.'

SELECT tablename AS tabla, indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public'
   AND tablename IN ('seguimiento_diario_aves_engorde',
                     'seguimiento_diario_levante',
                     'seguimiento_diario_produccion',
                     'seguimiento_diario_lote_reproductora_aves_engorde')
   AND indexdef ILIKE '%UNIQUE%'
   AND indexdef ILIKE '%fecha%'
 ORDER BY 1, 2;
