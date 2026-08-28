using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Deja la empresa <b>Demo</b> lista para que el equipo de costos de SanMarino practique la
    /// carga masiva y contraste los reportes <b>antes</b> de operar sobre la empresa real.
    ///
    /// <para>
    /// 🔴 <b>El problema no era el menú: la cadena estaba cortada en CUATRO niveles.</b> Medido
    /// ejecutando <c>fn_menu_usuario</c> con el usuario real <c>admin.demo@zootecnico.com</c> sobre
    /// la empresa 4, el grupo <b>Carga Masiva</b> se pintaba <b>VACÍO</b> y <b>Reportes</b> traía
    /// sólo 2 hijos. Arreglar un solo nivel no habría movido la pantalla.
    /// </para>
    ///
    /// <para>
    /// <b>El dato que explica el síntoma:</b> los dos roles de Demo <b>ya tenían</b> en
    /// <c>role_menus</c> el grupo <c>carga_masiva</c> y el <c>reporte_diario_costos_postura</c>. La
    /// configuración de ROL estaba lista desde siempre; lo que nunca se habilitó fue la
    /// <b>EMPRESA</b>. Por eso el síntoma era un grupo de menú vacío y no un 403.
    /// </para>
    ///
    /// <para><b>Los cinco pasos y por qué ninguno sobra:</b></para>
    /// <list type="number">
    ///   <item><description>
    ///     <b>Flags de <c>companies</c>.</b> El grave es
    ///     <c>reportes_alimento_desde_inventario_unificado</c>: SanMarino lo tiene en <b>true</b>
    ///     desde <c>20260814000000_ReportesUnificadoSanmarino</c> y Demo en false, así que el
    ///     Contable y el Técnico de Demo leían <c>farm_inventory_movements</c> (<b>2 filas</b>) en
    ///     vez de <c>inventario_gestion_movimiento</c> (<b>12</b>). Se practicaría contra otra
    ///     fuente y otra fórmula que las de producción — justo el error que la práctica viene a
    ///     evitar. Se alinean los cuatro flags que cambian captura o reportes, en las dos
    ///     direcciones: los que a Demo le <i>faltan</i> y los que le <i>sobran</i>.
    ///   </description></item>
    ///   <item><description>
    ///     <c>company_menus</c> — <c>migraciones_masivas</c>: sin esta fila el grupo
    ///     <b>Carga Masiva</b> (que Demo YA tenía habilitado) se renderiza sin un solo hijo. No hay
    ///     dónde hacer clic.
    ///   </description></item>
    ///   <item><description>
    ///     <c>company_menus</c> — <c>reporte_diario_costos_postura</c> y
    ///     <c>reporte_tecnico_semanal</c>: los dos reportes que SanMarino tiene y Demo no.
    ///     <c>fn_menu_usuario</c> exige <c>company_menus.is_enabled</c>, así que el rol podía
    ///     tenerlos asignados y no verlos igual.
    ///   </description></item>
    ///   <item><description>
    ///     <c>company_permissions</c> — <b>FAIL-CLOSED</b> (<c>CompanyPermissionCalculos</c>, reglas
    ///     R1/R3): sin la fila habilitada el permiso <b>no viaja en el JWT</b> aunque el rol lo
    ///     tenga, y ni siquiera se ofrece en el tab Permisos del modal de rol. Demo no tenía
    ///     <c>carga_masiva_postura</c> en su catálogo ⇒ ningún admin de Demo podía otorgarlo.
    ///   </description></item>
    ///   <item><description>
    ///     <c>role_permissions</c> + <c>role_menus</c> — cierran la cadena en los dos roles de Demo.
    ///   </description></item>
    /// </list>
    ///
    /// <para>
    /// <b>El contrasentido que se corrige:</b> <c>Admin Demo</c> tenía
    /// <c>carga_masiva_pollo_engorde</c> y <b>no</b> <c>carga_masiva_postura</c> — el permiso justo
    /// al revés de lo que Demo necesita. Demo no tiene un solo lote de engorde: toda su operación es
    /// levante + producción. Se apaga en <c>company_permissions</c> (espejo exacto de lo que hace
    /// SanMarino) y <b>NO</b> se borra de <c>role_permissions</c>: la regla <b>R5 no destructiva</b>
    /// del propio código dice que lo ya asignado que queda fuera se <i>reporta</i> como huérfano en
    /// la UI para que un admin decida, no se borra en silencio. En runtime R3 lo filtra igual.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Los permisos viajan en la sesión cifrada</b>: quien tenga sesión abierta al momento del
    /// deploy tiene que <b>re-loguearse</b> para que las keys nuevas aparezcan en su token.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Sobre apagar <c>maneja_codigos_erp_avicola</c>.</b> Los 17 lotes base que Demo tiene hoy
    /// llevan <c>codigo_erp</c> cargado (y 7 de levante + 1 de producción llevan <c>lote_erp</c>).
    /// Apagar el flag <b>no borra ese dato</b>: sólo deja de mostrar el campo en los formularios de
    /// granja / núcleo / galpón / lote — el flag se lee en el front
    /// (<c>active-company-config.service.ts</c>) y no toca la carga masiva, cuyas plantillas no
    /// tienen columna ERP. En la secuencia prevista el punto es inerte, porque el script de limpieza
    /// borra esos lotes; si se aplica la migración <b>sin</b> la limpieza, el único efecto es un
    /// campo oculto sobre registros viejos.
    /// </para>
    ///
    /// <para>
    /// <b>Localización.</b> La empresa por <c>identifier</c> (<c>'1111738751'</c>) y nunca por
    /// <c>name</c>: el nombre es texto libre y un espacio de más dejaría la migración sin efecto
    /// <b>y sin error</b>. Los menús por <c>menus.key</c> y los permisos por <c>permissions.key</c>,
    /// jamás por id fijo — los ids difieren local ↔ prod. Los roles por su vínculo real
    /// <c>role_companies</c> con Demo, no por nombre, así que un rol nuevo de Demo tampoco se
    /// escapa.
    /// </para>
    ///
    /// <para>
    /// ⛔ <b>Lo que NO hace, a propósito.</b> No toca datos operativos: la limpieza de Demo pedida
    /// junto con esta habilitación va como script de una sola vez
    /// (<c>backend/sql/migracion_limpieza_demo_practica_costos.sql</c>) y NO por migración — una
    /// migración que borra datos se re-ejecutaría en cualquier entorno levantado de cero y no hay
    /// <c>Down()</c> que la deshaga. Tampoco enciende <c>mobile_access</c> ni los otros 22 menús que
    /// SanMarino tiene y Demo no (ItalJira, Mapas, Vacunación, Implementación, Empresas, Geografía,
    /// db_studio…): quedan fuera por decisión de alcance.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/demo_lista_practica_carga_masiva_costos_plan.md</c>.
    /// Migración DATA-ONLY: Designer clonado, <c>ZooSanMarinoContextModelSnapshot</c> intacto.
    /// Idempotente (<c>WHERE NOT EXISTS</c> en los puentes, <c>IS DISTINCT FROM</c> en los UPDATE
    /// para no reescribir filas ya correctas). <c>Down()</c> simétrico.
    /// </summary>
    public partial class DemoListaParaPracticaCargaMasivaCostos : Migration
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

        /// <summary>NIT de la empresa Demo. Se localiza por acá y jamas por `name`.</summary>
        private const string IdentifierDemo = "1111738751";

        private const string UP_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- 1) FLAGS DE COMPORTAMIENTO: que Demo se comporte como Agroavicola Sanmarino.
