using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema) del módulo de tickets:
    ///  - 3 permisos (tickets.crear / tickets.gestionar / tickets.admin) → quedan disponibles
    ///    en el módulo de Roles y Permisos para asignarlos a los roles.
    ///  - Menú "Tickets" (grupo + 3 ítems) con menu_permissions, de modo que el sidebar
    ///    los muestra SOLO a quienes tengan el permiso correspondiente (el backend filtra por
    ///    MenuPermissions). Todo idempotente (NOT EXISTS) para soportar re-runs.
    /// </summary>
    public partial class SeedTicketsPermissionsAndMenu : Migration
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
-- 1) Permisos ----------------------------------------------------------------
INSERT INTO public.permissions (key, description)
SELECT v.key, v.descr
FROM (VALUES
    ('tickets.crear',     'Crear y ver sus propios tickets de soporte'),
    ('tickets.gestionar', 'Gestionar tickets: bandeja por país, tomar y cambiar estado'),
    ('tickets.admin',     'Administración global de tickets (todos los países)')
) AS v(key, descr)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);

-- 2) Menú: grupo padre -------------------------------------------------------
INSERT INTO public.menus (key, label, icon, route, ""order"", sort_order, is_group, is_active, parent_id, created_at, updated_at)
SELECT 'tickets', 'Tickets', 'clipboard-list', NULL, 900, 900, true, true, NULL, now(), now()
WHERE NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key = 'tickets');

-- 2b) Hijos ------------------------------------------------------------------
INSERT INTO public.menus (key, label, icon, route, ""order"", sort_order, is_group, is_active, parent_id, created_at, updated_at)
SELECT 'tickets.mis', 'Mis solicitudes', 'list', '/tickets', 1, 1, false, true, p.id, now(), now()
FROM public.menus p
WHERE p.key = 'tickets'
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key = 'tickets.mis');

INSERT INTO public.menus (key, label, icon, route, ""order"", sort_order, is_group, is_active, parent_id, created_at, updated_at)
SELECT 'tickets.gestion', 'Bandeja de gestión', 'tools', '/tickets/gestion', 2, 2, false, true, p.id, now(), now()
FROM public.menus p
WHERE p.key = 'tickets'
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key = 'tickets.gestion');

INSERT INTO public.menus (key, label, icon, route, ""order"", sort_order, is_group, is_active, parent_id, created_at, updated_at)
SELECT 'tickets.admin', 'Administración', 'user-shield', '/tickets/admin', 3, 3, false, true, p.id, now(), now()
FROM public.menus p
WHERE p.key = 'tickets'
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key = 'tickets.admin');

-- 3) menu_permissions: gating por permiso ------------------------------------
-- Grupo padre: visible si tiene cualquiera de los 3
INSERT INTO public.menu_permissions (menu_id, permission_id)
SELECT m.id, p.id
FROM public.menus m
JOIN public.permissions p ON p.key IN ('tickets.crear','tickets.gestionar','tickets.admin')
WHERE m.key = 'tickets'
  AND NOT EXISTS (SELECT 1 FROM public.menu_permissions mp WHERE mp.menu_id = m.id AND mp.permission_id = p.id);

-- Mis solicitudes: tickets.crear
INSERT INTO public.menu_permissions (menu_id, permission_id)
SELECT m.id, p.id
FROM public.menus m
JOIN public.permissions p ON p.key = 'tickets.crear'
WHERE m.key = 'tickets.mis'
  AND NOT EXISTS (SELECT 1 FROM public.menu_permissions mp WHERE mp.menu_id = m.id AND mp.permission_id = p.id);

-- Gestión: tickets.gestionar + tickets.admin
INSERT INTO public.menu_permissions (menu_id, permission_id)
SELECT m.id, p.id
FROM public.menus m
JOIN public.permissions p ON p.key IN ('tickets.gestionar','tickets.admin')
WHERE m.key = 'tickets.gestion'
  AND NOT EXISTS (SELECT 1 FROM public.menu_permissions mp WHERE mp.menu_id = m.id AND mp.permission_id = p.id);

-- Administración: tickets.admin
INSERT INTO public.menu_permissions (menu_id, permission_id)
SELECT m.id, p.id
FROM public.menus m
JOIN public.permissions p ON p.key = 'tickets.admin'
WHERE m.key = 'tickets.admin'
  AND NOT EXISTS (SELECT 1 FROM public.menu_permissions mp WHERE mp.menu_id = m.id AND mp.permission_id = p.id);
";

        private const string DOWN_SQL = @"
-- Quitar menu_permissions de los menús de tickets
DELETE FROM public.menu_permissions
WHERE menu_id IN (SELECT id FROM public.menus WHERE key IN ('tickets','tickets.mis','tickets.gestion','tickets.admin'));

-- Quitar los menús (hijos primero por la FK self)
DELETE FROM public.menus WHERE key IN ('tickets.mis','tickets.gestion','tickets.admin');
DELETE FROM public.menus WHERE key = 'tickets';

-- Quitar referencias a los permisos y luego los permisos
DELETE FROM public.menu_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN ('tickets.crear','tickets.gestionar','tickets.admin'));
DELETE FROM public.role_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN ('tickets.crear','tickets.gestionar','tickets.admin'));
DELETE FROM public.permissions WHERE key IN ('tickets.crear','tickets.gestionar','tickets.admin');
";
    }
}
