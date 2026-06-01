-- =============================================================================
-- Vistas READ-ONLY de validación de alimento engorde (plan fase_de_desarrollo/19).
-- Reconcilian el SEGUIMIENTO (fuente de verdad acordada) contra el INVENTARIO real.
--
-- Contexto:
--   * inventario_gestion_stock = stock físico actual por (farm,núcleo,galpón,item/tipo).
--   * Los movimientos Consumo del inventario se generan desde el seguimiento; su `reference`
--     trae "Seguimiento aves engorde #<seg_id> <fecha>" → atribuibles a su lote.
--   * El consumo del seguimiento que NO generó movimiento Consumo => stock sobreestimado.
--   * El stock es a nivel GALPÓN y acumulado entre ciclos secuenciales (2601→2602→2603);
--     el saldo de seguimiento es por ciclo. Comparaciones de stock son galpón-actual.
-- =============================================================================

-- 1) Validación POR LOTE -------------------------------------------------------
CREATE OR REPLACE VIEW vw_validacion_alimento_engorde_por_lote AS
WITH lotes AS (
  SELECT lote_ave_engorde_id id, lote_nombre, granja_id, COALESCE(TRIM(nucleo_id),'') nuc,
         COALESCE(TRIM(galpon_id),'') gal, galpon_id, fecha_encaset::date enc, estado_operativo_lote
  FROM lote_ave_engorde WHERE deleted_at IS NULL),
rng AS (
  SELECT lote_ave_engorde_id id, MIN(fecha)::date fmin, MAX(fecha)::date fmax,
         ROUND(SUM(COALESCE(consumo_kg_hembras,0)+COALESCE(consumo_kg_machos,0))::numeric,0) cons_seg
  FROM seguimiento_diario_aves_engorde GROUP BY 1),
saldo_ult AS (
  SELECT s.lote_ave_engorde_id id, s.saldo_alimento_kg saldo
  FROM seguimiento_diario_aves_engorde s
  WHERE (s.lote_ave_engorde_id, s.fecha) IN (SELECT lote_ave_engorde_id, MAX(fecha) FROM seguimiento_diario_aves_engorde GROUP BY 1)),
inv_cons_attr AS (
  SELECT s.lote_ave_engorde_id id, ROUND(SUM(m.quantity)::numeric,0) cons_inv_attr
  FROM inventario_gestion_movimiento m
  JOIN seguimiento_diario_aves_engorde s ON s.id = (substring(m.reference from '#([0-9]+)'))::int
  WHERE m.movement_type='Consumo' AND m.reference LIKE 'Seguimiento aves engorde #%'
  GROUP BY 1),
stock_now AS (
  SELECT l.id, ROUND(SUM(st.quantity)::numeric,0) stock_kg
  FROM lotes l JOIN inventario_gestion_stock st
    ON st.farm_id=l.granja_id AND COALESCE(TRIM(st.nucleo_id),'')=l.nuc AND COALESCE(TRIM(st.galpon_id),'')=l.gal
  GROUP BY l.id),
ing_antes_enc AS (
  SELECT l.id, COUNT(*) n, ROUND(SUM(h.cantidad_kg)::numeric,0) kg
  FROM lotes l JOIN lote_registro_historico_unificado h
    ON h.farm_id=l.granja_id AND COALESCE(TRIM(h.nucleo_id),'')=l.nuc AND COALESCE(TRIM(h.galpon_id),'')=l.gal
  WHERE NOT h.anulado AND h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA')
    AND NOT (h.tipo_evento='INV_INGRESO' AND h.referencia LIKE 'Seguimiento aves engorde #%')
    AND l.enc IS NOT NULL AND h.fecha_operacion < l.enc
  GROUP BY l.id)
SELECT l.id, l.lote_nombre, l.granja_id, l.galpon_id, l.estado_operativo_lote,
  l.enc fecha_encaset, r.fmin primer_seg, r.fmax ultimo_seg,
  COALESCE(iae.n,0) ingresos_antes_encaset, COALESCE(iae.kg,0) kg_antes_encaset,
  r.cons_seg, COALESCE(ica.cons_inv_attr,0) cons_inv_posteado,
  (r.cons_seg - COALESCE(ica.cons_inv_attr,0)) consumo_no_posteado,
  ROUND(su.saldo::numeric,0) saldo_seg, COALESCE(sn.stock_kg,0) stock_inventario,
  ROUND((su.saldo - COALESCE(sn.stock_kg,0))::numeric,0) dif_saldo_vs_stock
FROM lotes l
LEFT JOIN rng r ON r.id=l.id
LEFT JOIN saldo_ult su ON su.id=l.id
LEFT JOIN inv_cons_attr ica ON ica.id=l.id
LEFT JOIN stock_now sn ON sn.id=l.id
LEFT JOIN ing_antes_enc iae ON iae.id=l.id
WHERE r.id IS NOT NULL;

-- 2) Validación POR LOTE Y TIPO DE ALIMENTO (item) ----------------------------
DROP VIEW IF EXISTS vw_validacion_alimento_engorde_por_tipo;
CREATE VIEW vw_validacion_alimento_engorde_por_tipo AS
WITH lotes AS (
  SELECT lote_ave_engorde_id id, lote_nombre, granja_id, COALESCE(TRIM(nucleo_id),'') nuc,
         COALESCE(TRIM(galpon_id),'') gal, galpon_id, estado_operativo_lote
  FROM lote_ave_engorde WHERE deleted_at IS NULL),
inv_cons AS (
  SELECT s.lote_ave_engorde_id id, m.item_inventario_ecuador_id item,
         ROUND(SUM(m.quantity)::numeric,0) cons_inv
  FROM inventario_gestion_movimiento m
  JOIN seguimiento_diario_aves_engorde s ON s.id=(substring(m.reference from '#([0-9]+)'))::int
  WHERE m.movement_type='Consumo' AND m.reference LIKE 'Seguimiento aves engorde #%'
  GROUP BY 1,2),
stock_g AS (
  SELECT farm_id, COALESCE(TRIM(nucleo_id),'') nuc, COALESCE(TRIM(galpon_id),'') gal,
         item_inventario_ecuador_id item, ROUND(SUM(quantity)::numeric,0) stock_kg
  FROM inventario_gestion_stock GROUP BY 1,2,3,4),
pares AS (
  SELECT id, item FROM inv_cons
  UNION
  SELECT l.id, sg.item FROM lotes l JOIN stock_g sg
    ON sg.farm_id=l.granja_id AND sg.nuc=l.nuc AND sg.gal=l.gal
)
SELECT l.id lote_id, l.lote_nombre, l.galpon_id, l.estado_operativo_lote,
  p.item item_id, it.codigo, it.nombre tipo_alimento,
  COALESCE(ic.cons_inv,0) consumo_inv_posteado_lote,
  COALESCE(sg.stock_kg,0) stock_galpon_actual
FROM pares p
JOIN lotes l ON l.id=p.id
LEFT JOIN item_inventario_ecuador it ON it.id=p.item
LEFT JOIN inv_cons ic ON ic.id=p.id AND ic.item=p.item
LEFT JOIN stock_g sg ON sg.farm_id=l.granja_id AND sg.nuc=l.nuc AND sg.gal=l.gal AND sg.item=p.item;
