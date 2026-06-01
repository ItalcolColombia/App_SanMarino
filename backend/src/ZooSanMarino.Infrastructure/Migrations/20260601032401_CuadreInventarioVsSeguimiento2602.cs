using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// CUADRE inventario = seguimiento (fuente de verdad = SEGUIMIENTO). Para cada galpón con lote "2602",
    /// fija el stock por tipo al EXPECTED M0 del lote ACTIVO (último abierto: 2602 o el 2603 que lo relevó;
    /// ciclos cerrados excluidos → NO arrastra sobrante): expected(item) = GREATEST(0, ingresos_ciclo(item)
    /// − consumo(item)), con ingresos cycle-scoped (>= encaset, incluye fantasma) y consumo = posteado +
    /// faltante atribuido por tipo (multi-tipo → item con más ingresos, DETERMINISTA → idempotente).
    /// Respalda en _backup_cuadre_expected_2026_06_01 y registra AjusteStock por item. Down() restaura.
    /// Validado en LOCAL: 34/34 galpones activos cuadran stock=expected; idempotente (re-run sin cambios).
    /// ⚠️ Muta el inventario real. Revisar antes de prod.
    /// </summary>
    public partial class CuadreInventarioVsSeguimiento2602 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UP_SQL);
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(DOWN_SQL);
        private const string UP_SQL = @"-- =============================================================================
-- CUADRE DEFINITIVO inventario = expected M0 por tipo (fuente de verdad = SEGUIMIENTO).
-- Para cada galpón con lote ""2602"", toma su lote ACTIVO (último abierto: 2602 o el 2603 que
-- lo relevó; ciclos cerrados excluidos → NO arrastra sobrante) y fija el stock por tipo a:
--     expected(item) = GREATEST(0, ingresos_ciclo(item) − consumo_total(item))
-- ingresos_ciclo(item): histórico del item, scope galpón, fecha >= encaset del lote activo
--   (cycle-scoped → respeta eliminaciones de ciclos previos y no arrastra el sobrante anterior),
--   incluye fantasma (cuadrar_saldos/backfill), excluye seguimiento-ingreso y devoluciones.
-- consumo_total(item) = consumo POSTEADO(item) + FALTANTE atribuido(item). El faltante por día
--   se atribuye al tipo (multi-tipo → al item con más stock). Respeta lo ya posteado correctamente.
-- =============================================================================

DROP TABLE IF EXISTS _tmp_expected;
CREATE TEMP TABLE _tmp_expected AS
WITH gal AS (
  SELECT DISTINCT granja_id, COALESCE(TRIM(nucleo_id),'') nuc, COALESCE(TRIM(galpon_id),'') gal
  FROM lote_ave_engorde WHERE lote_nombre LIKE '%2602' AND deleted_at IS NULL),
activo AS (
  SELECT DISTINCT ON (g.granja_id, g.nuc, g.gal)
    l.lote_ave_engorde_id id, l.lote_nombre, l.granja_id, g.nuc, g.gal, l.galpon_id, l.fecha_encaset::date enc, l.company_id
  FROM gal g JOIN lote_ave_engorde l
    ON l.granja_id=g.granja_id AND COALESCE(TRIM(l.nucleo_id),'')=g.nuc AND COALESCE(TRIM(l.galpon_id),'')=g.gal AND l.deleted_at IS NULL
  ORDER BY g.granja_id, g.nuc, g.gal, l.fecha_encaset DESC),
ing AS (
  SELECT a.id, h.item_inventario_ecuador_id item,
    SUM(CASE WHEN h.tipo_evento='INV_INGRESO' THEN h.cantidad_kg WHEN h.tipo_evento='INV_TRASLADO_ENTRADA' THEN h.cantidad_kg
             WHEN h.tipo_evento='INV_TRASLADO_SALIDA' THEN -ABS(h.cantidad_kg) END) ingresos
  FROM activo a JOIN lote_registro_historico_unificado h
    ON h.farm_id=a.granja_id AND COALESCE(TRIM(h.nucleo_id),'')=a.nuc AND COALESCE(TRIM(h.galpon_id),'')=a.gal
  WHERE NOT h.anulado AND h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
    AND NOT (h.tipo_evento='INV_INGRESO' AND h.referencia LIKE 'Seguimiento aves engorde #%')
    AND NOT (h.referencia LIKE '%devoluci%n por eliminaci%n%')
    AND h.item_inventario_ecuador_id IS NOT NULL AND h.fecha_operacion >= a.enc
  GROUP BY a.id, h.item_inventario_ecuador_id),