--    Se alinean en las DOS direcciones. `IS DISTINCT FROM` => no reescribe lo ya correcto.
--
--    ON  reportes_alimento_desde_inventario_unificado : el Contable y el Tecnico tienen que leer
--        `inventario_gestion_movimiento` (el modulo unificado), como en Sanmarino. Con el flag
--        apagado leian `farm_inventory_movements`, que en Demo tiene 2 filas.
--    ON  captura_huevos_en_levante  : Sanmarino captura huevos en levante desde la semana 14; sin
--        el flag el formulario no muestra el bloque y el archivo de practica no tendria donde
--        cargarlos.
--    OFF maneja_codigos_erp_avicola : Demo mostraba campos de codigo ERP que en Sanmarino NO
--        existen => se practicaria con una columna de mas.
--    OFF permite_traslado_aves_cross_etapa : Demo permitia un traslado que en Sanmarino esta
--        prohibido => se aprenderia un flujo invalido.
--
--    NO se toca `mobile_access` (fuera de alcance: es acceso a la app movil).
-- ─────────────────────────────────────────────────────────────────────────────
UPDATE public.companies
   SET reportes_alimento_desde_inventario_unificado = true
 WHERE identifier = '" + IdentifierDemo + @"'
   AND reportes_alimento_desde_inventario_unificado IS DISTINCT FROM true;

UPDATE public.companies
   SET captura_huevos_en_levante = true
 WHERE identifier = '" + IdentifierDemo + @"'
   AND captura_huevos_en_levante IS DISTINCT FROM true;

UPDATE public.companies
   SET maneja_codigos_erp_avicola = false
 WHERE identifier = '" + IdentifierDemo + @"'
   AND maneja_codigos_erp_avicola IS DISTINCT FROM false;

