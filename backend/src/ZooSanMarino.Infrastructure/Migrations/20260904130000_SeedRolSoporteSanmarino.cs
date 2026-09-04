using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Siembra el rol <b>«Soporte Sanmarino»</b>: el encargado de soporte de <b>Agroavicola
    /// Sanmarino</b> y de nadie más.
    ///
    /// <para>
    /// <b>Por qué un rol y no una empresa administradora.</b> El pedido original era crear una
    /// «empresa administrador» para mover ahí el rol <c>Admin</c> y dejar a Sanmarino acotada como
    /// las demás. No hace falta: el eje global ya vive <b>fuera</b> de <c>companies</c> —la marca
    /// <c>users.is_super_admin</c> (ver <c>SuperAdminCalculos</c>) y el rol de administrador de
    /// aplicación por nombre exacto (ver <c>CatalogoGlobalAutorizacionCalculos</c>)—. Los roles no
    /// cuelgan de una empresa administradora sino de <c>role_companies</c> + <c>user_roles</c>, así
    /// que «mudar el rol Admin de empresa» no le quitaría un solo privilegio. Una empresa fantasma
    /// solo agregaría una fila que arrastra <c>company_permissions</c> y <c>company_menus</c>, sin
    /// granjas ni lotes.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>El nombre del rol importa.</b> No puede ser <c>Admin</c> ni <c>Administrador</c>: esos
    /// dos strings, comparados de forma <b>exacta</b>, son la llave que abre la escritura de los
    /// catálogos globales (<c>permissions</c>, <c>menus</c>), la administración de empresas y el
    /// listado de roles <b>sin</b> filtro de empresa. Un rol llamado así heredaría justo lo que este
    /// rol viene a no tener.
    /// </para>
    ///
    /// <para>
    /// <b>Qué lo contiene, sin código nuevo.</b> Menú = <c>role_menus</c> ∩
    /// <c>company_menus(Sanmarino)</c> ∩ <c>menu_permissions</c> (lo arma <c>fn_menu_usuario</c>);
    /// permisos efectivos = <c>role_permissions</c> ∩ <c>company_permissions(Sanmarino)</c>; y los
    /// roles que ve los filtra <c>Roles_GetAllAsync</c> por <c>role_companies</c> de la empresa
    /// activa. Las escrituras de catálogo global y de empresas le responden <b>403</b>.
    /// </para>
    ///
    /// <para>
    /// <b>Menús que NO se le dan a propósito:</b> <c>/config/companies</c>, <c>/config/db-studio</c>,
    /// <c>/config/countries</c> y <c>/config/master-lists</c> — los cuatro administran catálogos
    /// compartidos por todas las empresas y países.
    /// <b>Permiso que NO se le da:</b> <c>tickets.admin</c>, que es «todos los países»; lleva
    /// <c>tickets.gestionar</c>, que es la bandeja de su país.
    /// </para>
    ///
    /// <para>
    /// <b>El usuario no se crea acá</b>: se da de alta desde Configuración → Usuarios y se le asigna
    /// este rol con empresa Agroavicola Sanmarino (<c>user_roles.company_id</c>). Sembrar una
    /// identidad concreta en una migración la haría imposible de revocar sin otro despliegue.
    /// </para>
    ///
    /// <para>
    /// Data-only e <b>idempotente</b> (<c>WHERE NOT EXISTS</c>), con todos los lookups por
    /// <c>companies.name</c> / <c>menus.route</c> / <c>permissions.key</c> — <b>nunca por id</b>: los
    /// ids difieren entre local y producción. Si la empresa, un menú o un permiso no existen en el
    /// entorno, esa fila simplemente no se inserta y la migración no falla.
    /// Sin cambios de modelo (ModelSnapshot intacto).
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/soporte_sanmarino_y_admin_global_plan.md</c>.
    /// </summary>
    public partial class SeedRolSoporteSanmarino : Migration
    {
        private const string NombreRol = "Soporte Sanmarino";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DO $$
DECLARE
    v_role_id    integer;
    v_company_id integer;
BEGIN
    SELECT c.id INTO v_company_id FROM companies c WHERE c.name = 'Agroavicola Sanmarino';

    IF v_company_id IS NULL THEN
        RAISE NOTICE 'Soporte Sanmarino: no existe la empresa Agroavicola Sanmarino en este entorno; omitido.';
        RETURN;
    END IF;

    -- 1) El rol
    SELECT r.id INTO v_role_id FROM roles r WHERE r.name = '{NombreRol}';

    IF v_role_id IS NULL THEN
        INSERT INTO roles (name, description, allow_multiple_countries, allow_multiple_companies, is_company_admin)
        VALUES ('{NombreRol}',
                'Encargado de soporte de Agroavicola Sanmarino: administra los usuarios y roles de su empresa y atiende su bandeja de tickets. No administra empresas ni los catalogos globales del sistema.',
                false, false, false)
        RETURNING id INTO v_role_id;
    END IF;

    -- 2) La empresa del rol
    INSERT INTO role_companies (role_id, company_id)
    SELECT v_role_id, v_company_id
    WHERE NOT EXISTS (
        SELECT 1 FROM role_companies rc WHERE rc.role_id = v_role_id AND rc.company_id = v_company_id
    );

    -- 3) Menus, por RUTA. Los grupos padre (Configuracion, Lote, Seguimiento Diario, Movimientos,
    --    Reportes, Tickets) NO se listan: fn_menu_usuario sube por los ancestros sola.
    INSERT INTO role_menus (role_id, menu_id)
    SELECT v_role_id, m.id
    FROM menus m
    WHERE m.route IN (
        -- Lo que motiva el rol
        '/config/users',                    -- Usuarios de Sanmarino
        '/config/role-management',          -- Roles de Sanmarino (solo ve los de su empresa)
        '/tickets',                         -- Mis solicitudes
        '/tickets/gestion',                 -- Bandeja de gestion
        -- Operacion, para poder acompanar al usuario viendo lo mismo que el
        '/config/farm-management',
        '/config/lote-management',
        '/lote-reproductora',
        '/daily-log/seguimiento',
        '/daily-log/produccion',
        '/gestion-inventario',
        '/gestion-inventario/historial',
        '/cuadres-offline',
        '/traslados-huevos/lista',
        '/movimientos-aves/lista',
        '/reportes-tecnicos',
        '/reporte-contable',
        '/reporte-tecnico-semanal',
        '/reporte-diario-costos-postura'
    )
      AND NOT EXISTS (
        SELECT 1 FROM role_menus rm WHERE rm.role_id = v_role_id AND rm.menu_id = m.id
    );

    -- 4) Permisos, por KEY. Solo se hacen efectivos si company_permissions los habilita para
    --    Sanmarino (interseccion fail-closed) — los cuatro lo estan.
    INSERT INTO role_permissions (role_id, permission_id)
    SELECT v_role_id, p.id
    FROM permissions p
    WHERE p.key IN (
        'usuarios.gestionar',        -- crear/editar usuarios y asignar granjas
        'usuarios.revocar_sesion',   -- cerrar sesiones colgadas: el pedido tipico de soporte
        'tickets.crear',
        'tickets.gestionar'          -- bandeja de SU pais (NO tickets.admin, que es global)
    )
      AND NOT EXISTS (
        SELECT 1 FROM role_permissions rp WHERE rp.role_id = v_role_id AND rp.permission_id = p.id
    );

    RAISE NOTICE 'Soporte Sanmarino: rol % listo en la empresa %.', v_role_id, v_company_id;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se borra el rol solo si NADIE lo tiene asignado: revertir la migración no puede
            // dejar sin acceso a una persona real que ya lo esté usando. Sus filas hijas caen por
            // el ON DELETE CASCADE de role_companies / role_menus / role_permissions.
            migrationBuilder.Sql($@"
DELETE FROM roles r
 WHERE r.name = '{NombreRol}'
   AND NOT EXISTS (SELECT 1 FROM user_roles ur WHERE ur.role_id = r.id);
");
        }
    }
}