-- consumo posteado por (lote activo, item) desde el inventario
posted_item AS (
  SELECT a.id, m.item_inventario_ecuador_id item, SUM(m.quantity) posted
  FROM activo a JOIN seguimiento_diario_aves_engorde s ON s.lote_ave_engorde_id=a.id
  JOIN inventario_gestion_movimiento m
    ON m.movement_type='Consumo' AND m.reference LIKE 'Seguimiento aves engorde #%'
   AND (substring(m.reference from '#([0-9]+)'))::int = s.id
  GROUP BY a.id, m.item_inventario_ecuador_id),
-- consumo posteado por seguimiento (para el faltante)
posted_seg AS (
  SELECT (substring(m.reference from '#([0-9]+)'))::int seg_id, SUM(m.quantity) posted
  FROM inventario_gestion_movimiento m
  WHERE m.movement_type='Consumo' AND m.reference LIKE 'Seguimiento aves engorde #%' GROUP BY 1),
-- faltante por seguimiento (consumo reportado − posteado), atribuido al item con más stock del día
gap AS (
  SELECT a.id, s.id seg_id, a.granja_id, a.nuc, a.gal, s.tipo_alimento,
         (COALESCE(s.consumo_kg_hembras,0)+COALESCE(s.consumo_kg_machos,0)) - COALESCE(ps.posted,0) gap_kg
  FROM activo a JOIN seguimiento_diario_aves_engorde s ON s.lote_ave_engorde_id=a.id
  LEFT JOIN posted_seg ps ON ps.seg_id=s.id),
gap_item AS (
  SELECT g.id, g.seg_id, g.gap_kg, COALESCE(itn.id, itnum.id) item, g.granja_id, g.nuc, g.gal
  FROM gap g, LATERAL unnest(string_to_array(g.tipo_alimento,'/')) t(tipo)
  LEFT JOIN item_inventario_ecuador itn ON itn.nombre=TRIM(regexp_replace(TRIM(t.tipo),'^[HM]:\s*','')) AND itn.tipo_item='Alimento'
  LEFT JOIN item_inventario_ecuador itnum ON itnum.id=(CASE WHEN TRIM(regexp_replace(TRIM(t.tipo),'^[HM]:\s*','')) ~ '^[0-9]+$' THEN TRIM(regexp_replace(TRIM(t.tipo),'^[HM]:\s*',''))::int END)
  WHERE g.gap_kg > 0.001 AND COALESCE(itn.id, itnum.id) IS NOT NULL),
-- ranking DETERMINISTA: el faltante multi-tipo va al item con más ingresos del ciclo (inmutable),
-- no al de más stock (que cambia al aplicar el cuadre → no idempotente).
gap_ranked AS (
  SELECT gi.*, ROW_NUMBER() OVER (PARTITION BY gi.seg_id ORDER BY COALESCE(ig.ingresos,0) DESC, gi.item) rn
  FROM gap_item gi LEFT JOIN ing ig ON ig.id=gi.id AND ig.item=gi.item),
gap_by_item AS (SELECT id, item, SUM(gap_kg) faltante FROM gap_ranked WHERE rn=1 GROUP BY id, item),
items AS (SELECT id,item FROM ing UNION SELECT id,item FROM posted_item UNION SELECT id,item FROM gap_by_item)
SELECT a.id lote_id, a.lote_nombre, a.granja_id, a.nuc, a.gal, a.galpon_id, a.company_id, it.item,
       COALESCE(i.ingresos,0) ingresos,
       COALESCE(pi.posted,0) + COALESCE(gb.faltante,0) consumo,
       GREATEST(0, ROUND((COALESCE(i.ingresos,0) - COALESCE(pi.posted,0) - COALESCE(gb.faltante,0))::numeric,3)) expected
FROM items it JOIN activo a ON a.id=it.id
LEFT JOIN ing i ON i.id=it.id AND i.item=it.item
LEFT JOIN posted_item pi ON pi.id=it.id AND pi.item=it.item
LEFT JOIN gap_by_item gb ON gb.id=it.id AND gb.item=it.item;

-- ===== Respaldo + aplicación =====
CREATE TABLE IF NOT EXISTS _backup_cuadre_expected_2026_06_01 (
  stock_id INT, farm_id INT, nucleo_id VARCHAR, galpon_id VARCHAR, item INT, quantity_antes NUMERIC, snap TIMESTAMPTZ DEFAULT now());
