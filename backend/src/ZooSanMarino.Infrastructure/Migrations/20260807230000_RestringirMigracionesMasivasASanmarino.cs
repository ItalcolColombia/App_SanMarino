using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema). Tres cosas, todas idempotentes:
    ///
    /// 1. <b>Los permisos de carga masiva deben existir.</b> Los crea
    ///    <c>AddPermisosCargaMasivaMigracionesMasivas</c>, pero si esa migración quedó registrada sin
    ///    haber corrido (o alguien aplicó su Down), la fila no está y el permiso no aparece en Roles
    ///    y Permisos para asignarlo. Se re-asegura con NOT EXISTS.
    ///
    /// 2. <b>El módulo queda SOLO para Agroavicola Sanmarino.</b> <c>AddMigracionesMasivasMenu</c> lo
    ///    sembró heredando la visibilidad del módulo "Lotes", así que quedó habilitado en TODAS las
    ///    empresas. Decisión de negocio (ago-2026): por ahora solo Sanmarino Colombia.
    ///    ⚠️ La visibilidad real del menú la resuelve <c>RoleCompositeService.Menus_GetForUserAsync</c>
    ///    desde <c>role_menus</c> — NO lee <c>company_menus</c>. Por eso hay que limpiar las DOS:
    ///    <c>role_menus</c> oculta el módulo hoy; <c>company_menus</c> evita que el admin de otra
    ///    empresa lo vuelva a ofrecer mañana.
    ///    Se conserva el menú solo en roles de uso EXCLUSIVO de Sanmarino (todos sus usuarios son de
    ///    esa empresa); un rol compartido entre empresas se retira, porque mantenerlo se lo dejaría
    ///    también a la otra empresa.
    ///
    /// 3. <b>El rol que ya carga engorde en Sanmarino también puede cargar postura.</b> Ese era el
    ///    bloqueo reportado: "Implementador Sanmarino Colombia" tenía
    ///    <c>carga_masiva_pollo_engorde</c> pero NINGÚN rol de Sanmarino tenía
    ///    <c>carga_masiva_postura</c>, así que los tiles de Levante/Producción salían deshabilitados.
    ///    El grant es acotado: solo roles exclusivos de Sanmarino que YA tenían el permiso hermano.
    ///
    /// Todo se localiza por <c>companies.name</c> / <c>menus.route</c> / <c>permissions.key</c>,
    /// nunca por id: los ids difieren entre local y prod.
    /// </summary>
    public partial class RestringirMigracionesMasivasASanmarino : Migration
    {
        /// <summary>Empresa única habilitada para el módulo (nombre exacto en <c>companies</c>).</summary>
        private const string EmpresaHabilitada = "Agroavicola Sanmarino";

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
-- 1) Los dos permisos del módulo deben existir (re-aseguro idempotente)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.permissions (key, description)
SELECT v.key, v.descr
FROM (VALUES
    ('carga_masiva_pollo_engorde', 'Migraciones Masivas: acceso a la carga masiva de Pollo Engorde (Lotes, Seguimiento y Venta)'),
    ('carga_masiva_postura',       'Migraciones Masivas: acceso a la carga masiva de Postura (Seguimiento Levante y Produccion)')
) AS v(key, descr)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) El modulo queda SOLO para Agroavicola Sanmarino
-- ─────────────────────────────────────────────────────────────────────────────

-- 2.a company_menus: se retira de toda empresa que no sea la habilitada.
DELETE FROM public.company_menus cm
USING public.menus m
WHERE cm.menu_id = m.id
  AND m.route = '/migraciones-masivas'
  AND cm.company_id NOT IN (SELECT c.id FROM public.companies c WHERE c.name = 'Agroavicola Sanmarino');

-- 2.b ... y se garantiza que la empresa habilitada SI lo tenga.
INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT c.id, m.id, true, 0, NULL
FROM public.companies c
CROSS JOIN public.menus m
WHERE c.name = 'Agroavicola Sanmarino'
  AND m.route = '/migraciones-masivas'
  AND NOT EXISTS (
      SELECT 1 FROM public.company_menus cm
      WHERE cm.company_id = c.id AND cm.menu_id = m.id
  );

