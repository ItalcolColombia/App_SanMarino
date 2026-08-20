using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Crea los tres galpones que le faltan al núcleo <c>Modulo IV</c> de la granja <c>NIZA III</c>
    /// (Agroavicola Sanmarino): <c>Galpon 1</c>, <c>Galpon 2</c> y <c>Galpon 3</c>.
    /// </summary>
    /// <remarks>
    /// <b>De dónde salió.</b> Ticket de operación del 19ago26: el núcleo 4 de Niza 3 «no aparece» al
    /// crear lotes. No era el servicio ni permisos —el núcleo está activo y la usuaria tiene la granja
    /// asignada sin restricción de ubicación—: el núcleo quedó con <b>cero galpones</b>. El desplegable
    /// de la tab Galpones se deriva de los galpones cargados, así que un núcleo vacío no figura, y el
    /// formulario de lotes no tiene qué ofrecer.
    ///
    /// <para>
    /// <b>Por qué no se pudo arreglar desde la UI.</b> <c>galpones.galpon_id</c> es PK GLOBAL, pero el
    /// modal proponía el Id con el máximo de los galpones que el usuario VE. Con el alcance de la
    /// reportante proponía <c>G0443</c> —que existe en una granja que ella no ve— y el alta se
    /// rechazaba en cada intento. El front ya no inventa el Id (pide <c>GET /api/Galpon/siguiente-id</c>),
    /// pero esta migración deja los datos corregidos en el mismo despliegue.
    /// </para>
    ///
    /// <para>
    /// <b>Identidad por nombre</b>, nunca por id fijo (difieren local↔prod): empresa
    /// <c>Agroavicola Sanmarino</c> → granja <c>NIZA III</c> → núcleo <c>Modulo IV</c> (se acepta la
    /// grafía vieja <c>Modulo IV -</c>). <b>Fail-open:</b> si el entorno no tiene esa granja/núcleo,
    /// <c>RAISE NOTICE</c> y <c>RETURN</c> — un seed no puede tumbar el arranque de la app.
    /// <b>Idempotente</b> por partida doble: no hace nada si el núcleo ya tiene 3 galpones activos
    /// (p. ej. si operación los creó a mano antes del deploy) y, por galpón, salta el que ya exista
    /// con ese nombre. El Id se elige libre en tiempo de ejecución, igual que
    /// <c>GalponService.GenerateNextGalponIdAsync</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Sin medidas inventadas:</b> <c>ancho</c>/<c>largo</c> quedan NULL (la columna Área muestra
    /// «—» hasta que operación las cargue); <c>tipo_galpon</c> = <c>Abierto</c>, que es el de los otros
    /// 13 galpones de la granja. <c>created_by_user_id = 0</c> marca «creado por el sistema», igual que
    /// los núcleos/galpones sembrados de esa granja. Migración DATA-ONLY: Designer clonado,
    /// ModelSnapshot intacto. Espejo SQL en <c>backend/sql/crear_galpones_modulo_iv_niza_iii.sql</c>.
    /// </para>
    /// </remarks>
    public partial class SeedGalponesModuloIvNizaIii : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_company  integer;
    v_granja   integer;
    v_nucleo   varchar(64);
    v_activos  integer;
    v_nombre   text;
    v_n        integer;
    v_id       varchar(64);
    v_creados  integer := 0;
