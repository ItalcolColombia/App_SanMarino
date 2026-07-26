using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Santa Reyes — Fase 4: deja la empresa <c>Demo</c> lista para la EVALUACIÓN con el flujo
    /// CLÁSICO (todos los flags nuevos en <c>false</c>: sin campos ERP, sin clasificación de huevos
    /// por ítems, sin traslado cross-etapa; guía genética propia ya cargada → raza/año obligatorios).
    /// <para>
    /// La auditoría de Demo encontró EXACTAMENTE 3 faltantes, que son lo único que hace esta
    /// migración (data-only; no toca el modelo EF → Designer clonado, ModelSnapshot intacto):
    /// </para>
    /// <list type="number">
    /// <item>El dropdown de alimento del seguimiento diario quedaba casi vacío: se copian los ítems
    /// de alimento activos de <c>Agroavicola Sanmarino</c> a <c>Demo</c> (mismo país/columnas,
    /// <c>WHERE NOT EXISTS</c> por código).</item>
    /// <item>Typo del único ítem propio de Demo: <c>Alimneto ERP</c> → <c>Alimento ERP</c>.</item>
    /// <item>Demo veía el menú <c>Integración Panamá</c> (puente de otra empresa): se quita de
    /// <c>company_menus</c> y de los <c>role_menus</c> de los roles EXCLUSIVOS de Demo.</item>
    /// </list>
    /// Todo por LOOKUPS (nombre de empresa / <c>route</c> del menú, jamás ids fijos: difieren
    /// local↔prod) e IDEMPOTENTE (<c>NOT EXISTS</c> + <c>WHERE</c> con guarda): re-ejecutable sin
    /// efectos. Si la empresa Demo no existiera, todos los bloques son no-op.
    /// </summary>
    public partial class AlistarDemoParaPruebas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Ítems de alimento de 'Agroavicola Sanmarino' → 'Demo' ──────────────────
            // Sin ellos el dropdown de alimento del seguimiento diario de Demo solo ofrece 1 ítem.
            // Se conservan TODAS las columnas del origen (incluido pais_id) y se marca created_at/
            // updated_at = now(). Idempotente por (company Demo, codigo) — el índice único real es
            // (company_id, pais_id, codigo), así que la guarda es incluso más estricta.
            migrationBuilder.Sql(@"
INSERT INTO public.item_inventario_ecuador
    (codigo, nombre, tipo_item, unidad, descripcion, activo, company_id, pais_id,
     created_at, updated_at, grupo, tipo_inventario_codigo, descripcion_tipo_inventario,
     referencia, descripcion_item, concepto)
SELECT src.codigo, src.nombre, src.tipo_item, src.unidad, src.descripcion, src.activo,
       demo.id, src.pais_id,
       now(), now(), src.grupo, src.tipo_inventario_codigo, src.descripcion_tipo_inventario,
       src.referencia, src.descripcion_item, src.concepto
  FROM public.item_inventario_ecuador src
  CROSS JOIN (SELECT id FROM public.companies WHERE name = 'Demo' LIMIT 1) demo
 WHERE src.company_id = (SELECT id FROM public.companies WHERE name = 'Agroavicola Sanmarino' LIMIT 1)
   AND src.tipo_item = 'alimento'
   AND src.activo = true
   AND NOT EXISTS (
        SELECT 1 FROM public.item_inventario_ecuador d
         WHERE d.company_id = demo.id
           AND d.codigo = src.codigo);
");

            // ── 2. Typo del ítem propio de Demo ──────────────────────────────────────────
            // Idempotente por el WHERE sobre el nombre viejo (segunda pasada = 0 filas).
            migrationBuilder.Sql(@"
UPDATE public.item_inventario_ecuador
   SET nombre = 'Alimento ERP',
       updated_at = now()
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Demo' LIMIT 1)
   AND codigo = '4000'
   AND nombre = 'Alimneto ERP';
");

            // ── 3. Quitar el menú 'Integración Panamá' de Demo ───────────────────────────
            // Primero los role_menus, y SOLO de roles cuyo único vínculo en role_companies es Demo
            // (guarda anti-daño: los roles compartidos con otras empresas — p.ej. soporte global —
            // conservan el menú intacto). Después el company_menus de Demo. Localizado por route.
            migrationBuilder.Sql(@"
DELETE FROM public.role_menus rm
 USING public.menus m
 WHERE rm.menu_id = m.id
   AND m.route ILIKE '%sincronizacion-panama%'
   AND rm.role_id IN (SELECT role_id FROM public.role_companies GROUP BY role_id HAVING COUNT(*) = 1)
   AND rm.role_id IN (SELECT rc.role_id FROM public.role_companies rc
                       WHERE rc.company_id = (SELECT id FROM public.companies WHERE name = 'Demo' LIMIT 1));

DELETE FROM public.company_menus cm
 USING public.menus m
 WHERE cm.menu_id = m.id
   AND m.route ILIKE '%sincronizacion-panama%'
   AND cm.company_id = (SELECT id FROM public.companies WHERE name = 'Demo' LIMIT 1);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversión BEST-EFFORT y simétrica de lo que hace Up(), salvo el typo: revertirlo
            // significaría volver a escribir 'Alimneto ERP' (un error de datos), así que se conserva
            // corregido a propósito.

            // ── 3'. Restaurar el menú 'Integración Panamá' en Demo (si el menú sigue existiendo) ──
            migrationBuilder.Sql(@"
INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT demo.id, m.id, true, COALESCE(ref.sort_order, 0), ref.parent_menu_id
  FROM public.menus m
  CROSS JOIN (SELECT id FROM public.companies WHERE name = 'Demo' LIMIT 1) demo
  LEFT JOIN LATERAL (
        SELECT cm.sort_order, cm.parent_menu_id
          FROM public.company_menus cm
         WHERE cm.menu_id = m.id AND cm.company_id <> demo.id
         LIMIT 1) ref ON true
 WHERE m.route ILIKE '%sincronizacion-panama%'
   AND NOT EXISTS (
        SELECT 1 FROM public.company_menus x
         WHERE x.company_id = demo.id AND x.menu_id = m.id);

INSERT INTO public.role_menus (role_id, menu_id)
SELECT rc.role_id, m.id
  FROM public.role_companies rc
  CROSS JOIN public.menus m
 WHERE m.route ILIKE '%sincronizacion-panama%'
   AND rc.company_id = (SELECT id FROM public.companies WHERE name = 'Demo' LIMIT 1)
   AND rc.role_id IN (SELECT role_id FROM public.role_companies GROUP BY role_id HAVING COUNT(*) = 1)
   AND NOT EXISTS (
        SELECT 1 FROM public.role_menus x
         WHERE x.role_id = rc.role_id AND x.menu_id = m.id);
");

            // ── 1'. Borrar los ítems COPIADOS (los que existen en el origen), nunca el propio '4000' ──
            migrationBuilder.Sql(@"
DELETE FROM public.item_inventario_ecuador d
 WHERE d.company_id = (SELECT id FROM public.companies WHERE name = 'Demo' LIMIT 1)
   AND d.codigo <> '4000'
   AND d.codigo IN (
        SELECT src.codigo
          FROM public.item_inventario_ecuador src
         WHERE src.company_id = (SELECT id FROM public.companies WHERE name = 'Agroavicola Sanmarino' LIMIT 1)
           AND src.tipo_item = 'alimento');
");
        }
    }
}