UPDATE public.companies
   SET permite_traslado_aves_cross_etapa = false
 WHERE identifier = '" + IdentifierDemo + @"'
   AND permite_traslado_aves_cross_etapa IS DISTINCT FROM false;

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) company_menus: los 3 items que faltaban, localizados por `menus.key`.
--    `fn_menu_usuario` exige `cm.is_enabled`, asi que una fila apagada equivale a no tenerla.
--    El grupo padre `carga_masiva` y el grupo `reporte` YA estaban habilitados en Demo: sin el
--    hijo `migraciones_masivas` el grupo se renderizaba vacio.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order)
SELECT c.id, m.id, true, m.""order""
  FROM public.companies c
  CROSS JOIN public.menus m
 WHERE c.identifier = '" + IdentifierDemo + @"'
   AND m.key IN ('migraciones_masivas', 'reporte_diario_costos_postura', 'reporte_tecnico_semanal')
   AND NOT EXISTS (SELECT 1 FROM public.company_menus x
                    WHERE x.company_id = c.id AND x.menu_id = m.id);

-- Si la fila ya existia apagada, encenderla (idempotente y sin tocar las que ya estaban bien).
UPDATE public.company_menus cm
   SET is_enabled = true
  FROM public.companies c, public.menus m
 WHERE cm.company_id = c.id
   AND cm.menu_id    = m.id
   AND c.identifier  = '" + IdentifierDemo + @"'
   AND m.key IN ('migraciones_masivas', 'reporte_diario_costos_postura', 'reporte_tecnico_semanal')
   AND cm.is_enabled IS DISTINCT FROM true;

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) company_permissions: FAIL-CLOSED (CompanyPermissionCalculos R1/R3). Sin la fila habilitada el
--    permiso NO viaja en el JWT aunque el rol lo tenga, y no se ofrece en el modal de rol.
--    Espejo exacto de Agroavicola Sanmarino: postura ON, pollo engorde OFF.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT c.id, p.id, true
  FROM public.companies c
  CROSS JOIN public.permissions p
 WHERE c.identifier = '" + IdentifierDemo + @"'
   AND p.key = 'carga_masiva_postura'
   AND NOT EXISTS (SELECT 1 FROM public.company_permissions x
                    WHERE x.company_id = c.id AND x.permission_id = p.id);

UPDATE public.company_permissions cp
   SET is_enabled = true
  FROM public.companies c, public.permissions p
 WHERE cp.company_id = c.id
   AND cp.permission_id = p.id
   AND c.identifier = '" + IdentifierDemo + @"'
   AND p.key = 'carga_masiva_postura'
   AND cp.is_enabled IS DISTINCT FROM true;

-- Demo no tiene UN SOLO lote de engorde: el permiso de engorde solo abre tiles que no le
-- corresponden. Se APAGA aca (R3 lo filtra en el login) y NO se borra de role_permissions:
-- R5 dice que lo ya asignado que queda fuera se reporta como huerfano, no se borra en silencio.
UPDATE public.company_permissions cp
   SET is_enabled = false
  FROM public.companies c, public.permissions p
 WHERE cp.company_id = c.id
   AND cp.permission_id = p.id
   AND c.identifier = '" + IdentifierDemo + @"'
   AND p.key = 'carga_masiva_pollo_engorde'
   AND cp.is_enabled IS DISTINCT FROM false;

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) role_permissions: `carga_masiva_postura` a TODOS los roles de Demo, localizados por su
--    vinculo real en role_companies (no por nombre) para que un rol nuevo tampoco se escape.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT DISTINCT rc.role_id, p.id
  FROM public.role_companies rc
  JOIN public.companies c ON c.id = rc.company_id
  CROSS JOIN public.permissions p
 WHERE c.identifier = '" + IdentifierDemo + @"'
   AND p.key = 'carga_masiva_postura'
   AND NOT EXISTS (SELECT 1 FROM public.role_permissions rp
                    WHERE rp.role_id = rc.role_id AND rp.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 5) role_menus: el item `migraciones_masivas` a los roles de Demo. Los dos roles YA tenian el
--    grupo `carga_masiva` y el `reporte_diario_costos_postura` — faltaba el hijo que se clickea.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rc.role_id, m.id
  FROM public.role_companies rc
  JOIN public.companies c ON c.id = rc.company_id
  CROSS JOIN public.menus m
 WHERE c.identifier = '" + IdentifierDemo + @"'
   AND m.key IN ('migraciones_masivas', 'reporte_diario_costos_postura', 'reporte_tecnico_semanal')
   AND NOT EXISTS (SELECT 1 FROM public.role_menus rm
                    WHERE rm.role_id = rc.role_id AND rm.menu_id = m.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 6) El catalogo de alimento de Demo tiene que ofrecer LO MISMO que el de Sanmarino.
