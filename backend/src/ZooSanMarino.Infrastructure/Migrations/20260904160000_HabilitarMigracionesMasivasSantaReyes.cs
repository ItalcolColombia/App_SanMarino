using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema): habilita el módulo <b>Migraciones Masivas</b> para
    /// <b>Santa Reyes</b>, que necesita cargar el histórico de Levante y Producción por Excel.
    ///
    /// <para>
    /// <b>Por qué hacía falta.</b> <c>RestringirMigracionesMasivasASanmarino</c> (20260807230000) dejó
    /// el módulo solo para Agroavicola Sanmarino — decisión de negocio explícita de ago-2026, no un
    /// descuido. Esta migración la levanta para UNA empresa más, sin tocar a las demás.
    /// </para>
    ///
    /// <para>
    /// <b>Qué falta exactamente, medido.</b> De las cuatro capas de la cadena de acceso, tres ya
    /// estaban: <c>company_permissions</c> de Santa Reyes ya tiene <c>carga_masiva_postura</c>
    /// habilitado, los roles «Santa Reyes Administrador» y «Santa Reyes Implementador» ya lo tienen en
    /// <c>role_permissions</c> (se lo dio el seed de la empresa), y esos roles ya tienen el GRUPO
    /// <c>carga_masiva</c> en <c>role_menus</c> (sobrevivió a la restricción porque el DELETE de aquella
    /// migración filtra por <c>m.route = '/migraciones-masivas'</c> y el grupo tiene <c>route</c> NULL).
    /// Falta solo el ÍTEM del menú: <b>1 fila en <c>company_menus</c> y 2 en <c>role_menus</c></b>.
    /// Verificado invocando <c>fn_menu_usuario</c> con los dos usuarios reales de la empresa, dentro
    /// de una transacción revertida: sin estas filas devuelve <c>ve_migraciones_masivas = false</c>;
    /// con ellas aparecen el ítem y su grupo padre.
    /// </para>
    ///
    /// <para>
    /// <b>El grupo padre no necesita fila propia:</b> <c>fn_menu_usuario</c> sube los ancestros sola
    /// (decisión D3 de <c>FnMenuUsuarioSuperAdmin</c>). Y <c>menu_permissions</c> está vacío para
    /// <c>carga_masiva</c> y <c>migraciones_masivas</c>: el ítem se ve con <c>role_menus</c> +
    /// <c>company_menus</c>, no hay nada que sembrar ahí.
    /// </para>
    ///
    /// <para>
    /// Todo se localiza por <c>companies.identifier</c> y <c>menus.key</c>, nunca por id ni por
    /// <c>route</c>: los ids difieren entre local y prod, y la ruta de este módulo ya cambió una vez
    /// (<c>ReorganizarMenuCargaMasiva</c>). Una migración que localice por algo que cambió inserta 0
    /// filas, queda registrada como aplicada y el menú sigue sin aparecer, sin un solo error.
    /// </para>
    /// </summary>
    public partial class HabilitarMigracionesMasivasSantaReyes : Migration
    {
        /// <summary>NIT de Santa Reyes en <c>companies.identifier</c>.</summary>
        private const string IdentifierSantaReyes = "901000001-1";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UP_SQL);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(DOWN_SQL);

        private const string UP_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- 1) company_menus: el item que falta. `fn_menu_usuario` exige `cm.is_enabled`,
--    asi que una fila apagada equivale a no tenerla: hay INSERT y ademas UPDATE.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order)
SELECT c.id, m.id, true, m.""order""
  FROM public.companies c
  CROSS JOIN public.menus m
 WHERE c.identifier = '" + IdentifierSantaReyes + @"'
   AND m.key = 'migraciones_masivas'
   AND NOT EXISTS (SELECT 1 FROM public.company_menus x
                    WHERE x.company_id = c.id AND x.menu_id = m.id);

