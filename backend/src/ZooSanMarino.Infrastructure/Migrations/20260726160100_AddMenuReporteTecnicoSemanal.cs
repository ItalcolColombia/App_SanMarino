using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega el ítem de menú "Reporte Técnico Semanal" bajo el grupo "Reportes".
    /// Ruta Angular: '/reporte-tecnico-semanal'. A diferencia de otros reportes,
    /// se habilita SOLO para la empresa 'Agroavicola Sanmarino' (lookup por
    /// nombre, jamás por id fijo): company_menus solo para esa empresa y
    /// role_menus solo para roles de esa empresa que ya vean el hermano
    /// '/reportes-tecnicos'. Data-only (Designer clonado), idempotente.
    /// </summary>
    public partial class AddMenuReporteTecnicoSemanal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ── 1. INSERTAR ÍTEM DE MENÚ bajo 'Reportes' ─────────────────────────────
WITH parent_rep AS (
    SELECT id FROM menus
    WHERE (key = 'reporte' OR label ILIKE 'Reportes')
      AND (parent_id IS NULL OR parent_id = 0)
    ORDER BY id LIMIT 1
),
next_order AS (
    SELECT COALESCE(MAX(""order""), -1) + 1 AS num
    FROM menus WHERE parent_id = (SELECT id FROM parent_rep)
)
INSERT INTO menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT
    'Reporte Técnico Semanal',
    'chart-bar',
    '/reporte-tecnico-semanal',
    (SELECT id FROM parent_rep),
    (SELECT num FROM next_order),
    true,
    'reporte_tecnico_semanal',
    0,
    false,
    timezone('utc', now()),
    timezone('utc', now())
WHERE EXISTS (SELECT 1 FROM parent_rep)
  AND NOT EXISTS (SELECT 1 FROM menus WHERE route = '/reporte-tecnico-semanal');

-- ── 2. ASIGNAR SOLO A ROLES DE SANMARINO que ya ven '/reportes-tecnicos' ──
INSERT INTO role_menus (role_id, menu_id)
SELECT DISTINCT rm_src.role_id, nuevo.id
FROM menus nuevo
JOIN menus src            ON src.route      = '/reportes-tecnicos'
JOIN role_menus rm_src    ON rm_src.menu_id = src.id
JOIN role_companies rc    ON rc.role_id     = rm_src.role_id
JOIN companies c          ON c.id           = rc.company_id
                         AND c.name ILIKE 'Agroavicola Sanmarino'
WHERE nuevo.route = '/reporte-tecnico-semanal'
  AND NOT EXISTS (
      SELECT 1 FROM role_menus rm_ex
      WHERE rm_ex.role_id = rm_src.role_id AND rm_ex.menu_id = nuevo.id
  );

-- ── 3. HABILITAR SOLO PARA LA EMPRESA SANMARINO (company_menus) ──────────
INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT
    c.id,
    nuevo.id,
    true,
    COALESCE((SELECT MAX(cm.sort_order) + 1
                FROM company_menus cm
               WHERE cm.company_id = c.id), 0),
    (SELECT parent_id FROM menus WHERE route = '/reporte-tecnico-semanal')
FROM companies c
JOIN menus nuevo ON nuevo.route = '/reporte-tecnico-semanal'
WHERE c.name ILIKE 'Agroavicola Sanmarino'
  AND NOT EXISTS (
      SELECT 1 FROM company_menus cm_ex
      WHERE cm_ex.company_id = c.id AND cm_ex.menu_id = nuevo.id
  );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM company_menus WHERE menu_id = (SELECT id FROM menus WHERE route = '/reporte-tecnico-semanal' LIMIT 1);
DELETE FROM role_menus    WHERE menu_id = (SELECT id FROM menus WHERE route = '/reporte-tecnico-semanal' LIMIT 1);
DELETE FROM menus WHERE route = '/reporte-tecnico-semanal';
");
        }
    }
}
