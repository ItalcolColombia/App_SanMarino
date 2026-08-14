using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Módulo <b>Gerencia</b> con una sola vista: el <b>Panel de control</b> de ItalJira en modo
    /// solo-lectura global, gobernado por el permiso nuevo <c>tickets.indicadores</c>.
    ///
    /// <para>
    /// <b>Por qué hace falta un permiso nuevo:</b> el alcance global del panel lo decidía
    /// únicamente <c>tickets.admin</c> (<c>TicketService.AplicarFiltroTablero</c>). Un rol de
    /// gerencia con <c>tickets.gestionar</c> veía la pantalla pero con los casos asignados a él
    /// —o sea, todo en cero—, y darle <c>tickets.admin</c> para arreglarlo le habría concedido
    /// además crear casos a nombre de otro, gestionar cualquier caso, el tablero y el roadmap
    /// globales y la Configuración de ItalJira. <c>tickets.indicadores</c> abre SOLO las vistas de
    /// lectura (indicadores y reporte); la regla vive en <c>TicketAlcancePanelCalculos</c> y está
    /// cubierta por tests.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué una fila de menú NUEVA y con ruta propia:</b> <c>menus.parent_id</c> es único, así
    /// que <c>italjira.panel</c> no puede colgar a la vez de ItalJira y de Gerencia. Y la ruta no se
    /// comparte (<c>/gerencia/panel</c>, no <c>/italjira/panel</c>) porque las migraciones de este
    /// repo localizan menús por <c>route</c> —los ids difieren local↔prod— y dos filas con la misma
    /// ruta harían que cualquier seed futuro matcheara las dos.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué se siembra <c>company_permissions</c>:</b> es fail-closed por empresa
    /// (<c>CompanyPermissionCalculos.ResolverEfectivos</c>, regla R1: empresa sin configurar no
    /// habilita nada). Un permiso que no esté habilitado ahí NO viaja en el JWT aunque el rol lo
    /// tenga: el rol quedaría sin nada y parecería un bug del código.
    /// </para>
    ///
    /// <para>
    /// <b>Lo que esta migración NO hace, a propósito:</b> no crea el rol «Gerencia», ni inserta en
    /// <c>role_permissions</c> ni en <c>role_menus</c>. Esa asignación se hace desde la pantalla de
    /// Roles y Permisos (convención del repo), y así el rol y la empresa los elige quien opera, sin
    /// que la migración adivine nombres. Hasta que se asigne, el menú no lo ve nadie.
    /// </para>
    ///
    /// Migración DATA-ONLY: Designer generado por EF con el <c>Up()</c> vacío de operaciones de
    /// schema, ModelSnapshot intacto. Idempotente (<c>WHERE NOT EXISTS</c>), localizando por
    /// <c>permissions.key</c> / <c>menus.key</c>.
    /// </summary>
    public partial class MenuGerenciaPanelControl : Migration
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
-- 1) El permiso. Nombrado por el COMPORTAMIENTO, no por el tenant ni por el cargo.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.permissions (key, description)
SELECT 'tickets.indicadores',
       'ItalJira: ver el Panel de control (indicadores y reporte) de TODOS los casos, sin poder gestionarlos'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions WHERE key = 'tickets.indicadores');

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) El grupo «Gerencia». Va justo después de ItalJira (901) en el sidebar.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.menus (key, label, icon, route, ""order"", sort_order, is_group, is_active, parent_id, created_at, updated_at)
SELECT 'gerencia', 'Gerencia', 'briefcase', NULL, 902, 902, true, true, NULL, now(), now()
WHERE NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key = 'gerencia');

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) La única vista del grupo: el panel. Ruta PROPIA (ver el doc-comment).
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.menus (key, label, icon, route, ""order"", sort_order, is_group, is_active, parent_id, created_at, updated_at)
SELECT 'gerencia.panel', 'Panel de control', 'chart-bar', '/gerencia/panel', 1, 1, false, true, p.id, now(), now()
FROM public.menus p
WHERE p.key = 'gerencia'
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key = 'gerencia.panel');

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) Gate del ítem. El filtro de RoleCompositeService es un OR sobre estos keys:
--    gerencia entra por el permiso nuevo, y el admin lo sigue viendo si se le asigna.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.menu_permissions (menu_id, permission_id)
SELECT m.id, p.id
FROM public.menus m
JOIN public.permissions p ON p.key IN ('tickets.indicadores', 'tickets.admin')
WHERE m.key = 'gerencia.panel'
  AND NOT EXISTS (
        SELECT 1 FROM public.menu_permissions mp
        WHERE mp.menu_id = m.id AND mp.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 5) El permiso queda ASIGNABLE en las empresas donde el módulo ya existe (las que
--    tienen habilitado tickets.admin o tickets.gestionar). Sin esta fila el permiso
--    no viaja en el JWT: company_permissions es fail-closed por empresa.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT DISTINCT cp.company_id, nuevo.id, true
FROM public.company_permissions cp
JOIN public.permissions origen ON origen.id = cp.permission_id
     AND origen.key IN ('tickets.admin', 'tickets.gestionar')
     AND cp.is_enabled = true
CROSS JOIN public.permissions nuevo
WHERE nuevo.key = 'tickets.indicadores'
  AND NOT EXISTS (
        SELECT 1 FROM public.company_permissions x
        WHERE x.company_id = cp.company_id AND x.permission_id = nuevo.id);
";

        private const string DOWN_SQL = @"
-- El menú es nuevo: se va entero (asignaciones incluidas, si alguien alcanzó a hacerlas).
DELETE FROM public.role_menus       WHERE menu_id IN (SELECT id FROM public.menus WHERE key IN ('gerencia', 'gerencia.panel'));
DELETE FROM public.company_menus    WHERE menu_id IN (SELECT id FROM public.menus WHERE key IN ('gerencia', 'gerencia.panel'));
DELETE FROM public.menu_permissions WHERE menu_id IN (SELECT id FROM public.menus WHERE key IN ('gerencia', 'gerencia.panel'));
DELETE FROM public.menus            WHERE key = 'gerencia.panel';
DELETE FROM public.menus            WHERE key = 'gerencia';

-- El permiso también, con todo lo que lo referencie.
DELETE FROM public.role_permissions    WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'tickets.indicadores');
DELETE FROM public.company_permissions WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'tickets.indicadores');
DELETE FROM public.permissions         WHERE key = 'tickets.indicadores';
";
    }
}
