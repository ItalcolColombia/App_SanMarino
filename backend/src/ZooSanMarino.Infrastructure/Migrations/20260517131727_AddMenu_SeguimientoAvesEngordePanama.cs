using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega el ítem de menú "Seg. Pollo Engorde Panamá" bajo "Registros Diarios" (route = '/daily-log').
    /// Hereda acceso de los roles y empresas que ya tienen el módulo Ecuador (/daily-log/aves-engorde).
    /// Idempotente: no hace nada si la ruta ya existe.
    /// </summary>
    public partial class AddMenu_SeguimientoAvesEngordePanama : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ── 1. INSERTAR ÍTEM DE MENÚ ─────────────────────────────────────────────
WITH parent_daily AS (
    SELECT id
    FROM menus
    WHERE (label ILIKE '%Registros Diarios%' OR route = '/daily-log')
      AND (parent_id IS NULL OR parent_id = 0)
    ORDER BY id
    LIMIT 1
),
ecuador_order AS (
    SELECT COALESCE(m.""order"", 4) AS num
    FROM menus m
    WHERE m.route = '/daily-log/aves-engorde'
    LIMIT 1
)
INSERT INTO menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT
    'Seg. Pollo Engorde Panamá',
    'clipboard-list',
    '/daily-log/aves-engorde-panama',
    (SELECT id FROM parent_daily),
    (SELECT num FROM ecuador_order) + 1,
    true,
    'seguimiento_aves_engorde_panama',
    0,
    false,
    timezone('utc', now()),
    timezone('utc', now())
WHERE EXISTS (SELECT 1 FROM parent_daily)
  AND NOT EXISTS (
      SELECT 1 FROM menus WHERE route = '/daily-log/aves-engorde-panama'
  );

-- ── 2. DESPLAZAR ÍTEMS QUE OCUPABAN ESA POSICIÓN ─────────────────────────
-- Incrementa el order de los ítems siguientes al Ecuador para evitar colisión.
WITH parent_daily AS (
    SELECT id
    FROM menus
    WHERE (label ILIKE '%Registros Diarios%' OR route = '/daily-log')
      AND (parent_id IS NULL OR parent_id = 0)
    ORDER BY id LIMIT 1
),
ecuador_order AS (
    SELECT COALESCE(m.""order"", 4) AS num
    FROM menus m
    WHERE m.route = '/daily-log/aves-engorde'
    LIMIT 1
)
UPDATE menus
SET ""order"" = ""order"" + 1,
    updated_at = timezone('utc', now())
WHERE parent_id = (SELECT id FROM parent_daily)
  AND route <> '/daily-log/aves-engorde-panama'
  AND ""order"" > (SELECT num FROM ecuador_order);

-- ── 3. ASIGNAR A ROLES ────────────────────────────────────────────────────
-- Roles con acceso al módulo Ecuador también reciben el de Panamá.
INSERT INTO role_menus (role_id, menu_id)
SELECT DISTINCT rm_ec.role_id, nuevo.id
FROM menus nuevo
JOIN menus ecuador    ON ecuador.route  = '/daily-log/aves-engorde'
JOIN role_menus rm_ec ON rm_ec.menu_id  = ecuador.id
WHERE nuevo.route = '/daily-log/aves-engorde-panama'
  AND NOT EXISTS (
      SELECT 1 FROM role_menus rm_ex
      WHERE rm_ex.role_id = rm_ec.role_id
        AND rm_ex.menu_id = nuevo.id
  );

-- ── 4. ASIGNAR A EMPRESAS (company_menus) ────────────────────────────────
-- Empresas con el módulo Ecuador habilitado heredan el de Panamá.
INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT
    cm_ec.company_id,
    nuevo.id,
    true,
    cm_ec.sort_order + 1,
    cm_ec.parent_menu_id
FROM company_menus cm_ec
JOIN menus ecuador ON ecuador.id   = cm_ec.menu_id
                  AND ecuador.route = '/daily-log/aves-engorde'
JOIN menus nuevo   ON nuevo.route  = '/daily-log/aves-engorde-panama'
WHERE NOT EXISTS (
    SELECT 1 FROM company_menus cm_ex
    WHERE cm_ex.company_id = cm_ec.company_id
      AND cm_ex.menu_id    = nuevo.id
);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Revertir asignaciones a empresas
DELETE FROM company_menus
WHERE menu_id = (SELECT id FROM menus WHERE route = '/daily-log/aves-engorde-panama' LIMIT 1);

-- Revertir asignaciones a roles
DELETE FROM role_menus
WHERE menu_id = (SELECT id FROM menus WHERE route = '/daily-log/aves-engorde-panama' LIMIT 1);

-- Restaurar orden de ítems desplazados
WITH parent_daily AS (
    SELECT id
    FROM menus
    WHERE (label ILIKE '%Registros Diarios%' OR route = '/daily-log')
      AND (parent_id IS NULL OR parent_id = 0)
    ORDER BY id LIMIT 1
),
ecuador_order AS (
    SELECT COALESCE(m.""order"", 4) AS num
    FROM menus m
    WHERE m.route = '/daily-log/aves-engorde'
    LIMIT 1
)
UPDATE menus
SET ""order"" = ""order"" - 1,
    updated_at = timezone('utc', now())
WHERE parent_id = (SELECT id FROM parent_daily)
  AND route <> '/daily-log/aves-engorde-panama'
  AND ""order"" > (SELECT num FROM ecuador_order) + 1;

-- Eliminar el ítem de menú
DELETE FROM menus WHERE route = '/daily-log/aves-engorde-panama';
");
        }
    }
}
