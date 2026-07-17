-- ============================================================================
-- ⚠️⚠️⚠️  SCRIPT DE DATA-FIX — NO APLICAR EN PROD SIN OK EXPLÍCITO DE VERENICE  ⚠️⚠️⚠️
-- ============================================================================
--                          + BACKUP PREVIO (db-studio → copia completa)
--
-- Corrige los datos corruptos detectados en la re-validación "Matriz Verenice
-- rev 6-jul-26" (fase_de_desarrollo/postura_verenice_rev_6jul26_plan.md, Fase 0).
--
-- Idempotente: cada bloque solo toca filas que SIGUEN en el patrón corrupto
-- (guardas en el WHERE); una vez aplicado, correr de nuevo es un no-op.
--
-- Alcance: SOLO Postura Colombia, company_id = 1 (los ids de lote de abajo son
-- los verificados en BD local `sanmarinoapplocal` el 2026-07-17, que es copia
-- de prod). Verificar cada bloque contra el esquema real antes de correr en
-- otro ambiente (nombres/tipos de columna, ids de lote).
--
-- ⚠️ HALLAZGO CRÍTICO DE ESQUEMA (ver Bloque 1): existe un trigger
--    `trg_lotes_sync_lote_postura_levante` (AFTER INSERT OR UPDATE ON lotes)
--    que en CADA UPDATE de `lotes` reescribe TODAS las columnas espejo en
--    `lote_postura_levante` (incluida fecha_encaset, ano_tabla_genetica,
--    aves_h_actual, aves_m_actual, etc.), sin importar qué columna cambió.
--    Para los lotes 116/117 eso VUELVE A ROMPER los saldos reales (ver detalle
--    en el Bloque 1) si no se hace backup/restore alrededor del UPDATE.
--    NO existe trigger equivalente lotes/lote_postura_levante → lote_postura_produccion
--    (el Bloque 2 sincroniza esa tabla a mano).
-- ============================================================================


-- ============================================================================
-- BLOQUE 1 — REQ-011a / REQ-002-B36: lotes 116/117 (A374A/A374B "nuevos") con
-- fecha_encaset UN AÑO EN EL FUTURO (creados a mano vía LoteService.CreateAsync,
-- que no valida fecha futura). Verificado en BD local:
--   lote 116 "A374A": fecha_encaset = 2026-10-14 (debe ser la de 114 = 2025-10-16)
--   lote 117 "A374B": fecha_encaset = 2026-10-21 (debe ser la de 115 = 2025-10-21)
-- lote_postura_levante_id enlazado: 116→8, 117→9 (verificado: lote_postura_levante.lote_id
-- es INTEGER con FK real `fk_lpl_lote → lotes(lote_id)`, NO varchar; no hace falta cast a
-- texto ni fallback por lote_nombre+granja -- REVISAR si en el ambiente destino el tipo
-- difiere de lo verificado acá).
-- ============================================================================

-- 1.a) Backup defensivo de los saldos reales ANTES del UPDATE de `lotes`.
--      Por qué: lotes.hembras_l/machos_l/aves_encasetadas son NULL para 116/117
--      (nacieron 100% por traslado, sin encasetamiento propio), pero
--      lote_postura_levante.aves_h_actual/aves_m_actual YA tienen el saldo real
--      post-traslado (116: 7405 H / 738 M al 2026-07-17). El trigger
--      trg_lotes_sync_lote_postura_levante hace, en CUALQUIER UPDATE de `lotes`,
--      `aves_h_actual = NEW.hembras_l` (= NULL) → sin este backup/restore, el
--      simple fix de fecha BORRARÍA el saldo real de aves de 116/117.
DROP TABLE IF EXISTS _fix_lpl_aves_backup;
CREATE TEMP TABLE _fix_lpl_aves_backup AS
SELECT lote_postura_levante_id, aves_h_actual, aves_m_actual, aves_h_inicial, aves_m_inicial
FROM lote_postura_levante
WHERE lote_id IN (116, 117) AND company_id = 1;

-- 1.b) Fix de fecha_encaset (idempotente: solo si sigue en el futuro).
UPDATE lotes
SET fecha_encaset = (SELECT fecha_encaset FROM lotes o WHERE o.lote_id = 114),
    updated_at    = NOW() AT TIME ZONE 'utc'
WHERE lote_id = 116 AND company_id = 1 AND fecha_encaset > now();