--
--    La plantilla de Seguimiento (GenerarPlantillaSeguimientoAsync) arma la hoja `Referencias` con
--    los alimentos de la empresa y ata las columnas `Alimento 1 H / 2 H / 1 M / 2 M` a un
--    DESPLEGABLE sobre ese rango: el catalogo es, literalmente, lo que el equipo puede escribir en
--    el archivo.
--
--    Medido: los catalogos coinciden en los 61 alimentos, salvo UNO que solo existe en Demo —
--    'Alimento ERP' (codigo 4000), creado cuando Demo tenia `maneja_codigos_erp_avicola` encendido.
--    Si queda activo, el equipo puede elegirlo en la practica y no encontrarlo al pasar a Sanmarino.
--
--    Se DESACTIVA, no se borra: 8 movimientos y 4 filas de stock lo referencian (los borra el script
--    de limpieza, pero la migracion no puede asumir que ya corrio). `activo = false` lo saca de los
--    selectores sin tocar una sola fila de inventario.
-- ─────────────────────────────────────────────────────────────────────────────
UPDATE public.item_inventario_ecuador i
   SET activo = false, updated_at = now()
  FROM public.companies c
 WHERE i.company_id = c.id
   AND c.identifier = '" + IdentifierDemo + @"'
   AND i.codigo = '4000'
   AND i.activo IS DISTINCT FROM false
   AND NOT EXISTS (SELECT 1 FROM public.item_inventario_ecuador j
                    JOIN public.companies sm ON sm.id = j.company_id
                   WHERE sm.identifier = '100063' AND j.codigo = i.codigo);
";

        private const string DOWN_SQL = @"
-- Simetrico del Up(): deja Demo exactamente como estaba antes de esta migracion.
UPDATE public.item_inventario_ecuador i
   SET activo = true, updated_at = now()
  FROM public.companies c
 WHERE i.company_id = c.id
   AND c.identifier = '" + IdentifierDemo + @"'
   AND i.codigo = '4000'
   AND i.activo IS DISTINCT FROM true;

--
-- OJO con `reporte_diario_costos_postura`: el Up() lo inserta en role_menus por robustez (para que
-- un rol de Demo creado despues tampoco quede afuera), pero los DOS roles que Demo tiene hoy YA lo
-- tenian asignado — el Up() es un no-op para ellos. Por eso el Down() NO lo borra de role_menus:
-- borrarlo destruiria una asignacion PREVIA a esta migracion, que es justo lo que un Down() no debe
-- hacer. De company_menus si se va, que es lo unico que esta migracion agrego para ese item.
DELETE FROM public.role_menus rm
 USING public.companies c, public.role_companies rc, public.menus m
 WHERE c.identifier   = '" + IdentifierDemo + @"'
   AND rc.company_id  = c.id
   AND rm.role_id     = rc.role_id
   AND rm.menu_id     = m.id
   AND m.key IN ('migraciones_masivas', 'reporte_tecnico_semanal');

DELETE FROM public.role_permissions rp
 USING public.companies c, public.role_companies rc, public.permissions p
 WHERE c.identifier    = '" + IdentifierDemo + @"'
   AND rc.company_id   = c.id
   AND rp.role_id      = rc.role_id
   AND rp.permission_id = p.id
   AND p.key = 'carga_masiva_postura';

UPDATE public.company_permissions cp
   SET is_enabled = true
  FROM public.companies c, public.permissions p
 WHERE cp.company_id = c.id
   AND cp.permission_id = p.id
   AND c.identifier = '" + IdentifierDemo + @"'
   AND p.key = 'carga_masiva_pollo_engorde'
   AND cp.is_enabled IS DISTINCT FROM true;

DELETE FROM public.company_permissions cp
 USING public.companies c, public.permissions p
 WHERE cp.company_id = c.id
   AND cp.permission_id = p.id
   AND c.identifier = '" + IdentifierDemo + @"'
   AND p.key = 'carga_masiva_postura';

DELETE FROM public.company_menus cm
 USING public.companies c, public.menus m
 WHERE cm.company_id = c.id
   AND cm.menu_id    = m.id
   AND c.identifier  = '" + IdentifierDemo + @"'
   AND m.key IN ('migraciones_masivas', 'reporte_diario_costos_postura', 'reporte_tecnico_semanal');

UPDATE public.companies
   SET reportes_alimento_desde_inventario_unificado = false,
       captura_huevos_en_levante                    = false,
       maneja_codigos_erp_avicola                   = true,
       permite_traslado_aves_cross_etapa            = true
 WHERE identifier = '" + IdentifierDemo + @"';
";
    }
}
