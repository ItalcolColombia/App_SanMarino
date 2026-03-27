-- Historial unificado por lote Ave Engorde: inventario EC (ingresos, traslados, consumos)
-- y movimientos de venta de aves (Movimiento Pollo Engorde), con triggers AFTER INSERT.
-- Ejecutar en PostgreSQL tras existir: lote_ave_engorde, inventario_gestion_movimiento,
-- item_inventario_ecuador, movimiento_pollo_engorde.

-- 1) Tabla destino
CREATE TABLE IF NOT EXISTS public.lote_registro_historico_unificado (
    id                          BIGSERIAL PRIMARY KEY,
    company_id                  INTEGER NOT NULL,
    lote_ave_engorde_id         INTEGER NULL,
    farm_id                     INTEGER NOT NULL,
    nucleo_id                   VARCHAR(64) NULL,
    galpon_id                   VARCHAR(64) NULL,
    fecha_operacion             DATE NOT NULL,
    tipo_evento                 VARCHAR(40) NOT NULL,
    -- Origen del registro: tabla + id (evita duplicados al re-ejecutar)
    origen_tabla                VARCHAR(80) NOT NULL,
    origen_id                   INTEGER NOT NULL,
    movement_type_original      VARCHAR(40) NULL,
    item_inventario_ecuador_id  INTEGER NULL,
    item_resumen                VARCHAR(400) NULL,
    cantidad_kg                 NUMERIC(18, 3) NULL,
    unidad                      VARCHAR(20) NULL,
    cantidad_hembras            INTEGER NULL,
    cantidad_machos             INTEGER NULL,
    cantidad_mixtas             INTEGER NULL,
    referencia                  VARCHAR(500) NULL,
    numero_documento            VARCHAR(200) NULL,
    -- Suma acumulada de kg que ENTRAN al lote (Ingreso + traslados entrada a esa ubicación)
    acumulado_entradas_alimento_kg NUMERIC(18, 3) NULL,
    anulado                     BOOLEAN NOT NULL DEFAULT FALSE,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_lote_hist_origen UNIQUE (origen_tabla, origen_id),
    CONSTRAINT fk_lote_hist_lote FOREIGN KEY (lote_ave_engorde_id)
        REFERENCES public.lote_ave_engorde (lote_ave_engorde_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_lote_hist_lote_fecha
    ON public.lote_registro_historico_unificado (lote_ave_engorde_id, fecha_operacion DESC);
CREATE INDEX IF NOT EXISTS ix_lote_hist_company_fecha
    ON public.lote_registro_historico_unificado (company_id, fecha_operacion DESC);
CREATE INDEX IF NOT EXISTS ix_lote_hist_tipo
    ON public.lote_registro_historico_unificado (tipo_evento);

COMMENT ON TABLE public.lote_registro_historico_unificado IS
'Historial unificado por lote: movimientos inventario EC (kg) y ventas de aves (H/M/X). '
'Alimentación: acumulado_entradas_alimento_kg solo suma entradas (ingreso + traslado entrada).';

-- 2) Resolver lote más reciente por ubicación (granja + núcleo + galpón)
CREATE OR REPLACE FUNCTION public.fn_lote_ave_engorde_id_desde_ubicacion(
    p_farm_id INTEGER,
    p_nucleo_id VARCHAR,
    p_galpon_id VARCHAR
) RETURNS INTEGER
LANGUAGE sql
STABLE
AS $$
    SELECT l.lote_ave_engorde_id
    FROM public.lote_ave_engorde l
    WHERE l.granja_id = p_farm_id
      AND COALESCE(TRIM(l.nucleo_id), '') = COALESCE(TRIM(p_nucleo_id), '')
      AND COALESCE(TRIM(l.galpon_id), '') = COALESCE(TRIM(p_galpon_id), '')
      AND l.deleted_at IS NULL
    ORDER BY l.lote_ave_engorde_id DESC
    LIMIT 1;
$$;

-- 3) Mapeo movement_type inventario -> tipo_evento interno
CREATE OR REPLACE FUNCTION public.fn_tipo_evento_inventario(p_mt VARCHAR) RETURNS VARCHAR
LANGUAGE plpgsql
IMMUTABLE
AS $$
BEGIN
    IF p_mt IS NULL THEN RETURN 'INV_OTRO'; END IF;
    IF p_mt ILIKE 'Ingreso' THEN RETURN 'INV_INGRESO'; END IF;
    IF p_mt ILIKE 'TrasladoEntrada' OR p_mt ILIKE 'TrasladoInterGranjaEntrada' THEN RETURN 'INV_TRASLADO_ENTRADA'; END IF;
    IF p_mt ILIKE 'TrasladoSalida' OR p_mt ILIKE 'TrasladoInterGranjaSalida'
       OR p_mt ILIKE 'TrasladoInterGranjaPendiente' THEN RETURN 'INV_TRASLADO_SALIDA'; END IF;
    IF p_mt ILIKE 'Consumo' THEN RETURN 'INV_CONSUMO'; END IF;
    RETURN 'INV_OTRO';
