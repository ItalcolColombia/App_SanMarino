using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// v2 de <c>fn_migracion_venta_engorde</c> (carga masiva de ventas de pollo engorde).
    /// <para>
    /// Novedades: despacho MULTI-LOTE (<c>factura_id</c> + <c>numero_despacho</c> y los 9 campos de
    /// peso ya prorrateados desde C# con la misma función pura de la venta por pantalla), campos de
    /// despacho completos, <c>estado</c> por fila ('Pendiente' no descuenta el lote: lo hará
    /// <c>CompleteAsync</c> al confirmar), descuento sobre MIXTAS cuando <c>es_venta_mixta</c>
    /// (espeja CompleteAsync) e idempotencia por RANGO DE DÍA + <c>numero_despacho</c>.
    /// </para>
    /// <para>
    /// La idempotencia vieja comparaba <c>fecha_movimiento = fecha::timestamptz</c> (medianoche)
    /// mientras la UI graba a MEDIODÍA UTC ⇒ recargar por Excel un día ya vendido por pantalla
    /// duplicaba la venta y descontaba el lote dos veces. Efecto colateral consciente: dos
    /// despachos legítimos del mismo lote/fecha/cantidades ya no colapsan si traen N° Despacho
    /// distinto.
    /// </para>
    /// <para>
    /// <c>CREATE OR REPLACE</c> ⇒ idempotente. NO se edita la migración original
    /// <c>20260712190000_AddFnMigracionVentaEngorde</c>, ya aplicada en prod.
    /// Fuente canónica: <c>backend/sql/fn_migracion_venta_engorde.sql</c>.
    /// </para>
    /// </summary>
    public partial class FnMigracionVentaEngordeV2Despachos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_migracion_venta_engorde(
    p_company_id integer,
    p_usuario    integer,
    p_rows       jsonb
) RETURNS integer
LANGUAGE plpgsql AS $$
DECLARE
    v_insertados integer := 0;
    f            RECORD;
    v_granja_id  integer;
    v_nucleo_id  varchar;
    v_galpon_id  varchar;
    v_id         integer;
    v_estado     text;
