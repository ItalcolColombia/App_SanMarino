using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed (sin cambios de schema) de los permisos de la <b>doble validación</b> de los seguimientos
    /// diarios: validar y quitar la validación en levante, producción y pollo engorde.
    /// </summary>
    /// <remarks>
    /// <b>Por qué validar y des-validar son permisos distintos.</b> Validar es capturar: confirma que
    /// el registro del día está bien y recién ahí se descuenta el alimento y las aves. Des-validar
    /// devuelve unidades que ya se descontaron, que es una operación de corrección — y es la única vía
    /// para editar un registro cerrado. Con un solo permiso, quien puede confirmar el día también
    /// puede deshacer descuentos viejos, que no es lo que se quiere.
    /// </remarks>
    /// <remarks>
    /// <b>Reproductora no aparece acá:</b> reusa <c>seguimiento_reproductora_engorde.confirmar</c>, que
    /// sembró <c>20260722020045_SeedPermisosConfirmarEliminarSeguimientoReproductora</c>. Su columna
    /// <c>confirmado</c> es la misma marca de validación, así que darle un permiso nuevo dejaría dos
    /// llaves para la misma puerta.
    /// </remarks>
    /// <remarks>
    /// <b>A quién se le otorga:</b> a los roles que YA tienen el menú del módulo, localizando el menú
    /// por <c>route</c> y nunca por id (los ids difieren local↔prod). El permiso de <b>validar</b> se
    /// otorga; el de <b>des-validar</b> NO se otorga a nadie a propósito — se asigna a mano a quien
    /// corresponda, porque deshacer un descuento no es una tarea de captura diaria.
    ///
    /// Idempotente (<c>WHERE NOT EXISTS</c>). Migración DATA-ONLY: Designer clonado, ModelSnapshot
    /// intacto.
    /// </remarks>
    public partial class SeedPermisosValidarSeguimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1) Catálogo de permisos
INSERT INTO public.permissions (key, description)
SELECT v.key, v.descr
FROM (VALUES
    ('seguimiento_levante.validar',       'Seguimiento Levante: validar el registro (aplica el consumo de alimento y el descuento de aves)'),
    ('seguimiento_levante.desvalidar',    'Seguimiento Levante: quitar la validación (devuelve alimento y aves; habilita corregir)'),
    ('seguimiento_produccion.validar',    'Seguimiento Producción: validar el registro (aplica el consumo de alimento y el descuento de aves)'),
    ('seguimiento_produccion.desvalidar', 'Seguimiento Producción: quitar la validación (devuelve alimento y aves; habilita corregir)'),
    ('seguimiento_engorde.validar',       'Seguimiento Pollo Engorde: validar el registro (aplica el consumo de alimento y el descuento de aves)'),
    ('seguimiento_engorde.desvalidar',    'Seguimiento Pollo Engorde: quitar la validación (devuelve alimento y aves; habilita corregir)')
) AS v(key, descr)
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = v.key);

-- 2) Otorgar SOLO el permiso de validar, y solo a los roles que ya tienen el menú del módulo.
--    Menús localizados por route: los ids difieren entre local y producción.
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rm.role_id, p.id
FROM public.role_menus rm
JOIN public.menus m ON m.id = rm.menu_id
JOIN (VALUES
    ('/daily-log/seguimiento',       'seguimiento_levante.validar'),
    ('/daily-log/produccion',        'seguimiento_produccion.validar'),
    ('/daily-log/aves-engorde',      'seguimiento_engorde.validar'),
    ('/daily-log/aves-engorde-panama','seguimiento_engorde.validar')
) AS ruta(route, perm) ON ruta.route = m.route
JOIN public.permissions p ON p.key = ruta.perm
WHERE NOT EXISTS (
    SELECT 1 FROM public.role_permissions rp
    WHERE rp.role_id = rm.role_id AND rp.permission_id = p.id
);
");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Quita las concesiones y los seis permisos. No toca nada más: si alguien asignó
        /// <c>desvalidar</c> a mano, esa fila también se va con el permiso, que es lo correcto — el
        /// permiso deja de existir.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM public.role_permissions rp
USING public.permissions p
WHERE rp.permission_id = p.id
  AND p.key IN (
    'seguimiento_levante.validar', 'seguimiento_levante.desvalidar',
    'seguimiento_produccion.validar', 'seguimiento_produccion.desvalidar',
    'seguimiento_engorde.validar', 'seguimiento_engorde.desvalidar');

DELETE FROM public.permissions
WHERE key IN (
    'seguimiento_levante.validar', 'seguimiento_levante.desvalidar',
    'seguimiento_produccion.validar', 'seguimiento_produccion.desvalidar',
    'seguimiento_engorde.validar', 'seguimiento_engorde.desvalidar');
");
        }
    }
}
