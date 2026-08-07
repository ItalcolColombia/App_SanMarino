using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega el ítem de menú "Reporte Diario Costos Postura" bajo el grupo "Reportes".
    /// Ruta Angular: '/reporte-diario-costos-postura'.
    ///
    /// ROLES: hereda los de "Reporte Contable" ('/reporte-contable'), que es el reporte de postura
    /// con la audiencia correcta (incluye el rol "costos Sanmarino").
    /// EMPRESAS: SOLO Agroavicola Sanmarino. El pedido es para Sanmarino Colombia; el reporte es
    /// generico y funciona para cualquier empresa con postura, pero habilitarlo en las demas es una
    /// decision de negocio que se toma desde la UI de administracion, no desde una migracion.
    ///
    /// Idempotente (INSERT ... WHERE NOT EXISTS), localiza los menus por RUTA (los ids difieren
    /// entre local y produccion).
    /// </summary>
    public partial class AddMenuReporteDiarioCostosPostura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ── 1. ITEM DE MENU bajo 'Reportes' ──────────────────────────────────────
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
    'Reporte Diario Costos Postura',
    'coins',
    '/reporte-diario-costos-postura',
    (SELECT id FROM parent_rep),
    (SELECT num FROM next_order),
    true,
    'reporte_diario_costos_postura',
    0,
    false,
    timezone('utc', now()),
    timezone('utc', now())
WHERE EXISTS (SELECT 1 FROM parent_rep)
  AND NOT EXISTS (SELECT 1 FROM menus WHERE route = '/reporte-diario-costos-postura');

-- ── 2. ROLES (heredan de 'Reporte Contable') ─────────────────────────────
INSERT INTO role_menus (role_id, menu_id)
SELECT DISTINCT rm_src.role_id, nuevo.id
FROM menus nuevo
JOIN menus src         ON src.route      = '/reporte-contable'
JOIN role_menus rm_src ON rm_src.menu_id = src.id
WHERE nuevo.route = '/reporte-diario-costos-postura'
  AND NOT EXISTS (
      SELECT 1 FROM role_menus rm_ex
      WHERE rm_ex.role_id = rm_src.role_id AND rm_ex.menu_id = nuevo.id
  );

-- ── 3. EMPRESAS: solo Agroavicola Sanmarino ──────────────────────────────
INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT
    cm_src.company_id,
    nuevo.id,
    true,
    cm_src.sort_order + 1,
    cm_src.parent_menu_id
FROM company_menus cm_src
JOIN menus src   ON src.id      = cm_src.menu_id AND src.route = '/reporte-contable'
JOIN menus nuevo ON nuevo.route = '/reporte-diario-costos-postura'
JOIN companies c ON c.id        = cm_src.company_id AND c.name = 'Agroavicola Sanmarino'
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
DELETE FROM company_menus WHERE menu_id = (SELECT id FROM menus WHERE route = '/reporte-diario-costos-postura' LIMIT 1);
DELETE FROM role_menus    WHERE menu_id = (SELECT id FROM menus WHERE route = '/reporte-diario-costos-postura' LIMIT 1);
DELETE FROM menus WHERE route = '/reporte-diario-costos-postura';
");
        }
    }
}
