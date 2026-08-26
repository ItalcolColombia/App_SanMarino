using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// F4 de <c>fase_de_desarrollo/guia_genetica_tres_modulos_plan.md</c>: los <b>TRES</b> ítems de
    /// menú de guía genética, uno por modelo de datos. No se unifican las tablas — son modelos
    /// genuinamente distintos y se dejan separados a propósito; lo que se corrige es que hoy hay
    /// tres tablas y dos puertas, y ninguna abre la habitación correcta.
    ///
    /// <list type="table">
    ///   <item><description><b>Guía Genética Pollo Engorde</b> → <c>/config/guia-genetica-ecuador</c> → <c>guia_genetica_ecuador_header/_detalle</c> (Ecuador + Panamá)</description></item>
    ///   <item><description><b>Guía Genética Sanmarino</b> → <c>/config/guia-genetica</c> → <c>guia_genetica_sanmarino_colombia</c> (Sanmarino / Demo)</description></item>
    ///   <item><description><b>Guía Genética Santa Reyes</b> → <c>/config/guia-genetica-santa-reyes</c> → <c>guia_genetica_santa_reyes</c> (perfil <c>reducida</c>) — <b>ítem nuevo</b></description></item>
    /// </list>
    ///
    /// <para>
    /// 🔴 <b>Por qué esta migración es defensiva hasta la paranoia.</b> Las dos filas de
    /// <c>menus</c> que ya existen <b>no las creó ninguna migración</b>: viven sólo como espejo en
    /// <c>backend/sql/add_guia_genetica_menu.sql</c> y <c>add_guia_genetica_ecuador_menu.sql</c>, y
    /// alguien las corrió a mano en producción. O sea: <b>el repo no puede probar qué filas de
    /// <c>menus</c> existen realmente en prod</b>. Por eso cada paso localiza <b>siempre por
    /// <c>route</c></b> (jamás por id — los ids difieren local ↔ prod), inserta con
    /// <c>WHERE NOT EXISTS</c> y deja el mismo estado final exista o no la fila previa.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Se DESACTIVA, no se borra.</b> A las empresas de perfil <c>reducida</c> se les pone
    /// <c>is_enabled=false</c> en <c>company_menus</c> para los dos ítems viejos, y sólo
    /// <b>después de comprobar que el ítem nuevo quedó habilitado para esa misma empresa</b>. Si el
    /// <c>INSERT</c> compensatorio no pegara, la empresa se quedaría sin ninguna pantalla de guía.
    /// Borrar la fila sería irreversible; desactivarla es un <c>UPDATE</c> de vuelta.
    /// </para>
    ///
    /// <para>
    /// <b>Cómo se resuelven las empresas destino:</b> por el flag de comportamiento
    /// <c>companies.guia_genetica_perfil = 'reducida'</c> (migración <c>20260826142448</c>) o por
    /// DATOS (<c>EXISTS</c> sobre <c>guia_genetica_santa_reyes</c>) — <b>nunca</b> por
    /// <c>name = 'Santa Reyes'</c> ni por país (CLAUDE.md §🏢). La empresa #4 que mañana use el
    /// modelo plano hereda estos menús cambiando un dato, sin desplegar código.
    /// </para>
    ///
    /// <para>
    /// <b>Medido el 26-ago-2026 sobre la copia de producción local</b> (y corrige el supuesto del
    /// plan): el ítem que Santa Reyes heredó del clon de menús es el <b>27</b>
    /// (<c>/config/guia-genetica</c>, la tabla ancha de Sanmarino), <b>no</b> el 51 de engorde. El
    /// filtro del seed excluía <c>%engorde%</c> y Sanmarino nunca tuvo el ítem de Ecuador, así que no
    /// había nada de engorde que heredar. Da igual para esta migración: desactiva <b>los dos</b>.
    /// </para>
    ///
    /// <para>
    /// <b>El icono es <c>clipboard-list</c>, no <c>dna</c>, y no es capricho:</b>
    /// <c>frontend/src/app/shared/services/menu.service.ts</c> mapea los iconos por nombre contra un
    /// <c>ICON_MAP</c> cerrado y <c>'dna'</c> <b>no está</b> ⇒ el ítem de Ecuador se dibuja hoy sin
    /// icono. Copiar ese nombre habría copiado el defecto.
    /// </para>
    ///
    /// <para>
    /// 🔗 <b>Depende de F3</b>: el ítem nuevo apunta a <c>/config/guia-genetica-santa-reyes</c>, ruta
    /// que el front todavía no declara en <c>app.config.ts</c>. Esta migración y la pantalla tienen
    /// que salir en el <b>mismo release</b>: desplegada sola, la empresa de perfil reducido pierde su
    /// ítem viejo y el nuevo no lleva a ningún lado.
    /// </para>
    ///
    /// Espejo legible: <c>backend/sql/seed_menus_guia_genetica_tres_modulos.sql</c>.
    /// Migración DATA-ONLY (no cambia el modelo): Designer clonado de la migración inmediatamente
    /// anterior, <c>ZooSanMarinoContextModelSnapshot</c> intacto. Idempotente y re-ejecutable.
    /// </summary>
    public partial class SeedMenusGuiaGeneticaTresModulos : Migration
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
-- 1) Las TRES filas de `menus`. Localizadas SIEMPRE por `route`.
--
--    `menus.key` es NOT NULL y UNIQUE (uq_menus_key): el segundo NOT EXISTS no es decorativo,
--    evita que un entorno con la key ya tomada (por otra ruta) reviente el INSERT. Una migración
--    que revienta al arrancar mata la tarea ECS antes del primer log.
--
--    El padre se resuelve del hermano que ya exista y, sólo como último recurso, por el nodo raíz
--    de Configuración — igual que hacían los dos .sql corridos a mano.
-- ─────────────────────────────────────────────────────────────────────────────

