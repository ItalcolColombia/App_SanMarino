using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Acota los menús de <b>Agroavicola Sanmarino</b> a los que corresponden a su operación:
    /// deja de habilitarle los catálogos globales del sistema, los módulos internos de desarrollo e
    /// implementación, y un módulo que es de otro país.
    ///
    /// <para>
    /// <b>Por qué.</b> Sanmarino era, por herencia histórica de haber sido la primera empresa, la más
    /// abierta de las cinco: <b>49 de 68</b> menús habilitados, contra 27 de Demo, 25 de Ecuador,
    /// 24 de Santa Reyes y 23 de Panamá. Entre ellos, los que administran el sistema entero —Empresas
    /// y db_studio— y los de la operación interna del área de desarrollo. La empresa pasa a estar
    /// acotada como cualquier otra; lo global se administra desde el super admin, no desde una
    /// empresa.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Depende de <c>FnMenuUsuarioSuperAdmin</c> (D5) y no puede ir antes.</b>
    /// <c>/config/companies</c> y <c>/config/db-studio</c> estaban habilitados en <b>una sola
    /// empresa</b>: ésta. Sin el bypass del gate de empresa para el super admin, apagarlos acá lo
    /// dejaría sin el módulo Empresas en toda la aplicación y sin ruta de vuelta por la UI —para
    /// rehabilitarlo hay que entrar a Configuración → Empresas → Menús, que es justo el menú que
    /// desaparece—. El orden de los timestamps (120000 antes que 140000) es parte del arreglo.
    /// </para>
    ///
    /// <para>
    /// <b>Impacto medido sobre la copia de producción (4-sep-2026)</b>, contando solo usuarios que no
    /// son super admin: 12 de los 17 ítems no los ve <b>ninguna</b> persona de Sanmarino —figuraban
    /// habilitados y nadie los tenía en sus <c>role_menus</c>—. Los 5 restantes sí tienen a alguien
    /// detrás y se apagan por decisión explícita del usuario (limpieza completa):
    /// <list type="bullet">
    ///   <item>Geografía — rol <c>Sistemas sanmarino</c>, 1 usuario.</item>
    ///   <item>ItalJira Backlog, Roadmap y Panel de control — rol <c>Lider Demanda &amp; Delivery</c>,
    ///         1 usuario.</item>
    ///   <item><b>Bandeja de gestión quedó FUERA</b> de la limpieza, aunque estaba en la lista: ver
    ///         la nota al lado de <c>RutasApagadas</c>. Apagarla dejaría sin bandeja al rol de
    ///         soporte que la migración anterior crea para atenderla.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>Apaga, no borra.</b> Se pone <c>is_enabled = false</c> y la fila queda: la pantalla
    /// Configuración → Empresas → Menús sigue mostrando el switch para revertir cualquiera de estos
    /// sin un despliegue. Borrar la fila oculta igual (D1) pero deja la pantalla sin el interruptor.
    /// </para>
    ///
    /// <para>
    /// <b>Los grupos padre se apagan con sus hijos</b> (ItalJira, Implementación, Mapas, Vacunación):
    /// un grupo cuyos hijos se apagaron todos y que sigue asignado en <c>role_menus</c> se pinta
    /// <b>vacío</b> en el sidebar, no desaparece. Los grupos que conservan hijos —Configuración,
    /// Tickets, Carga Masiva— no se tocan.
    /// </para>
    ///
    /// <para>
    /// Data-only e <b>idempotente</b>: lookups por <c>companies.name</c> y <c>menus.route</c> —los
    /// grupos, que no tienen ruta, se deducen de sus hijos y no se nombran por <c>label</c>—, y
    /// <c>IS DISTINCT FROM</c> para no reescribir lo que ya está apagado. Si un entorno no tiene
    /// alguna de estas rutas, esa fila simplemente no se toca y la migración no falla.
    /// Sin cambios de modelo (ModelSnapshot intacto).
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/soporte_sanmarino_y_admin_global_plan.md</c>.
    /// </summary>
    public partial class AcotaMenusSanmarino : Migration
    {
        /// <summary>
        /// Las hojas, por ruta. Agrupadas por el motivo que las saca.
        /// </summary>
        private const string RutasApagadas = @"
        -- Catálogos GLOBALES del sistema: los administra el super admin, no una empresa.
            ('/config/companies'),
            ('/config/db-studio'),
            ('/config/countries'),
        -- Operación interna del área de desarrollo e implementación.
            ('/italjira/backlog'),
            ('/italjira/tablero'),
            ('/italjira/roadmap'),
            ('/italjira/panel'),
            ('/italjira/configuracion'),
            ('/implementacion/planes'),
            ('/implementacion/mis-tareas'),
            ('/mapas'),
            ('/mapas/configuraciones'),
            ('/vacunacion/cronograma'),
            ('/vacunacion/registro'),
            ('/vacunacion/reportes'),
        -- De otro país.
            ('/migraciones/sincronizacion-panama')
";

        // ⚠️ `/tickets/gestion` (Bandeja de gestión) estaba en la lista de la limpieza completa y se
        // SACÓ a propósito: el menú efectivo es la intersección role_menus ∩ company_menus, así que
        // apagarlo en la empresa dejaría al rol «Soporte Sanmarino» —que se crea en la migración
        // anterior justamente para atender esa bandeja— sin poder verla. Las dos decisiones eran
        // incompatibles y ésta es la única combinación que cumple las dos.
        //
        // Efecto colateral asumido: el rol `Sistemas sanmarino`, que hoy también la tiene, la
        // conserva. Si se quiere sacársela a él y no al soporte, el lugar es su `role_menus`, no
        // `company_menus` — la empresa habilita el módulo, el rol decide quién entra.

        // Los grupos raíz que quedan vacíos (ItalJira, Implementación, Mapas, Vacunación) NO se
        // nombran por label: se DEDUCEN de sus hijos. Un literal como 'Implementación' dentro de
        // este .cs depende de que el archivo se lea como UTF-8, y un acento mal leído convierte el
        // UPDATE en un no-op silencioso — el peor modo de fallar para una migración de datos.
        //
        // El criterio es estructural y da el mismo conjunto en Up() y en Down(): grupo raíz, sin
        // ruta propia, cuyos hijos activos CON FILA en company_menus de esta empresa están TODOS en
        // la lista de rutas apagadas. Se mira la existencia de la fila y no `is_enabled`, para que
        // Down() calcule exactamente los mismos grupos que Up() ya apagó.
        //
        // Deja fuera, correctamente, a los grupos que conservan hijos: Configuración (Usuarios,
        // Roles, Listas maestras...), Tickets (Mis solicitudes) y Carga Masiva (Migración Manual).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Aplicar(migrationBuilder, habilitar: false);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => Aplicar(migrationBuilder, habilitar: true);

        private static void Aplicar(MigrationBuilder migrationBuilder, bool habilitar)
        {
            var valor = habilitar ? "true" : "false";

            migrationBuilder.Sql($@"
DO $$
DECLARE
    v_company_id integer;
    v_filas      integer;
BEGIN
    SELECT c.id INTO v_company_id FROM companies c WHERE c.name = 'Agroavicola Sanmarino';

    IF v_company_id IS NULL THEN
        RAISE NOTICE 'Acota menus Sanmarino: no existe la empresa en este entorno; omitido.';
        RETURN;
    END IF;

    WITH rutas(route) AS (
        VALUES {RutasApagadas}    ),
    objetivo AS (
        -- Las hojas.
        SELECT m.id
          FROM menus m
          JOIN rutas r ON r.route = m.route
        UNION
        -- Los grupos raíz que quedan sin un solo hijo: se deducen, no se nombran.
        SELECT p.id
          FROM menus p
         WHERE p.parent_id IS NULL
           AND p.route IS NULL
           AND EXISTS (
               SELECT 1
                 FROM menus h
                 JOIN company_menus cmh ON cmh.menu_id = h.id AND cmh.company_id = v_company_id
                WHERE h.parent_id = p.id AND h.is_active
           )
           AND NOT EXISTS (
               SELECT 1
                 FROM menus h
                 JOIN company_menus cmh ON cmh.menu_id = h.id AND cmh.company_id = v_company_id
                WHERE h.parent_id = p.id
                  AND h.is_active
                  AND NOT EXISTS (SELECT 1 FROM rutas r WHERE r.route = h.route)
           )
    )
    UPDATE company_menus cm
       SET is_enabled = {valor}
      FROM objetivo o
     WHERE cm.company_id = v_company_id
       AND cm.menu_id    = o.id
       AND cm.is_enabled IS DISTINCT FROM {valor};   -- idempotente: no ensucia lo que ya está así

    GET DIAGNOSTICS v_filas = ROW_COUNT;
    RAISE NOTICE 'Acota menus Sanmarino: % menus puestos en is_enabled = {valor}.', v_filas;
END $$;
");
        }
    }
}
