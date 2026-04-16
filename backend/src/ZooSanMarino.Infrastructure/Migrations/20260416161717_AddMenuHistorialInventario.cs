using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega el ítem de menú "Historial de Inventario" como hijo de "Gestión de Inventario"
    /// (ruta padre: /gestion-inventario).
    /// Lo asigna automáticamente a todos los roles que ya tienen acceso al padre.
    /// Idempotente: no hace nada si la ruta ya existe.
    /// </summary>
    public partial class AddMenuHistorialInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1. Insertar el ítem de menú bajo ""Configuración"" (key = 'config')
--    Idempotente: no hace nada si ya existe la ruta.
WITH parent AS (
    SELECT id
    FROM menus
    WHERE key = 'config'
    ORDER BY id
    LIMIT 1
),
next_order AS (
    SELECT COALESCE(MAX(m.""order""), 0) + 1 AS num
    FROM menus m
    WHERE m.parent_id = (SELECT id FROM parent)
)
INSERT INTO menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT
    'Historial de Inventario',
    'history',
    '/gestion-inventario/historial',
    (SELECT id FROM parent),
    (SELECT num FROM next_order),
    true,
    'inventory_historial',
    0,
    false,
    timezone('utc', now()),
    timezone('utc', now())
WHERE EXISTS (SELECT 1 FROM parent)
  AND NOT EXISTS (
      SELECT 1 FROM menus WHERE route = '/gestion-inventario/historial'
  );

-- 2. Asignar a todos los roles que tienen acceso al padre (""Configuración"")
INSERT INTO role_menus (role_id, menu_id)
SELECT r.role_id, child.id
FROM menus child
CROSS JOIN (
    SELECT DISTINCT rm.role_id
    FROM role_menus rm
    INNER JOIN menus parent ON parent.id = rm.menu_id
    WHERE parent.key = 'config'
) r
WHERE child.route = '/gestion-inventario/historial'
  AND NOT EXISTS (
      SELECT 1 FROM role_menus rm2
      WHERE rm2.role_id = r.role_id AND rm2.menu_id = child.id
  );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM role_menus
WHERE menu_id = (SELECT id FROM menus WHERE route = '/gestion-inventario/historial' LIMIT 1);

DELETE FROM menus WHERE route = '/gestion-inventario/historial';
");
        }
    }
}
