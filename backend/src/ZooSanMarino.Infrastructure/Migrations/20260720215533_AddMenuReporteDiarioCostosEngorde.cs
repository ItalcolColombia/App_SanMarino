using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega el ítem de menú "Reporte Diario Costos Engorde" bajo el grupo "Reportes".
    /// Ruta Angular: '/reporte-diario-costos-engorde'. Hereda roles y empresas de
    /// "Informe Semanal Pollo Engorde" ('/informe-semanal-engorde'). Idempotente.
    /// </summary>
    public partial class AddMenuReporteDiarioCostosEngorde : Migration
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
    'Reporte Diario Costos Engorde',
    'coins',
    '/reporte-diario-costos-engorde',
    (SELECT id FROM parent_rep),
    (SELECT num FROM next_order),
    true,
    'reporte_diario_costos_engorde',
    0,
    false,
    timezone('utc', now()),
    timezone('utc', now())
WHERE EXISTS (SELECT 1 FROM parent_rep)
  AND NOT EXISTS (SELECT 1 FROM menus WHERE route = '/reporte-diario-costos-engorde');

-- ── 2. ASIGNAR A ROLES (heredan de 'Informe Semanal Pollo Engorde') ──────
INSERT INTO role_menus (role_id, menu_id)
SELECT DISTINCT rm_src.role_id, nuevo.id
FROM menus nuevo
JOIN menus src         ON src.route     = '/informe-semanal-engorde'
JOIN role_menus rm_src ON rm_src.menu_id = src.id
WHERE nuevo.route = '/reporte-diario-costos-engorde'
  AND NOT EXISTS (
      SELECT 1 FROM role_menus rm_ex
      WHERE rm_ex.role_id = rm_src.role_id AND rm_ex.menu_id = nuevo.id
  );

-- ── 3. ASIGNAR A EMPRESAS (company_menus) ────────────────────────────────
INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT
    cm_src.company_id,
    nuevo.id,
    true,
    cm_src.sort_order + 1,
    cm_src.parent_menu_id
FROM company_menus cm_src
JOIN menus src   ON src.id      = cm_src.menu_id AND src.route = '/informe-semanal-engorde'
JOIN menus nuevo ON nuevo.route = '/reporte-diario-costos-engorde'
WHERE NOT EXISTS (
    SELECT 1 FROM company_menus cm_ex
    WHERE cm_ex.company_id = cm_src.company_id AND cm_ex.menu_id = nuevo.id
);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM company_menus WHERE menu_id = (SELECT id FROM menus WHERE route = '/reporte-diario-costos-engorde' LIMIT 1);
DELETE FROM role_menus    WHERE menu_id = (SELECT id FROM menus WHERE route = '/reporte-diario-costos-engorde' LIMIT 1);
DELETE FROM menus WHERE route = '/reporte-diario-costos-engorde';
");
        }
    }
}