UPDATE lotes
SET fecha_encaset = (SELECT fecha_encaset FROM lotes o WHERE o.lote_id = 115),
    updated_at    = NOW() AT TIME ZONE 'utc'
WHERE lote_id = 117 AND company_id = 1 AND fecha_encaset > now();

-- 1.c) Sincronizar la copia en lote_postura_levante.
--      El trigger trg_lotes_sync_lote_postura_levante YA sincronizó fecha_encaset
--      automáticamente en 1.b (verificado: su rama UPDATE hace
--      `SET fecha_encaset = NEW.fecha_encaset ... WHERE lote_id = NEW.lote_id`).
--      Este UPDATE es un fallback idempotente por si el trigger no existe en el
--      ambiente destino (p.ej. si se restauró un dump viejo sin la migración que
--      lo crea) -- REVISAR que el trigger exista en destino antes de asumir que
--      este bloque es redundante; si existe, este UPDATE es un no-op.
UPDATE lote_postura_levante lpl
SET fecha_encaset = l.fecha_encaset,
    updated_at    = NOW() AT TIME ZONE 'utc'
FROM lotes l
WHERE lpl.lote_id = l.lote_id          -- ambos INTEGER, FK real fk_lpl_lote (verificado)
  AND lpl.lote_id IN (116, 117)
  AND lpl.company_id = 1
  AND lpl.fecha_encaset IS DISTINCT FROM l.fecha_encaset;

-- 1.d) Restaurar los saldos reales que el trigger haya podido pisar en 1.b/1.c.
UPDATE lote_postura_levante lpl
SET aves_h_actual  = bak.aves_h_actual,
    aves_m_actual  = bak.aves_m_actual,
    aves_h_inicial = bak.aves_h_inicial,
    aves_m_inicial = bak.aves_m_inicial
FROM _fix_lpl_aves_backup bak
WHERE lpl.lote_postura_levante_id = bak.lote_postura_levante_id
  AND lpl.aves_h_actual IS DISTINCT FROM bak.aves_h_actual;

DROP TABLE IF EXISTS _fix_lpl_aves_backup;

-- NOTA (fuera de alcance de este bloque, NO incluido): poblar
-- lotes.hembras_l/machos_l/aves_encasetadas de 116/117 con lo trasladado
-- (7.617 H / 1.010 M, ver Bloque 4) es una alternativa a re-fechar la fila de
-- ingreso del traslado. Si se decide esa vía en vez del re-fechado del Bloque 4,
-- OJO: el trigger volvería a pisar aves_h_actual/aves_m_actual con esos valores
-- INICIALES (no con el saldo post-mortalidad actual) -- repetir el patrón
-- backup/restore de 1.a/1.d, o mejor, ajustar el trigger para no pisar
-- aves_h_actual/aves_m_actual en UPDATE si ya fueron movidos por traslado.


-- ============================================================================
-- BLOQUE 2 — REQ-002g/i: K345A/B (lotes 13/14) con ano_tabla_genetica=2023
-- (el script de alineación de julio, backend/sql/backfill_raza_ano_lotes_produccion.sql,
-- nunca corrió en prod). Verificado en BD local: ambos con ano_tabla_genetica=2023,
-- raza='AP', company_id=1; SÍ existe guía AP+2023 (no es un "sin match", es un
-- match contra el año VIEJO de la guía -- el join no falla, compara contra la
-- tabla equivocada).
-- ============================================================================

-- 2.a) Fix en `lotes` (idempotente). El trigger trg_lotes_sync_lote_postura_levante
--      sincroniza ano_tabla_genetica a lote_postura_levante automáticamente; para
--      13/14 NO hay riesgo de pisar saldos (verificado: aves_h_actual/aves_m_actual
--      ya son iguales a hembras_l/machos_l en lote_postura_levante, no hubo
--      decremento por traslado ahí) -- no hace falta backup/restore como en Bloque 1.
UPDATE lotes
SET ano_tabla_genetica = 2026,
    updated_at         = NOW() AT TIME ZONE 'utc'
WHERE lote_id IN (13, 14) AND company_id = 1 AND ano_tabla_genetica = 2023;

-- 2.b) Sincronizar lote_postura_levante (fallback idempotente; ver nota trigger 1.c).
UPDATE lote_postura_levante lpl
SET ano_tabla_genetica = l.ano_tabla_genetica,
    updated_at         = NOW() AT TIME ZONE 'utc'