END;
$$;

-- 4) Acumulado de kg entrados al lote hasta esta fila (solo tipos de entrada)
CREATE OR REPLACE FUNCTION public.fn_acumulado_entradas_alimento(
    p_lote_id INTEGER,
    p_hasta_id BIGINT
) RETURNS NUMERIC
LANGUAGE sql
STABLE
AS $$
    SELECT COALESCE(SUM(h.cantidad_kg), 0)::NUMERIC(18, 3)
    FROM public.lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.anulado = FALSE
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA')
      AND h.id <= p_hasta_id;
$$;

-- 5) Trigger: cada INSERT en inventario_gestion_movimiento
CREATE OR REPLACE FUNCTION public.trg_lote_hist_desde_inventario_gestion()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_lote INTEGER;
    v_tipo VARCHAR(40);
    v_item_txt VARCHAR(400);
    v_acum NUMERIC(18, 3);
    v_hist_id BIGINT;
BEGIN
    v_lote := public.fn_lote_ave_engorde_id_desde_ubicacion(
        NEW.farm_id, NEW.nucleo_id, NEW.galpon_id
    );
    v_tipo := public.fn_tipo_evento_inventario(NEW.movement_type);

    SELECT CONCAT(i.codigo, ' — ', i.nombre)
    INTO v_item_txt
    FROM public.item_inventario_ecuador i
    WHERE i.id = NEW.item_inventario_ecuador_id;

    INSERT INTO public.lote_registro_historico_unificado (
        company_id, lote_ave_engorde_id, farm_id, nucleo_id, galpon_id,
        fecha_operacion, tipo_evento, origen_tabla, origen_id,
        movement_type_original, item_inventario_ecuador_id, item_resumen,
        cantidad_kg, unidad, referencia, numero_documento,
        acumulado_entradas_alimento_kg
    ) VALUES (
        NEW.company_id,
        v_lote,
        NEW.farm_id,
        NEW.nucleo_id,
        NEW.galpon_id,
        (NEW.created_at AT TIME ZONE 'UTC')::DATE,
        v_tipo,
        'inventario_gestion_movimiento',
        NEW.id,
        NEW.movement_type,
        NEW.item_inventario_ecuador_id,
        v_item_txt,
        NEW.quantity,
        NEW.unit,
        NEW.reference,
        NULL,
        NULL
    )
    RETURNING id INTO v_hist_id;

    IF v_lote IS NOT NULL AND v_tipo IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA') THEN
        v_acum := public.fn_acumulado_entradas_alimento(v_lote, v_hist_id);
        UPDATE public.lote_registro_historico_unificado
        SET acumulado_entradas_alimento_kg = v_acum
        WHERE id = v_hist_id;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_inventario_gestion_movimiento_lote_hist ON public.inventario_gestion_movimiento;
CREATE TRIGGER trg_inventario_gestion_movimiento_lote_hist
    AFTER INSERT ON public.inventario_gestion_movimiento
    FOR EACH ROW
    EXECUTE PROCEDURE public.trg_lote_hist_desde_inventario_gestion();

-- 6) Trigger: venta de aves (Movimiento Pollo Engorde) — hembras / machos / mixtas
CREATE OR REPLACE FUNCTION public.trg_lote_hist_desde_movimiento_pollo_engorde()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_fecha DATE;
    v_farm INTEGER;
    v_nuc VARCHAR(64);
    v_gal VARCHAR(64);
