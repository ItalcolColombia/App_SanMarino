using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Saca la gestión del área de desarrollo del módulo de Tickets a un grupo propio: **ItalJira**.
    ///
    /// En <b>Tickets</b> quedan las dos vistas de los usuarios y del resolutor:
    /// «Mis solicitudes» y «Bandeja de gestión».
    /// A <b>ItalJira</b> se mudan «Tablero», «Roadmap», «Panel de control» y «Administración»
    /// (renombrada a «Configuración»), y se agrega el «Backlog» de historias.
    ///
    /// <b>Por qué UPDATE y no DELETE + INSERT:</b> <c>role_menus</c> y <c>company_menus</c>
    /// referencian el menú por <c>menu_id</c>. Renombrando la fila EN SITIO, cada rol y cada
    /// empresa que ya veía esas vistas las sigue viendo sin tocar una sola asignación. Borrarlas y
    /// recrearlas dejaría el sidebar vacío para todos hasta reasignarlas a mano en producción.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto. Idempotente
    /// (<c>WHERE NOT EXISTS</c> + <c>UPDATE</c> por <c>key</c>, que sobre una base ya migrada
    /// afecta 0 filas). Los menús se localizan por <c>key</c>, nunca por id fijo: los ids difieren
    /// entre local y producción.
    /// </summary>
    public partial class MenusItalJiraFueraDeTickets : Migration
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
-- 1) El grupo nuevo. Va justo después de Tickets en el sidebar.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.menus (key, label, icon, route, ""order"", sort_order, is_group, is_active, parent_id, created_at, updated_at)
SELECT 'italjira', 'ItalJira', 'clipboard-list', NULL, 901, 901, true, true, NULL, now(), now()
WHERE NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key = 'italjira');

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Mudanza EN SITIO de las cuatro vistas de gestión (conserva role_menus/company_menus).
--    La ruta vieja del front redirige a la nueva, así que un enlace guardado sigue funcionando.
-- ─────────────────────────────────────────────────────────────────────────────
UPDATE public.menus m SET
    key        = 'italjira.tablero',
    label      = 'Tablero',
    route      = '/italjira/tablero',
    parent_id  = (SELECT id FROM public.menus WHERE key = 'italjira'),
    ""order""   = 2,
    sort_order = 2,
    updated_at = now()
WHERE m.key = 'tickets.tablero';

UPDATE public.menus m SET
    key        = 'italjira.roadmap',
    label      = 'Roadmap',
    route      = '/italjira/roadmap',
    parent_id  = (SELECT id FROM public.menus WHERE key = 'italjira'),
    ""order""   = 3,
    sort_order = 3,
    updated_at = now()
WHERE m.key = 'tickets.roadmap';

UPDATE public.menus m SET
    key        = 'italjira.panel',
    label      = 'Panel de control',
    route      = '/italjira/panel',
    parent_id  = (SELECT id FROM public.menus WHERE key = 'italjira'),
    ""order""   = 4,
    sort_order = 4,
    updated_at = now()
WHERE m.key = 'tickets.panel';

-- «Administración» pasa a «Configuración» y su ruta deja de contener la palabra `admin`
-- (AWS WAF devuelve 403 a esos paths — incidente documentado en el repo).
UPDATE public.menus m SET
    key        = 'italjira.configuracion',
    label      = 'Configuración',
    route      = '/italjira/configuracion',
    parent_id  = (SELECT id FROM public.menus WHERE key = 'italjira'),
    ""order""   = 5,
    sort_order = 5,
    updated_at = now()
WHERE m.key = 'tickets.admin';

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) El menú nuevo: Backlog (el árbol historia → tarea → subtarea).
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.menus (key, label, icon, route, ""order"", sort_order, is_group, is_active, parent_id, created_at, updated_at)
SELECT 'italjira.backlog', 'Backlog', 'list-tree', '/italjira/backlog', 1, 1, false, true, p.id, now(), now()
FROM public.menus p
WHERE p.key = 'italjira'
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key = 'italjira.backlog');

-- Gate por permiso: es una vista de gestión, igual que el resto de ItalJira.
INSERT INTO public.menu_permissions (menu_id, permission_id)
SELECT m.id, p.id
FROM public.menus m
JOIN public.permissions p ON p.key IN ('tickets.gestionar','tickets.admin')
WHERE m.key = 'italjira.backlog'
  AND NOT EXISTS (
        SELECT 1 FROM public.menu_permissions mp
        WHERE mp.menu_id = m.id AND mp.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) El grupo tiene que estar asignado a los mismos roles/empresas que sus hijos,
--    o el sidebar no lo dibuja aunque los hijos existan.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rm.role_id, grupo.id
FROM public.role_menus rm
JOIN public.menus hijo ON hijo.id = rm.menu_id
     AND hijo.key IN ('italjira.tablero','italjira.roadmap','italjira.panel','italjira.configuracion')
CROSS JOIN public.menus grupo
WHERE grupo.key = 'italjira'
  AND NOT EXISTS (
        SELECT 1 FROM public.role_menus x
        WHERE x.role_id = rm.role_id AND x.menu_id = grupo.id);

INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT DISTINCT ON (cm.company_id)
       cm.company_id, grupo.id, cm.is_enabled, grupo.sort_order, NULL
FROM public.company_menus cm
JOIN public.menus hijo ON hijo.id = cm.menu_id
     AND hijo.key IN ('italjira.tablero','italjira.roadmap','italjira.panel','italjira.configuracion')
CROSS JOIN public.menus grupo
WHERE grupo.key = 'italjira'
  AND NOT EXISTS (
        SELECT 1 FROM public.company_menus y
        WHERE y.company_id = cm.company_id AND y.menu_id = grupo.id)
ORDER BY cm.company_id, cm.is_enabled DESC;

-- El puntero de padre que guarda company_menus también se muda al grupo nuevo.
UPDATE public.company_menus cm SET parent_menu_id = (SELECT id FROM public.menus WHERE key = 'italjira')
WHERE cm.menu_id IN (
        SELECT id FROM public.menus
        WHERE key IN ('italjira.tablero','italjira.roadmap','italjira.panel','italjira.configuracion'))
  AND cm.parent_menu_id IS DISTINCT FROM (SELECT id FROM public.menus WHERE key = 'italjira')
  AND cm.parent_menu_id IS NOT NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- 5) El Backlog se hereda de quien ya ve el Tablero de ItalJira.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rm.role_id, nuevo.id
FROM public.role_menus rm
JOIN public.menus origen ON origen.id = rm.menu_id AND origen.key = 'italjira.tablero'
CROSS JOIN public.menus nuevo
WHERE nuevo.key = 'italjira.backlog'
  AND NOT EXISTS (
        SELECT 1 FROM public.role_menus x
        WHERE x.role_id = rm.role_id AND x.menu_id = nuevo.id);

INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT DISTINCT ON (cm.company_id)
       cm.company_id, nuevo.id, cm.is_enabled, nuevo.sort_order, nuevo.parent_id
FROM public.company_menus cm
JOIN public.menus origen ON origen.id = cm.menu_id AND origen.key = 'italjira.tablero'
CROSS JOIN public.menus nuevo
WHERE nuevo.key = 'italjira.backlog'
  AND NOT EXISTS (
        SELECT 1 FROM public.company_menus y
        WHERE y.company_id = cm.company_id AND y.menu_id = nuevo.id)
ORDER BY cm.company_id, cm.is_enabled DESC;

-- ─────────────────────────────────────────────────────────────────────────────
-- 6) Tickets se reordena: solo quedan las dos vistas que usa el negocio.
-- ─────────────────────────────────────────────────────────────────────────────
UPDATE public.menus SET ""order"" = 1, sort_order = 1, updated_at = now() WHERE key = 'tickets.mis';
UPDATE public.menus SET ""order"" = 2, sort_order = 2, updated_at = now() WHERE key = 'tickets.gestion';
";

        private const string DOWN_SQL = @"
-- El Backlog es nuevo: se va entero.
DELETE FROM public.role_menus       WHERE menu_id IN (SELECT id FROM public.menus WHERE key = 'italjira.backlog');
DELETE FROM public.company_menus    WHERE menu_id IN (SELECT id FROM public.menus WHERE key = 'italjira.backlog');
DELETE FROM public.menu_permissions WHERE menu_id IN (SELECT id FROM public.menus WHERE key = 'italjira.backlog');
DELETE FROM public.menus            WHERE key = 'italjira.backlog';

-- Las cuatro vistas vuelven a Tickets, otra vez EN SITIO.
UPDATE public.menus SET key='tickets.tablero', label='Tablero de casos', route='/tickets/tablero',
       parent_id=(SELECT id FROM public.menus WHERE key='tickets'), ""order""=4, sort_order=4, updated_at=now()
WHERE key='italjira.tablero';

UPDATE public.menus SET key='tickets.roadmap', label='Roadmap', route='/tickets/roadmap',
       parent_id=(SELECT id FROM public.menus WHERE key='tickets'), ""order""=5, sort_order=5, updated_at=now()
WHERE key='italjira.roadmap';

UPDATE public.menus SET key='tickets.panel', label='Panel de control', route='/tickets/panel',
       parent_id=(SELECT id FROM public.menus WHERE key='tickets'), ""order""=6, sort_order=6, updated_at=now()
WHERE key='italjira.panel';

UPDATE public.menus SET key='tickets.admin', label='Administración', route='/tickets/admin',
       parent_id=(SELECT id FROM public.menus WHERE key='tickets'), ""order""=3, sort_order=3, updated_at=now()
WHERE key='italjira.configuracion';

UPDATE public.company_menus cm SET parent_menu_id = (SELECT id FROM public.menus WHERE key='tickets')
WHERE cm.menu_id IN (SELECT id FROM public.menus WHERE key IN ('tickets.tablero','tickets.roadmap','tickets.panel'))
  AND cm.parent_menu_id IS NOT NULL;

DELETE FROM public.role_menus    WHERE menu_id IN (SELECT id FROM public.menus WHERE key = 'italjira');
DELETE FROM public.company_menus WHERE menu_id IN (SELECT id FROM public.menus WHERE key = 'italjira');
DELETE FROM public.menus         WHERE key = 'italjira';
";
    }
}
