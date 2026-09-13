using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Siembra de los módulos de permisos: catálogo, clasificación M:N, módulos de cada empresa y su
    /// efecto sobre <c>company_permissions</c>. Plan: <c>fase_de_desarrollo/modulos_permisos_por_empresa_plan.md</c>.
    ///
    /// <para>
    /// 🔴 <b>Por qué.</b> <c>company_permissions</c> se configuraba permiso por permiso y nadie lo mantuvo:
    /// medido el 12-sep-2026 sobre la copia de producción, <b>Santa Reyes y Demo —sólo menús de postura—
    /// tenían 16 y 14 permisos de engorde prendidos</b> (Santa Reyes incluso <c>sincronizacion_panama.*</c>),
    /// y por eso el modal de roles ofrecía «todo, sin control».
    /// </para>
    ///
    /// <para>
    /// <b>Decisiones del usuario (12-sep-2026):</b> D1 M:N · D2 los módulos de cada empresa salen de sus
    /// menús reales · D5 Vacunación se apaga en Demo/Santa Reyes (ninguna empresa tiene sus menús) ·
    /// D6 se aplican las pérdidas y se BLOQUEAN las ganancias · D7 Integración Panamá SOLO a
    /// <c>ItalcolPanama</c> aunque Ecuador tenga el menú.
    /// </para>
    ///
    /// <para>
    /// <b>Impacto medido (antes de escribir esto):</b> pierden permiso sólo usuarios de empresas de postura, y
    /// sólo en keys de pantallas que su empresa no tiene (p. ej. Sanmarino <c>abrir_lote</c> 15 usuarios,
    /// usado únicamente en engorde). Ganancias: 0 — la regla S1 deja APAGADO todo permiso sin fila que ya
    /// esté asignado a un rol de la empresa (8 usuarios habrían recuperado <c>seguimiento_*.validar</c>).
    /// Verificación: <c>backend/sql/verificar_modulos_permisos_impacto.sql</c> antes/después.
    /// </para>
    ///
    /// <para>
    /// <b>Espejo de <c>PermisoModuloCalculos.ResolverSiembra</c></b> (sus tests son el contrato). Localiza por
    /// <c>permission_modules.key</c>, <c>permissions.key</c>, <c>menus.route</c> y <c>companies.name</c>,
    /// nunca por id. Idempotente: los módulos de empresa sólo se siembran en empresas SIN configuración, y la
    /// materialización sólo apaga lo que ningún módulo prendido cubre e inserta lo que falta.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Los permisos viajan en la sesión:</b> quien tenga una abierta conserva los claims viejos hasta
    /// volver a entrar. El menú nuevo también llega recién con el re-login.
    /// </para>
    ///
    /// Migración DATA-ONLY: Designer clonado del ModelSnapshot vigente (el de <c>AddModulosDePermisos</c>).
    /// </summary>
    public partial class SeedModulosDePermisos : Migration
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
-- 1) Catálogo de módulos.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.permission_modules (key, nombre, descripcion, orden)
SELECT v.key, v.nombre, v.descripcion, v.orden
FROM (VALUES
    ('postura',            'Postura',               'Levante y producción: seguimiento diario y su validación, corrección de lotes, carga masiva y guía genética.', 10),
    ('pollo_engorde',      'Pollo Engorde',         'Lotes de engorde y reproductora, seguimiento y su validación, ventas y despachos, liquidación, cuadre de alimento y carga masiva.', 20),
    ('integracion_panama', 'Integración Panamá',    'Sincronización con ZooPanamaPollo (guía genética, lotes, seguimiento y reproductora).', 30),
    ('inventario',         'Gestión de Inventario', 'Editar, eliminar y fechar hacia atrás registros de inventario.', 40),
    ('vacunacion',         'Vacunación',            'Plantillas del plan, cronograma, registro de aplicación y reportes de cumplimiento.', 50),
    ('tickets',            'Tickets e ItalJira',    'Mesa de ayuda: crear, gestionar y administrar casos; panel de indicadores.', 60),
    ('administracion',     'Administración',        'Usuarios, sesiones activas, roles y catálogo de menús.', 70)
) AS v(key, nombre, descripcion, orden)
WHERE NOT EXISTS (SELECT 1 FROM public.permission_modules m WHERE m.key = v.key);

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Clasificación M:N (plan §2). Key ausente en el entorno ⇒ el JOIN la saltea.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.permission_module_permissions (module_id, permission_id)
SELECT m.id, p.id
FROM (VALUES
    ('postura', 'carga_masiva_postura'),
    ('postura', 'seguimiento_levante.validar'),
    ('postura', 'seguimiento_levante.desvalidar'),
    ('postura', 'seguimiento_produccion.validar'),
    ('postura', 'seguimiento_produccion.desvalidar'),
    ('postura', 'lote.corregir_aves'),
    ('postura', 'lote.corregir_fecha_encaset'),
    ('postura', 'guia_genetica.gestionar'),
    ('postura', 'registros.fecha_retroactiva'),
    ('pollo_engorde', 'carga_masiva_pollo_engorde'),
    ('pollo_engorde', 'confirmar_despacho'),
    ('pollo_engorde', 'abrir_lote'),
    ('pollo_engorde', 'liquidar_lote'),
    ('pollo_engorde', 'liquidacion.aplicar_correccion'),
    ('pollo_engorde', 'cuadrar_ingresos_traslados_seguimiento'),
    ('pollo_engorde', 'lote_base_pollo_engorde.ver'),
    ('pollo_engorde', 'lote_base_pollo_engorde.crear'),
    ('pollo_engorde', 'lote_base_pollo_engorde.editar'),
    ('pollo_engorde', 'lote_base_pollo_engorde.eliminar'),
    ('pollo_engorde', 'lote_reproductora_engorde.editar'),
    ('pollo_engorde', 'lote_reproductora_engorde.eliminar'),
    ('pollo_engorde', 'movimientos_pollo_engorde.corregir_ventas'),
    ('pollo_engorde', 'movimientos_pollo_engorde.descargar_excel'),
    ('pollo_engorde', 'movimientos_pollo_engorde.organizar_peso'),
    ('pollo_engorde', 'movimientos_pollo_engorde.validar_ventas'),
    ('pollo_engorde', 'movimientos_pollo_engorde.vender_lotes_cerrados'),
    ('pollo_engorde', 'seguimiento_engorde.validar'),
    ('pollo_engorde', 'seguimiento_engorde.desvalidar'),
    ('pollo_engorde', 'seguimiento_reproductora_engorde.confirmar'),
    ('pollo_engorde', 'seguimiento_reproductora_engorde.eliminar'),
    ('pollo_engorde', 'lote.corregir_aves'),
    ('pollo_engorde', 'lote.corregir_fecha_encaset'),
    ('pollo_engorde', 'guia_genetica.gestionar'),
    ('pollo_engorde', 'editar_registro'),
    ('pollo_engorde', 'eliminar_registro'),
    ('pollo_engorde', 'registros.fecha_retroactiva'),
    ('integracion_panama', 'sincronizacion_panama.ver'),
    ('integracion_panama', 'sincronizacion_panama.ejecutar'),
    ('inventario', 'editar_registro'),
    ('inventario', 'eliminar_registro'),
    ('inventario', 'registros.fecha_retroactiva'),
    ('vacunacion', 'vacunacion.cronograma.ver'),
    ('vacunacion', 'vacunacion.cronograma.administrar'),
    ('vacunacion', 'vacunacion.plantillas.ver'),
    ('vacunacion', 'vacunacion.plantillas.administrar'),
    ('vacunacion', 'vacunacion.registro.aplicar'),
    ('vacunacion', 'vacunacion.reportes.ver'),
    ('tickets', 'tickets.crear'),
    ('tickets', 'tickets.gestionar'),
    ('tickets', 'tickets.admin'),
    ('tickets', 'tickets.indicadores'),
    ('administracion', 'usuarios.gestionar'),
    ('administracion', 'usuarios.revocar_sesion'),
    ('administracion', 'roles.gestionar'),
    ('administracion', 'menus.gestionar')
) AS v(modulo, permiso)
JOIN public.permission_modules m ON m.key = v.modulo
JOIN public.permissions p        ON p.key = v.permiso
WHERE NOT EXISTS (
    SELECT 1 FROM public.permission_module_permissions x
    WHERE x.module_id = m.id AND x.permission_id = p.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) Módulos de cada empresa (D2) — SÓLO empresas sin configuración de módulos, para no pisar lo