-- 1a) Guía Genética Sanmarino (tabla ancha compartida). Normalmente YA existe.
INSERT INTO public.menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT 'Guía Genética Sanmarino',
       'clipboard-list',
       '/config/guia-genetica',
       (SELECT m.id FROM public.menus m
         WHERE (m.route = '/config' OR m.label ILIKE '%config%') AND m.parent_id IS NULL
         ORDER BY m.id LIMIT 1),
       5, true, 'guia_genetica', 0, false, now(), now()
WHERE NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.route = '/config/guia-genetica')
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key  = 'guia_genetica');

-- 1b) Guía Genética Pollo Engorde (Ecuador + Panamá). Normalmente YA existe.
INSERT INTO public.menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT 'Guía Genética Pollo Engorde',
       'clipboard-list',
       '/config/guia-genetica-ecuador',
       COALESCE(
         (SELECT m.parent_id FROM public.menus m WHERE m.route = '/config/guia-genetica' LIMIT 1),
         (SELECT m.id FROM public.menus m
           WHERE (m.route = '/config' OR m.label ILIKE '%config%') AND m.parent_id IS NULL
           ORDER BY m.id LIMIT 1)
       ),
       12, true, 'guia_genetica_ecuador', 0, false, now(), now()
WHERE NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.route = '/config/guia-genetica-ecuador')
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key  = 'guia_genetica_ecuador');

-- 1c) Guía Genética Santa Reyes — el ítem NUEVO (tabla plana de 3 métricas).
--     Hereda el `order` del ítem que reemplaza, para que caiga en el mismo lugar del submenú.
INSERT INTO public.menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT 'Guía Genética Santa Reyes',
       'clipboard-list',
       '/config/guia-genetica-santa-reyes',
       COALESCE(
         (SELECT m.parent_id FROM public.menus m WHERE m.route = '/config/guia-genetica'         LIMIT 1),
         (SELECT m.parent_id FROM public.menus m WHERE m.route = '/config/guia-genetica-ecuador' LIMIT 1),
         (SELECT m.id FROM public.menus m
           WHERE (m.route = '/config' OR m.label ILIKE '%config%') AND m.parent_id IS NULL
           ORDER BY m.id LIMIT 1)
       ),
       COALESCE((SELECT m.""order"" FROM public.menus m WHERE m.route = '/config/guia-genetica' LIMIT 1), 5),
       true, 'guia_genetica_santa_reyes', 0, false, now(), now()