-- 2.c role_menus: es lo que el backend LEE para armar el menu del usuario. Se conserva solo en
--     roles de uso exclusivo de la empresa habilitada (tienen al menos un usuario de esa empresa
--     y ninguno de otra).
DELETE FROM public.role_menus rm
USING public.menus m
WHERE rm.menu_id = m.id
  AND m.route = '/migraciones-masivas'
  AND NOT (
        EXISTS (
            SELECT 1 FROM public.user_roles ur
            JOIN public.companies c ON c.id = ur.company_id
            WHERE ur.role_id = rm.role_id AND c.name = 'Agroavicola Sanmarino'
        )
    AND NOT EXISTS (
            SELECT 1 FROM public.user_roles ur
            JOIN public.companies c ON c.id = ur.company_id
            WHERE ur.role_id = rm.role_id AND c.name <> 'Agroavicola Sanmarino'
        )
  );

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) Permiso de POSTURA para el rol de Sanmarino que ya cargaba ENGORDE
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.roles r
CROSS JOIN public.permissions p
WHERE p.key = 'carga_masiva_postura'
  -- ya tenia el permiso hermano de engorde
  AND EXISTS (
      SELECT 1 FROM public.role_permissions rp
      JOIN public.permissions pe ON pe.id = rp.permission_id
      WHERE rp.role_id = r.id AND pe.key = 'carga_masiva_pollo_engorde'
  )
  -- y es un rol de uso exclusivo de la empresa habilitada
  AND EXISTS (
      SELECT 1 FROM public.user_roles ur
      JOIN public.companies c ON c.id = ur.company_id
      WHERE ur.role_id = r.id AND c.name = 'Agroavicola Sanmarino'
  )
  AND NOT EXISTS (
      SELECT 1 FROM public.user_roles ur
      JOIN public.companies c ON c.id = ur.company_id
      WHERE ur.role_id = r.id AND c.name <> 'Agroavicola Sanmarino'
  )
  AND NOT EXISTS (
      SELECT 1 FROM public.role_permissions rp
      WHERE rp.role_id = r.id AND rp.permission_id = p.id
  );

-- 3.b Ese rol tiene que ver el item de menu (si no, tiene el permiso pero no la pantalla).
INSERT INTO public.role_menus (role_id, menu_id)
SELECT r.id, m.id
FROM public.roles r
CROSS JOIN public.menus m
WHERE m.route = '/migraciones-masivas'
  AND EXISTS (
      SELECT 1 FROM public.role_permissions rp
      JOIN public.permissions pe ON pe.id = rp.permission_id
      WHERE rp.role_id = r.id
        AND pe.key IN ('carga_masiva_postura', 'carga_masiva_pollo_engorde')
  )
  AND EXISTS (
      SELECT 1 FROM public.user_roles ur
      JOIN public.companies c ON c.id = ur.company_id
      WHERE ur.role_id = r.id AND c.name = 'Agroavicola Sanmarino'
  )
  AND NOT EXISTS (
      SELECT 1 FROM public.user_roles ur
      JOIN public.companies c ON c.id = ur.company_id
      WHERE ur.role_id = r.id AND c.name <> 'Agroavicola Sanmarino'
  )
  AND NOT EXISTS (
      SELECT 1 FROM public.role_menus rm
      WHERE rm.role_id = r.id AND rm.menu_id = m.id
  );
";

        // Revertir = volver al estado que dejaba AddMigracionesMasivasMenu: el modulo hereda la
        // visibilidad del modulo "Lotes" en todas las empresas y roles. No se puede reconstruir la
        // foto exacta previa (las filas borradas no se guardan), pero si el punto de partida.
        private const string DOWN_SQL = @"
-- role_menus: reheredar del modulo ""Lotes""
WITH ref AS (
    SELECT id FROM public.menus
    WHERE (route = '/lote' OR route LIKE '/lote%' OR label ILIKE '%lote%')
      AND parent_id IS NULL
    ORDER BY id LIMIT 1
)
INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rm_ref.role_id, nuevo.id
FROM public.role_menus rm_ref
JOIN ref ON ref.id = rm_ref.menu_id
CROSS JOIN public.menus nuevo
WHERE nuevo.route = '/migraciones-masivas'
  AND NOT EXISTS (
      SELECT 1 FROM public.role_menus rm
      WHERE rm.role_id = rm_ref.role_id AND rm.menu_id = nuevo.id
  );

-- company_menus: reheredar del modulo ""Lotes""
WITH ref AS (
    SELECT id FROM public.menus
    WHERE (route = '/lote' OR route LIKE '/lote%' OR label ILIKE '%lote%')
      AND parent_id IS NULL
    ORDER BY id LIMIT 1
)
INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT cm_ref.company_id, nuevo.id, true, cm_ref.sort_order + 1, NULL
FROM public.company_menus cm_ref
JOIN ref ON ref.id = cm_ref.menu_id
JOIN public.menus nuevo ON nuevo.route = '/migraciones-masivas'
WHERE NOT EXISTS (
    SELECT 1 FROM public.company_menus cm
    WHERE cm.company_id = cm_ref.company_id AND cm.menu_id = nuevo.id
);
";
    }
}
