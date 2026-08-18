-- K345: los días que viven en `seguimiento_diario_levante` Y en `seguimiento_diario_produccion`.
--
-- Contexto (18-ago-2026): K345A (lote 13) y K345B (lote 14) son los ÚNICOS de la base con días en
-- las dos tablas — 15 en total. El guard ya impide crear nuevos; estos son el residuo. 14 son la
-- semana de transición de julio 2025 (misma mortalidad de los dos lados, levante con el alimento y
-- producción con los huevos, que arrancan 33 → 1.595) y 1 es el 7-abr-2026, donde la fila de levante
-- está vacía y dice `observaciones = 'pruebas sistemas'`.
--
-- Decisión (usuario, 18-ago-2026): producción manda desde el primer huevo.
--
-- ⚠️ NO es un DELETE pelado. Medido antes de escribir esto: el alimento YA está en producción con
-- el mismo valor o mayor, pero `sel_m` (21 + 112 = 133 machos seleccionados), el C.V. y la
-- uniformidad viven SOLO en levante, y `produccion_resultado_levante` no los preserva (su
-- `ac_sel_m` llega a 8 cuando el total de levante del lote 13 es 241, y el lote 14 ni figura).
-- Borrar sin rescatar perdería 133 aves. Por eso: PASO 1 rescata, PASO 2 borra.
--
-- IDEMPOTENTE: la 2ª corrida no encuentra traslape y afecta 0 filas.

BEGIN;

-- Respaldo de las filas completas antes de tocarlas (el DELETE es duro: la tabla no tiene soft-delete).
CREATE TABLE IF NOT EXISTS _backup_traslape_levante_k345_20260818 AS
SELECT sl.* FROM seguimiento_diario_levante sl
JOIN seguimiento_diario_produccion sp
  ON sp.lote_id::text = sl.lote_id::text AND sp.fecha_registro::date = sl.fecha::date;

CREATE TEMP TABLE _traslape AS
SELECT sl.id AS lev_id, sp.id AS pro_id
FROM seguimiento_diario_levante sl
JOIN seguimiento_diario_produccion sp
  ON sp.lote_id::text = sl.lote_id::text AND sp.fecha_registro::date = sl.fecha::date;

-- PASO 1 — rescatar lo que solo vive en levante. COALESCE/CASE: jamás pisa un valor que producción
-- ya tenga (por eso `peso_h` conserva sus 3.341,40 y 3.307,20).
UPDATE seguimiento_diario_produccion sp SET
    sel_h              = CASE WHEN COALESCE(sp.sel_h,0) = 0 THEN COALESCE(sl.sel_h, sp.sel_h) ELSE sp.sel_h END,
    sel_m              = CASE WHEN COALESCE(sp.sel_m,0) = 0 THEN COALESCE(sl.sel_m, sp.sel_m) ELSE sp.sel_m END,
    cv_hembras         = COALESCE(sp.cv_hembras, sl.cv_hembras),
    cv_machos          = COALESCE(sp.cv_machos, sl.cv_machos),
    uniformidad        = COALESCE(sp.uniformidad, sl.uniformidad_hembras),
    uniformidad_machos = COALESCE(sp.uniformidad_machos, sl.uniformidad_machos),
    metadata           = COALESCE(sp.metadata, sl.metadata)
FROM seguimiento_diario_levante sl, _traslape t
WHERE t.pro_id = sp.id AND t.lev_id = sl.id;

-- PASO 2 — retirar las filas de levante. El trigger `trg_tombstone_seguimiento_diario_levante`
-- deja cada borrado en `sync_tombstones`, así que los clientes offline se enteran.
DELETE FROM seguimiento_diario_levante WHERE id IN (SELECT lev_id FROM _traslape);

COMMIT;

-- Verificación esperada: 0 días traslapados · SUM(sel_m) de esos días sigue en 133 ·
-- SUM(cons_kg_h+cons_kg_m) de producción sin cambio (18.159,0) · +15 tombstones.