BEGIN
    IF NEW.deleted_at IS NOT NULL THEN
        RETURN NEW;
    END IF;

    IF NEW.tipo_movimiento IS DISTINCT FROM 'Venta' THEN
        RETURN NEW;
    END IF;

    IF NEW.lote_ave_engorde_origen_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT l.granja_id, l.nucleo_id, l.galpon_id
    INTO v_farm, v_nuc, v_gal
    FROM public.lote_ave_engorde l
    WHERE l.lote_ave_engorde_id = NEW.lote_ave_engorde_origen_id
      AND l.deleted_at IS NULL;

    IF v_farm IS NULL THEN
        v_farm := NEW.granja_origen_id;
        v_nuc := NEW.nucleo_origen_id;
        v_gal := NEW.galpon_origen_id;
    END IF;

    IF v_farm IS NULL THEN
        RETURN NEW;
    END IF;

    v_fecha := (NEW.fecha_movimiento AT TIME ZONE 'UTC')::DATE;

    INSERT INTO public.lote_registro_historico_unificado (
        company_id, lote_ave_engorde_id, farm_id, nucleo_id, galpon_id,
        fecha_operacion, tipo_evento, origen_tabla, origen_id,
        movement_type_original, item_inventario_ecuador_id, item_resumen,
        cantidad_kg, unidad,
        cantidad_hembras, cantidad_machos, cantidad_mixtas,
        referencia, numero_documento, acumulado_entradas_alimento_kg
    ) VALUES (
        NEW.company_id,
        NEW.lote_ave_engorde_origen_id,
        v_farm,
        v_nuc,
        v_gal,
        v_fecha,
        'VENTA_AVES',
        'movimiento_pollo_engorde',
        NEW.id,
        NEW.tipo_movimiento,
        NULL,
        NULL,
        NULL,
        NULL,
        NEW.cantidad_hembras,
        NEW.cantidad_machos,
        NEW.cantidad_mixtas,
        CONCAT('Mov. ', NEW.numero_movimiento),
        NEW.numero_despacho,
        NULL
    )
    ON CONFLICT (origen_tabla, origen_id) DO UPDATE SET
        fecha_operacion = EXCLUDED.fecha_operacion,
        cantidad_hembras = EXCLUDED.cantidad_hembras,
        cantidad_machos = EXCLUDED.cantidad_machos,
        cantidad_mixtas = EXCLUDED.cantidad_mixtas,
        referencia = EXCLUDED.referencia,
        numero_documento = EXCLUDED.numero_documento,
        farm_id = EXCLUDED.farm_id,
        nucleo_id = EXCLUDED.nucleo_id,
        galpon_id = EXCLUDED.galpon_id,
        lote_ave_engorde_id = EXCLUDED.lote_ave_engorde_id;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_movimiento_pollo_engorde_lote_hist ON public.movimiento_pollo_engorde;
CREATE TRIGGER trg_movimiento_pollo_engorde_lote_hist
    AFTER INSERT OR UPDATE OF cantidad_hembras, cantidad_machos, cantidad_mixtas,
        fecha_movimiento, tipo_movimiento, numero_despacho,
        lote_ave_engorde_origen_id, granja_origen_id, nucleo_origen_id, galpon_origen_id
    ON public.movimiento_pollo_engorde
    FOR EACH ROW
    EXECUTE PROCEDURE public.trg_lote_hist_desde_movimiento_pollo_engorde();

CREATE OR REPLACE FUNCTION public.trg_lote_hist_mov_pollo_anulado()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.tipo_movimiento IS DISTINCT FROM 'Venta' THEN
        RETURN NEW;
    END IF;
    IF NEW.estado IS DISTINCT FROM 'Anulado' AND NEW.deleted_at IS NULL THEN
        RETURN NEW;
    END IF;
    UPDATE public.lote_registro_historico_unificado
    SET anulado = TRUE
    WHERE origen_tabla = 'movimiento_pollo_engorde'
      AND origen_id = NEW.id;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_movimiento_pollo_engorde_lote_hist_anula ON public.movimiento_pollo_engorde;
CREATE TRIGGER trg_movimiento_pollo_engorde_lote_hist_anula
    AFTER UPDATE OF estado, deleted_at ON public.movimiento_pollo_engorde
    FOR EACH ROW
    WHEN (NEW.estado = 'Anulado' OR NEW.deleted_at IS NOT NULL)
    EXECUTE PROCEDURE public.trg_lote_hist_mov_pollo_anulado();