BEGIN
    FOR f IN
        SELECT * FROM jsonb_to_recordset(p_rows) AS x(
            lote_id             integer,
            fecha               date,
            cant_h              integer,
            cant_m              integer,
            cant_x              integer,
            motivo              text,
            observaciones       text,
            peso_bruto          double precision,
            peso_tara           double precision,
            peso_bruto_global   double precision,
            peso_tara_global    double precision,
            peso_neto_global    double precision,
            peso_bruto_real     double precision,
            peso_tara_real      double precision,
            peso_neto           double precision,
            promedio_peso_ave   double precision,
            edad_aves           integer,
            raza                text,
            placa               text,
            factura_id          uuid,
            numero_despacho     text,
            total_pollos_galpon integer,
            hora_salida         time,
            guia_agrocalidad    text,
            sellos              text,
            ayuno               text,
            conductor           text,
            planta_destino      text,
            descripcion         text,
            estado              text,
            es_venta_mixta      boolean
        )
    LOOP
        -- Lote de la empresa (scoping tenant); si no existe o es de otra empresa, se omite la fila.
        SELECT granja_id, nucleo_id, galpon_id
          INTO v_granja_id, v_nucleo_id, v_galpon_id
          FROM public.lote_ave_engorde
         WHERE lote_ave_engorde_id = f.lote_id
           AND company_id = p_company_id
           AND deleted_at IS NULL;
        IF NOT FOUND THEN
            CONTINUE;
        END IF;

        v_estado := COALESCE(NULLIF(f.estado, ''), 'Completado');

        -- Idempotencia: misma venta ya cargada → se omite. La fecha se compara por RANGO DE DÍA
        -- porque la UI graba a mediodía UTC y esta carga a medianoche: sin el rango, recargar por
        -- Excel un día ya vendido por pantalla duplicaría la venta y el descuento del lote.
        IF EXISTS (
            SELECT 1 FROM public.movimiento_pollo_engorde m
             WHERE m.company_id = p_company_id
               AND m.tipo_movimiento = 'Venta'
               AND m.lote_ave_engorde_origen_id = f.lote_id
               AND m.fecha_movimiento >= f.fecha::timestamptz
               AND m.fecha_movimiento <  (f.fecha + 1)::timestamptz
               AND m.cantidad_hembras = COALESCE(f.cant_h, 0)
               AND m.cantidad_machos  = COALESCE(f.cant_m, 0)
               AND m.cantidad_mixtas  = COALESCE(f.cant_x, 0)
               AND COALESCE(m.numero_despacho, '') = COALESCE(f.numero_despacho, '')
               AND m.deleted_at IS NULL
        ) THEN
            CONTINUE;
        END IF;

        -- Pre-asignar el id para construir numero_movimiento definitivo y que el trigger de
        -- histórico grabe la referencia correcta ya en el INSERT (numero_movimiento no está en
        -- la lista UPDATE OF del trigger, por eso no se puede corregir con un UPDATE posterior).
        v_id := nextval(pg_get_serial_sequence('public.movimiento_pollo_engorde', 'id'));

        INSERT INTO public.movimiento_pollo_engorde (
            id, numero_movimiento, fecha_movimiento, tipo_movimiento,
            lote_ave_engorde_origen_id, granja_origen_id, nucleo_origen_id, galpon_origen_id,
            cantidad_hembras, cantidad_machos, cantidad_mixtas,
            motivo_movimiento, descripcion, observaciones, estado,
            usuario_movimiento_id, fecha_procesamiento,
            edad_aves, raza, placa,
            factura_id, numero_despacho, total_pollos_galpon, hora_salida,
            guia_agrocalidad, sellos, ayuno, conductor, planta_destino,
            es_venta_mixta,
            peso_bruto, peso_tara,
            peso_bruto_global, peso_tara_global, peso_neto_global,
            peso_bruto_real, peso_tara_real, peso_neto, promedio_peso_ave,
            company_id, created_by_user_id, created_at
        ) VALUES (
            v_id,
            'MPE-' || to_char(f.fecha, 'YYYYMMDD') || '-' || lpad(v_id::text, 6, '0'),
            f.fecha::timestamptz, 'Venta',
            f.lote_id, v_granja_id, v_nucleo_id, v_galpon_id,
            COALESCE(f.cant_h, 0), COALESCE(f.cant_m, 0), COALESCE(f.cant_x, 0),
            f.motivo, f.descripcion, f.observaciones, v_estado,
            0,
            CASE WHEN v_estado = 'Completado' THEN f.fecha::timestamptz ELSE NULL END,
            f.edad_aves, f.raza, f.placa,
            f.factura_id, f.numero_despacho, f.total_pollos_galpon, f.hora_salida,
            f.guia_agrocalidad, f.sellos, f.ayuno, f.conductor, f.planta_destino,
            COALESCE(f.es_venta_mixta, false),
            f.peso_bruto, f.peso_tara,
            f.peso_bruto_global, f.peso_tara_global, f.peso_neto_global,
            f.peso_bruto_real, f.peso_tara_real, f.peso_neto, f.promedio_peso_ave,
            p_company_id, p_usuario, (NOW() AT TIME ZONE 'utc')
        );

        -- Descuento del contador del lote UNA vez, sólo si la venta nace 'Completado' (espeja
        -- CompleteAsync). Las 'Pendiente' descuentan al confirmarse desde la pantalla.
        -- GREATEST(0, ...) respeta el check ck_lae_nonneg_counts.
        IF v_estado = 'Completado' THEN
            IF COALESCE(f.es_venta_mixta, false) THEN
                -- Venta sobre mixtas (Panamá): las aves viven en hembras_l/machos_l y el campo
                -- mixtas no aplica ⇒ se fuerza a 0, igual que CompleteAsync.
                UPDATE public.lote_ave_engorde
                   SET hembras_l  = GREATEST(0, COALESCE(hembras_l, 0) - COALESCE(f.cant_h, 0)),
                       machos_l   = GREATEST(0, COALESCE(machos_l, 0)  - COALESCE(f.cant_m, 0)),
                       mixtas     = 0,
                       updated_at = (NOW() AT TIME ZONE 'utc')
                 WHERE lote_ave_engorde_id = f.lote_id;
            ELSE
                UPDATE public.lote_ave_engorde
                   SET hembras_l  = GREATEST(0, COALESCE(hembras_l, 0) - COALESCE(f.cant_h, 0)),
                       machos_l   = GREATEST(0, COALESCE(machos_l, 0)  - COALESCE(f.cant_m, 0)),
                       mixtas     = GREATEST(0, COALESCE(mixtas, 0)    - COALESCE(f.cant_x, 0)),
                       updated_at = (NOW() AT TIME ZONE 'utc')
                 WHERE lote_ave_engorde_id = f.lote_id;
            END IF;
        END IF;

        v_insertados := v_insertados + 1;
    END LOOP;

    RETURN v_insertados;
END;
$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La v1 se restaura re-aplicando 20260712190000; no se dropea la función para no
            // romper la carga masiva si se revierte solo esta migración.
        }
    }
}
