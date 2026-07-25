using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Limpieza de solo-datos (sin cambios de schema): borra TODOS los seguimientos diarios de
    /// <b>Panamá</b> (empresa <c>ItalcolPanama</c>, resuelta por nombre) — tanto el seguimiento
    /// diario de <b>pollo engorde</b> (<c>seguimiento_diario_aves_engorde</c>, incluidos los
    /// espejos <c>origen_cruce</c> y los de lotes soft-deleted) como el de <b>reproductora
    /// engorde</b> (<c>seguimiento_diario_lote_reproductora_aves_engorde</c>) — para re-digitarlos
    /// con la carga masiva (evita los errores de digitación acumulados).
    ///
    /// Además revierte el único efecto colateral vivo de esos registros: el consumo de alimento del
    /// inventario (modelo B). Los movimientos <c>Consumo</c>/<c>Ingreso</c> con referencia
    /// «Seguimiento aves engorde #…» / «Seguimiento reproductora #…» en granjas de Panamá se
    /// netean por (granja, núcleo, galpón, ítem) y ese neto se DEVUELVE a
    /// <c>inventario_gestion_stock</c> (creando la fila si fue eliminada después del consumo, con
    /// país por <c>departamentos.pais_id</c> igual que el runtime); luego se borran esos movimientos
    /// y su espejo en <c>lote_registro_historico_unificado</c> (el trigger espejo es solo-INSERT,
    /// no cascadea). Sin la devolución, la re-carga masiva descontaría el alimento DOS veces
    /// (la carga masiva reutiliza <c>CreateAsync</c>, que replica todos los efectos).
    ///
    /// Orden crítico:
    ///   1º engorde y 2º reproductora → el trigger <c>trg_cruce_reproductora_engorde</c> (AFTER por
    ///   fila) corre <c>fn_cruce_reproductora_a_engorde</c> sobre estado ya vacío y queda no-op
    ///   (no regenera cruces ni hace falta DISABLE TRIGGER);
    ///   3º devolver stock ANTES de 4º borrar los movimientos (el neto se calcula de los movimientos
    ///   aún existentes) y 5º borrar el espejo del histórico.
    ///
    /// No toca: lotes engorde/reproductora (fechas, aves iniciales, corridas), historial "Inicio",
    /// ventas (movimiento_pollo_engorde / VENTA_AVES), ingresos y traslados reales de alimento,
    /// ni datos de otras empresas. Retiro de aves: no aplica en Panamá (auditado: 0 filas en
    /// inventario_aves; los saldos de aves se derivan de los seguimientos).
    ///
    /// Idempotente: re-ejecutar deja los DELETE en 0 filas y la CTE de neteo vacía (no-op), por lo
    /// que el stock no se ajusta dos veces.
    /// </summary>
    public partial class LimpiezaSeguimientosEngordePanama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ─────────────────────────────────────────────────────────────────────────────
-- 1) Seguimiento diario POLLO ENGORDE de lotes de Panamá (incluye origen_cruce
--    y lotes soft-deleted). Sin triggers ni FKs sobre esta tabla.
-- ─────────────────────────────────────────────────────────────────────────────
DELETE FROM public.seguimiento_diario_aves_engorde s
USING public.lote_ave_engorde lae
WHERE lae.lote_ave_engorde_id = s.lote_ave_engorde_id
  AND lae.company_id IN (SELECT id FROM public.companies WHERE name = 'ItalcolPanama');

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Seguimiento diario REPRODUCTORA ENGORDE de lotes de Panamá. El trigger de
--    cruce dispara fn_cruce_reproductora_a_engorde por fila, pero con el paso 1
--    ya ejecutado y esta tabla vaciada no encuentra nada que consolidar (no-op).
-- ─────────────────────────────────────────────────────────────────────────────
DELETE FROM public.seguimiento_diario_lote_reproductora_aves_engorde s
USING public.lote_reproductora_ave_engorde lr,
      public.lote_ave_engorde lae
WHERE lr.id = s.lote_reproductora_ave_engorde_id
  AND lae.lote_ave_engorde_id = lr.lote_ave_engorde_id
  AND lae.company_id IN (SELECT id FROM public.companies WHERE name = 'ItalcolPanama');