--    que un admin ya haya decidido. Una fila por módulo (prendido o apagado): queda explícito.
--    · Empresa con company_menus ⇒ módulo prendido si tiene habilitada alguna pantalla del módulo.
--    · Empresa SIN company_menus (fail-open de fn_menu_usuario) ⇒ todos prendidos: hoy ve todo.
--    · D7: integracion_panama sólo a ItalcolPanama, sin importar el menú.
-- ─────────────────────────────────────────────────────────────────────────────
WITH mod_menu(modulo, route_like) AS (VALUES
    ('postura', '/config/lote-management'),
    ('postura', '/lote-reproductora'),
    ('postura', '/daily-log/seguimiento'),
    ('postura', '/daily-log/produccion'),
    ('postura', '/daily-log/seguimiento-diario-lote-reproductora'),
    ('postura', '/traslados-huevos/lista'),
    ('postura', '/reportes-tecnicos'),
    ('postura', '/reporte-tecnico-produccion'),
    ('postura', '/reporte-tecnico-semanal'),
    ('postura', '/reporte-diario-costos-postura'),
    ('postura', '/config/guia-genetica'),
    ('postura', '/config/guia-genetica-santa-reyes'),
    ('pollo_engorde', '/config/lote-engorde'),
    ('pollo_engorde', '/config/lote-reproductora-ave-engorde'),
    ('pollo_engorde', '/daily-log/aves-engorde'),
    ('pollo_engorde', '/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde'),
    ('pollo_engorde', '/movimiento-pollo-engorde/lista'),
    ('pollo_engorde', '/indicador-ecuador'),
    ('pollo_engorde', '/informe-semanal-engorde'),
    ('pollo_engorde', '/reporte-diario-costos-engorde'),
    ('pollo_engorde', '/config/guia-genetica-ecuador'),
    ('inventario', '/gestion-inventario'),
    ('inventario', '/gestion-inventario/historial'),
    ('inventario', '/inventario-gastos'),
    ('vacunacion', '/vacunacion/%'),
    ('tickets', '/tickets%'),
    ('tickets', '/italjira/%'),
    ('tickets', '/gerencia/panel'),
    ('administracion', '/config/users'),
    ('administracion', '/config/role-management')
),
empresas AS (
    SELECT c.id, c.name,
           EXISTS (SELECT 1 FROM public.company_menus cm WHERE cm.company_id = c.id) AS tiene_menus
      FROM public.companies c
     WHERE NOT EXISTS (SELECT 1 FROM public.company_permission_modules x WHERE x.company_id = c.id)
),
por_menu AS (
    SELECT DISTINCT cm.company_id, mm.modulo
      FROM public.company_menus cm
      JOIN public.menus mn   ON mn.id = cm.menu_id AND cm.is_enabled
      JOIN mod_menu mm       ON mn.route LIKE mm.route_like
)
INSERT INTO public.company_permission_modules (company_id, module_id, is_enabled)
SELECT e.id, m.id,
       CASE
           WHEN m.key = 'integracion_panama' THEN e.name = 'ItalcolPanama'
           WHEN NOT e.tiene_menus           THEN true
           ELSE EXISTS (SELECT 1 FROM por_menu pm WHERE pm.company_id = e.id AND pm.modulo = m.key)
       END
  FROM empresas e
 CROSS JOIN public.permission_modules m
 WHERE m.key IN ('postura','pollo_engorde','integracion_panama','inventario','vacunacion','tickets','administracion');

