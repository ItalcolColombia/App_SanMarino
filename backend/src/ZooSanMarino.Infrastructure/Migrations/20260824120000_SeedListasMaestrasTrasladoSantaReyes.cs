using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only, sin cambio de esquema. Siembra para <b>Santa Reyes</b> las 5 listas maestras de
    /// traslado que le faltaban.
    ///
    /// <para>
    /// <b>Por qué.</b> Medido en la BD el 24-ago-2026: Santa Reyes tenía <b>una sola</b> fila en
    /// <c>master_lists</c> (<c>region_option_key</c>), mientras Sanmarino, Demo y Ecuador ya tenían
    /// las de traslado. Sin la lista <c>traslado_de_huevos_planta_destino</c>, el desplegable de
    /// destino del traslado de huevos sale <b>vacío</b>: es exactamente el hueco de
    /// <c>TK-2026-000180</c> / <c>SR-DEF-6</c> (F10.1), cuyo requerimiento literal es *«…no vemos
    /// bien que la bodega de salida sea digitada… debe ser una lista desplegable»*.
    /// </para>
    ///
    /// <para>
    /// <b>La opción «Bodega General»</b> la pidió el usuario en sesión (24-ago-2026): una bodega
    /// genérica para que la lista no nazca vacía y el campo tenga de dónde leer. Los destinos reales
    /// del cliente se agregan después desde la pantalla de listas maestras
    /// (<c>/config/master-lists</c>), sin tocar código — que es justamente el punto de mover esto a
    /// lista maestra.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente</b>: cada <c>INSERT</c> va con <c>WHERE NOT EXISTS</c>, sobre la clave natural
    /// de cada tabla (<c>key + company_id + country_id</c> para la lista;
    /// <c>master_list_id + value</c> para la opción). Dos pasadas seguidas dejan exactamente las
    /// mismas filas. <b>Fail-open</b> con <c>RAISE NOTICE</c>: si la empresa o su país no existen
    /// (BD de otro entorno), no siembra nada y no rompe el arranque.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>No toca ninguna otra empresa.</b> Todo cuelga de <c>companies.name = 'Santa Reyes'</c>.
    /// </para>
    /// </summary>
    public partial class SeedListasMaestrasTrasladoSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    c_empresa    text := 'Santa Reyes';
    v_company_id int;
    v_pais_id    int;
    v_pais_nom   text;
    v_list_id    int;
    v_opt        text;
    v_idx        int;
    r            record;
BEGIN
    SELECT co.id INTO v_company_id
      FROM public.companies co
     WHERE co.name = c_empresa
     ORDER BY co.id
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE NOTICE 'SeedListasMaestrasTrasladoSantaReyes: la empresa % no existe; no se siembra nada.', c_empresa;
        RETURN;
    END IF;

    SELECT cp.pais_id, p.pais_nombre
      INTO v_pais_id, v_pais_nom
      FROM public.company_pais cp
      JOIN public.paises p ON p.pais_id = cp.pais_id
     WHERE cp.company_id = v_company_id
     ORDER BY cp.pais_id
     LIMIT 1;

    IF v_pais_id IS NULL THEN
        RAISE NOTICE 'SeedListasMaestrasTrasladoSantaReyes: % no tiene pais en company_pais; no se siembra nada.', c_empresa;
        RETURN;
    END IF;

    FOR r IN
        SELECT * FROM (VALUES
            ('traslado_de_huevos_planta_destino',   'Lista de las Planta Destino', ARRAY['Bodega General']),
            ('traslado_de_huevos_tipo_destino',     'Tipo de Destino',             ARRAY['Granja', 'Planta']),
            ('traslado_de_huevos_tipo_de_operacion','tipo de operacion',           ARRAY['Traslado', 'Venta']),
            ('traslado_de_huevos_venta_motivo',     'Motivo venta',                ARRAY['Cliente']),
            ('movimiento_de_aves_tipo_movimiento',  'Tipo Movimiento',             ARRAY['Traslado', 'Venta'])
        ) AS t(key, name, opciones)
    LOOP
        INSERT INTO public.master_lists (key, name, company_id, company_name, country_id, country_name)
        SELECT r.key, r.name, v_company_id, c_empresa, v_pais_id, v_pais_nom
         WHERE NOT EXISTS (SELECT 1
                             FROM public.master_lists ml
                            WHERE ml.key        = r.key
                              AND ml.company_id = v_company_id
                              AND ml.country_id = v_pais_id);

        SELECT ml.id INTO v_list_id
          FROM public.master_lists ml
         WHERE ml.key        = r.key
           AND ml.company_id = v_company_id
           AND ml.country_id = v_pais_id
         ORDER BY ml.id
         LIMIT 1;

        v_idx := 0;
        FOREACH v_opt IN ARRAY r.opciones
        LOOP
            INSERT INTO public.master_list_options (master_list_id, value, ""order"")
            SELECT v_list_id, v_opt, v_idx
             WHERE NOT EXISTS (SELECT 1
                                 FROM public.master_list_options o
                                WHERE o.master_list_id = v_list_id
                                  AND o.value          = v_opt);
            v_idx := v_idx + 1;
        END LOOP;
    END LOOP;
END $$;
");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Borra SOLO las 5 listas de Santa Reyes sembradas acá. Las opciones caen solas por el
        /// <c>ON DELETE CASCADE</c> de <c>master_list_options.master_list_id</c>.
        /// <c>region_option_key</c> no se toca: es de otra migración y <c>farms.regional_id</c>
        /// apunta al id de una de sus opciones.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM public.master_lists ml
 USING public.companies co
 WHERE co.id = ml.company_id
   AND co.name = 'Santa Reyes'
   AND ml.key IN ('traslado_de_huevos_planta_destino',
                  'traslado_de_huevos_tipo_destino',
                  'traslado_de_huevos_tipo_de_operacion',
                  'traslado_de_huevos_venta_motivo',
                  'movimiento_de_aves_tipo_movimiento');
");
        }
    }
}