FROM lotes l
WHERE lpl.lote_id = l.lote_id
  AND lpl.lote_id IN (13, 14)
  AND lpl.company_id = 1
  AND lpl.ano_tabla_genetica IS DISTINCT FROM l.ano_tabla_genetica;

-- 2.c) Sincronizar lote_postura_produccion -- SIN trigger que lo haga solo
--      (trg_lotes_sync_lote_postura_levante NO toca lote_postura_produccion;
--      verificado: lote_postura_produccion solo tiene el trigger de histórico).
--      Enlace verificado: lote_postura_produccion.lote_id es INTEGER con FK real
--      fk_lpp_lote → lotes(lote_id) (mismo patrón que Bloque 1, no hace falta cast).
UPDATE lote_postura_produccion lpp
SET ano_tabla_genetica = l.ano_tabla_genetica,
    updated_at         = NOW() AT TIME ZONE 'utc'
FROM lotes l
WHERE lpp.lote_id = l.lote_id
  AND lpp.lote_id IN (13, 14)
  AND lpp.company_id = 1
  AND lpp.ano_tabla_genetica IS DISTINCT FROM l.ano_tabla_genetica;

-- 2.d) SELECT DE AUDITORÍA (solo lectura, correr ANTES de decidir el fix general):
--      lotes activos de company 1 cuyo (raza, ano_tabla_genetica) NO tiene NINGÚN
--      match en guia_genetica_sanmarino_colombia (ojo: esto es distinto del caso
--      13/14, que SÍ matchea pero contra el año viejo -- este SELECT solo agarra
--      años que ni siquiera existen en la guía, p.ej. un año tipeado mal tipo 2027).
--      Al 2026-07-17 en BD local devuelve 0 filas (no hay otro lote roto ahora
--      mismo) -- se deja como chequeo de regresión / para correr de nuevo antes
--      de aplicar en prod, por si aparecieron lotes nuevos rotos entretanto.
SELECT
    l.lote_id,
    l.lote_nombre,
    l.raza,
    l.ano_tabla_genetica,
    l.fase,
    l.fecha_encaset
FROM lotes l
WHERE l.company_id = 1
  AND l.deleted_at IS NULL
  AND NOT EXISTS (
        SELECT 1
        FROM guia_genetica_sanmarino_colombia g
        WHERE g.company_id = l.company_id
          AND g.deleted_at IS NULL
          AND g.raza = l.raza
          AND g.anio_guia = l.ano_tabla_genetica::text
      )
ORDER BY l.lote_id;


-- ============================================================================
-- BLOQUE 3 — REQ-012a: fecha_inicio_produccion desalineada del primer dato real.
-- Verificado en BD local: lote_postura_produccion 6 (P-K345B) y 7 (P-K345A)
-- tienen fecha_inicio_produccion = 2026-04-10, pero seguimiento_diario_produccion
-- (legacy) tiene filas desde 2025-07-16/19 -- casi 9 meses antes. El backend
-- (LotePosturaProduccionService.ProjectToDetail) lee FechaInicioProduccion
-- directo de lote_postura_produccion (verificado en código), por eso el fix va ahí.
-- Fuentes de "primer dato real": seguimiento_diario_produccion (legacy, columna
-- lote_postura_produccion_id + fecha_registro) UNION seguimiento_diario_levante
-- con tipo_seguimiento='produccion' (unificado, columna lote_postura_produccion_id
-- + fecha) -- mismo patrón que fn_indicadores_produccion_postura.
-- Idempotente: solo actualiza si la fecha real encontrada es ANTERIOR a la
-- guardada (si ya se sincronizó, MIN() ya no es < la guardada → no-op).
-- ============================================================================

WITH primeros AS (
    SELECT
        lpp.lote_postura_produccion_id,
        LEAST(
            lpp.fecha_inicio_produccion,
            (SELECT MIN(sdp.fecha_registro)
               FROM seguimiento_diario_produccion sdp
              WHERE sdp.lote_postura_produccion_id = lpp.lote_postura_produccion_id
                AND sdp.company_id = lpp.company_id),
            (SELECT MIN(sdl.fecha)
               FROM seguimiento_diario_levante sdl
              WHERE sdl.lote_postura_produccion_id = lpp.lote_postura_produccion_id
                AND sdl.tipo_seguimiento = 'produccion')
        ) AS fecha_minima_real
    FROM lote_postura_produccion lpp
    WHERE lpp.deleted_at IS NULL
      AND lpp.company_id = 1
      AND lpp.fecha_inicio_produccion IS NOT NULL
)
UPDATE lote_postura_produccion lpp
SET fecha_inicio_produccion = pr.fecha_minima_real,
    updated_at              = NOW() AT TIME ZONE 'utc'
