using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only, sin cambio de esquema ni de modelo. Siembra la lista maestra
    /// <c>venta_pollo_engorde_empresa</c> («Empresa de venta (pollo engorde)») para <b>cada empresa y cada país</b>
    /// que tenga en <c>company_pais</c>, con la opción por defecto <b>Planta</b>.
    ///
    /// <para>
    /// <b>Para qué.</b> El modal de venta de pollo engorde (Panamá y Ecuador) pide a qué empresa se vendió o se
    /// envió el despacho, y la tabla de ventas se puede filtrar por ella. Las opciones NO están en el código: viven en
    /// esta lista maestra, que cada empresa administra desde Config → Listas maestras
    /// (<c>/config/master-lists</c>) sin tocar código. La pantalla resuelve la lista por <c>key</c> + empresa activa +
    /// país activo (<c>MasterListService.GetByKeyAsync</c>), por eso hay una fila por (empresa, país).
    /// </para>
    ///
    /// <para>
    /// <b>Dónde se guarda la elección.</b> En <c>movimiento_pollo_engorde.planta_destino</c> (columna que ya existía y
    /// que ya llena la carga masiva de ventas), como TEXTO de la opción: <c>MasterListService.UpdateAsync</c> borra y
    /// recrea las opciones al guardar una lista, así que sus ids cambian en cada edición y no sirven como referencia.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué a todas las empresas.</b> Solo Ecuador y Panamá tienen ventas de engorde hoy, pero sembrar a todas
    /// deja la lista disponible para administrar el día que otra empresa empiece a vender engorde, sin pedirle nada a
    /// desarrollo. La lista no cambia el comportamiento de una empresa que no vende engorde.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente.</b> La lista se inserta con <c>WHERE NOT EXISTS</c> sobre su clave natural
    /// (<c>key + company_id + country_id</c>) y la opción <b>solo si la lista todavía no tiene ninguna</b>: una lista
    /// que ya traiga opciones es configuración de alguien y no se toca (tampoco se le agrega «Planta» a la fuerza).
    /// Dos pasadas seguidas dejan exactamente las mismas filas.
    /// </para>
    ///
    /// <para>
    /// ⚠️ La <c>key</c> tiene que coincidir con <c>EMPRESA_VENTA_ENGORDE_KEY</c> del front
    /// (<c>features/movimientos-pollo-engorde/services/empresa-venta-engorde.service.ts</c>).
    /// </para>
    /// </summary>
    public partial class SeedListaMaestraEmpresaVentaEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    c_key     constant text := 'venta_pollo_engorde_empresa';
    c_nombre  constant text := 'Empresa de venta (pollo engorde)';
    c_defecto constant text := 'Planta';
    r         record;
    v_list_id int;
BEGIN
    FOR r IN
        SELECT cp.company_id, co.name AS company_name, cp.pais_id, p.pais_nombre
          FROM public.company_pais cp
          JOIN public.companies co ON co.id      = cp.company_id
          JOIN public.paises    p  ON p.pais_id  = cp.pais_id
         ORDER BY cp.company_id, cp.pais_id
    LOOP
        INSERT INTO public.master_lists (key, name, company_id, company_name, country_id, country_name)
        SELECT c_key, c_nombre, r.company_id, r.company_name, r.pais_id, r.pais_nombre
         WHERE NOT EXISTS (SELECT 1
                             FROM public.master_lists ml
                            WHERE ml.key        = c_key
                              AND ml.company_id = r.company_id
                              AND ml.country_id = r.pais_id);

        SELECT ml.id INTO v_list_id
          FROM public.master_lists ml
         WHERE ml.key        = c_key
           AND ml.company_id = r.company_id
           AND ml.country_id = r.pais_id
         ORDER BY ml.id
         LIMIT 1;

        INSERT INTO public.master_list_options (master_list_id, value, ""order"")
        SELECT v_list_id, c_defecto, 0
         WHERE NOT EXISTS (SELECT 1
                             FROM public.master_list_options o
                            WHERE o.master_list_id = v_list_id);
    END LOOP;
END $$;
");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Borra las listas de esa <c>key</c> (las opciones caen solas por el <c>ON DELETE CASCADE</c> de
        /// <c>master_list_options.master_list_id</c>), incluidas las opciones que cada empresa haya agregado después.
        /// No toca <c>movimiento_pollo_engorde.planta_destino</c>: el texto guardado en las ventas queda como está.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM public.master_lists
 WHERE key = 'venta_pollo_engorde_empresa';
");
        }
    }
}
