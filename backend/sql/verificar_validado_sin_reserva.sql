-- =============================================================================
-- verificar_validado_sin_reserva.sql — SOLO LECTURA (todo termina en ROLLBACK)
-- =============================================================================
-- Comprueba que `validado` de los seguimientos diarios (levante, producción y engorde) cuenta la
-- verdad: un registro está SIN validar solo mientras tiene alimento o aves SEPARADOS (reservas
-- ACTIVAS) esperando su aplicación. Mide tres situaciones y simula la migración
-- 20260918210000_NormalizarValidadoSeguimientosSinReserva dos veces (la 2.ª debe decir UPDATE 0).
--
-- ▶ PASO PREVIO A ENCENDER `requiere_validacion_seguimiento_diario` EN CUALQUIER EMPRESA: en el
--   resumen final ([7]) `normalizables_tras_migracion`, `limbo_pendiente_de_decidir` y
--   `reservas_huerfanas` tienen que dar 0. Encender el flag con filas «mal nacidas» las muestra como
--   pendientes, a las 24 h pasan a EN RETRASO y bloquean el alta de días nuevos del lote.
--
-- Uso:  psql -h <host> -p <port> -U <user> -d <db> -v ON_ERROR_STOP=1 -f verificar_validado_sin_reserva.sql
--
-- Lectura del resultado:
--   [1] la doble validación por empresa.
--   [2] NORMALIZABLES: sin validar, SIN ninguna reserva, empresa con el flag APAGADO. Descuentan al
--       guardar, así que su efecto YA se aplicó y tienen que estar validados. Es lo que arregla la
--       migración. (Nacieron mal por la rama Colombia de Producción o por un creador que no lo marcó.)
--   [3] LIMBO: sin validar CON reserva, empresa con el flag APAGADO. Son pendientes legítimos que
--       quedaron colgados porque el flag se apagó con ellos pendientes: su alimento y sus aves están
--       SEPARADOS y nunca se aplicaron. NO los toca la migración —validarlos (aplica el descuento) o
--       borrarlos (libera la reserva) es una decisión de operación—. Con el flag apagado la pantalla no
--       muestra el botón de validar.
--   [4] RESERVAS HUÉRFANAS: ACTIVAS cuyo registro dueño ya no existe. Comprometen stock sin dueño.
--   [5] Informativo: sin validar y sin reserva en empresas con el flag ENCENDIDO. Pueden ser
--       registros sin nada que separar que esperan la confirmación; NO se normalizan.
--   [6] Simulación de la migración: filas afectadas por módulo (producción, levante, engorde), 2.ª
--       pasada (0) y prueba de que lo que NO se debe tocar queda idéntico.
--   [7] Resumen.
-- =============================================================================

BEGIN;

-- Vista unificada de los tres módulos que usan `validado`. (Reproductora usa `confirmado` con otra
-- semántica —habilita el cruce a engorde— y NO entra acá.)
CREATE TEMP VIEW _seg AS
SELECT 'PRODUCCION'::text AS modulo, s.id::bigint AS id, s.company_id, s.validado, s.fecha_registro::date AS fecha
  FROM public.seguimiento_diario_produccion s
 WHERE s.deleted_at IS NULL
UNION ALL
SELECT 'LEVANTE', s.id::bigint, s.company_id, s.validado, s.fecha::date
  FROM public.seguimiento_diario_levante s
UNION ALL
SELECT 'ENGORDE', s.id::bigint, l.company_id, s.validado, s.fecha::date
  FROM public.seguimiento_diario_aves_engorde s
  JOIN public.lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id;

-- Reservas de alimento y de aves con una forma común (cantidad en kg o en aves).
CREATE TEMP VIEW _res AS
SELECT origen_modulo AS modulo, origen_seguimiento_id AS id, estado, 'alimento'::text AS tipo, cantidad_kg AS cantidad
  FROM public.seguimiento_reserva_alimento
UNION ALL
SELECT origen_modulo, origen_seguimiento_id, estado, 'aves', (hembras + machos + mixtas)::numeric
  FROM public.seguimiento_reserva_aves;

-- Reservas ACTIVAS cuyo registro dueño ya no existe (Reproductora incluida: su tabla no está en _seg).
CREATE TEMP VIEW _huerfanas AS
SELECT r.tipo, r.origen_modulo, r.origen_seguimiento_id, r.cantidad
  FROM (
        SELECT 'alimento'::text AS tipo, origen_modulo, origen_seguimiento_id, cantidad_kg AS cantidad
          FROM public.seguimiento_reserva_alimento WHERE estado = 'ACTIVA'
        UNION ALL
        SELECT 'aves', origen_modulo, origen_seguimiento_id, (hembras + machos + mixtas)::numeric
          FROM public.seguimiento_reserva_aves WHERE estado = 'ACTIVA'
       ) r
 WHERE (r.origen_modulo = 'PRODUCCION'
        AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_produccion s
                         WHERE s.id = r.origen_seguimiento_id AND s.deleted_at IS NULL))
    OR (r.origen_modulo = 'LEVANTE'
        AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_levante s WHERE s.id = r.origen_seguimiento_id))
    OR (r.origen_modulo = 'ENGORDE'
        AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_aves_engorde s WHERE s.id = r.origen_seguimiento_id))
    OR (r.origen_modulo = 'REPRODUCTORA'
        AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_lote_reproductora_aves_engorde s WHERE s.id = r.origen_seguimiento_id));