-- ─────────────────────────────────────────────────────────────────────────────
-- 3a) Devolver al stock el alimento consumido por los seguimientos borrados:
--     neto = Σ Consumo − Σ Ingreso(devoluciones) por (granja, núcleo, galpón,
--     ítem). Todo movimiento presente tiene su efecto vivo en stock (la
--     anulación manual borra el movimiento tras revertir), así el neto es
--     exactamente lo pendiente de devolver.
-- ─────────────────────────────────────────────────────────────────────────────
WITH pan AS (
    SELECT id FROM public.companies WHERE name = 'ItalcolPanama'
),
mov AS (
    SELECT m.farm_id, m.nucleo_id, m.galpon_id,
           m.item_inventario_ecuador_id AS item_id,
           SUM(CASE WHEN m.movement_type = 'Consumo' THEN m.quantity ELSE -m.quantity END) AS neto
    FROM public.inventario_gestion_movimiento m
    JOIN public.farms f ON f.id = m.farm_id
    WHERE f.company_id IN (SELECT id FROM pan)
      AND (m.reference LIKE 'Seguimiento aves engorde #%'
        OR m.reference LIKE 'Seguimiento reproductora #%')
    GROUP BY m.farm_id, m.nucleo_id, m.galpon_id, m.item_inventario_ecuador_id
)
UPDATE public.inventario_gestion_stock s
SET quantity   = s.quantity + mov.neto,
    updated_at = now()
FROM mov
WHERE s.farm_id = mov.farm_id
  AND s.item_inventario_ecuador_id = mov.item_id
  AND s.nucleo_id IS NOT DISTINCT FROM mov.nucleo_id
  AND s.galpon_id IS NOT DISTINCT FROM mov.galpon_id;

-- ─────────────────────────────────────────────────────────────────────────────
-- 3b) Ubicaciones cuya fila de stock fue eliminada después del consumo
--     (EliminacionStock): crearla con el neto, espejo del create-if-missing de
--     AnularMovimientoHistoricoAsync (company de la granja, país por
--     departamentos.pais_id, unidad kg).
-- ─────────────────────────────────────────────────────────────────────────────
WITH pan AS (
    SELECT id FROM public.companies WHERE name = 'ItalcolPanama'
),
mov AS (
    SELECT m.farm_id, m.nucleo_id, m.galpon_id,
           m.item_inventario_ecuador_id AS item_id,
           SUM(CASE WHEN m.movement_type = 'Consumo' THEN m.quantity ELSE -m.quantity END) AS neto
    FROM public.inventario_gestion_movimiento m
    JOIN public.farms f ON f.id = m.farm_id
    WHERE f.company_id IN (SELECT id FROM pan)
      AND (m.reference LIKE 'Seguimiento aves engorde #%'
        OR m.reference LIKE 'Seguimiento reproductora #%')
    GROUP BY m.farm_id, m.nucleo_id, m.galpon_id, m.item_inventario_ecuador_id
)
INSERT INTO public.inventario_gestion_stock
      (company_id, pais_id, farm_id, nucleo_id, galpon_id,
       item_inventario_ecuador_id, quantity, unit, created_at, updated_at)
SELECT f.company_id, d.pais_id, mov.farm_id, mov.nucleo_id, mov.galpon_id,
       mov.item_id, mov.neto, 'kg', now(), now()
FROM mov
JOIN public.farms f          ON f.id = mov.farm_id
JOIN public.departamentos d  ON d.departamento_id = f.departamento_id
WHERE mov.neto > 0
  AND NOT EXISTS (
      SELECT 1
      FROM public.inventario_gestion_stock s
      WHERE s.farm_id = mov.farm_id
        AND s.item_inventario_ecuador_id = mov.item_id
        AND s.nucleo_id IS NOT DISTINCT FROM mov.nucleo_id
        AND s.galpon_id IS NOT DISTINCT FROM mov.galpon_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) Borrar los movimientos de inventario ligados a los seguimientos borrados
--    (después de 3a/3b, que los usan para el neteo).
-- ─────────────────────────────────────────────────────────────────────────────
DELETE FROM public.inventario_gestion_movimiento m
USING public.farms f
WHERE f.id = m.farm_id
  AND f.company_id IN (SELECT id FROM public.companies WHERE name = 'ItalcolPanama')
  AND (m.reference LIKE 'Seguimiento aves engorde #%'
    OR m.reference LIKE 'Seguimiento reproductora #%');

-- ─────────────────────────────────────────────────────────────────────────────
-- 5) Borrar el espejo del histórico unificado (INV_CONSUMO/INV_INGRESO ligados,
--    incluidos los ya anulados): el trigger espejo es solo-INSERT y no cascadea.
-- ─────────────────────────────────────────────────────────────────────────────
DELETE FROM public.lote_registro_historico_unificado h
USING public.farms f
WHERE f.id = h.farm_id
  AND f.company_id IN (SELECT id FROM public.companies WHERE name = 'ItalcolPanama')
  AND (h.referencia LIKE 'Seguimiento aves engorde #%'
    OR h.referencia LIKE 'Seguimiento reproductora #%');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Limpieza de una sola vía: los seguimientos borrados no se pueden reconstruir desde la
            // BD (se re-digitan con la carga masiva). No-op intencional.
        }
    }
}
