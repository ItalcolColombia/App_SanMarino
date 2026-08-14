using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// FASE A — menú «Silos» (la lista maestra) bajo Configuración, habilitado SOLO en Santa Reyes.
    ///
    /// <para>
    /// El menú se crea una vez y se enlaza por <c>company_menus</c> (lo habilita en la UI de admin de
    /// roles) y por <c>role_menus</c> (lo hace visible en el sidebar de cada rol). Son los dos: con
    /// <c>company_menus</c> solo, el módulo queda habilitado pero <b>no aparece</b> — el sidebar sale
    /// de <c>role_menus</c>.
    /// </para>
    ///
    /// <para>
    /// Todo localizado por <c>route</c> / <c>key</c> / nombre, nunca por id fijo: los ids de local y
    /// prod no coinciden. Idempotente.
    /// </para>
    /// </summary>
    public partial class MenuSilosSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1) El menú, colgado del grupo Configuración (localizado por su route conocida).
INSERT INTO public.menus (label, icon, route, ""order"", is_active, parent_id, key, sort_order, is_group, created_at, updated_at)
SELECT 'Silos', 'layer-group', '/config/silos', 0, TRUE,
       (SELECT m.parent_id FROM public.menus m WHERE m.route = '/config/master-lists' LIMIT 1),
       'config.silos', 0, FALSE, now(), now()
 WHERE NOT EXISTS (SELECT 1 FROM public.menus WHERE route = '/config/silos' OR key = 'config.silos');

-- 2) Habilitarlo para las empresas que manejan inventario por silo (hoy solo Santa Reyes).
INSERT INTO public.company_menus (company_id, menu_id)
SELECT c.id, m.id
  FROM public.companies c
 CROSS JOIN public.menus m
 WHERE c.maneja_inventario_por_silo = TRUE
   AND m.route = '/config/silos'
   AND NOT EXISTS (
        SELECT 1 FROM public.company_menus cm
         WHERE cm.company_id = c.id AND cm.menu_id = m.id
   );

-- 3) Y mostrarlo en el sidebar de los roles de esas empresas que YA ven la lista maestra
--    (mismo criterio de acceso: quien administra listas maestras administra los silos).
INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rc.role_id, m.id
  FROM public.role_companies rc
  JOIN public.companies c ON c.id = rc.company_id AND c.maneja_inventario_por_silo = TRUE
 CROSS JOIN public.menus m
 WHERE m.route = '/config/silos'
   AND EXISTS (
        SELECT 1
          FROM public.role_menus rm
          JOIN public.menus mm ON mm.id = rm.menu_id
         WHERE rm.role_id = rc.role_id
           AND mm.route = '/config/master-lists'
   )
   AND NOT EXISTS (
        SELECT 1 FROM public.role_menus rm2
         WHERE rm2.role_id = rc.role_id AND rm2.menu_id = m.id
   );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM public.role_menus    WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/config/silos');
DELETE FROM public.company_menus WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/config/silos');
DELETE FROM public.menus         WHERE route = '/config/silos';
");
        }
    }
}
