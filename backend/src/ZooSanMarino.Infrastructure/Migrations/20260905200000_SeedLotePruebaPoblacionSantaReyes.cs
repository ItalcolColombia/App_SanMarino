using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed DATA-ONLY del lote de pruebas <c>SR-2025-01</c> de Santa Reyes, con todo lo que la carga
    /// masiva necesita encontrar YA CREADO: el lote, su historial de etapa, el espejo de levante, los
    /// dos silos asignados y los cuatro tipos de huevo declarados.
    ///
    /// <para>
    /// <b>Para qué es.</b> Los dos archivos de carga masiva del ciclo completo
    /// (<c>Poblacion_Ciclo_Santa_Reyes/</c>, plan
    /// <c>fase_de_desarrollo/poblacion_ciclo_completo_santa_reyes_plan.md</c>) eligen el lote EN
    /// PANTALLA: no lo crean. Sus fechas arrancan el 24-02-2025 y sus consumos salen de
    /// <c>Silo 1</c> y <c>Silo 2</c>, así que sin esta ficha exacta el importador rechaza cada fila
    /// con «la fecha es anterior al encasetamiento» o «el silo no está asignado a este lote».
    /// </para>
    ///
    /// <para>
    /// <b>Lo que NO hace, a propósito.</b> No cierra ni liquida el levante: el cierre calcula la
    /// liquidación y crea el lote de producción con las aves VIVAS al cerrar, que dependen de los
    /// datos que todavía no se importaron. Ese paso queda entre las dos importaciones, a mano.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente y reversible.</b> Todo entra con <c>WHERE NOT EXISTS</c>, así que correrla dos
    /// veces no duplica nada; y si la empresa, la granja, el núcleo, el galpón o los silos no existen
    /// en el entorno, la migración <b>no hace nada</b> en vez de fallar (el caso de un entorno sin
    /// Santa Reyes). <c>Down()</c> borra el lote y todo lo que cuelga de él, pero sólo si nadie le
    /// cargó seguimientos — si ya tiene datos, se deja y se avisa.
    /// </para>
    ///
    /// <para>
    /// Nada de ids literales: empresa por <c>name</c>, granja/núcleo/galpón por nombre, silos por
    /// nombre y ítems de huevo por código ERP. Los ids difieren entre local y producción
    /// (<c>galpones.galpon_id</c> es PK global) y hardcodearlos apuntaría a otra granja.
    /// </para>
    /// </summary>
    public partial class SeedLotePruebaPoblacionSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $mig$
                DECLARE
                    c_lote        CONSTANT varchar := 'SR-2025-01';
                    c_empresa     CONSTANT varchar := 'Santa Reyes';
                    c_granja      CONSTANT varchar := 'La Esperanza';
                    c_nucleo      CONSTANT varchar := 'Núcleo 1';
                    c_galpon      CONSTANT varchar := 'Galpón 2';
                    c_encaset     CONSTANT date    := DATE '2025-02-24';
                    c_hembras     CONSTANT integer := 20000;
                    c_raza        CONSTANT varchar := 'Hy Line Brown';
                    c_anio_guia   CONSTANT integer := 2026;
                    c_silos       CONSTANT varchar[] := ARRAY['Silo 1', 'Silo 2'];
                    -- Códigos ERP de catalogo_items (item_type = 'huevo') de la empresa.
                    c_huevos      CONSTANT varchar[] := ARRAY['2756', '528', '538', '537'];

                    v_company_id  integer;
                    v_pais_id     integer;
                    v_farm_id     integer;
                    v_nucleo_id   varchar;
                    v_galpon_id   varchar;
                    v_lote_id     integer;
                    v_faltan      integer;
                BEGIN
                    SELECT id INTO v_company_id
                      FROM public.companies WHERE name = c_empresa LIMIT 1;
                    IF v_company_id IS NULL THEN
                        RAISE NOTICE 'SeedLotePruebaPoblacionSantaReyes: no existe la empresa %, no se hace nada.', c_empresa;
                        RETURN;
                    END IF;

                    SELECT id INTO v_farm_id
                      FROM public.farms
                     WHERE company_id = v_company_id AND name = c_granja AND deleted_at IS NULL
                     ORDER BY id LIMIT 1;
                    IF v_farm_id IS NULL THEN
                        RAISE NOTICE 'SeedLotePruebaPoblacionSantaReyes: no existe la granja %, no se hace nada.', c_granja;
                        RETURN;
                    END IF;

                    SELECT nucleo_id INTO v_nucleo_id
                      FROM public.nucleos
                     WHERE granja_id = v_farm_id AND nucleo_nombre = c_nucleo
                     ORDER BY nucleo_id LIMIT 1;

                    SELECT galpon_id INTO v_galpon_id
                      FROM public.galpones
                     WHERE granja_id = v_farm_id AND nucleo_id = v_nucleo_id
                       AND galpon_nombre = c_galpon AND deleted_at IS NULL
                     ORDER BY galpon_id LIMIT 1;

                    IF v_nucleo_id IS NULL OR v_galpon_id IS NULL THEN
                        RAISE NOTICE 'SeedLotePruebaPoblacionSantaReyes: no existe % / % en la granja %, no se hace nada.',
                                     c_nucleo, c_galpon, c_granja;
                        RETURN;
                    END IF;

                    SELECT pais_id INTO v_pais_id
                      FROM public.paises WHERE lower(pais_nombre) = 'colombia' ORDER BY pais_id LIMIT 1;

                    -- ── 1) El lote ────────────────────────────────────────────────────────────
                    -- fecha_encaset a MEDIODÍA UTC: una fecha pura a medianoche se relee en el día
                    -- anterior con sesión en hora local, y el front calcularía la edad corrida un día.
                    INSERT INTO public.lotes
                           (lote_nombre, granja_id, nucleo_id, galpon_id, fecha_encaset,
                            hembras_l, machos_l, aves_encasetadas, raza, ano_tabla_genetica,
                            tipo_linea, fase, pais_id, pais_nombre, empresa_nombre,
                            company_id, created_by_user_id, created_at)
                    SELECT c_lote, v_farm_id, v_nucleo_id, v_galpon_id,
                           (c_encaset + TIME '12:00') AT TIME ZONE 'UTC',
                           c_hembras, NULL, c_hembras, c_raza, c_anio_guia,
                           'ROJA', 'Levante', v_pais_id, 'Colombia', c_empresa,
                           v_company_id, 1, now()
                     WHERE NOT EXISTS (SELECT 1 FROM public.lotes l
                                        WHERE l.company_id = v_company_id
                                          AND l.granja_id  = v_farm_id
                                          AND l.lote_nombre = c_lote
                                          AND l.deleted_at IS NULL);

                    SELECT lote_id INTO v_lote_id
                      FROM public.lotes
                     WHERE company_id = v_company_id AND granja_id = v_farm_id
                       AND lote_nombre = c_lote AND deleted_at IS NULL
                     ORDER BY lote_id LIMIT 1;

                    IF v_lote_id IS NULL THEN
                        RAISE EXCEPTION 'SeedLotePruebaPoblacionSantaReyes: el lote % no quedó creado.', c_lote;
                    END IF;

                    -- ── 2) Historial de etapa (lo crea LoteService.CreateAsync junto al lote) ──
                    INSERT INTO public.lote_etapa_levante
                           (lote_id, aves_inicio_hembras, aves_inicio_machos, fecha_inicio, created_at)
                    SELECT l.lote_id, COALESCE(l.hembras_l, 0), COALESCE(l.machos_l, 0),
                           COALESCE(l.fecha_encaset, now()), now()
                      FROM public.lotes l
                     WHERE l.lote_id = v_lote_id
                       AND NOT EXISTS (SELECT 1 FROM public.lote_etapa_levante e WHERE e.lote_id = l.lote_id);

                    -- ── 3) Espejo de levante ──────────────────────────────────────────────────
                    -- En una BD con el trigger trg_lotes_sync_lote_postura_levante ya existe (lo creó
                    -- el INSERT de arriba) y este bloque no hace nada; el INSERT es el respaldo para
                    -- un entorno sin ese trigger. Los acumuladores de traslado tienen DEFAULT 0.
                    INSERT INTO public.lote_postura_levante
                           (lote_nombre, granja_id, nucleo_id, galpon_id, fecha_encaset,
                            hembras_l, machos_l, aves_encasetadas, raza, ano_tabla_genetica, tipo_linea,
                            pais_id, pais_nombre, empresa_nombre, lote_id,
                            aves_h_inicial, aves_m_inicial, aves_h_actual, aves_m_actual,
                            empresa_id, usuario_id, estado, etapa, edad, estado_cierre,
                            company_id, created_by_user_id, created_at)
                    SELECT l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.fecha_encaset,
                           l.hembras_l, l.machos_l, l.aves_encasetadas, l.raza, l.ano_tabla_genetica, l.tipo_linea,
                           l.pais_id, l.pais_nombre, l.empresa_nombre, l.lote_id,
                           l.hembras_l, l.machos_l, l.hembras_l, l.machos_l,
                           l.company_id, l.created_by_user_id, l.fase, l.fase,
                           GREATEST(0, ((now() AT TIME ZONE 'utc')::date - (l.fecha_encaset AT TIME ZONE 'utc')::date) / 7),
                           'Abierto',
                           l.company_id, l.created_by_user_id, now()
                      FROM public.lotes l
                     WHERE l.lote_id = v_lote_id
                       AND NOT EXISTS (SELECT 1 FROM public.lote_postura_levante x WHERE x.lote_id = l.lote_id);

                    -- ── 4) Silos asignados al lote ────────────────────────────────────────────
                    -- La carga masiva valida el silo DOS veces: activo en la granja del lote y
                    -- presente acá (ConsumoSiloCalculos / lote_silos). Sin estas filas cada fila de
                    -- la hoja Datos se rechaza.
                    INSERT INTO public.lote_silos (company_id, lote_id, farm_silo_id, activo, created_at)
                    SELECT v_company_id, v_lote_id, fs.id, true, now()
                      FROM public.farm_silos fs
                     WHERE fs.granja_id = v_farm_id AND fs.deleted_at IS NULL AND fs.activo
                       AND fs.nombre = ANY (c_silos)
                       AND NOT EXISTS (SELECT 1 FROM public.lote_silos ls
                                        WHERE ls.lote_id = v_lote_id AND ls.farm_silo_id = fs.id);

                    SELECT cardinality(c_silos) - count(*) INTO v_faltan
                      FROM public.lote_silos ls
                      JOIN public.farm_silos fs ON fs.id = ls.farm_silo_id
                     WHERE ls.lote_id = v_lote_id AND fs.nombre = ANY (c_silos);
                    IF v_faltan > 0 THEN
                        RAISE WARNING 'SeedLotePruebaPoblacionSantaReyes: faltan % silo(s) de % en la granja %; la carga masiva va a rechazar esas filas.',
                                      v_faltan, c_silos, c_granja;
                    END IF;

                    -- ── 5) Tipos de huevo declarados por el lote (F7.3) ───────────────────────
                    -- La hoja "Huevos" SOLO acepta los ítems declarados acá; sin ellos ni siquiera se
                    -- emite en la plantilla y el archivo se rechaza entero.
                    INSERT INTO public.lote_huevo_items (company_id, lote_id, catalog_item_id, activo, created_at)
                    SELECT v_company_id, v_lote_id, ci.id, true, now()
                      FROM public.catalogo_items ci
                     WHERE ci.company_id = v_company_id AND ci.activo AND ci.item_type = 'huevo'
                       AND ci.codigo = ANY (c_huevos)
                       AND NOT EXISTS (SELECT 1 FROM public.lote_huevo_items lhi
                                        WHERE lhi.lote_id = v_lote_id AND lhi.catalog_item_id = ci.id);

                    SELECT cardinality(c_huevos) - count(*) INTO v_faltan
                      FROM public.lote_huevo_items lhi
                      JOIN public.catalogo_items ci ON ci.id = lhi.catalog_item_id
                     WHERE lhi.lote_id = v_lote_id AND ci.codigo = ANY (c_huevos);
                    IF v_faltan > 0 THEN
                        RAISE WARNING 'SeedLotePruebaPoblacionSantaReyes: faltan % ítem(s) de huevo de %; la hoja Huevos va a fallar.',
                                      v_faltan, c_huevos;
                    END IF;

                    RAISE NOTICE 'SeedLotePruebaPoblacionSantaReyes: lote % listo (lote_id %), granja % / % / %.',
                                 c_lote, v_lote_id, c_granja, c_nucleo, c_galpon;
                END
                $mig$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se borra sólo si el lote sigue VACÍO. Si alguien ya le importó el histórico, borrarlo
            // se llevaría por delante seguimientos, movimientos de inventario y el espejo de huevos;
            // en ese caso se deja y se avisa, que es lo reversible de verdad.
            migrationBuilder.Sql("""
                DO $mig$
                DECLARE
                    c_lote    CONSTANT varchar := 'SR-2025-01';
                    v_lote_id integer;
                    v_datos   bigint;
                BEGIN
                    SELECT l.lote_id INTO v_lote_id
                      FROM public.lotes l
                      JOIN public.companies c ON c.id = l.company_id AND c.name = 'Santa Reyes'
                     WHERE l.lote_nombre = c_lote AND l.deleted_at IS NULL
                     ORDER BY l.lote_id LIMIT 1;
                    IF v_lote_id IS NULL THEN RETURN; END IF;

                    SELECT (SELECT count(*) FROM public.seguimiento_diario_levante s
                             WHERE s.tipo_seguimiento = 'levante' AND s.lote_id = v_lote_id::text)
                         + (SELECT count(*) FROM public.seguimiento_diario_produccion s
                             WHERE s.lote_id = v_lote_id)
                      INTO v_datos;

                    IF v_datos > 0 THEN
                        RAISE WARNING 'SeedLotePruebaPoblacionSantaReyes (Down): el lote % ya tiene % registro(s) de seguimiento; NO se borra.',
                                      c_lote, v_datos;
                        RETURN;
                    END IF;

                    DELETE FROM public.lote_huevo_items      WHERE lote_id = v_lote_id;
                    DELETE FROM public.lote_silos            WHERE lote_id = v_lote_id;
                    DELETE FROM public.lote_etapa_levante    WHERE lote_id = v_lote_id;
                    DELETE FROM public.lote_postura_levante  WHERE lote_id = v_lote_id;
                    DELETE FROM public.lotes                 WHERE lote_id = v_lote_id;
                END
                $mig$;
                """);
        }
    }
}