BEGIN
    SELECT c.id INTO v_company
    FROM public.companies c
    WHERE lower(trim(c.name)) = 'agroavicola sanmarino'
    LIMIT 1;

    IF v_company IS NULL THEN
        RAISE NOTICE 'SeedGalponesModuloIvNizaIii: no existe la empresa Agroavicola Sanmarino; nada que hacer.';
        RETURN;
    END IF;

    SELECT f.id INTO v_granja
    FROM public.farms f
    WHERE f.company_id = v_company
      AND lower(trim(f.name)) = 'niza iii'
      AND f.deleted_at IS NULL
    LIMIT 1;

    IF v_granja IS NULL THEN
        RAISE NOTICE 'SeedGalponesModuloIvNizaIii: no existe la granja NIZA III activa; nada que hacer.';
        RETURN;
    END IF;

    -- Se acepta la grafia vieja 'Modulo IV -' (se renombro a 'Modulo IV' el 18ago26).
    SELECT n.nucleo_id INTO v_nucleo
    FROM public.nucleos n
    WHERE n.granja_id = v_granja
      AND n.deleted_at IS NULL
      AND lower(trim(trailing ' -' FROM trim(n.nucleo_nombre))) = 'modulo iv'
    LIMIT 1;

    IF v_nucleo IS NULL THEN
        RAISE NOTICE 'SeedGalponesModuloIvNizaIii: la granja NIZA III no tiene el nucleo Modulo IV activo; nada que hacer.';
        RETURN;
    END IF;

    SELECT count(*) INTO v_activos
    FROM public.galpones g
    WHERE g.granja_id = v_granja AND g.nucleo_id = v_nucleo AND g.deleted_at IS NULL;

    IF v_activos >= 3 THEN
        RAISE NOTICE 'SeedGalponesModuloIvNizaIii: el nucleo ya tiene % galpon(es) activo(s); no se toca nada.', v_activos;
        RETURN;
    END IF;

    FOREACH v_nombre IN ARRAY ARRAY['Galpon 1', 'Galpon 2', 'Galpon 3']
    LOOP
        CONTINUE WHEN EXISTS (
            SELECT 1 FROM public.galpones g
            WHERE g.granja_id = v_granja AND g.nucleo_id = v_nucleo
              AND g.deleted_at IS NULL
              AND lower(trim(g.galpon_nombre)) = lower(v_nombre)
        );

        -- Id libre: el proximo despues del maximo global 'Gnnnn', avanzando si estuviera ocupado
        -- (la PK es global, incluye borrados y todas las empresas). Misma regla que el backend.
        SELECT coalesce(max((regexp_match(g.galpon_id, '^G([0-9]+)$'))[1]::int), 0) + 1
          INTO v_n
        FROM public.galpones g
        WHERE g.galpon_id ~ '^G[0-9]+$';

        LOOP
            v_id := 'G' || lpad(v_n::text, 4, '0');
            EXIT WHEN NOT EXISTS (SELECT 1 FROM public.galpones x WHERE x.galpon_id = v_id);
            v_n := v_n + 1;
        END LOOP;

        INSERT INTO public.galpones
            (galpon_id, nucleo_id, granja_id, galpon_nombre,
             ancho, largo, tipo_galpon, company_id, created_by_user_id, created_at)
        VALUES
            (v_id, v_nucleo, v_granja, v_nombre,
             NULL, NULL, 'Abierto', v_company, 0, now());

        v_creados := v_creados + 1;
    END LOOP;

    RAISE NOTICE 'SeedGalponesModuloIvNizaIii: % galpon(es) creado(s) en el nucleo % de la granja %.',
        v_creados, v_nucleo, v_granja;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo se borran los galpones que sembró esta migración (created_by_user_id = 0) y
            // únicamente si siguen VACÍOS. Si operación ya los usó —un lote, inventario, producción—
            // se dejan: revertir la migración no puede llevarse por delante datos de negocio.
            migrationBuilder.Sql(@"
DELETE FROM public.galpones g
 WHERE g.galpon_nombre IN ('Galpon 1', 'Galpon 2', 'Galpon 3')
   AND g.created_by_user_id = 0
   AND g.granja_id IN (
        SELECT f.id FROM public.farms f
        JOIN public.companies c ON c.id = f.company_id
        WHERE lower(trim(c.name)) = 'agroavicola sanmarino'
          AND lower(trim(f.name)) = 'niza iii')
   AND g.nucleo_id IN (
        SELECT n.nucleo_id FROM public.nucleos n
        WHERE n.granja_id = g.granja_id
          AND lower(trim(trailing ' -' FROM trim(n.nucleo_nombre))) = 'modulo iv')
   AND NOT EXISTS (SELECT 1 FROM public.lotes            x WHERE x.galpon_id = g.galpon_id)
   AND NOT EXISTS (SELECT 1 FROM public.lote_ave_engorde x WHERE x.galpon_id = g.galpon_id)
   AND NOT EXISTS (SELECT 1 FROM public.produccion_lotes x WHERE x.galpon_id = g.galpon_id)
   AND NOT EXISTS (SELECT 1 FROM public.lote_galpones    x WHERE x.galpon_id = g.galpon_id)
   AND NOT EXISTS (SELECT 1 FROM public.inventario_aves  x WHERE x.galpon_id = g.galpon_id);
");
        }
    }
}
