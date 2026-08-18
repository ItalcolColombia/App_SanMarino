using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema): los 2 permisos del plan de vacunación de la empresa
    /// (W1.3), su asignación a los roles que hoy ya administran/ven el cronograma, y el menú
    /// <c>Plantillas</c> dentro del grupo Vacunación que ya existe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Claves nuevas y no reutilizar las de cronograma.</b> Editar el plan de la empresa alcanza a
    /// todos los lotes futuros; editar un cronograma alcanza a uno. Como los permisos se heredan de
    /// quien ya tenía los equivalentes de cronograma, <b>hoy nadie gana ni pierde acceso</b>: la
    /// población efectiva es idéntica. Lo que cambia es que mañana se puede quitar «editar el plan de
    /// toda la empresa» sin quitar «editar el cronograma de un lote».
    /// </para>
    /// <para>
    /// Data-only e idempotente (<c>WHERE NOT EXISTS</c> en todo). Sin <c>role_menus</c> automático:
    /// el menú se asigna por la UI de Roles, igual que los otros 3 de Vacunación.
    /// </para>
    /// </remarks>
    public partial class AddPermisosYMenuVacunacionPlantillas : Migration
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
INSERT INTO public.permissions (key, description)
SELECT v.key, v.description
FROM (VALUES
    ('vacunacion.plantillas.ver', 'Vacunación: ver el plan de vacunación estándar de la empresa (plantillas)'),
    ('vacunacion.plantillas.administrar', 'Vacunación: crear/editar el plan de vacunación estándar de la empresa (perfil administrador)')
) AS v(key, description)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);

-- Hereda de cronograma: quien ya podia VER el cronograma puede ver el plan de la empresa.
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT rp.role_id, nuevo.id
FROM public.role_permissions rp
JOIN public.permissions origen ON origen.id = rp.permission_id AND origen.key = 'vacunacion.cronograma.ver'
CROSS JOIN LATERAL (SELECT id FROM public.permissions WHERE key = 'vacunacion.plantillas.ver') AS nuevo
WHERE NOT EXISTS (
    SELECT 1 FROM public.role_permissions x
    WHERE x.role_id = rp.role_id AND x.permission_id = nuevo.id
);

-- ...y quien ya podia ADMINISTRAR el cronograma puede administrar el plan.
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT rp.role_id, nuevo.id
FROM public.role_permissions rp
JOIN public.permissions origen ON origen.id = rp.permission_id AND origen.key = 'vacunacion.cronograma.administrar'
CROSS JOIN LATERAL (SELECT id FROM public.permissions WHERE key = 'vacunacion.plantillas.administrar') AS nuevo
WHERE NOT EXISTS (
    SELECT 1 FROM public.role_permissions x
    WHERE x.role_id = rp.role_id AND x.permission_id = nuevo.id
);

-- Menu: hijo del grupo 'vacunacion' ya existente. Va primero (order 0) porque el plan de la empresa
-- es lo que se define ANTES de mirar el cronograma de cada lote.
INSERT INTO menus (label, icon, route, parent_id, ""order"", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT 'Plantillas del Plan', 'clipboard-list', '/vacunacion/plantillas', p.id, 0, 0, false, true, 'vacunacion.plantillas', NOW(), NOW()
FROM menus p
WHERE p.key = 'vacunacion'
  AND NOT EXISTS (SELECT 1 FROM menus WHERE key = 'vacunacion.plantillas');
";

        // menu_permissions/role_menus/company_menus referencian menus con ON DELETE CASCADE.
        private const string DOWN_SQL = @"
DELETE FROM menus WHERE key = 'vacunacion.plantillas';

DELETE FROM public.role_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key LIKE 'vacunacion.plantillas.%');

DELETE FROM public.menu_permissions
WHERE permission_id IN (SELECT id FROM public.permissions WHERE key LIKE 'vacunacion.plantillas.%');

DELETE FROM public.permissions WHERE key LIKE 'vacunacion.plantillas.%';
";
    }
}