-- ─────────────────────────────────────────────────────────────────────────────
-- 4a) Materialización S1 — apagar los permisos CLASIFICADOS que ningún módulo prendido de la
--     empresa cubre. Sin clasificar ⇒ intactos. Sólo empresas con configuración de módulos.
-- ─────────────────────────────────────────────────────────────────────────────
WITH cubiertos AS (
    SELECT DISTINCT cpm.company_id, pmp.permission_id
      FROM public.company_permission_modules cpm
      JOIN public.permission_module_permissions pmp ON pmp.module_id = cpm.module_id
     WHERE cpm.is_enabled
)
UPDATE public.company_permissions cp
   SET is_enabled = false
 WHERE cp.is_enabled
   AND EXISTS (SELECT 1 FROM public.company_permission_modules x WHERE x.company_id = cp.company_id)
   AND EXISTS (SELECT 1 FROM public.permission_module_permissions c WHERE c.permission_id = cp.permission_id)
   AND NOT EXISTS (SELECT 1 FROM cubiertos k
                    WHERE k.company_id = cp.company_id AND k.permission_id = cp.permission_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4b) Materialización S1 — insertar los cubiertos que no tienen fila. Prendido SÓLO si ningún rol de
--     la empresa lo tiene asignado: si lo tiene, prenderlo resucitaría esa asignación huérfana en el
--     login (D6: nadie gana nada en silencio). La empresa llega al rol por role_companies O por
--     user_roles (ambos caminos están poblados — mismo criterio que CompanyPermissionService).
--     Lo que ya tiene fila se respeta tal cual (un apagado explícito es ajuste fino).
-- ─────────────────────────────────────────────────────────────────────────────
WITH cubiertos AS (
    SELECT DISTINCT cpm.company_id, pmp.permission_id
      FROM public.company_permission_modules cpm
      JOIN public.permission_module_permissions pmp ON pmp.module_id = cpm.module_id
     WHERE cpm.is_enabled
),
roles_empresa AS (
    SELECT DISTINCT x.company_id, rp.permission_id
      FROM (SELECT role_id, company_id FROM public.role_companies
            UNION
            SELECT role_id, company_id FROM public.user_roles) x
      JOIN public.role_permissions rp ON rp.role_id = x.role_id
)
INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
SELECT k.company_id, k.permission_id,
       NOT EXISTS (SELECT 1 FROM roles_empresa r
                    WHERE r.company_id = k.company_id AND r.permission_id = k.permission_id)
  FROM cubiertos k
 WHERE NOT EXISTS (SELECT 1 FROM public.company_permissions cp
                    WHERE cp.company_id = k.company_id AND cp.permission_id = k.permission_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 5) Menú «Módulos y permisos» bajo Configuración, junto a Roles. Misma audiencia que «Empresas»
