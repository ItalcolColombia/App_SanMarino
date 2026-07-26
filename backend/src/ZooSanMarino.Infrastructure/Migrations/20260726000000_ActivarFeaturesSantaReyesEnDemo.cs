using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Santa Reyes — la empresa <c>Demo</c> pasa a EXHIBIR las features nuevas (decisión 25-jul-2026 PM,
    /// pivote sobre <c>20260725230000_AlistarDemoParaPruebas</c> que la había dejado en flujo clásico):
    /// el cliente evalúa sobre Demo y debe VER los cambios que tendrá que realizar (campos ERP a
    /// diligenciar, clasificación de huevo por ítems, traslado cross-etapa).
    /// <list type="number">
    /// <item>Enciende en Demo los 3 flags de comportamiento: <c>maneja_codigos_erp_avicola</c>,
    /// <c>clasificacion_huevo_por_items</c> y <c>permite_traslado_aves_cross_etapa</c> (mismo set que
    /// Santa Reyes). Los campos ERP arrancan vacíos a propósito: son "lo que hay que llenar".</item>
    /// <item>Copia a Demo el catálogo de huevo de Santa Reyes (21 ítems <c>item_type='huevo'</c>,
    /// metadata Primera/Pnc): sin él, el modal de clasificación del seguimiento de producción no tiene
    /// ítems que ofrecer. Guarda alineada al índice único real <c>(company_id, pais_id, codigo)</c>
    /// — verificado sin colisiones con los ítems existentes de Demo.</item>
    /// </list>
    /// Data-only (Designer clonado, ModelSnapshot intacto), IDEMPOTENTE (<c>IS DISTINCT FROM</c> +
    /// <c>NOT EXISTS</c>) y por LOOKUPS de nombre (ids difieren local↔prod). Ordena DESPUÉS de todas
    /// las migraciones de Santa Reyes (los flags/columnas ya existen) y si Demo o Santa Reyes no
    /// existieran, todos los bloques son no-op.
    /// </summary>
    public partial class ActivarFeaturesSantaReyesEnDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Flags de features (mismo set que Santa Reyes) ─────────────────────────
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET maneja_codigos_erp_avicola        = true,
       clasificacion_huevo_por_items     = true,
       permite_traslado_aves_cross_etapa = true
 WHERE name = 'Demo'
   AND (maneja_codigos_erp_avicola        IS DISTINCT FROM true
     OR clasificacion_huevo_por_items     IS DISTINCT FROM true
     OR permite_traslado_aves_cross_etapa IS DISTINCT FROM true);
");

            // ── 2. Catálogo de huevo Santa Reyes → Demo ──────────────────────────────────
            // Fuente del modal de clasificación (GET /api/catalogo-alimentos/filter?typeItem=huevo,
            // scoping por empresa). Se conservan metadata (categoria/um/tipoHuevo) y pais del origen.
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Simétrico best-effort: quita de Demo los ítems de huevo que vinieron de Santa Reyes
            // (solo los sin movimientos/stock referenciándolos — FKs RESTRICT) y apaga los flags.
            migrationBuilder.Sql(@"
DELETE FROM public.catalogo_items d
 WHERE d.company_id = (SELECT id FROM public.companies WHERE name = 'Demo' LIMIT 1)
   AND d.item_type = 'huevo'
   AND d.codigo IN (
        SELECT s.codigo FROM public.catalogo_items s
         WHERE s.company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes' LIMIT 1)
           AND s.item_type = 'huevo')
   AND NOT EXISTS (SELECT 1 FROM public.farm_inventory_movements fim WHERE fim.catalog_item_id = d.id)
   AND NOT EXISTS (SELECT 1 FROM public.farm_product_inventory  fpi WHERE fpi.catalog_item_id = d.id);

UPDATE public.companies
   SET maneja_codigos_erp_avicola        = false,
       clasificacion_huevo_por_items     = false,
       permite_traslado_aves_cross_etapa = false
 WHERE name = 'Demo'
   AND (maneja_codigos_erp_avicola        IS DISTINCT FROM false
     OR clasificacion_huevo_por_items     IS DISTINCT FROM false
     OR permite_traslado_aves_cross_etapa IS DISTINCT FROM false);
");
        }
    }
}
