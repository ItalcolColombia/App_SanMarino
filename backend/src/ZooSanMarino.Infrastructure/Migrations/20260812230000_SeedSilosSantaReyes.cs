using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// FASE A — datos de Santa Reyes para «inventario por silo». Migración <b>data-only</b>: no
    /// cambia el modelo (por eso su Designer es el snapshot vigente y el ModelSnapshot no se toca).
    ///
    /// <list type="number">
    ///   <item>Enciende <c>maneja_inventario_por_silo</c> en Santa Reyes.</item>
    ///   <item>Crea la lista maestra 1..100 en <c>silo_catalogo</c> (el «voy a crear una lista de
    ///         silos del 1 al 100»).</item>
    ///   <item>Vincula por NOMBRE los 38 <c>farm_silos</c> que la granja La Esperanza ya tiene
    ///         (cargados en la Fase 1) con su entrada del catálogo.</item>
    ///   <item>Normaliza el tipo legacy <c>Insumos</c> a <c>Bodega</c>: esa ubicación ahora también
    ///         guarda alimento y admite traslado interno bodega→silo.</item>
    /// </list>
    ///
    /// <para>
    /// Todo idempotente (<c>WHERE NOT EXISTS</c> / <c>IS DISTINCT FROM</c>) y localizado por NOMBRE,
    /// nunca por id fijo: los ids de local y prod no coinciden.
    /// </para>
    /// <para>
    /// ⚠️ El timestamp es posterior al seed que crea la empresa (<c>20260725190000</c>); si se
    /// regenerara con un id menor, en prod correría contra una empresa inexistente.
    /// </para>
    /// </summary>
    public partial class SeedSilosSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1) Flag de empresa ───────────────────────────────────────────────
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET maneja_inventario_por_silo = TRUE
 WHERE name = 'Santa Reyes'
   AND maneja_inventario_por_silo IS DISTINCT FROM TRUE;
");

            // ── 2) Lista maestra 1..100 ──────────────────────────────────────────
            migrationBuilder.Sql(@"
INSERT INTO public.silo_catalogo (company_id, numero, nombre, activo, created_at)
SELECT c.id, g.n, 'Silo ' || g.n, TRUE, now()
  FROM public.companies c
 CROSS JOIN generate_series(1, 100) AS g(n)
 WHERE c.name = 'Santa Reyes'
   AND NOT EXISTS (
        SELECT 1 FROM public.silo_catalogo sc
         WHERE sc.company_id = c.id
           AND sc.numero     = g.n
           AND sc.deleted_at IS NULL
   );
");

            // ── 3) Vincular los farm_silos existentes con el catálogo ────────────
            // Los 38 silos de La Esperanza se cargaron en la Fase 1 con los nombres 'Silo 1'..'Silo 38',
            // que son exactamente los del catálogo recién creado ⇒ el match por nombre es exacto.
            migrationBuilder.Sql(@"
UPDATE public.farm_silos fs
   SET silo_catalogo_id = sc.id,
       updated_at       = now()
  FROM public.silo_catalogo sc
 WHERE sc.company_id  = fs.company_id
   AND sc.nombre      = fs.nombre
   AND sc.deleted_at IS NULL
   AND fs.deleted_at IS NULL
   AND fs.tipo        = 'Silo'
   AND fs.silo_catalogo_id IS DISTINCT FROM sc.id;
");

            // ── 4) 'Insumos' (legacy) → 'Bodega' ─────────────────────────────────
            // No es un cambio de tenant: el tipo se renombra porque su significado cambió (la bodega
            // pasa a guardar alimento además de insumos). Solo Santa Reyes tiene filas con ese valor.
            migrationBuilder.Sql(@"
UPDATE public.farm_silos
   SET tipo       = 'Bodega',
       updated_at = now()
 WHERE tipo = 'Insumos';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Simétrico hasta donde tiene sentido: se apaga el flag, se desvincula el catálogo y se
            // borran las entradas de la lista maestra que NO quedaron en uso por alguna granja.
            // El tipo 'Bodega' NO vuelve a 'Insumos': el valor nuevo es el correcto del dominio.
            migrationBuilder.Sql(@"
UPDATE public.farm_silos fs
   SET silo_catalogo_id = NULL
  FROM public.companies c
 WHERE c.id = fs.company_id
   AND c.name = 'Santa Reyes';

DELETE FROM public.silo_catalogo sc
 USING public.companies c
 WHERE c.id = sc.company_id
   AND c.name = 'Santa Reyes'
   AND NOT EXISTS (
        SELECT 1 FROM public.farm_silos fs WHERE fs.silo_catalogo_id = sc.id
   );

UPDATE public.companies
   SET maneja_inventario_por_silo = FALSE
 WHERE name = 'Santa Reyes';
");
        }
    }
}
