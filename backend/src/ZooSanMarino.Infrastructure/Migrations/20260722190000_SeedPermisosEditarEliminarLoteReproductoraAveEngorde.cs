using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed (sin cambios de schema): permisos de los botones Editar y Eliminar del módulo
    /// Lote Reproductora Aves de Engorde. Se OTORGAN a los roles que ya tienen el menú del módulo
    /// (route /config/lote-reproductora-ave-engorde) para preservar el acceso actual (hoy sin gate).
    /// Idempotente (NOT EXISTS). Convención del key: "modulo.accion".
    /// </summary>
    public partial class SeedPermisosEditarEliminarLoteReproductoraAveEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1) Permisos
INSERT INTO public.permissions (key, description)
SELECT v.key, v.descr
FROM (VALUES
    ('lote_reproductora_engorde.editar',   'Lote Reproductora Engorde: editar la reproductora'),
    ('lote_reproductora_engorde.eliminar', 'Lote Reproductora Engorde: eliminar la reproductora')
) AS v(key, descr)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);

-- 2) Otorgar ambos permisos a los roles que ya tienen el menú del módulo.
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rm.role_id, p.id
FROM public.role_menus rm
JOIN public.menus m ON m.id = rm.menu_id
 AND m.route = '/config/lote-reproductora-ave-engorde'
CROSS JOIN public.permissions p
WHERE p.key IN ('lote_reproductora_engorde.editar', 'lote_reproductora_engorde.eliminar')
  AND NOT EXISTS (
      SELECT 1 FROM public.role_permissions rp
      WHERE rp.role_id = rm.role_id AND rp.permission_id = p.id
  );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM public.role_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN (
    'lote_reproductora_engorde.editar',
    'lote_reproductora_engorde.eliminar'));
DELETE FROM public.menu_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN (
    'lote_reproductora_engorde.editar',
    'lote_reproductora_engorde.eliminar'));
DELETE FROM public.permissions WHERE key IN (
    'lote_reproductora_engorde.editar',
    'lote_reproductora_engorde.eliminar');
");
        }
    }
}