\echo '[1] Doble validación por empresa'
SELECT id AS empresa_id, name AS empresa, requiere_validacion_seguimiento_diario AS doble_validacion
  FROM public.companies
 ORDER BY id;

\echo '[2] NORMALIZABLES: sin validar, SIN reserva, flag APAGADO (los arregla la migración)'
SELECT s.modulo, s.company_id, c.name AS empresa, count(*) AS filas,
       min(s.fecha) AS desde, max(s.fecha) AS hasta,
       (array_agg(s.id ORDER BY s.id))[1:20] AS primeros_ids
  FROM _seg s
  JOIN public.companies c ON c.id = s.company_id
 WHERE NOT s.validado
   AND NOT c.requiere_validacion_seguimiento_diario
   AND NOT EXISTS (SELECT 1 FROM _res r WHERE r.modulo = s.modulo AND r.id = s.id)
 GROUP BY 1, 2, 3
 ORDER BY 1, 2;

\echo '[3] LIMBO: sin validar CON reserva, flag APAGADO (NO los toca la migración: hay que decidir)'
SELECT s.modulo, s.company_id, c.name AS empresa, s.id, s.fecha,
       string_agg(DISTINCT r.estado, '/') AS estados_reserva,
       coalesce(sum(r.cantidad) FILTER (WHERE r.tipo = 'alimento' AND r.estado = 'ACTIVA'), 0) AS kg_reservados,
       coalesce(sum(r.cantidad) FILTER (WHERE r.tipo = 'aves'     AND r.estado = 'ACTIVA'), 0) AS aves_reservadas
  FROM _seg s
  JOIN public.companies c ON c.id = s.company_id
  JOIN _res r ON r.modulo = s.modulo AND r.id = s.id
 WHERE NOT s.validado
   AND NOT c.requiere_validacion_seguimiento_diario
 GROUP BY 1, 2, 3, 4, 5
 ORDER BY 1, 2, 4;

\echo '[4] RESERVAS HUÉRFANAS: ACTIVAS cuyo registro ya no existe'
SELECT tipo, origen_modulo AS modulo, count(*) AS filas, sum(cantidad) AS cantidad
  FROM _huerfanas
 GROUP BY 1, 2
 ORDER BY 1, 2;

\echo '[5] Informativo: sin validar y sin reserva con el flag ENCENDIDO (NO se normalizan)'
SELECT s.modulo, s.company_id, c.name AS empresa, count(*) AS filas, min(s.fecha) AS desde, max(s.fecha) AS hasta
  FROM _seg s
  JOIN public.companies c ON c.id = s.company_id
 WHERE NOT s.validado
   AND c.requiere_validacion_seguimiento_diario
   AND NOT EXISTS (SELECT 1 FROM _res r WHERE r.modulo = s.modulo AND r.id = s.id)
 GROUP BY 1, 2, 3
 ORDER BY 1, 2;

-- Fotos ANTES de simular: lo que la migración arregla y lo que NO debe tocar.
CREATE TEMP TABLE _normalizables_antes ON COMMIT DROP AS
SELECT s.modulo, s.id
  FROM _seg s
  JOIN public.companies c ON c.id = s.company_id
 WHERE NOT s.validado
   AND NOT c.requiere_validacion_seguimiento_diario
   AND NOT EXISTS (SELECT 1 FROM _res r WHERE r.modulo = s.modulo AND r.id = s.id);

CREATE TEMP TABLE _con_reserva_antes ON COMMIT DROP AS
SELECT s.modulo, s.id, s.validado
  FROM _seg s
 WHERE EXISTS (SELECT 1 FROM _res r WHERE r.modulo = s.modulo AND r.id = s.id);

CREATE TEMP TABLE _flag_on_antes ON COMMIT DROP AS
SELECT s.modulo, s.id, s.validado
  FROM _seg s
  JOIN public.companies c ON c.id = s.company_id
 WHERE c.requiere_validacion_seguimiento_diario;

\echo '[6] Simulación de la migración (mismo SQL). 1.ª pasada: filas por módulo — producción, levante, engorde'
UPDATE public.seguimiento_diario_produccion s
   SET validado = true
  FROM public.companies c
 WHERE c.id = s.company_id
   AND c.requiere_validacion_seguimiento_diario = false
   AND s.validado = false
   AND s.deleted_at IS NULL
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_alimento r
                    WHERE r.origen_modulo = 'PRODUCCION' AND r.origen_seguimiento_id = s.id)
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_aves r
                    WHERE r.origen_modulo = 'PRODUCCION' AND r.origen_seguimiento_id = s.id);