FROM primeros pr
WHERE lpp.lote_postura_produccion_id = pr.lote_postura_produccion_id
  AND pr.fecha_minima_real IS NOT NULL
  AND pr.fecha_minima_real < lpp.fecha_inicio_produccion;


-- ============================================================================
-- BLOQUE 4 — REQ-008c / REQ-009: filas de traslado del lote 114→116 mal fechadas
-- con la fecha en que se EJECUTÓ el traslado (2026-06-08 y 2026-06-11) en vez de
-- la fecha del movimiento físico real de las aves. Genera semanas fantasma
-- 34/35 en el Reporte Semanal del lote 114 y hace que 116 arranque su serie con
-- 142 filas previas en saldo 0 (min_fecha real de seguimiento de 116/lpl_id=8:
-- 2025-11-04, muy anterior a la fecha del traslado -- fuerte indicio de que el
-- movimiento físico fue ~2025-11-04, PERO NO ES UNA CONFIRMACIÓN: es una pista
-- para la conversación con Verenice, no un dato a asumir).
--
-- Filas verificadas en BD local (seguimiento_diario_levante, tipo_seguimiento='levante'):
--   id 876/877 (2026-06-08 19:00:00-05): SALIDA 1010 M (114→116) / INGRESO 1010 M
--   id 890/891 (2026-06-11 19:00:00-05): SALIDA 7617 H (114→116) / INGRESO 7617 H
-- ============================================================================

-- ⛔ REQUIERE FECHA REAL DEL MOVIMIENTO (confirmar con Verenice) — NO adivines la fecha.
-- Reemplazar :fecha_real_movimiento_1 / :fecha_real_movimiento_2 por las fechas
-- confirmadas antes de descomentar. Mantiene la hora tal cual está (19:00:00-05)
-- salvo que Verenice indique otra.
--
-- UPDATE seguimiento_diario_levante
-- SET fecha = '<fecha_real_movimiento_1>'::timestamptz
-- WHERE id IN (876, 877) AND es_traslado = true;
--
-- UPDATE seguimiento_diario_levante
-- SET fecha = '<fecha_real_movimiento_2>'::timestamptz
-- WHERE id IN (890, 891) AND es_traslado = true;
--
-- Tras re-fechar, revisar si las filas quedan dentro de una semana ya cerrada
-- por otros registros de esa fecha en 114/116 (posible choque con el índice
-- único uq_sdlr_tipo_lote_rep_fecha (tipo_seguimiento, lote_id, reproductora_id,
-- fecha)) -- si ya existe una fila de ese lote en esa fecha, decidir con negocio
-- si se fusiona en vez de re-fechar.


-- ============================================================================
-- VERIFICACIONES POST-APLICACIÓN (solo lectura, no destructivas -- correr
-- después de cada bloque para confirmar el resultado antes de seguir)
-- ============================================================================

-- Debe devolver fecha_encaset = la de 114/115 (no futura) para 116/117, y los
-- saldos aves_h_actual/aves_m_actual de 116 = 7405/738 (no NULL).
-- SELECT lote_id, lote_nombre, fecha_encaset FROM lotes WHERE lote_id IN (116,117);
-- SELECT lote_postura_levante_id, lote_id, fecha_encaset, aves_h_actual, aves_m_actual
--   FROM lote_postura_levante WHERE lote_id IN (116,117);

-- Debe devolver ano_tabla_genetica = 2026 en las 3 tablas para 13/14.
-- SELECT lote_id, ano_tabla_genetica FROM lotes WHERE lote_id IN (13,14);
-- SELECT lote_id, ano_tabla_genetica FROM lote_postura_levante WHERE lote_id IN (13,14);
-- SELECT lote_id, ano_tabla_genetica FROM lote_postura_produccion WHERE lote_id IN (13,14);

-- Debe devolver fecha_inicio_produccion ~2025-07-16/19 para P-K345A/B (no 2026-04-10).
-- SELECT lote_postura_produccion_id, lote_nombre, fecha_inicio_produccion
--   FROM lote_postura_produccion WHERE lote_id IN (13,14);
