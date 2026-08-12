using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed data-only (sin cambios de schema) de <c>company_permissions</c>.
    ///
    /// <para>
    /// El filtro por empresa es FAIL-CLOSED: una empresa sin filas no habilita ningún permiso, ni
    /// para asignar a un rol ni en el login. Por eso este seed tiene que dejar a cada empresa
    /// exactamente con lo que HOY usa — el invariante que hay que verificar tras aplicarlo es que
    /// <b>los permisos efectivos de cada usuario no cambien</b>.
    /// </para>
    ///
    /// <para>Dos pasos, ambos idempotentes (<c>NOT EXISTS</c>):</para>
    /// <list type="number">
    ///   <item>
    ///     <b>Lo que la empresa ya usa.</b> Por cada empresa, los permisos de los roles vinculados a
    ///     ella. La empresa llega al rol por <c>role_companies</c> O por <c>user_roles.company_id</c>;
    ///     los dos caminos están poblados en producción, así que se unen (usar solo uno dejaría
    ///     permisos afuera y alguien perdería acceso al desplegar).
    ///   </item>
    ///   <item>
    ///     <b>Red de seguridad.</b> Una empresa que quede con CERO filas tras el paso 1 (empresa nueva,
    ///     o sin roles todavía) recibe el catálogo completo: fail-closed no puede impedir que se cree
    ///     el primer rol. Es el mismo criterio que aplica <c>CompanyService.CreateAsync</c> de acá en
    ///     adelante.
    ///   </item>
    /// </list>
    ///
    /// <para>
    /// Nada se localiza por id: los ids de empresas y permisos difieren entre local y prod.
    /// </para>
    /// </summary>
    public partial class SeedCompanyPermissionsDesdeRolesActuales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UP_SQL);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }

        private const string UP_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- 1) Cada empresa conserva lo que hoy usan sus roles
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT DISTINCT rc.company_id, rp.permission_id, true
FROM (
    SELECT company_id, role_id FROM public.role_companies
    UNION
    SELECT company_id, role_id FROM public.user_roles
) rc
JOIN public.role_permissions rp ON rp.role_id = rc.role_id
WHERE EXISTS (SELECT 1 FROM public.companies c  WHERE c.id = rc.company_id)
  AND EXISTS (SELECT 1 FROM public.permissions p WHERE p.id = rp.permission_id)
  AND NOT EXISTS (
      SELECT 1 FROM public.company_permissions cp
      WHERE cp.company_id = rc.company_id AND cp.permission_id = rp.permission_id
  );

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Red de seguridad: empresa sin ninguna fila ⇒ catálogo completo
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT c.id, p.id, true
FROM public.companies c
CROSS JOIN public.permissions p
WHERE NOT EXISTS (
    SELECT 1 FROM public.company_permissions cp WHERE cp.company_id = c.id
);
";

        // Revertir = volver al comportamiento previo (catálogo global sin recortes). Se vacía la
        // tabla; el schema lo quita AddCompanyPermissions.Down. No se puede reconstruir la foto
        // exacta previa a un Set manual del admin, pero sí el punto de partida.
        private const string DOWN_SQL = @"
DELETE FROM public.company_permissions;
";
    }
}
