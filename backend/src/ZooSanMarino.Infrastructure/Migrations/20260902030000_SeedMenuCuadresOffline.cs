using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// H1 de <c>fase_de_desarrollo/pwa_cierre_huecos_plan.md</c>: el ítem de menú de la
    /// <b>bandeja de cuadre</b> de las capturas offline (<c>/cuadres-offline</c>).
    ///
    /// <para>
    /// 🔴 <b>Por qué existe.</b> El backend emite <c>requiere_cuadre</c> desde el 22-ago
    /// (<c>6f17d44</c>) y expone la bandeja (<c>GET /api/Sync/cuadres</c>), pero <b>no la llamaba
    /// nadie</b>: cero referencias en el front. El dispositivo del galponero borra esa operación de
    /// su cola —y hace bien, el día sí se guardó—, así que sin esta pantalla la divergencia de stock
    /// no la veía nadie. Un emisor sin lector promete un control que no ocurre.
    /// </para>
    ///
    /// <para>
    /// 🔗 <b>Depende de la pantalla</b>: el ítem apunta a <c>/cuadres-offline</c>, ruta que declara
    /// <c>app.config.ts</c> en este mismo commit. Migración y pantalla tienen que salir en el
    /// <b>mismo release</b>: desplegada sola, el ítem lleva a una ruta inexistente.
    /// </para>
    ///
    /// <para>
    /// <b>Dónde queda:</b> ítem <b>raíz</b> con <c>order = 6</c> — entre «Gestión de Inventario» (5)
    /// y «Movimientos» (7), que es donde lo busca quien va a corregir el faltante. No se cuelga de
    /// «Gestión de Inventario» porque ese nodo es una <b>hoja con ruta propia</b>; convertirlo en
    /// grupo cambiaría cómo lo dibuja el sidebar para todas las empresas.
    /// </para>
    ///
    /// <para>
    /// <b>A quién se le habilita.</b> Empresas: <b>todas las que ya tengan configurado su menú</b>
    /// —la PWA está desplegada para todas y una bandeja vacía es información correcta, no ruido—.
    /// Roles: los que <b>hoy ven <c>/gestion-inventario</c></b>, resuelto por DATOS y por ruta, nunca
    /// por nombre de rol ni de empresa (CLAUDE.md §🏢). El criterio no es arbitrario: la bandeja dice
    /// «cargá el ingreso en Gestión de inventario», así que quien la ve tiene que poder ir a hacerlo.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Hallazgo que esta migración NO resuelve:</b> <c>SyncController</c> gatea la bandeja sólo
    /// con <c>[Authorize]</c> + alcance de empresa (fail-closed del lado del servidor), sin permiso
    /// granular. O sea que <b>el menú es hoy la única puerta</b> a «marcar como revisada». Se deja
    /// así a propósito —resolver sólo marca visto, queda <c>cuadre_resuelto_por</c> como auditoría, y
    /// esconder el botón sin gatear el endpoint sería una mitigación de front—, pero si mañana se
    /// agrega un permiso granular, este seed es el lugar donde acotar los roles.
    /// </para>
    ///
    /// Migración DATA-ONLY (no cambia el modelo): Designer clonado de la migración inmediatamente
    /// anterior, <c>ZooSanMarinoContextModelSnapshot</c> intacto. Idempotente y re-ejecutable.
    /// </summary>
    public partial class SeedMenuCuadresOffline : Migration
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
-- 1) La fila de `menus`. Localizada SIEMPRE por `route` (los ids difieren local ↔ prod).
--
--    `menus.key` es NOT NULL y UNIQUE (uq_menus_key): el segundo NOT EXISTS no es decorativo —
--    evita que un entorno con la key ya tomada reviente el INSERT, y una migración que revienta al
--    arrancar mata la tarea ECS antes del primer log.
--
--    El icono es `clipboard-list` porque está en el ICON_MAP cerrado de
--    frontend/src/app/shared/services/menu.service.ts. Un nombre fuera de ese mapa dibuja el ítem
--    SIN icono, en silencio (le pasó a 'dna' en el ítem de guía genética de Ecuador).
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT 'Cuadres sin conexión',
       'clipboard-list',
       '/cuadres-offline',
       NULL,
       6, true, 'cuadres_offline', 0, false, now(), now()
WHERE NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.route = '/cuadres-offline')
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key   = 'cuadres_offline');

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) company_menus — todas las empresas que YA tienen su menú configurado.
--
--    🔴 El último EXISTS NO es redundante: fn_menu_usuario es FAIL-OPEN por empresa — una empresa
--    SIN ninguna fila en company_menus no se filtra y ve el catálogo completo. Insertarle UNA sola
--    fila la convertiría en filtrada y le dejaría el menú reducido a ese único ítem. Hoy las 5
--    empresas tienen filas; la guarda existe para la empresa #6 que nazca con el menú sin configurar
--    (sin la fila igual ve el ítem, justamente por el fail-open).
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT c.id, nuevo.id, true, 0, NULL
  FROM public.companies c
 CROSS JOIN (SELECT m.id FROM public.menus m WHERE m.route = '/cuadres-offline') AS nuevo
 WHERE NOT EXISTS (SELECT 1 FROM public.company_menus x
                    WHERE x.company_id = c.id AND x.menu_id = nuevo.id)
   AND EXISTS     (SELECT 1 FROM public.company_menus x WHERE x.company_id = c.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) role_menus — ANTI-LOCKOUT. Sin esto el ítem no se ve aunque company_menus lo habilite:
--    fn_menu_usuario interseca role_menus ∩ company_menus, y un rol CON filas en role_menus no cae
--    al fallback del catálogo completo.
--
--    Los roles se resuelven por DATOS: los que hoy ven /gestion-inventario, localizado por ruta.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rm.role_id, nuevo.id
  FROM public.role_menus rm
  JOIN public.menus origen ON origen.id = rm.menu_id AND origen.route = '/gestion-inventario'
 CROSS JOIN (SELECT m.id FROM public.menus m WHERE m.route = '/cuadres-offline') AS nuevo
 WHERE NOT EXISTS (SELECT 1 FROM public.role_menus x
                    WHERE x.role_id = rm.role_id AND x.menu_id = nuevo.id);
";

        // El Down borra lo que el Up creó, en orden inverso a las FK. Es reversible de verdad: el
        // ítem es nuevo en esta migración, así que no hay historia previa que preservar.
        private const string DOWN_SQL = @"
DELETE FROM public.role_menus
 WHERE menu_id IN (SELECT m.id FROM public.menus m WHERE m.route = '/cuadres-offline');

DELETE FROM public.company_menus
 WHERE menu_id IN (SELECT m.id FROM public.menus m WHERE m.route = '/cuadres-offline');

DELETE FROM public.menus
 WHERE route = '/cuadres-offline';
";
    }
}