UPDATE public.company_menus cm
   SET is_enabled = true
  FROM public.companies c, public.menus m
 WHERE cm.company_id = c.id
   AND cm.menu_id    = m.id
   AND c.identifier  = '" + IdentifierSantaReyes + @"'
   AND m.key = 'migraciones_masivas'
   AND cm.is_enabled IS DISTINCT FROM true;

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) role_menus: el item a los roles de Santa Reyes, localizados por su vinculo REAL en
--    role_companies (no por nombre) para que un rol nuevo de la empresa tampoco se escape.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rc.role_id, m.id
  FROM public.role_companies rc
  JOIN public.companies c ON c.id = rc.company_id
  CROSS JOIN public.menus m
 WHERE c.identifier = '" + IdentifierSantaReyes + @"'
   AND m.key = 'migraciones_masivas'
   AND NOT EXISTS (SELECT 1 FROM public.role_menus rm
                    WHERE rm.role_id = rc.role_id AND rm.menu_id = m.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) company_permissions: re-aseguro de POSTURA (fail-closed R1/R3 de CompanyPermissionCalculos:
--    sin la fila habilitada el permiso no viaja en el JWT aunque el rol lo tenga) y APAGADO de
--    ENGORDE.
--
--    Santa Reyes es postura comercial y no tiene UN SOLO lote de engorde: con el permiso de engorde
--    encendido, el paso 1 del modulo le ofreceria 4 tiles que no le corresponden, tres de los cuales
--    devuelven una lista de lotes vacia sin explicar por que. Se APAGA la fila de empresa y NO se
--    borra de role_permissions (R5: lo asignado que queda fuera se reporta como huerfano, no se
--    borra en silencio). Mismo criterio que se aplico a Demo.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT c.id, p.id, true
  FROM public.companies c
  CROSS JOIN public.permissions p
 WHERE c.identifier = '" + IdentifierSantaReyes + @"'
   AND p.key = 'carga_masiva_postura'
   AND NOT EXISTS (SELECT 1 FROM public.company_permissions x
                    WHERE x.company_id = c.id AND x.permission_id = p.id);

UPDATE public.company_permissions cp
   SET is_enabled = true
  FROM public.companies c, public.permissions p
 WHERE cp.company_id = c.id
   AND cp.permission_id = p.id
   AND c.identifier = '" + IdentifierSantaReyes + @"'
   AND p.key = 'carga_masiva_postura'
   AND cp.is_enabled IS DISTINCT FROM true;

UPDATE public.company_permissions cp
   SET is_enabled = false
  FROM public.companies c, public.permissions p
 WHERE cp.company_id = c.id
   AND cp.permission_id = p.id
   AND c.identifier = '" + IdentifierSantaReyes + @"'
   AND p.key = 'carga_masiva_pollo_engorde'
   AND cp.is_enabled IS DISTINCT FROM false;

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) role_permissions: re-aseguro de `carga_masiva_postura` en los roles de la empresa. El seed
--    original se lo dio por CROSS JOIN contra `permissions`; si un rol se creo despues, no lo tiene.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rc.role_id, p.id
  FROM public.role_companies rc
  JOIN public.companies c ON c.id = rc.company_id
  CROSS JOIN public.permissions p
 WHERE c.identifier = '" + IdentifierSantaReyes + @"'
   AND p.key = 'carga_masiva_postura'
   AND NOT EXISTS (SELECT 1 FROM public.role_permissions rp
                    WHERE rp.role_id = rc.role_id AND rp.permission_id = p.id);
";

        // Revertir = volver a esconderle el modulo a Santa Reyes. Se borran EXACTAMENTE las filas de
        // menu que el Up agrega (las de permisos NO se tocan: ya existian antes de esta migracion, y
        // borrarlas dejaria a la empresa peor que como estaba).
        private const string DOWN_SQL = @"
DELETE FROM public.company_menus cm
 USING public.companies c, public.menus m
 WHERE cm.company_id = c.id
   AND cm.menu_id    = m.id
   AND c.identifier  = '" + IdentifierSantaReyes + @"'
   AND m.key = 'migraciones_masivas';

DELETE FROM public.role_menus rm
 USING public.role_companies rc, public.companies c, public.menus m
 WHERE rm.role_id  = rc.role_id
   AND rc.company_id = c.id
   AND rm.menu_id  = m.id
   AND c.identifier = '" + IdentifierSantaReyes + @"'
   AND m.key = 'migraciones_masivas';
";
    }
}
