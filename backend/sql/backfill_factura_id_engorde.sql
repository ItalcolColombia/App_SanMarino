-- ============================================================================
-- Backfill de factura_id en movimiento_pollo_engorde (Parte C / R3.4)
-- ----------------------------------------------------------------------------
-- Objetivo: asignar un mismo factura_id (UID) a todas las líneas de venta que
-- pertenecen a un mismo despacho histórico, para que la lista de ventas las
-- agrupe en una sola factura (como hace ahora CreateVentaGranjaDespachoAsync
-- para los despachos nuevos).
--
-- Criterio de agrupación (igual que la lógica de "Organizar Peso"):
--   (company_id, TRIM(numero_despacho), fecha_movimiento::date, granja_origen_id)
-- Solo se tocan ventas con numero_despacho no vacío y factura_id NULL. Las
-- ventas sueltas sin numero_despacho se dejan en NULL (se muestran como fila
-- simple; el frontend hace fallback correcto).
--
-- IDEMPOTENTE: re-ejecutarlo no cambia nada (ya no quedan factura_id NULL en el
-- universo objetivo). Envuelto en transacción con tabla snapshot para rollback.
-- Ejecutar UNA vez por entorno (local / prod) tras aplicar la migración
-- 20260530020432_AddVentaFacturaMermaSobranteEngorde.
-- ============================================================================

BEGIN;

-- 1) Snapshot de respaldo (solo de las filas que se van a tocar).
CREATE TABLE IF NOT EXISTS _backfill_factura_id_2026_05_30 (
    id          integer PRIMARY KEY,
    factura_id  uuid
);

INSERT INTO _backfill_factura_id_2026_05_30 (id, factura_id)
SELECT m.id, m.factura_id
FROM movimiento_pollo_engorde m
WHERE m.factura_id IS NULL
  AND m.tipo_movimiento = 'Venta'
  AND m.deleted_at IS NULL
  AND NULLIF(TRIM(m.numero_despacho), '') IS NOT NULL
ON CONFLICT (id) DO NOTHING;

-- 2) Un UID por grupo de despacho.
WITH grupos AS (
    SELECT m.company_id,
           TRIM(m.numero_despacho)        AS nd,
           (m.fecha_movimiento::date)      AS dia,
           COALESCE(m.granja_origen_id, 0) AS go,
           gen_random_uuid()               AS fid
    FROM movimiento_pollo_engorde m
    WHERE m.factura_id IS NULL
      AND m.tipo_movimiento = 'Venta'
      AND m.deleted_at IS NULL
      AND NULLIF(TRIM(m.numero_despacho), '') IS NOT NULL
    GROUP BY m.company_id, TRIM(m.numero_despacho), (m.fecha_movimiento::date), COALESCE(m.granja_origen_id, 0)
)
UPDATE movimiento_pollo_engorde m
SET    factura_id = g.fid
FROM   grupos g
WHERE  m.factura_id IS NULL
  AND  m.tipo_movimiento = 'Venta'
  AND  m.deleted_at IS NULL
  AND  m.company_id = g.company_id
  AND  TRIM(m.numero_despacho) = g.nd
  AND  (m.fecha_movimiento::date) = g.dia
  AND  COALESCE(m.granja_origen_id, 0) = g.go;

-- 3) Reporte de control.
DO $$
DECLARE
    v_facturas integer;
    v_lineas   integer;
BEGIN
    SELECT count(DISTINCT factura_id), count(*)
      INTO v_facturas, v_lineas
      FROM movimiento_pollo_engorde
     WHERE factura_id IS NOT NULL AND tipo_movimiento = 'Venta' AND deleted_at IS NULL;
    RAISE NOTICE 'Backfill factura_id: % facturas con % líneas de venta.', v_facturas, v_lineas;
END $$;

COMMIT;
