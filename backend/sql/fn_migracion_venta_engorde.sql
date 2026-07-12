-- =============================================================================
-- Migración masiva de VENTAS de pollo de engorde (histórico).
-- Inserta el movimiento en estado 'Completado' (con su numero_movimiento definitivo,
-- pre-asignando el id desde la secuencia para que el trigger de histórico capture la
-- referencia correcta), y descuenta el contador del lote UNA sola vez (espeja CompleteAsync).
-- El trigger trg_movimiento_pollo_engorde_lote_hist escribe el histórico VENTA_AVES en
-- lote_registro_historico_unificado automáticamente en el INSERT (NO se toca acá).
-- Idempotente: misma venta (empresa + lote + fecha + cantidades) no se duplica.
-- p_rows = jsonb array de filas ya validadas por el backend.
-- =============================================================================
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
    v_total      integer;
    v_neto       double precision;
BEGIN
    FOR f IN
        SELECT * FROM jsonb_to_recordset(p_rows) AS x(
            lote_id       integer,
            fecha         date,
            cant_h        integer,
            cant_m        integer,
            cant_x        integer,
            motivo        text,
            observaciones text,
            peso_bruto    double precision,
            peso_tara     double precision,
            edad_aves     integer,
            raza          text,
            placa         text
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

        -- Idempotencia: misma venta ya cargada (lote + fecha + cantidades) → se omite.
        IF EXISTS (
            SELECT 1 FROM public.movimiento_pollo_engorde m
             WHERE m.company_id = p_company_id
               AND m.tipo_movimiento = 'Venta'
               AND m.lote_ave_engorde_origen_id = f.lote_id
               AND m.fecha_movimiento = f.fecha::timestamptz
               AND m.cantidad_hembras = COALESCE(f.cant_h, 0)
               AND m.cantidad_machos  = COALESCE(f.cant_m, 0)
               AND m.cantidad_mixtas  = COALESCE(f.cant_x, 0)
               AND m.deleted_at IS NULL
        ) THEN
            CONTINUE;
        END IF;

        v_total := COALESCE(f.cant_h, 0) + COALESCE(f.cant_m, 0) + COALESCE(f.cant_x, 0);
        v_neto  := CASE WHEN f.peso_bruto IS NOT NULL AND f.peso_tara IS NOT NULL
                        THEN f.peso_bruto - f.peso_tara ELSE NULL END;

        -- Pre-asignar el id para construir numero_movimiento definitivo y que el trigger de
        -- histórico grabe la referencia correcta ya en el INSERT (numero_movimiento no está en
        -- la lista UPDATE OF del trigger, por eso no se puede corregir con un UPDATE posterior).
        v_id := nextval(pg_get_serial_sequence('public.movimiento_pollo_engorde', 'id'));

        INSERT INTO public.movimiento_pollo_engorde (
            id, numero_movimiento, fecha_movimiento, tipo_movimiento,
            lote_ave_engorde_origen_id, granja_origen_id, nucleo_origen_id, galpon_origen_id,
            cantidad_hembras, cantidad_machos, cantidad_mixtas,
            motivo_movimiento, observaciones, estado,
            usuario_movimiento_id, fecha_procesamiento,
            edad_aves, raza, placa,
            peso_bruto, peso_tara, peso_neto, promedio_peso_ave,
            company_id, created_by_user_id, created_at
        ) VALUES (
            v_id,
            'MPE-' || to_char(f.fecha, 'YYYYMMDD') || '-' || lpad(v_id::text, 6, '0'),
            f.fecha::timestamptz, 'Venta',
            f.lote_id, v_granja_id, v_nucleo_id, v_galpon_id,
            COALESCE(f.cant_h, 0), COALESCE(f.cant_m, 0), COALESCE(f.cant_x, 0),
            f.motivo, f.observaciones, 'Completado',
            0, f.fecha::timestamptz,
            f.edad_aves, f.raza, f.placa,
            f.peso_bruto, f.peso_tara, v_neto,
            CASE WHEN v_neto IS NOT NULL AND v_total > 0 THEN v_neto / v_total ELSE NULL END,
            p_company_id, p_usuario, (NOW() AT TIME ZONE 'utc')
        );

        -- Descuento del contador del lote UNA vez (espeja CompleteAsync). GREATEST(0,...) respeta
        -- el check ck_lae_nonneg_counts.
        UPDATE public.lote_ave_engorde
           SET hembras_l  = GREATEST(0, COALESCE(hembras_l, 0) - COALESCE(f.cant_h, 0)),
               machos_l   = GREATEST(0, COALESCE(machos_l, 0)  - COALESCE(f.cant_m, 0)),
               mixtas     = GREATEST(0, COALESCE(mixtas, 0)    - COALESCE(f.cant_x, 0)),
               updated_at = (NOW() AT TIME ZONE 'utc')
         WHERE lote_ave_engorde_id = f.lote_id;

        v_insertados := v_insertados + 1;
    END LOOP;

    RETURN v_insertados;
END;
$$;
