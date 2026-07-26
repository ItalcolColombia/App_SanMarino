using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// La empresa <c>Demo</c> vuelve a la clasificación de huevos CLÁSICA de Sanmarino en el
    /// seguimiento diario de producción (decisión 25-jul-2026 PM): revierte SOLO la parte de
    /// huevos del pivote <c>20260726000000_ActivarFeaturesSantaReyesEnDemo</c>.
    /// <list type="number">
    /// <item>Apaga <c>clasificacion_huevo_por_items</c> en Demo → modal/grilla/indicadores/
    /// reporte contable quedan con las 11 columnas clásicas que suman a Huevo Total (con flag
    /// OFF el comportamiento previo es byte a byte, cubierto por los tests del gating).</item>
    /// <item>Quita de Demo los 21 ítems de huevo copiados desde Santa Reyes (por <c>codigo</c>
    /// presente en el catálogo huevo de SR, espejo del Down de la activación), con guardas:
    /// sin referencias en inventario (<c>farm_inventory_movements</c> /
    /// <c>farm_product_inventory</c>, FKs RESTRICT) ni en
    /// <c>seguimiento_diario_produccion.metadata->'huevoItems'</c> de lotes de Demo — si el
    /// cliente ya clasificó, esos ítems se conservan (el flag OFF ya oculta la UI y
    /// <c>huevo_tot</c> guarda los totales legacy).</item>
    /// </list>
    /// Los otros dos flags de Demo (<c>maneja_codigos_erp_avicola</c>,
    /// <c>permite_traslado_aves_cross_etapa</c>) NO se tocan. Santa Reyes no se toca.
    /// Data-only (ModelSnapshot intacto), idempotente y por lookups de nombre (ids difieren
    /// local↔prod); si Demo o Santa Reyes no existieran, todos los bloques son no-op.
    /// </summary>
    public partial class DesactivarClasificacionHuevoItemsEnDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Flag OFF: la parte de huevos de Demo vuelve a la clásica ───────────────
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET clasificacion_huevo_por_items = false
 WHERE name = 'Demo'
   AND clasificacion_huevo_por_items IS DISTINCT FROM false;
");

            // ── 2. Quitar de Demo el catálogo huevo copiado de Santa Reyes ────────────────
            migrationBuilder.Sql(@"
DELETE FROM public.catalogo_items d
 WHERE d.company_id = (SELECT id FROM public.companies WHERE name = 'Demo' LIMIT 1)
   AND d.item_type = 'huevo'
   AND d.codigo IN (
        SELECT s.codigo FROM public.catalogo_items s
         WHERE s.company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes' LIMIT 1)
           AND s.item_type = 'huevo')
   AND NOT EXISTS (SELECT 1 FROM public.farm_inventory_movements fim WHERE fim.catalog_item_id = d.id)
   AND NOT EXISTS (SELECT 1 FROM public.farm_product_inventory  fpi WHERE fpi.catalog_item_id = d.id)
   AND NOT EXISTS (
        SELECT 1
          FROM public.seguimiento_diario_produccion s
          JOIN public.lotes l ON l.lote_id = s.lote_id
          JOIN LATERAL jsonb_array_elements(
                 CASE WHEN jsonb_typeof(s.metadata->'huevoItems') = 'array'
                      THEN s.metadata->'huevoItems' ELSE '[]'::jsonb END) it ON true
         WHERE l.company_id = d.company_id
           AND (it->>'catalogItemId') ~ '^[0-9]+$'
           AND (it->>'catalogItemId')::int = d.id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Simétrico best-effort: Demo vuelve a exhibir la clasificación por ítems
            // (mismo flag + misma copia de catálogo que 20260726000000).
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET clasificacion_huevo_por_items = true
 WHERE name = 'Demo'
   AND clasificacion_huevo_por_items IS DISTINCT FROM true;
");

            migrationBuilder.Sql(@"
INSERT INTO public.catalogo_items
    (codigo, nombre, metadata, activo, created_at, updated_at, company_id, pais_id, item_type)
SELECT src.codigo, src.nombre, src.metadata, src.activo, now(), now(),
       demo.id, src.pais_id, src.item_type
  FROM public.catalogo_items src
  CROSS JOIN (SELECT id FROM public.companies WHERE name = 'Demo' LIMIT 1) demo
 WHERE src.company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes' LIMIT 1)
   AND src.item_type = 'huevo'
   AND src.activo = true
   AND NOT EXISTS (
        SELECT 1 FROM public.catalogo_items d
         WHERE d.company_id = demo.id
           AND d.pais_id IS NOT DISTINCT FROM src.pais_id
           AND d.codigo = src.codigo);
");
        }
    }
}
