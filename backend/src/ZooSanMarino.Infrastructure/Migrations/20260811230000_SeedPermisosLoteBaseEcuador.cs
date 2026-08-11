using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Migración DATA-ONLY: da a los roles administradores de <b>ItalcolEcuador</b> los permisos
    /// <c>lote_base_pollo_engorde.ver / .crear / .editar</c>.
    /// <para>
    /// <b>Por qué hace falta:</b> la pestaña de programación y el botón «Asignar granjas» están
    /// gateados por <c>*appHasPermission</c>, y al medir <b>ningún rol de Ecuador</b> tenía esos
    /// permisos (solo Panamá, Demo, Santa Reyes y Sanmarino). Con el flag encendido pero sin permiso,
    /// Ecuador vería el lote base obligatorio en el formulario y <b>ninguna forma de administrarlo</b>.
    /// </para>
    /// <para>
    /// <c>.eliminar</c> NO se otorga, igual que en Panamá: borrar un base con lotes o gastos colgando
    /// se bloquea igual en el service, pero el permiso se concede a mano cuando se necesite.
    /// </para>
    /// <para>
    /// Idempotente: lookups por nombre de empresa/rol/permiso (los ids difieren local↔prod) y
    /// <c>NOT EXISTS</c>. Si la empresa, el rol o el permiso no existen, no inserta nada y pasa igual.
    /// </para>
    /// </summary>
    public partial class SeedPermisosLoteBaseEcuador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO role_permissions (role_id, permission_id)
                SELECT r.id, p.id
                  FROM roles r
                  JOIN role_companies rc ON rc.role_id = r.id
                  JOIN companies c       ON c.id = rc.company_id AND c.name = 'ItalcolEcuador'
                  JOIN permissions p     ON p.key IN (
                           'lote_base_pollo_engorde.ver',
                           'lote_base_pollo_engorde.crear',
                           'lote_base_pollo_engorde.editar')
                 WHERE r.name IN ('Ecuador Administrador', 'Lider implementación - Regional Ecuador')
                   AND NOT EXISTS (
                        SELECT 1 FROM role_permissions rp
                         WHERE rp.role_id = r.id AND rp.permission_id = p.id
                   );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM role_permissions rp
                 USING roles r, role_companies rc, companies c, permissions p
                 WHERE rp.role_id = r.id
                   AND rp.permission_id = p.id
                   AND rc.role_id = r.id
                   AND c.id = rc.company_id AND c.name = 'ItalcolEcuador'
                   AND r.name IN ('Ecuador Administrador', 'Lider implementación - Regional Ecuador')
                   AND p.key IN (
                        'lote_base_pollo_engorde.ver',
                        'lote_base_pollo_engorde.crear',
                        'lote_base_pollo_engorde.editar');
            ");
        }
    }
}
