-- SIN-MIGRACION: diagnóstico de solo lectura. No crea ni modifica ningún objeto.
--
-- Disponibilidad de un lote de postura: qué devolvía ANTES y qué devuelve AHORA.
--
-- El endpoint elegía entre aves y huevos según `lotes.fase`, y devolvía la otra mitad en NULL. Eso
-- rompía dos cosas a la vez:
--   * los lotes con huevos estaban en fase='Levante' y nunca llegaban al camino de huevos;
--   * los lotes ruteados a huevos quedaban con `Aves = null`, y `ValidarDisponibilidadAvesAsync`
--     devuelve false en ese caso ⇒ traslados de aves BLOQUEADOS.
--
-- Ahora la fase sale de la regla canónica (levante cerrado Y fila viva en lote_postura_produccion),
-- `Aves` se informa siempre y `Huevos` cuando hay LPP.

\echo === 1) Ruteo: fase declarada vs regla canonica, y que se destraba ===
SELECT
  l.lote_id,
  l.lote_nombre,
  l.fase                                   AS fase_declarada,
  COALESCE(lev.estado_cierre,'(sin fila)') AS cierre_levante,
  COALESCE(p.lote_postura_produccion_id::text,'-') AS lpp,
  -- lo que hacía el codigo viejo
  CASE WHEN l.fase = 'Produccion' THEN 'Huevos (Aves=null)' ELSE 'Aves' END AS antes,
  -- la regla canonica: levante cerrado Y LPP viva
  CASE WHEN lev.estado_cierre = 'Cerrado' AND p.lote_postura_produccion_id IS NOT NULL
       THEN 'Produccion' ELSE 'Levante' END AS tipo_lote_ahora,
  CASE WHEN l.fase = 'Produccion' AND (COALESCE(l.hembras_l,0) + COALESCE(l.machos_l,0)) > 0
       THEN COALESCE(l.hembras_l,0) + COALESCE(l.machos_l,0) ELSE 0 END AS aves_que_estaban_bloqueadas
FROM lotes l
LEFT JOIN lote_postura_produccion p ON p.lote_id = l.lote_id AND p.deleted_at IS NULL
LEFT JOIN lote_postura_levante   lev ON lev.lote_id = l.lote_id AND lev.deleted_at IS NULL
WHERE l.deleted_at IS NULL
  AND (l.fase = 'Produccion' OR p.lote_postura_produccion_id IS NOT NULL)
ORDER BY l.lote_id;

\echo === 2) Aves: cuanto sobrestimaba la formula que solo miraba levante ===
-- La fórmula vieja restaba solo la mortalidad de seguimiento_diario_levante. Para un lote que ya
-- pasó a producción, la mortalidad y la selección de produccion tambien salieron del lote.
WITH lev AS (
  SELECT s.lote_id::int AS lote_id,
         -- lo unico que restaba la formula vieja
         SUM(COALESCE(s.mortalidad_hembras,0)) AS mort_h,
         SUM(COALESCE(s.mortalidad_machos,0))  AS mort_m,
         -- lo que ignoraba: seleccion y error de sexaje TAMBIEN salieron del lote
         SUM(COALESCE(s.sel_h,0) + COALESCE(s.error_sexaje_hembras,0)) AS otras_h,
         SUM(COALESCE(s.sel_m,0) + COALESCE(s.error_sexaje_machos,0))  AS otras_m
  FROM seguimiento_diario_levante s
  WHERE s.lote_id ~ '^[0-9]+$'
  GROUP BY 1
),
prod AS (
  SELECT p.lote_id,
         SUM(sp.mortalidad_hembras + sp.sel_h + sp.error_sexaje_hembras) AS bajas_h,
         SUM(sp.mortalidad_machos  + sp.sel_m + sp.error_sexaje_machos)  AS bajas_m
  FROM seguimiento_diario_produccion sp
  JOIN lote_postura_produccion p
    ON p.lote_postura_produccion_id = sp.lote_postura_produccion_id AND p.deleted_at IS NULL
  GROUP BY 1
),
ret AS (
  SELECT m.lote_origen_id AS lote_id,
         SUM(COALESCE(m.cantidad_hembras,0)) AS ret_h,
         SUM(COALESCE(m.cantidad_machos,0))  AS ret_m
  FROM movimiento_aves m
  WHERE m.estado = 'Completado'
  GROUP BY 1
)
SELECT
  l.lote_id, l.lote_nombre,
  GREATEST(0, COALESCE(l.hembras_l,0) - COALESCE(lev.mort_h,0) - COALESCE(ret.ret_h,0)) AS h_antes,
  GREATEST(0, COALESCE(l.hembras_l,0) - COALESCE(lev.mort_h,0) - COALESCE(lev.otras_h,0)
              - COALESCE(prod.bajas_h,0) - COALESCE(ret.ret_h,0))                        AS h_ahora,
  GREATEST(0, COALESCE(l.machos_l,0) - COALESCE(lev.mort_m,0) - COALESCE(ret.ret_m,0))   AS m_antes,
  GREATEST(0, COALESCE(l.machos_l,0) - COALESCE(lev.mort_m,0) - COALESCE(lev.otras_m,0)
              - COALESCE(prod.bajas_m,0) - COALESCE(ret.ret_m,0))                        AS m_ahora,
  COALESCE(lev.otras_h,0) + COALESCE(lev.otras_m,0)
    + COALESCE(prod.bajas_h,0) + COALESCE(prod.bajas_m,0)                                AS aves_que_faltaba_restar
FROM lotes l
LEFT JOIN lev  ON lev.lote_id  = l.lote_id
LEFT JOIN prod ON prod.lote_id = l.lote_id
LEFT JOIN ret  ON ret.lote_id  = l.lote_id
WHERE l.deleted_at IS NULL
  AND (l.fase = 'Produccion' OR EXISTS (SELECT 1 FROM lote_postura_produccion p
                                        WHERE p.lote_id = l.lote_id AND p.deleted_at IS NULL))
ORDER BY aves_que_faltaba_restar DESC, l.lote_id;

\echo === 3) Huevos que el endpoint por lote pasa a informar (antes: 0 en todos) ===
SELECT
  l.lote_id, l.lote_nombre,
  p.lote_postura_produccion_id AS lpp,
  0                            AS huevos_antes,
  COALESCE(e.huevo_tot_dinamico, 0)  AS huevos_ahora,
  COALESCE(e.huevo_tot_historico, 0) AS historico,
  CASE WHEN e.lote_postura_produccion_id IS NULL
       THEN 'sin fila de espejo: el service la recalcula al primer pedido' ELSE '' END AS nota
FROM lotes l
JOIN lote_postura_produccion p ON p.lote_id = l.lote_id AND p.deleted_at IS NULL
LEFT JOIN espejo_huevo_produccion e ON e.lote_postura_produccion_id = p.lote_postura_produccion_id
WHERE l.deleted_at IS NULL
ORDER BY l.lote_id;
