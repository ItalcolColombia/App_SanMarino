using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Tabla <c>company_permissions</c>: qué permisos del catálogo global están habilitados en cada
    /// empresa. Gemela de <c>company_menus</c>, pero ésta SÍ manda — filtra lo asignable a un rol y
    /// se intersecta con los permisos efectivos del usuario en el login
    /// (<c>AuthService.PermisosEfectivosAsync</c>).
    /// <para>
    /// Solo crea el schema. La configuración inicial la siembra
    /// <c>SeedCompanyPermissionsDesdeRolesActuales</c>, que corre inmediatamente después: hasta que
    /// ese seed pase, la tabla está vacía y el gate es fail-closed.
    /// </para>
    /// <para>
    /// SQL crudo con <c>IF NOT EXISTS</c> en vez de <c>CreateTable</c>: idempotente, para soportar
    /// re-runs y un entorno donde la tabla ya se hubiera creado a mano.
    /// </para>
    /// </summary>
    public partial class AddCompanyPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.company_permissions (
    company_id    integer NOT NULL,
    permission_id integer NOT NULL,
    is_enabled    boolean NOT NULL DEFAULT true,
    CONSTRAINT pk_company_permissions PRIMARY KEY (company_id, permission_id),
    CONSTRAINT fk_company_permissions_companies_company_id
        FOREIGN KEY (company_id) REFERENCES public.companies (id) ON DELETE CASCADE,
    CONSTRAINT fk_company_permissions_permissions_permission_id
        FOREIGN KEY (permission_id) REFERENCES public.permissions (id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_company_permissions_permission_id
    ON public.company_permissions (permission_id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS public.company_permissions;");
        }
    }
}
