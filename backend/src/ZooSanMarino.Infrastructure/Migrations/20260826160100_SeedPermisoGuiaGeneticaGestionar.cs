using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Permiso <c>guia_genetica.gestionar</c>: la key que el módulo nuevo de guía reducida
    /// (<c>GuiaGeneticaSantaReyesController</c>, vía <c>GuiaGeneticaEscrituraGuard</c>) ya exige en
    /// el código y que <b>ninguna migración había sembrado</b>.
    ///
    /// <para>
    /// 🔴 <b>Sin esta migración el módulo nace inutilizable.</b> El permiso <b>invierte el
    /// default</b>: hasta hoy la guía la escribía cualquier sesión autenticada. Con el guard puesto
    /// y la key ausente de <c>permissions</c>, <b>toda</b> escritura responde 403 — y, peor, la key
    /// ni siquiera sería asignable desde la pantalla de Roles, así que no habría forma de arreglarlo
    /// desde la app. Es exactamente lo que pasó con <c>usuarios.revocar_sesion</c>
    /// (<c>20260825130000_SeedPermisoUsuariosGestionar</c>), y por eso este archivo copia ese patrón
    /// paso por paso.
    /// </para>
    ///
    /// <para>
    /// <b>Los tres pasos y por qué ninguno sobra:</b>
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     <c>permissions</c> — la key, con la convención <c>modulo.accion</c> del repo.
    ///   </description></item>
    ///   <item><description>
    ///     <c>company_permissions</c> — <b>FAIL-CLOSED</b> en
    ///     <c>AuthService.PermisosEfectivosAsync</c>: sin la fila, el permiso <b>no viaja en el
    ///     JWT</b> aunque el rol lo tenga, y tampoco se ofrece en el tab Permisos del modal de rol.
    ///     <c>SembrarCatalogoCompletoSiVaciaAsync</c> sólo siembra empresas vacías, no rellena keys
    ///     nuevas en empresas ya configuradas ⇒ este paso es obligatorio, no opcional.
    ///   </description></item>
    ///   <item><description>
    ///     <c>role_permissions</c> — <b>anti-lockout</b>: la reciben todos los roles que hoy ven
    ///     alguno de los <b>tres</b> ítems de guía, localizados por <c>route</c> (jamás por id: los
    ///     ids difieren local ↔ prod). Incluye el ítem nuevo, así que corre bien después de
    ///     <c>20260826160000_SeedMenusGuiaGeneticaTresModulos</c> y también si alguien la re-ejecuta.
    ///   </description></item>
    /// </list>
    ///
    /// <para>
    /// ⛔ <b>Lo que NO hace, a propósito: no toca <c>menu_permissions</c>.</b> Esa tabla sí gatea
    /// (<c>fn_menu_usuario</c> exige que el usuario tenga al menos una de las keys que el menú pide),
    /// y hoy tiene 17 filas, ninguna de guía genética. Agregarle una fila <b>escondería</b> el ítem
    /// a quien no tenga la key — lo contrario de lo pedido: consultar y exportar la guía queda
    /// abierto, como el resto de los módulos de configuración. La escritura la corta el guard del
    /// controller, que es donde importa.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Los permisos viajan en la sesión cifrada</b>: quien tenga sesión abierta al momento del
    /// deploy tiene que <b>re-loguearse</b> para que la key nueva aparezca en su token.
    /// </para>
    ///
    /// <para>
    /// <b>Alcance real del gate</b> (medido en el árbol de trabajo, 26-ago-2026): la key se exige
    /// sólo en <c>GuiaGeneticaSantaReyesController</c> — los controllers de la tabla ancha
    /// (<c>ProduccionAvicolaRaw</c>, <c>ExcelImport</c>) llevan únicamente el guard de <i>perfil</i>,
    /// a propósito, para no cambiarle el comportamiento a las cuatro empresas que hoy escriben sin
    /// permiso alguno. Otorgarla igual a los roles de esas empresas es inerte hoy y evita el lockout
    /// el día que una de ellas pase a perfil reducido.
    /// </para>
    ///
    /// Espejo legible: <c>backend/sql/seed_permiso_guia_genetica_gestionar.sql</c>.
    /// Migración DATA-ONLY: Designer clonado, <c>ZooSanMarinoContextModelSnapshot</c> intacto.
    /// Idempotente (<c>WHERE NOT EXISTS</c>), localizando por <c>permissions.key</c> y por
    /// <c>menus.route</c>.
    /// </summary>
    public partial class SeedPermisoGuiaGeneticaGestionar : Migration
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
-- ─────────────────────────────────────────────────────────────────────────────
-- 1) La key. Convencion `modulo.accion`, la misma que exige
--    GuiaGeneticaEscrituraAutorizacionCalculos.PermisoGestionar.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.permissions (key, description)
SELECT 'guia_genetica.gestionar',
       'Guia Genetica: crear, editar, importar y dar de baja lineas de la guia. Sin este permiso el modulo queda de solo lectura: se consulta y se exporta.'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = 'guia_genetica.gestionar');

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Habilitada en TODAS las empresas. company_permissions es FAIL-CLOSED: sin esta fila el
--    permiso no viaja en el JWT aunque el rol lo tenga.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT c.id, p.id, true
  FROM public.companies c
 CROSS JOIN public.permissions p
 WHERE p.key = 'guia_genetica.gestionar'
   AND NOT EXISTS (SELECT 1 FROM public.company_permissions x
                    WHERE x.company_id = c.id AND x.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) ANTI-LOCKOUT: todo rol que HOY ve alguno de los tres itemes de guia. Por route, nunca por id.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rm.role_id, p.id
  FROM public.role_menus rm
  JOIN public.menus m ON m.id = rm.menu_id
                     AND m.route IN ('/config/guia-genetica',
                                     '/config/guia-genetica-ecuador',
                                     '/config/guia-genetica-santa-reyes')
 CROSS JOIN public.permissions p
 WHERE p.key = 'guia_genetica.gestionar'
   AND NOT EXISTS (SELECT 1 FROM public.role_permissions rp
                    WHERE rp.role_id = rm.role_id AND rp.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) Y al rol Admin, que puede no tener el menu cableado y igual tiene que poder administrar.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT 1, p.id
  FROM public.permissions p
 WHERE p.key = 'guia_genetica.gestionar'
   AND EXISTS (SELECT 1 FROM public.roles r WHERE r.id = 1)
   AND NOT EXISTS (SELECT 1 FROM public.role_permissions rp
                    WHERE rp.role_id = 1 AND rp.permission_id = p.id);
";

        private const string DOWN_SQL = @"
DELETE FROM public.role_permissions    WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'guia_genetica.gestionar');
DELETE FROM public.company_permissions WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'guia_genetica.gestionar');
DELETE FROM public.permissions         WHERE key = 'guia_genetica.gestionar';
";
    }
}