WHERE NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.route = '/config/guia-genetica-santa-reyes')
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key  = 'guia_genetica_santa_reyes');

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Los rótulos, CON tildes. `IS DISTINCT FROM` para no ensuciar `updated_at` de lo que ya
--    está bien (2ª pasada = 0 filas afectadas).
--
--    Ojo con la historia: 20260623080001_RenameMenu_GuiaGenetica dejó el ítem de ENGORDE
--    rotulado 'Guia Genetica' (sin tildes), o sea que el ítem que en el sidebar decía
--    'Guia Genetica' era el de Ecuador y el de Sanmarino decía 'Guía Genética'. Indistinguibles
--    a simple vista. Acá cada uno pasa a decir de qué modelo es.
-- ─────────────────────────────────────────────────────────────────────────────
UPDATE public.menus
   SET label = 'Guía Genética Pollo Engorde', updated_at = now()
 WHERE route = '/config/guia-genetica-ecuador'
   AND label IS DISTINCT FROM 'Guía Genética Pollo Engorde';

UPDATE public.menus
   SET label = 'Guía Genética Sanmarino', updated_at = now()
 WHERE route = '/config/guia-genetica'
   AND label IS DISTINCT FROM 'Guía Genética Sanmarino';

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) company_menus — ALTA del ítem nuevo para las empresas de perfil reducido.
--
--    `reducidas` se define por COMPORTAMIENTO (el flag) o por DATOS (filas propias), nunca por
--    nombre de empresa. La definición se repite en los pasos 3, 4 y 5 porque un CTE sólo alcanza a
--    su propia sentencia: si se toca una, se tocan las tres.
--
--    sort_order se hereda del ítem que reemplaza (hoy el sidebar lo ignora — ordena por
--    menus.""order"" — pero dejarlo coherente cuesta cero).
--
--    🔴 El último NOT EXISTS del WHERE (""la empresa ya tiene alguna fila en company_menus"") NO es
--    redundante: fn_menu_usuario es FAIL-OPEN por empresa (D2) — una empresa SIN ninguna fila no se
--    filtra y ve todo el catálogo. Insertarle UNA sola fila la convertiría en filtrada y le dejaría
--    el menú reducido a ese único ítem. Hoy las 5 empresas tienen filas, así que la guarda no cambia
--    nada; existe para la empresa #6 que mañana nazca con perfil reducido y el menú sin configurar.
--    Sin la fila igual ve el ítem, justamente por el fail-open.
-- ─────────────────────────────────────────────────────────────────────────────
WITH reducidas AS (
    SELECT c.id
      FROM public.companies c
     WHERE c.guia_genetica_perfil = 'reducida'
        OR EXISTS (SELECT 1 FROM public.guia_genetica_santa_reyes g WHERE g.company_id = c.id)
)
INSERT INTO public.company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT r.id,
       nuevo.id,
       true,
       COALESCE((SELECT cm.sort_order
                   FROM public.company_menus cm
                   JOIN public.menus mo ON mo.id = cm.menu_id
                  WHERE cm.company_id = r.id
                    AND mo.route IN ('/config/guia-genetica', '/config/guia-genetica-ecuador')
                  ORDER BY cm.sort_order
                  LIMIT 1), 0),
       NULL
  FROM reducidas r
 CROSS JOIN (SELECT m.id FROM public.menus m WHERE m.route = '/config/guia-genetica-santa-reyes') AS nuevo
 WHERE NOT EXISTS (SELECT 1 FROM public.company_menus x
                    WHERE x.company_id = r.id AND x.menu_id = nuevo.id)
   AND EXISTS     (SELECT 1 FROM public.company_menus x WHERE x.company_id = r.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) role_menus — ANTI-LOCKOUT. El ítem nuevo va a los roles de esas empresas que HOY ven alguno
