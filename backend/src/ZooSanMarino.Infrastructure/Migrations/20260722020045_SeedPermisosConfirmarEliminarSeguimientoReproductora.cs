using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed (sin cambios de schema): permisos de los botones Confirmar y Eliminar del módulo
    /// Seguimiento Diario Reproductora (Pollo Engorde). Se OTORGAN a los roles que ya tienen el
    /// menú del módulo (route /daily-log/seguimiento-diario-lote-reproductora_pollo_engorde) para
    /// preservar el borrado actual (hoy sin permiso) y habilitar Confirmar. Idempotente (NOT EXISTS).
    /// Convención del key: "modulo.accion".
    /// </summary>
    public partial class SeedPermisosConfirmarEliminarSeguimientoReproductora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1) Permisos
INSERT INTO public.permissions (key, description)
SELECT v.key, v.descr
FROM (VALUES
    ('seguimiento_reproductora_engorde.confirmar', 'Seguimiento Reproductora Engorde: confirmar registro (habilita la sincronización hacia pollo engorde)'),
    ('seguimiento_reproductora_engorde.eliminar',  'Seguimiento Reproductora Engorde: eliminar registro (retorna aves y consumo)')
) AS v(key, descr)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);

-- 2) Otorgar ambos permisos a los roles que ya tienen el menú del módulo.
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rm.role_id, p.id
FROM public.role_menus rm
JOIN public.menus m ON m.id = rm.menu_id
 AND m.route = '/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde'
CROSS JOIN public.permissions p
WHERE p.key IN ('seguimiento_reproductora_engorde.confirmar', 'seguimiento_reproductora_engorde.eliminar')
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
    'seguimiento_reproductora_engorde.confirmar',
    'seguimiento_reproductora_engorde.eliminar'));
DELETE FROM public.menu_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key IN (
    'seguimiento_reproductora_engorde.confirmar',
    'seguimiento_reproductora_engorde.eliminar'));
DELETE FROM public.permissions WHERE key IN (
    'seguimiento_reproductora_engorde.confirmar',
    'seguimiento_reproductora_engorde.eliminar');
");
        }
    }
}
