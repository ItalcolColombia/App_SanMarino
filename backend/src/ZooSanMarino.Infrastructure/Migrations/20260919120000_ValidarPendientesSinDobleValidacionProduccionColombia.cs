using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only, sin cambio de modelo): VALIDA los seguimientos de PRODUCCIÓN que
    /// quedaron pendientes con alimento y aves separados en una empresa cuya doble validación ya está
    /// APAGADA.
    ///
    /// <para>
    /// <b>Qué pasó.</b> Santa Reyes tuvo la doble validación encendida hasta el 18-sep-2026 y se apagó con
    /// 6 registros pendientes (#676–#681, del 13/09: 7.011 kg de alimento y 259 aves separados). Con el
    /// flag apagado la pantalla oculta el botón de validar, así que quedaron colgados: su consumo nunca
    /// salió del inventario. El apagado ahora valida antes los pendientes (<c>CompanyService.UpdateAsync</c>);
    /// esta migración corrige los que ya quedaron.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué SQL y no el servicio.</b> Lo único que corre en producción son las migraciones EF
    /// (<c>Database__RunMigrations=true</c>) y una migración no puede invocar servicios C#. Por eso este
    /// bloque REPLICA <c>ValidacionSeguimientoService.ValidarAsync</c> para el único caso que existe —
    /// PRODUCCIÓN, Colombia, modelo B a nivel granja— y se comprobó contra la API con una prueba diferencial
    /// (mismo conjunto de filas resultante en movimientos, stock, reservas, registros e histórico).
    /// </para>
    ///
    /// <para>
    /// <b>Qué hace por registro</b> (en orden cronológico, en un sub-bloque con <c>EXCEPTION</c>: si algo
    /// falla se deshace SOLO ese registro y la migración sigue; nunca aborta el arranque):
    /// </para>
    /// <list type="number">
    ///   <item>«Marcar primero»: <c>validado = true</c> con <c>WHERE validado = false</c>
    ///     (<c>TomarValidacionAsync</c>), <c>validado_por = 'migracion'</c>.</item>
    ///   <item>Alimento, agrupando las reservas por (ítem, tipo de ítem, silo) como <c>AItemConsumo</c>:
    ///     el ítem del catálogo se resuelve por CÓDIGO a <c>item_inventario</c> de la empresa dueña de la
    ///     granja y país 1 (<c>ColombiaInventarioIdResolutionCalculos</c>); si la empresa maneja silos, el
    ///     silo es obligatorio y debe ser un silo activo de la granja (<c>ConsumoSiloCalculos</c>); el
    ///     stock es el de nivel granja <c>(granja, ítem, núcleo NULL, galpón NULL, silo)</c> y se descuenta
    ///     con la sentencia atómica de <c>DescontarStockAtomicoAsync</c> (<c>quantity &gt;= q</c>: sin saldo,
    ///     el registro se omite); y se escribe el movimiento <c>Consumo</c> con la referencia
    ///     <c>Seguimiento producción #&lt;id&gt; &lt;fecha&gt; (validado)</c>, fechado el DÍA DEL SEGUIMIENTO a las
    ///     18:00 UTC (<c>AnclaConsumoUtc</c>), empresa y país de la GRANJA, unidad del ítem.</item>
    ///   <item>Reservas de alimento y de aves: ACTIVA → APLICADA con <c>aplicada_at</c>. En producción no
    ///     se mueven aves: el saldo lo manda <c>fn_seguimiento_diario_produccion</c>, que ya cuenta las bajas
    ///     de todas las filas sin mirar <c>validado</c> (<c>AplicarAvesAsync</c>).</item>
    /// </list>
    ///
    /// <para>
    /// <b>Criterio de selección (todas a la vez):</b> registro de producción <c>validado = false</c>, no
    /// borrado, de una empresa con <c>requiere_validacion_seguimiento_diario = false</c> y con al menos una
    /// reserva ACTIVA (alimento o aves). Un registro con reservas de otro país se omite: usa otro camino de
    /// inventario (núcleo/galpón) que esta migración no replica.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente:</b> un registro validado sale del criterio ⇒ re-ejecutarla no hace nada. <b>Robusta al
    /// stock del despliegue:</b> el stock de ese día no es el de la copia con la que se probó; sin saldo el
    /// registro queda pendiente (y <c>backend/sql/verificar_validado_sin_reserva.sql</c> lo lista), nunca se
    /// deja el stock en negativo. Sin reversa: deshacerla es <c>DesvalidarAsync</c>, uno por uno.
    /// </para>
    /// </summary>
    public partial class ValidarPendientesSinDobleValidacionProduccionColombia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    reg          record;
    grupo        record;
    v_fecha      date;
    v_company    integer;
    v_pais       integer;
    v_por_silo   boolean;
    v_item_b     integer;
    v_unidad     varchar;
    v_stock_id   integer;
    v_stock_qty  numeric;
    v_ref        text;
    v_validados  integer := 0;
    v_omitidos   integer := 0;
