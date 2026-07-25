using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Limpieza de solo-datos (sin cambios de schema), complemento de
    /// <see cref="LimpiezaSeguimientosEngordePanama"/> (20260725120000): deja el INVENTARIO de
    /// alimento de <b>Panamá</b> (empresa <c>ItalcolPanama</c>, resuelta por nombre) en CERO para
    /// que la operación arranque desde el inicio — se borran los ingresos de alimento (Entrada
    /// planta/granja), traslados (incl. tránsitos inter-granja), eliminaciones de stock, el stock
    /// por ubicación y todos los eventos <c>INV_*</c> del histórico unificado.
    ///
    /// Orden con la migración anterior: aquélla corre primero (borra seguimientos y les devuelve el
    /// consumo al stock); ésta corre después y deja stock/movimientos/histórico de inventario en 0.
    /// Cada una queda consistente por sí sola.
    ///
    /// Qué NO toca:
    ///   • el CATÁLOGO de ítems de inventario (Panamá lo necesita para la re-carga masiva);
    ///   • las ventas de aves (<c>movimiento_pollo_engorde</c> y su evento <c>VENTA_AVES</c> del
    ///     histórico): no son inventario de alimento;
    ///   • lotes, historial "Inicio", guía genética, ni nada de otras empresas.
    /// Auditado en copia de prod (2026-07-25): las tablas legacy (<c>farm_product_inventory</c>,
    /// <c>farm_inventory_movements</c>, <c>historial_inventario</c>, <c>inventario_gasto*</c>,
    /// <c>inventario_aves</c>) tienen 0 filas de Panamá; no hay FKs hacia las tablas borradas y no
    /// existen traslados cross-empresa. En las tres tablas el scope por <c>company_id</c> coincide
    /// 1:1 con el scope por granja.
    ///
    /// Nota operativa: tras el deploy hay que REGISTRAR PRIMERO los ingresos de alimento y recién
    /// después correr la carga masiva de seguimientos — el descuento de consumo con stock
    /// insuficiente se ignora en silencio (try/catch) y ese consumo quedaría sin registrar.
    ///
    /// Idempotente: DELETEs puros; re-ejecutar afecta 0 filas.
    /// </summary>
    public partial class LimpiezaInventarioAlimentoPanama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ─────────────────────────────────────────────────────────────────────────────
-- 1) Histórico unificado: TODOS los eventos de inventario (tipo_evento INV_*:
--    INV_INGRESO, INV_CONSUMO, INV_TRASLADO_ENTRADA/SALIDA, INV_OTRO) de la
--    empresa. Se conserva VENTA_AVES (ventas, no inventario). El '\_' escapa el
--    guion bajo del LIKE.
-- ─────────────────────────────────────────────────────────────────────────────
DELETE FROM public.lote_registro_historico_unificado h
WHERE h.company_id IN (SELECT id FROM public.companies WHERE name = 'ItalcolPanama')
  AND h.tipo_evento LIKE 'INV\_%';

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Movimientos de inventario de la empresa (ingresos Entrada planta/granja,
--    traslados y tránsitos inter-granja, eliminaciones de stock). El trigger
--    que espeja al histórico es solo-INSERT: no re-inserta nada al borrar.
-- ─────────────────────────────────────────────────────────────────────────────
DELETE FROM public.inventario_gestion_movimiento m
WHERE m.company_id IN (SELECT id FROM public.companies WHERE name = 'ItalcolPanama');

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) Stock por ubicación de la empresa: en cero. El runtime recrea cada fila al
--    registrar el próximo ingreso (RegistrarIngresoAsync crea si no existe).
-- ─────────────────────────────────────────────────────────────────────────────
DELETE FROM public.inventario_gestion_stock s
WHERE s.company_id IN (SELECT id FROM public.companies WHERE name = 'ItalcolPanama');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Limpieza de una sola vía: los ingresos/traslados/stock borrados no se pueden
            // reconstruir desde la BD (la operación los re-registra desde cero). No-op intencional.
        }
    }
}
