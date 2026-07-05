using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Unificación de inventario: migra Colombia (company 1) del módulo VIEJO (modelo A:
    /// farm_product_inventory / farm_inventory_movements / catalogo_items) al módulo NUEVO
    /// unificado (modelo B: inventario_gestion_stock / inventario_gestion_movimiento /
    /// item_inventario_ecuador) a NIVEL GRANJA (nucleo/galpon NULL; Colombia maneja_alimento_por_galpon=false).
    ///
    /// Data-only + IDEMPOTENTE (INSERT ... WHERE NOT EXISTS / DELETE). Sin DDL: el esquema destino
    /// ya está alineado. Habilita el consumo del seguimiento diario Colombia (ColombiaInventarioConsumoService,
    /// mapeo A→B por código), que estaba inerte por falta de ítems en el catálogo nuevo.
    ///
    /// Fuente/spec probada en local (PG17): backend/sql/migracion_inventario_colombia_0{1..4}_*.sql.
    /// Verificado: 61 ítems, 20 saldos idénticos al origen, 323 movimientos con kardex reconciliando 21/21.
    /// </summary>
    public partial class MigrarInventarioColombiaAModeloB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── PASO 1 · ÍTEMS: catalogo_items(co1) → item_inventario_ecuador(co1, pais1) ──
            // UNIQUE (company_id, pais_id, codigo). Colombia = alimento (nivel granja).
            migrationBuilder.Sql(@"
INSERT INTO public.item_inventario_ecuador
    (codigo, nombre, tipo_item, unidad, activo, company_id, pais_id, concepto, created_at, updated_at)
SELECT c.codigo, c.nombre, lower(c.item_type), 'kg', c.activo, 1, 1, NULL, now(), now()
FROM public.catalogo_items c
WHERE c.company_id = 1
  AND NOT EXISTS (
      SELECT 1 FROM public.item_inventario_ecuador i
      WHERE i.company_id = 1 AND i.pais_id = 1 AND i.codigo = c.codigo
  );");

            // ── PASO 2 · STOCK: farm_product_inventory(co1) → inventario_gestion_stock(co1, pais1) ──
            // Nivel granja (nucleo/galpon NULL). Preserva quantity, unit y la FECHA ORIGINAL.
            migrationBuilder.Sql(@"
INSERT INTO public.inventario_gestion_stock
    (company_id, pais_id, farm_id, nucleo_id, galpon_id, item_inventario_ecuador_id, quantity, unit, created_at, updated_at)
SELECT 1, 1, fpi.farm_id, NULL, NULL, i.id, fpi.quantity, fpi.unit, fpi.created_at, fpi.updated_at
FROM public.farm_product_inventory fpi
JOIN public.catalogo_items          c ON c.id = fpi.catalog_item_id
JOIN public.item_inventario_ecuador i ON i.company_id = 1 AND i.pais_id = 1 AND i.codigo = c.codigo
WHERE fpi.company_id = 1
  AND NOT EXISTS (
      SELECT 1 FROM public.inventario_gestion_stock s
      WHERE s.company_id = 1 AND s.farm_id = fpi.farm_id
        AND s.item_inventario_ecuador_id = i.id
        AND s.nucleo_id IS NULL AND s.galpon_id IS NULL
  );");

            // ── PASO 3 · MOVIMIENTOS: farm_inventory_movements(co1) → inventario_gestion_movimiento ──
            // Entry→Ingreso(+), Exit→Consumo(-), TransferIn→TrasladoEntrada(+), TransferOut→TrasladoSalida(-).
            // Preserva created_at original (orden del kardex), reference, reason, transfer_group_id, usuario.
            migrationBuilder.Sql(@"
INSERT INTO public.inventario_gestion_movimiento
    (company_id, pais_id, farm_id, nucleo_id, galpon_id, item_inventario_ecuador_id,
     quantity, unit, movement_type, reference, reason, transfer_group_id,
     created_at, created_by_user_id, estado)
SELECT 1, 1, m.farm_id, NULL, NULL, i.id, m.quantity, m.unit,
    CASE m.movement_type
        WHEN 'Entry'       THEN 'Ingreso'
        WHEN 'Exit'        THEN 'Consumo'
        WHEN 'TransferIn'  THEN 'TrasladoEntrada'
        WHEN 'TransferOut' THEN 'TrasladoSalida'
        ELSE 'AjusteStock'
    END,
    m.reference,
    COALESCE(NULLIF(m.reason, ''), m.destination, m.origin),
    m.transfer_group_id, m.created_at, m.responsible_user_id, NULL
FROM public.farm_inventory_movements m
JOIN public.catalogo_items          c ON c.id = m.catalog_item_id
JOIN public.item_inventario_ecuador i ON i.company_id = 1 AND i.pais_id = 1 AND i.codigo = c.codigo
WHERE m.company_id = 1
  AND NOT EXISTS (
      SELECT 1 FROM public.inventario_gestion_movimiento d
      WHERE d.company_id = 1 AND d.farm_id = m.farm_id
        AND d.item_inventario_ecuador_id = i.id
        AND d.created_at = m.created_at AND d.quantity = m.quantity AND d.unit = m.unit
  );");

            // ── PASO 4 · MENÚ: quitar el inventario VIEJO (menus 10 y 32 → /inventario) de Colombia ──
            // Deja solo el nuevo (menu 50 → /gestion-inventario) + Ítems (49). No borra código del viejo.
            migrationBuilder.Sql(@"
DELETE FROM public.company_menus WHERE company_id = 1 AND menu_id IN (10, 32);
DELETE FROM public.role_menus    WHERE menu_id IN (10, 32) AND role_id IN (1, 5, 12);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vacío: NO se revierte una migración de datos de inventario en producción
            // (Colombia ya opera sobre el módulo nuevo; el consumo del seguimiento escribe ahí). Revertir
            // borraría saldos/movimientos vivos. El módulo viejo y su menú se retiran por separado tras validar.
        }
    }
}