INSERT INTO _backup_cuadre_expected_2026_06_01 (stock_id,farm_id,nucleo_id,galpon_id,item,quantity_antes)
SELECT st.id, st.farm_id, st.nucleo_id, st.galpon_id, st.item_inventario_ecuador_id, st.quantity
FROM inventario_gestion_stock st
WHERE EXISTS (SELECT 1 FROM _tmp_expected e WHERE e.granja_id=st.farm_id AND e.nuc=COALESCE(TRIM(st.nucleo_id),'') AND e.gal=COALESCE(TRIM(st.galpon_id),''))
  AND NOT EXISTS (SELECT 1 FROM _backup_cuadre_expected_2026_06_01 b WHERE b.stock_id=st.id);
-- audit: AjusteStock por item que cambia
INSERT INTO inventario_gestion_movimiento (company_id,pais_id,farm_id,nucleo_id,galpon_id,item_inventario_ecuador_id,quantity,unit,movement_type,reference,reason,created_at,created_by_user_id,estado)
SELECT e.company_id,2,e.granja_id,e.nuc,e.gal,e.item,ABS(e.expected-COALESCE(st.quantity,0)),'kg','AjusteStock','Cuadre expected M0 2602 (2026-06-01)','Cuadre inventario=seguimiento (ingresos-consumo)',now(),'cuadre-expected','Ajuste manual'
FROM _tmp_expected e LEFT JOIN inventario_gestion_stock st ON st.farm_id=e.granja_id AND COALESCE(TRIM(st.nucleo_id),'')=e.nuc AND COALESCE(TRIM(st.galpon_id),'')=e.gal AND st.item_inventario_ecuador_id=e.item
WHERE ABS(e.expected-COALESCE(st.quantity,0))>0.001
  AND NOT EXISTS (SELECT 1 FROM inventario_gestion_movimiento m WHERE m.reference='Cuadre expected M0 2602 (2026-06-01)' AND m.farm_id=e.granja_id AND COALESCE(TRIM(m.galpon_id),'')=e.gal AND m.item_inventario_ecuador_id=e.item);
-- set quantity = expected
UPDATE inventario_gestion_stock st SET quantity=e.expected, updated_at=now()
FROM _tmp_expected e WHERE st.farm_id=e.granja_id AND COALESCE(TRIM(st.nucleo_id),'')=e.nuc AND COALESCE(TRIM(st.galpon_id),'')=e.gal
  AND st.item_inventario_ecuador_id=e.item AND st.quantity <> e.expected;
INSERT INTO inventario_gestion_stock (company_id,pais_id,farm_id,nucleo_id,galpon_id,item_inventario_ecuador_id,quantity,unit,created_at,updated_at)
SELECT e.company_id,2,e.granja_id,e.nuc,e.gal,e.item,e.expected,'kg',now(),now()
FROM _tmp_expected e WHERE e.expected>0 AND NOT EXISTS (SELECT 1 FROM inventario_gestion_stock st
  WHERE st.farm_id=e.granja_id AND COALESCE(TRIM(st.nucleo_id),'')=e.nuc AND COALESCE(TRIM(st.galpon_id),'')=e.gal AND st.item_inventario_ecuador_id=e.item);
-- leftover de ciclos previos -> 0
UPDATE inventario_gestion_stock st SET quantity=0, updated_at=now()
WHERE st.quantity<>0
  AND EXISTS (SELECT 1 FROM _tmp_expected e WHERE e.granja_id=st.farm_id AND e.nuc=COALESCE(TRIM(st.nucleo_id),'') AND e.gal=COALESCE(TRIM(st.galpon_id),''))
  AND NOT EXISTS (SELECT 1 FROM _tmp_expected e WHERE e.granja_id=st.farm_id AND e.nuc=COALESCE(TRIM(st.nucleo_id),'') AND e.gal=COALESCE(TRIM(st.galpon_id),'') AND e.item=st.item_inventario_ecuador_id);
";
        private const string DOWN_SQL = @"
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='_backup_cuadre_expected_2026_06_01') THEN
    UPDATE inventario_gestion_stock s SET quantity=b.quantity_antes, updated_at=now()
    FROM _backup_cuadre_expected_2026_06_01 b WHERE b.stock_id=s.id;
  END IF;
END $$;
DELETE FROM inventario_gestion_movimiento WHERE reference='Cuadre expected M0 2602 (2026-06-01)';
";
    }
}