BEGIN
    FOR reg IN
        SELECT s.id
          FROM public.seguimiento_diario_produccion s
          JOIN public.companies c ON c.id = s.company_id
         WHERE s.validado = false
           AND s.deleted_at IS NULL
           AND c.requiere_validacion_seguimiento_diario = false
           AND (   EXISTS (SELECT 1 FROM public.seguimiento_reserva_alimento r
                            WHERE r.origen_modulo = 'PRODUCCION' AND r.origen_seguimiento_id = s.id AND r.estado = 'ACTIVA')
                OR EXISTS (SELECT 1 FROM public.seguimiento_reserva_aves r
                            WHERE r.origen_modulo = 'PRODUCCION' AND r.origen_seguimiento_id = s.id AND r.estado = 'ACTIVA'))
         ORDER BY s.fecha_registro, s.id
    LOOP
        BEGIN
            -- (1) Marcar primero, aplicar despues (TomarValidacionAsync).
            UPDATE public.seguimiento_diario_produccion
               SET validado = true, validado_at = now(), validado_por = 'migracion', updated_at = now()
             WHERE id = reg.id AND validado = false;
            IF NOT FOUND THEN
                CONTINUE;
            END IF;

            -- Fecha del seguimiento = la de la primera reserva de alimento (reservas[0].FechaSeguimiento).
            SELECT min(r.fecha_seguimiento) INTO v_fecha
              FROM public.seguimiento_reserva_alimento r
             WHERE r.origen_modulo = 'PRODUCCION' AND r.origen_seguimiento_id = reg.id AND r.estado = 'ACTIVA';

            -- (2) Alimento: una clave por (granja, silo, item, tipo de item), como AItemConsumo.
            FOR grupo IN
                SELECT r.pais_id, r.farm_id, r.nucleo_id, r.galpon_id, r.silo_id,
                       r.item_inventario_id, r.es_item_inventario, sum(r.cantidad_kg) AS kg
                  FROM public.seguimiento_reserva_alimento r
                 WHERE r.origen_modulo = 'PRODUCCION' AND r.origen_seguimiento_id = reg.id AND r.estado = 'ACTIVA'
                 GROUP BY r.pais_id, r.farm_id, r.nucleo_id, r.galpon_id, r.silo_id, r.item_inventario_id, r.es_item_inventario
                 ORDER BY r.farm_id, r.silo_id NULLS FIRST, r.item_inventario_id, r.es_item_inventario
            LOOP
                IF grupo.kg <= 0 THEN
                    CONTINUE;
                END IF;

                -- Solo Colombia (modelo B a nivel granja): los otros paises usan nucleo/galpon.
                IF grupo.pais_id IS DISTINCT FROM 1 OR grupo.nucleo_id IS NOT NULL OR grupo.galpon_id IS NOT NULL THEN
                    RAISE EXCEPTION 'modelo de inventario no soportado por la migracion (pais %, nucleo %, galpon %)',
                        grupo.pais_id, grupo.nucleo_id, grupo.galpon_id;
                END IF;

                -- Empresa y pais de la GRANJA (GetFarmCompanyAndPaisAsync), y como ubica su inventario.
                SELECT f.company_id, d.pais_id INTO v_company, v_pais
                  FROM public.farms f
                  LEFT JOIN public.departamentos d ON d.departamento_id = f.departamento_id
                 WHERE f.id = grupo.farm_id;
                IF v_company IS NULL OR v_company <= 0 OR v_pais IS NULL THEN
                    RAISE EXCEPTION 'no se pudo resolver la empresa o el pais de la granja %', grupo.farm_id;
                END IF;

                SELECT c.maneja_inventario_por_silo INTO v_por_silo FROM public.companies c WHERE c.id = v_company;

                -- Silo (ConsumoSiloCalculos.ValidarClaves): por silo => obligatorio y activo en la granja;
                -- clasico => no debe traer silo.
                IF coalesce(v_por_silo, false) THEN
                    IF grupo.silo_id IS NULL OR grupo.silo_id <= 0 THEN
                        RAISE EXCEPTION 'falta el silo del alimento (la empresa maneja el inventario por silo)';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM public.farm_silos fs
                                    WHERE fs.id = grupo.silo_id AND fs.granja_id = grupo.farm_id
                                      AND fs.deleted_at IS NULL AND fs.activo) THEN
                        RAISE EXCEPTION 'el silo % no esta activo en la granja %', grupo.silo_id, grupo.farm_id;
                    END IF;
                ELSIF grupo.silo_id IS NOT NULL THEN
                    RAISE EXCEPTION 'la empresa no maneja el inventario por silo pero la reserva trae el silo %', grupo.silo_id;
                END IF;

                -- Item del inventario (ColombiaInventarioIdResolutionCalculos): directo, o catalogo -> por CODIGO.
                v_item_b := NULL;
                v_unidad := NULL;
                IF grupo.es_item_inventario THEN
                    SELECT ii.id, ii.unidad INTO v_item_b, v_unidad
                      FROM public.item_inventario ii
                     WHERE ii.id = grupo.item_inventario_id AND ii.company_id = v_company AND ii.pais_id = 1;
                ELSE
                    SELECT ii.id, ii.unidad INTO v_item_b, v_unidad
                      FROM public.catalogo_items ci
                      JOIN public.item_inventario ii
                        ON ii.codigo = ci.codigo
                       AND ii.company_id = v_company AND ii.pais_id = 1
                     WHERE ci.id = grupo.item_inventario_id AND ci.codigo IS NOT NULL
                     ORDER BY ii.id
                     LIMIT 1;
                END IF;
                IF v_item_b IS NULL THEN
                    RAISE EXCEPTION 'el item % no tiene equivalente en el inventario de la empresa %',
                        grupo.item_inventario_id, v_company;
                END IF;

                -- Stock de nivel granja (farm, item, nucleo NULL, galpon NULL, silo).
                v_stock_id := NULL;
                v_stock_qty := NULL;
                SELECT st.id, st.quantity INTO v_stock_id, v_stock_qty
                  FROM public.inventario_gestion_stock st
                 WHERE st.farm_id = grupo.farm_id AND st.item_inventario_id = v_item_b
                   AND st.nucleo_id IS NULL AND st.galpon_id IS NULL
                   AND st.silo_id IS NOT DISTINCT FROM grupo.silo_id;
                IF v_stock_id IS NULL OR v_stock_qty < grupo.kg THEN
                    RAISE EXCEPTION 'stock insuficiente del item % en la granja % (silo %): disponible %, requerido %',
                        v_item_b, grupo.farm_id, grupo.silo_id, coalesce(v_stock_qty, 0), grupo.kg;
                END IF;

                -- Descuento ATOMICO (DescontarStockAtomicoAsync).
                UPDATE public.inventario_gestion_stock
                   SET quantity = quantity - grupo.kg, updated_at = now()
                 WHERE id = v_stock_id AND quantity >= grupo.kg;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'el stock cambio mientras se descontaba (item %, granja %)', v_item_b, grupo.farm_id;
                END IF;

                -- Movimiento de consumo (RegistrarConsumoNivelGranjaAsync). Referencia: ReferenciaInventario().
                v_ref := 'Seguimiento producción #' || reg.id || ' ' || to_char(v_fecha, 'YYYY-MM-DD') || ' (validado)';
                INSERT INTO public.inventario_gestion_movimiento
                    (company_id, pais_id, farm_id, nucleo_id, galpon_id, silo_id, item_inventario_id, quantity, unit,
                     movement_type, estado, reference, reason, created_at, created_by_user_id)
                VALUES
                    (v_company, v_pais, grupo.farm_id, NULL, NULL, grupo.silo_id, v_item_b, grupo.kg,
                     coalesce(nullif(btrim(v_unidad), ''), 'kg'),
                     'Consumo', 'Consumo', v_ref, NULL,
                     (v_fecha::timestamp + interval '18 hours') AT TIME ZONE 'UTC', NULL);
            END LOOP;

            -- (3) Reservas de alimento y de aves: ACTIVA -> APLICADA.
            UPDATE public.seguimiento_reserva_alimento
               SET estado = 'APLICADA', aplicada_at = now()
             WHERE origen_modulo = 'PRODUCCION' AND origen_seguimiento_id = reg.id AND estado = 'ACTIVA';
            UPDATE public.seguimiento_reserva_aves
               SET estado = 'APLICADA', aplicada_at = now()
             WHERE origen_modulo = 'PRODUCCION' AND origen_seguimiento_id = reg.id AND estado = 'ACTIVA';

            v_validados := v_validados + 1;
        EXCEPTION WHEN OTHERS THEN
            -- Se deshace SOLO este registro (sub-bloque): queda pendiente y el resto sigue.
            v_omitidos := v_omitidos + 1;
            RAISE NOTICE 'Validacion por migracion: produccion #% omitido: %', reg.id, SQLERRM;
        END;
    END LOOP;

    RAISE NOTICE 'Validacion por migracion de produccion: % validados, % omitidos', v_validados, v_omitidos;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin reversa a propósito: deshacer una validación devuelve el alimento registro por registro
            // (DesvalidarAsync) y, sobre lo que ya validaron los operarios, no se puede distinguir qué
            // registros validó esta migración salvo por `validado_por = 'migracion'`.
        }
    }
}