--    (/config/companies): hereda su padre, sus company_menus y sus role_menus. Localizado por route;
--    `menus.key` es UNIQUE, de ahí el segundo NOT EXISTS. Icono `key`: está en el ICON_MAP cerrado de
--    frontend/src/app/shared/services/menu.service.ts.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT 'Módulos y permisos', 'key', '/config/permission-modules', padre.parent_id, 3, true, 'permission_modules', 0, false, now(), now()
  FROM (SELECT m.parent_id FROM public.menus m WHERE m.route = '/config/companies' LIMIT 1) AS padre
 WHERE NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.route = '/config/permission-modules')
   AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key   = 'permission_modules');

INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
-- NULL::integer, no NULL a secas: dentro de un SELECT DISTINCT Postgres infiere `text` y el INSERT
-- revienta con 42804 contra parent_menu_id (lo atrapó la prueba en transacción sobre la copia de prod).
SELECT DISTINCT cm.company_id, nuevo.id, true, 0, NULL::integer
  FROM public.company_menus cm
  JOIN public.menus origen ON origen.id = cm.menu_id AND origen.route = '/config/companies' AND cm.is_enabled
 CROSS JOIN (SELECT m.id FROM public.menus m WHERE m.route = '/config/permission-modules') AS nuevo
 WHERE NOT EXISTS (SELECT 1 FROM public.company_menus x
                    WHERE x.company_id = cm.company_id AND x.menu_id = nuevo.id);

INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rm.role_id, nuevo.id
  FROM public.role_menus rm
  JOIN public.menus origen ON origen.id = rm.menu_id AND origen.route = '/config/companies'
 CROSS JOIN (SELECT m.id FROM public.menus m WHERE m.route = '/config/permission-modules') AS nuevo
 WHERE NOT EXISTS (SELECT 1 FROM public.role_menus x
                    WHERE x.role_id = rm.role_id AND x.menu_id = nuevo.id);
";

        // Borra lo que el Up creó en las tablas nuevas y el menú. ⚠️ NO revierte la materialización de
        // company_permissions (pasos 4a/4b): no hay registro de su estado previo. Para volver atrás en los
        // permisos de una empresa se prenden desde Empresas → 🔑 (el gate de módulos deja de aplicar al no
        // haber filas en company_permission_modules).
        private const string DOWN_SQL = @"
DELETE FROM public.role_menus
 WHERE menu_id IN (SELECT m.id FROM public.menus m WHERE m.route = '/config/permission-modules');
DELETE FROM public.company_menus
 WHERE menu_id IN (SELECT m.id FROM public.menus m WHERE m.route = '/config/permission-modules');
DELETE FROM public.menus WHERE route = '/config/permission-modules';

DELETE FROM public.company_permission_modules;
DELETE FROM public.permission_module_permissions;
DELETE FROM public.permission_modules
 WHERE key IN ('postura','pollo_engorde','integracion_panama','inventario','vacunacion','tickets','administracion');
";
    }
}