UPDATE public.seguimiento_diario_levante s
   SET validado = true
  FROM public.companies c
 WHERE c.id = s.company_id
   AND c.requiere_validacion_seguimiento_diario = false
   AND s.validado = false
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_alimento r
                    WHERE r.origen_modulo = 'LEVANTE' AND r.origen_seguimiento_id = s.id)
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_aves r
                    WHERE r.origen_modulo = 'LEVANTE' AND r.origen_seguimiento_id = s.id);

UPDATE public.seguimiento_diario_aves_engorde s
   SET validado = true
  FROM public.lote_ave_engorde l
  JOIN public.companies c ON c.id = l.company_id
 WHERE l.lote_ave_engorde_id = s.lote_ave_engorde_id
   AND c.requiere_validacion_seguimiento_diario = false
   AND s.validado = false
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_alimento r
                    WHERE r.origen_modulo = 'ENGORDE' AND r.origen_seguimiento_id = s.id)
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_aves r
                    WHERE r.origen_modulo = 'ENGORDE' AND r.origen_seguimiento_id = s.id);

\echo '[6] 2.ª pasada: idempotencia — tiene que decir UPDATE 0 tres veces'
UPDATE public.seguimiento_diario_produccion s
   SET validado = true
  FROM public.companies c
 WHERE c.id = s.company_id
   AND c.requiere_validacion_seguimiento_diario = false
   AND s.validado = false
   AND s.deleted_at IS NULL
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_alimento r
                    WHERE r.origen_modulo = 'PRODUCCION' AND r.origen_seguimiento_id = s.id)
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_aves r
                    WHERE r.origen_modulo = 'PRODUCCION' AND r.origen_seguimiento_id = s.id);

UPDATE public.seguimiento_diario_levante s
   SET validado = true
  FROM public.companies c
 WHERE c.id = s.company_id
   AND c.requiere_validacion_seguimiento_diario = false
   AND s.validado = false
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_alimento r
                    WHERE r.origen_modulo = 'LEVANTE' AND r.origen_seguimiento_id = s.id)
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_aves r
                    WHERE r.origen_modulo = 'LEVANTE' AND r.origen_seguimiento_id = s.id);

UPDATE public.seguimiento_diario_aves_engorde s
   SET validado = true
  FROM public.lote_ave_engorde l
  JOIN public.companies c ON c.id = l.company_id
 WHERE l.lote_ave_engorde_id = s.lote_ave_engorde_id
   AND c.requiere_validacion_seguimiento_diario = false
   AND s.validado = false
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_alimento r
                    WHERE r.origen_modulo = 'ENGORDE' AND r.origen_seguimiento_id = s.id)
   AND NOT EXISTS (SELECT 1 FROM public.seguimiento_reserva_aves r
                    WHERE r.origen_modulo = 'ENGORDE' AND r.origen_seguimiento_id = s.id);

\echo '[6] Lo que NO se debe tocar queda idéntico: filas con reserva y filas de empresas con el flag ENCENDIDO que cambiaron (0 y 0)'
SELECT (SELECT count(*) FROM _con_reserva_antes a
          JOIN _seg s ON s.modulo = a.modulo AND s.id = a.id
         WHERE s.validado IS DISTINCT FROM a.validado) AS con_reserva_que_cambio,
       (SELECT count(*) FROM _flag_on_antes a
          JOIN _seg s ON s.modulo = a.modulo AND s.id = a.id
         WHERE s.validado IS DISTINCT FROM a.validado) AS flag_encendido_que_cambio;

\echo '[7] Resumen — para encender la doble validación en una empresa, las tres últimas columnas tienen que ser 0'
SELECT (SELECT count(*) FROM _normalizables_antes) AS normalizables_que_arregla_la_migracion,
       (SELECT count(*)
          FROM _seg s JOIN public.companies c ON c.id = s.company_id
         WHERE NOT s.validado AND NOT c.requiere_validacion_seguimiento_diario
           AND NOT EXISTS (SELECT 1 FROM _res r WHERE r.modulo = s.modulo AND r.id = s.id)) AS normalizables_tras_migracion,
       (SELECT count(*)
          FROM _seg s JOIN public.companies c ON c.id = s.company_id
         WHERE NOT s.validado AND NOT c.requiere_validacion_seguimiento_diario
           AND EXISTS (SELECT 1 FROM _res r WHERE r.modulo = s.modulo AND r.id = s.id)) AS limbo_pendiente_de_decidir,
       (SELECT count(*) FROM _huerfanas) AS reservas_huerfanas;

ROLLBACK;