--    de los otros dos. Sin esto el menú no se ve aunque company_menus lo habilite:
--    fn_menu_usuario interseca role_menus ∩ company_menus (un rol CON role_menus no cae al
--    fallback del catálogo completo).
--    Se localiza por route, nunca por id de menú.
-- ─────────────────────────────────────────────────────────────────────────────
WITH reducidas AS (
    SELECT c.id
      FROM public.companies c
     WHERE c.guia_genetica_perfil = 'reducida'
        OR EXISTS (SELECT 1 FROM public.guia_genetica_santa_reyes g WHERE g.company_id = c.id)
)
INSERT INTO public.role_menus (role_id, menu_id)
SELECT DISTINCT rm.role_id, nuevo.id
  FROM public.role_menus rm
  JOIN public.menus m           ON m.id = rm.menu_id
                               AND m.route IN ('/config/guia-genetica', '/config/guia-genetica-ecuador')
  JOIN public.role_companies rc ON rc.role_id = rm.role_id
  JOIN reducidas r              ON r.id = rc.company_id
 CROSS JOIN (SELECT m2.id FROM public.menus m2 WHERE m2.route = '/config/guia-genetica-santa-reyes') AS nuevo
 WHERE NOT EXISTS (SELECT 1 FROM public.role_menus x
                    WHERE x.role_id = rm.role_id AND x.menu_id = nuevo.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 5) company_menus — BAJA (is_enabled=false, NO delete) de los dos ítems viejos, y SÓLO para las
--    empresas de perfil reducido que ya tienen el nuevo HABILITADO. Ese EXISTS es el seguro: si
--    el paso 3 no hubiera pegado, la empresa se quedaría sin ninguna pantalla de guía.
--    Las filas de role_menus viejas NO se tocan: la puerta la cierra company_menus, y borrarlas
--    sería irreversible sin ganar nada.
-- ─────────────────────────────────────────────────────────────────────────────
WITH reducidas AS (
    SELECT c.id
      FROM public.companies c
     WHERE c.guia_genetica_perfil = 'reducida'
        OR EXISTS (SELECT 1 FROM public.guia_genetica_santa_reyes g WHERE g.company_id = c.id)
)
UPDATE public.company_menus cm
   SET is_enabled = false
  FROM public.menus m, reducidas r
 WHERE m.id = cm.menu_id
   AND cm.company_id = r.id
   AND m.route IN ('/config/guia-genetica', '/config/guia-genetica-ecuador')
   AND cm.is_enabled IS DISTINCT FROM false
   AND EXISTS (SELECT 1
                 FROM public.company_menus cx
                 JOIN public.menus mx ON mx.id = cx.menu_id
                WHERE cx.company_id = r.id
                  AND mx.route = '/config/guia-genetica-santa-reyes'
                  AND cx.is_enabled);
";

        // El Down deshace exactamente lo que el Up hizo, en orden inverso:
        //   - reabre los dos ítems viejos para las empresas de perfil reducido (estaban en true),
        //   - borra las asignaciones del ítem nuevo y el ítem,
        //   - devuelve los rótulos al estado que dejó 20260623080001_RenameMenu_GuiaGenetica.
        // No borra las filas de `menus` 1a/1b: pudieron existir desde antes (se corrieron a mano) y
        // borrarlas se llevaría puesto el módulo de cuatro empresas.
        private const string DOWN_SQL = @"
WITH reducidas AS (
    SELECT c.id
      FROM public.companies c
     WHERE c.guia_genetica_perfil = 'reducida'
        OR EXISTS (SELECT 1 FROM public.guia_genetica_santa_reyes g WHERE g.company_id = c.id)
)
UPDATE public.company_menus cm
   SET is_enabled = true
  FROM public.menus m, reducidas r
 WHERE m.id = cm.menu_id
   AND cm.company_id = r.id
   AND m.route IN ('/config/guia-genetica', '/config/guia-genetica-ecuador')
   AND cm.is_enabled IS DISTINCT FROM true;

DELETE FROM public.role_menus
 WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/config/guia-genetica-santa-reyes');

DELETE FROM public.company_menus
 WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/config/guia-genetica-santa-reyes');

DELETE FROM public.menu_permissions
 WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/config/guia-genetica-santa-reyes');

DELETE FROM public.menus WHERE route = '/config/guia-genetica-santa-reyes';

UPDATE public.menus
   SET label = 'Guia Genetica', updated_at = now()
 WHERE route = '/config/guia-genetica-ecuador'
   AND label IS DISTINCT FROM 'Guia Genetica';

UPDATE public.menus
   SET label = 'Guía Genética', updated_at = now()
 WHERE route = '/config/guia-genetica'
   AND label IS DISTINCT FROM 'Guía Genética';
";
    }
}
