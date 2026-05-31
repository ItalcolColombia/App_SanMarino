using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPesoIndividualSeguimientoEngorde : Migration
    {
        // R3.5 — Peso INDIVIDUAL de la venta en el Seguimiento Diario.
        // 1) columnas de peso en la tabla historial unificada
        // 2) trigger desde movimiento_pollo_engorde copia el peso individual (corrección de trigger)
        // 3) backfill de ventas existentes
        // 4) fn_seguimiento_diario_engorde re-creada exponiendo el peso individual por fecha.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ALTER_SQL);
            migrationBuilder.Sql(TRIGGER_SQL);
            migrationBuilder.Sql(BACKFILL_SQL);
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_seguimiento_diario_engorde(INT);", suppressTransaction: true);
            migrationBuilder.Sql(FN_SQL, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op funcional: revertir las columnas dejaría el trigger y la función en estado
            // inconsistente. Para rollback, re-aplicar manualmente la versión previa de la función
            // y el trigger, y opcionalmente DROP de las columnas de peso.
        }

        private const string ALTER_SQL = @"ALTER TABLE public.lote_registro_historico_unificado
    ADD COLUMN IF NOT EXISTS peso_neto numeric(18,3) NULL,
    ADD COLUMN IF NOT EXISTS peso_tara_real numeric(18,3) NULL,
    ADD COLUMN IF NOT EXISTS promedio_peso_ave numeric(18,4) NULL;";

        private const string BACKFILL_SQL = @"UPDATE public.lote_registro_historico_unificado u
SET peso_neto = m.peso_neto, peso_tara_real = m.peso_tara_real, promedio_peso_ave = m.promedio_peso_ave
FROM public.movimiento_pollo_engorde m
WHERE u.origen_tabla = 'movimiento_pollo_engorde' AND u.origen_id = m.id AND u.tipo_evento = 'VENTA_AVES';";

        private const string TRIGGER_SQL = @"CREATE OR REPLACE FUNCTION public.trg_lote_hist_desde_movimiento_pollo_engorde()
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
        peso_neto, peso_tara_real, promedio_peso_ave,
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
        NEW.peso_neto,
        NEW.peso_tara_real,
        NEW.promedio_peso_ave,
        CONCAT('Mov. ', NEW.numero_movimiento),
        NEW.numero_despacho,
        NULL
    )
    ON CONFLICT (origen_tabla, origen_id) DO UPDATE SET
        fecha_operacion = EXCLUDED.fecha_operacion,
        cantidad_hembras = EXCLUDED.cantidad_hembras,
        cantidad_machos = EXCLUDED.cantidad_machos,
        cantidad_mixtas = EXCLUDED.cantidad_mixtas,
        peso_neto = EXCLUDED.peso_neto,
        peso_tara_real = EXCLUDED.peso_tara_real,
        promedio_peso_ave = EXCLUDED.promedio_peso_ave,
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
        peso_neto, peso_tara_real, promedio_peso_ave,
        fecha_movimiento, tipo_movimiento, numero_despacho,
        lote_ave_engorde_origen_id, granja_origen_id, nucleo_origen_id, galpon_origen_id
    ON public.movimiento_pollo_engorde
    FOR EACH ROW
    EXECUTE PROCEDURE public.trg_lote_hist_desde_movimiento_pollo_engorde();";

        private const string FN_SQL = @"-- =============================================================================
-- fn_seguimiento_diario_engorde(p_lote_id INT)
-- Devuelve la tabla diaria de seguimiento de un lote de pollo engorde.
-- Tabla fuente: seguimiento_diario_aves_engorde
--   (tabla original — migración SplitByCountry del 20260517 no aplicada en la BD)
--
-- v2 (2026-05-28) — Plan: fase_de_desarrollo/10_fix_fn_seguimiento_diario_engorde_saldos.md
--   * Fix bug crítico: `rango_seg` ahora cast a ::DATE (la comparación DATE >= TIMESTAMPTZ
--     con hora 12:00 ocultaba el ingreso del PRIMER día del seguimiento).
--   * `INV_TRASLADO_SALIDA` ahora usa ABS() para coincidir con backend (Math.Abs).
--   * Nuevo CTE `apertura_alimento`: saldo inicial del galpón antes del primer seguimiento.
--   * `saldo_alimento_kg` se calcula DINÁMICAMENTE en SQL (apertura + SUM acumulado − consumo seg)
--     con piso 0 — la función ya no depende del valor persistido por el backend.
--   * `consumo_bodega_kg` = `consumo_dia_kg` (consumo real del seguimiento, no INV_CONSUMO
--     ambiguo del histórico que puede pertenecer a otros lotes del mismo galpón).
--
-- v1 (2026-05-20):
--   * Numéricos continuos → DOUBLE PRECISION (elimina trailing zeros: 123.000 → 123)
--   * documento: solo INV_INGRESO (scope galpón) + VENTA_AVES (scope lote), exactamente
--     igual al frontend: (h.numeroDocumento?.trim() || h.referencia?.trim())
--   * CTE docs_por_fecha separado de hist_alimento (que mantiene solo los kg)
-- =============================================================================

CREATE OR REPLACE FUNCTION fn_seguimiento_diario_engorde(p_lote_id INT)
RETURNS TABLE (
    -- Identificación
    seg_id                      BIGINT,
    fecha                       DATE,
    -- Tiempo
    edad_dia                    INT,
    semana                      SMALLINT,
    -- Seguimiento crudo
    mortalidad_hembras          INT,
    mortalidad_machos           INT,
    sel_h                       INT,
    sel_m                       INT,
    error_sexaje_hembras        INT,
    error_sexaje_machos         INT,
    -- Calculados simples
    total_mort_sel_dia          INT,
    perdidas_totales_dia        INT,
    consumo_kg_hembras          DOUBLE PRECISION,
    consumo_kg_machos           DOUBLE PRECISION,
    consumo_dia_kg              DOUBLE PRECISION,
    -- Acumulados corrientes (window functions)
    acum_consumo_kg             DOUBLE PRECISION,
    saldo_aves                  INT,
    pct_perdidas_dia            DOUBLE PRECISION,
    -- Saldo alimento persistido por RecalcularSaldoAlimentoPorLoteAsync
    saldo_alimento_kg           DOUBLE PRECISION,
    -- Histórico agregado por fecha
    ingreso_alimento_kg         DOUBLE PRECISION,
    traslado_entrada_kg         DOUBLE PRECISION,
    traslado_salida_kg          DOUBLE PRECISION,
    consumo_bodega_kg           DOUBLE PRECISION,
    -- Documento: numeroDocumento || referencia de INV_INGRESO + VENTA_AVES
    documento                   TEXT,
    despacho_hembras            INT,
    despacho_machos             INT,
    despacho_mixtas             INT,
    -- Peso INDIVIDUAL real de la venta de ESTE lote en la fecha (R3.5), no el global de factura
    despacho_peso_neto          DOUBLE PRECISION,
    despacho_peso_tara          DOUBLE PRECISION,
    despacho_promedio_peso_ave  DOUBLE PRECISION,
    -- Mediciones del seguimiento
    tipo_alimento               TEXT,
    peso_prom_hembras           DOUBLE PRECISION,
    peso_prom_machos            DOUBLE PRECISION,
    uniformidad_hembras         DOUBLE PRECISION,
    uniformidad_machos          DOUBLE PRECISION,
    cv_hembras                  DOUBLE PRECISION,
    cv_machos                   DOUBLE PRECISION,
    consumo_agua_diario         DOUBLE PRECISION,
    consumo_agua_ph             DOUBLE PRECISION,
    consumo_agua_orp            DOUBLE PRECISION,
    consumo_agua_temperatura    DOUBLE PRECISION,
    observaciones               TEXT,
    ciclo                       TEXT,
    metadata                    JSONB,
    items_adicionales           JSONB,
    historico_consumo_alimento  JSONB,
    created_by_user_id          TEXT
) LANGUAGE sql STABLE AS $$

WITH

-- 1. Datos clave del lote
lote_info AS (
    SELECT
        l.granja_id,
        COALESCE(TRIM(l.nucleo_id), '')  AS nucleo_id,
        COALESCE(TRIM(l.galpon_id), '')  AS galpon_id,
        l.fecha_encaset,
        COALESCE(l.aves_encasetadas, 0)  AS aves_encasetadas,
        COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0) AS suma_hm,
        LOWER(COALESCE(l.estado_operativo_lote, '')) AS estado_operativo_lote
    FROM lote_ave_engorde l
    WHERE l.lote_ave_engorde_id = p_lote_id
      AND l.deleted_at IS NULL
),

-- 2. Rango de fechas del ciclo (para acotar el histórico)
--    ⚠️ FIX v2: castear a ::DATE.
--    ⚠️ FIX v4 (plan #14): para lotes ABIERTOS, fecha_max es NULL → no se aplica
--    tope superior, así los movs post-último-seg aparecen también en la tabla. Para
--    lotes CERRADOS se mantiene MAX(seg.fecha) para no incluir movs del lote siguiente.
rango_seg AS (
    SELECT
        MIN(s.fecha)::DATE AS fecha_min,
        CASE
            WHEN (SELECT LOWER(COALESCE(estado_operativo_lote, ''))
                  FROM lote_ave_engorde
                  WHERE lote_ave_engorde_id = p_lote_id
                    AND deleted_at IS NULL) = 'cerrado'
            THEN MAX(s.fecha)::DATE
            ELSE NULL
        END AS fecha_max
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 3. Bajas totales en seguimiento
salidas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) +
        COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) +
        COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0)
    ), 0) AS bajas_seguimiento
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 4. Ventas totales de aves (VENTA_AVES)
ventas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)
    ), 0) AS total_ventas
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
),

-- 5. Aves iniciales (espejo exacto de avesInicialesLote() del frontend)
aves_iniciales AS (
    SELECT
        CASE
            WHEN li.estado_operativo_lote = 'cerrado'
                THEN GREATEST(1, st.bajas_seguimiento + vt.total_ventas)
            WHEN li.aves_encasetadas > 0 AND li.suma_hm = 0 THEN li.aves_encasetadas
            WHEN li.suma_hm > 0 AND li.aves_encasetadas = 0 THEN li.suma_hm
            WHEN li.aves_encasetadas = li.suma_hm              THEN li.aves_encasetadas
            ELSE li.aves_encasetadas
        END AS inicial
    FROM lote_info li
    CROSS JOIN salidas_totales st
    CROSS JOIN ventas_totales vt
),

-- 6. Ventas VENTA_AVES por fecha (despachos y saldo aves)
ventas_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        COALESCE(SUM(
            COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)
        ), 0)                                                                          AS ventas_dia,
        COALESCE(SUM(COALESCE(h.cantidad_hembras, 0)), 0)                             AS despacho_h,
        COALESCE(SUM(COALESCE(h.cantidad_machos,  0)), 0)                             AS despacho_m,
        COALESCE(SUM(COALESCE(h.cantidad_mixtas,  0)), 0)                             AS despacho_x,
        COALESCE(SUM(COALESCE(h.peso_neto,      0)), 0)::FLOAT8                        AS despacho_peso_neto,
        COALESCE(SUM(COALESCE(h.peso_tara_real, 0)), 0)::FLOAT8                        AS despacho_peso_tara
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
    GROUP BY DATE(h.fecha_operacion)
),

-- 7. kg de alimento por fecha (scope: granja + nucleo + galpon)
--    Excluye: VENTA_AVES, devoluciones por eliminación, INV_INGRESO de sistema de seguimiento
--    ⚠️ FIX v2: `INV_TRASLADO_SALIDA` con ABS() (puede venir negativo en datos sucios — el
--    backend hace -Math.Abs(kg); replicamos para coincidencia exacta del saldo).
hist_alimento AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_INGRESO'
             AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
            THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END), 0)::FLOAT8                  AS ingreso_kg,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA'
            THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END), 0)::FLOAT8                  AS traslado_entrada_kg,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'
            THEN ABS(COALESCE(h.cantidad_kg, 0)) ELSE 0 END), 0)::FLOAT8             AS traslado_salida_kg
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
      AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max)
    GROUP BY DATE(h.fecha_operacion)
),

-- 7b. Saldo de apertura del galpón ANTES del primer seguimiento (v2)
--     Replica `ComputeSaldoAperturaGalponAntesPrimerSeguimiento` del backend.
--     ⚠️ FIX v3 (2026-05-28, plan #12): filtrado por `fecha_encaset` del lote.
--     Antes la apertura incluía movimientos del LOTE ANTERIOR que ocupó el mismo
--     galpón (ej. lote 75/2602: heredaba 132,277 kg → saldo día 1 137,557 vs.
--     esperado 5,280). Ahora solo cuenta movimientos desde el encaset del lote
--     actual; el galpón se considera ""limpio"" antes de esa fecha.
apertura_alimento AS (
    SELECT COALESCE(SUM(
        CASE h.tipo_evento
            WHEN 'INV_INGRESO'          THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_ENTRADA' THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
            ELSE 0
        END
    ), 0)::FLOAT8 AS apertura_kg
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON rs.fecha_min IS NOT NULL
    WHERE NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND NOT (h.tipo_evento = 'INV_INGRESO'
               AND h.referencia IS NOT NULL
               AND h.referencia LIKE 'Seguimiento aves engorde #%')
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND DATE(h.fecha_operacion) < rs.fecha_min
      AND (li.fecha_encaset IS NULL
           OR DATE(h.fecha_operacion) >= li.fecha_encaset::DATE)
),

-- 8. Documento por fecha: exactamente como el frontend
--    → (h.numeroDocumento?.trim() || h.referencia?.trim())
--    Solo eventos:
--      - INV_INGRESO (scope galpón, excluye devoluciones de sistema)
--      - VENTA_AVES  (scope lote)
docs_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        STRING_AGG(
            DISTINCT NULLIF(TRIM(COALESCE(h.numero_documento, h.referencia, '')), ''),
            ', '
        )                                                                              AS documento
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
      AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max)
      AND (
          -- INV_INGRESO sin devoluciones de sistema → scope galpón
          (h.tipo_evento = 'INV_INGRESO'
           AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id)
          OR
          -- VENTA_AVES → scope lote
          (h.tipo_evento = 'VENTA_AVES'
           AND h.lote_ave_engorde_id = p_lote_id)
      )
    GROUP BY DATE(h.fecha_operacion)
),

-- 8b. ⚠️ FIX v4 (plan #14): UNIVERSO DE FECHAS = fechas con seguimiento ∪ fechas con
--     movimientos del histórico (alimento o ventas). Esto permite mostrar en la tabla
--     fechas donde solo hubo un ingreso/traslado/venta pero el usuario aún no creó
--     un seguimiento diario. Antes esas fechas quedaban ocultas.
fechas_universo AS (
    -- Fechas con seguimiento (siempre se incluyen)
    SELECT DATE(s.fecha) AS fecha, s.id AS seg_id
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
    UNION ALL
    -- Fechas con movimientos PERO sin seguimiento (no duplicar fechas ya incluidas arriba)
    SELECT DATE(h.fecha_operacion) AS fecha, NULL::BIGINT AS seg_id
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (
          -- Movs de alimento scope galpón
          (h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
           AND NOT (h.tipo_evento = 'INV_INGRESO'
                    AND h.referencia IS NOT NULL
                    AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id)
          OR
          -- Ventas scope lote
          (h.tipo_evento = 'VENTA_AVES' AND h.lote_ave_engorde_id = p_lote_id)
      )
      -- No incluir fechas pre-encaset
      AND (li.fecha_encaset IS NULL OR DATE(h.fecha_operacion) >= li.fecha_encaset::DATE)
      -- Acotar al rango del ciclo (si hay seguimientos)
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
      AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max)
      -- Excluir fechas que YA tienen seguimiento (evitar duplicados)
      AND NOT EXISTS (
          SELECT 1 FROM seguimiento_diario_aves_engorde s2
          WHERE s2.lote_ave_engorde_id = p_lote_id
            AND DATE(s2.fecha) = DATE(h.fecha_operacion)
      )
    GROUP BY DATE(h.fecha_operacion)
),

-- 9. Seguimiento enriquecido con todos los campos calculados.
--    ⚠️ FIX v4: parte de fechas_universo (no de seguimiento). Cuando no hay seg
--    los campos del seguimiento son NULL/0; los movs del histórico siguen visibles.
seg_enriquecido AS (
    SELECT
        s.id                                                                           AS seg_id,
        fu.fecha                                                                       AS fecha,
        -- Edad y semana (siempre desde fecha_encaset, aunque no haya seg)
        CASE WHEN li.fecha_encaset IS NOT NULL
             THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
             ELSE 0 END                                                                AS edad_dia,
        LEAST(8, GREATEST(1,
            CEIL((CASE WHEN li.fecha_encaset IS NOT NULL
                       THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
                       ELSE 0 END + 1) / 7.0)
        ))::SMALLINT                                                                   AS semana,
        -- Crudos del seguimiento (NULL/0 si no hay seg en esa fecha)
        COALESCE(s.mortalidad_hembras,   0)                                            AS mortalidad_hembras,
        COALESCE(s.mortalidad_machos,    0)                                            AS mortalidad_machos,
        COALESCE(s.sel_h,                0)                                            AS sel_h,
        COALESCE(s.sel_m,                0)                                            AS sel_m,
        COALESCE(s.error_sexaje_hembras, 0)                                            AS error_sexaje_hembras,
        COALESCE(s.error_sexaje_machos,  0)                                            AS error_sexaje_machos,
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
            + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)                              AS total_mort_sel_dia,
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
            + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)
            + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_totales_dia,
        COALESCE(s.consumo_kg_hembras, 0)::FLOAT8                                      AS consumo_kg_hembras,
        COALESCE(s.consumo_kg_machos,  0)::FLOAT8                                      AS consumo_kg_machos,
        (COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS consumo_dia_kg,
        s.saldo_alimento_kg::FLOAT8                                                    AS saldo_alimento_kg,
        s.tipo_alimento,
        s.peso_prom_hembras::FLOAT8                                                    AS peso_prom_hembras,
        s.peso_prom_machos::FLOAT8                                                     AS peso_prom_machos,
        s.uniformidad_hembras::FLOAT8                                                  AS uniformidad_hembras,
        s.uniformidad_machos::FLOAT8                                                   AS uniformidad_machos,
        s.cv_hembras::FLOAT8                                                           AS cv_hembras,
        s.cv_machos::FLOAT8                                                            AS cv_machos,
        s.consumo_agua_diario::FLOAT8                                                  AS consumo_agua_diario,
        s.consumo_agua_ph::FLOAT8                                                      AS consumo_agua_ph,
        s.consumo_agua_orp::FLOAT8                                                     AS consumo_agua_orp,
        s.consumo_agua_temperatura::FLOAT8                                             AS consumo_agua_temperatura,
        s.observaciones,
        s.ciclo,
        s.metadata,
        s.items_adicionales,
        s.historico_consumo_alimento,
        s.created_by_user_id,
        COALESCE(vpf.ventas_dia, 0)                                                    AS ventas_dia,
        COALESCE(vpf.despacho_h, 0)                                                    AS despacho_h,
        COALESCE(vpf.despacho_m, 0)                                                    AS despacho_m,
        COALESCE(vpf.despacho_x, 0)                                                    AS despacho_x,
        COALESCE(vpf.despacho_peso_neto, 0)                                            AS despacho_peso_neto,
        COALESCE(vpf.despacho_peso_tara, 0)                                            AS despacho_peso_tara,
        COALESCE(ha.ingreso_kg,          0)                                            AS ingreso_alimento_kg,
        COALESCE(ha.traslado_entrada_kg, 0)                                            AS traslado_entrada_kg,
        COALESCE(ha.traslado_salida_kg,  0)                                            AS traslado_salida_kg,
        dpf.documento
    FROM fechas_universo fu
    CROSS JOIN lote_info li
    LEFT JOIN seguimiento_diario_aves_engorde s ON s.id = fu.seg_id
    LEFT JOIN ventas_por_fecha vpf ON vpf.fecha = fu.fecha
    LEFT JOIN hist_alimento    ha  ON ha.fecha  = fu.fecha
    LEFT JOIN docs_por_fecha   dpf ON dpf.fecha = fu.fecha
)

-- 10. Query final: window functions para acumulados corrientes
SELECT
    se.seg_id,
    se.fecha,
    se.edad_dia,
    se.semana,
    se.mortalidad_hembras,
    se.mortalidad_machos,
    se.sel_h,
    se.sel_m,
    se.error_sexaje_hembras,
    se.error_sexaje_machos,
    se.total_mort_sel_dia,
    se.perdidas_totales_dia,
    se.consumo_kg_hembras,
    se.consumo_kg_machos,
    se.consumo_dia_kg,
    -- Acumulado consumo
    SUM(se.consumo_dia_kg) OVER w_ord                                                 AS acum_consumo_kg,
    -- Saldo aves con piso-0
    GREATEST(0,
        ai.inicial - SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_ord
    )::INT                                                                             AS saldo_aves,
    -- % pérdidas (denominador = saldo al inicio del día = saldo_aves de la fila anterior)
    CASE
        WHEN GREATEST(0,
            ai.inicial - COALESCE(SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0)
        ) > 0
        THEN (100.0 * se.total_mort_sel_dia /
            GREATEST(0,
                ai.inicial - COALESCE(SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0)
            ))::FLOAT8
        WHEN se.total_mort_sel_dia > 0 THEN 100.0::FLOAT8
        ELSE NULL
    END                                                                                AS pct_perdidas_dia,
    -- ⚠️ FIX v3 (plan #12, segundo bug): el SUM OVER se basaba en `se.ingreso_alimento_kg` que viene
    -- del LEFT JOIN con hist_alimento → pierde movimientos en días sin seguimiento. Ahora usamos
    -- subconsulta correlacionada sobre hist_alimento que agrega TODOS los movimientos del rango
    -- (con o sin seguimiento) hasta la fecha del registro actual. El consumo del seguimiento
    -- sigue acumulándose con SUM OVER. Resultado: saldo coincide con RecalcularSaldoAlimentoPorLoteAsync.
    GREATEST(0,
        (SELECT apertura_kg FROM apertura_alimento)
        + COALESCE((SELECT SUM(ha2.ingreso_kg + ha2.traslado_entrada_kg - ha2.traslado_salida_kg)
                    FROM hist_alimento ha2
                    WHERE ha2.fecha <= se.fecha), 0)
        - SUM(se.consumo_dia_kg) OVER w_ord
    )::FLOAT8                                                                          AS saldo_alimento_kg,
    se.ingreso_alimento_kg,
    se.traslado_entrada_kg,
    se.traslado_salida_kg,
    -- ⚠️ FIX v2: consumo_bodega_kg ahora refleja el consumo del seguimiento (no INV_CONSUMO histórico
    -- que es ambiguo y puede pertenecer a otros lotes del mismo galpón).
    se.consumo_dia_kg                                                                  AS consumo_bodega_kg,
    se.documento,
    se.despacho_h  AS despacho_hembras,
    se.despacho_m  AS despacho_machos,
    se.despacho_x  AS despacho_mixtas,
    se.despacho_peso_neto                                                             AS despacho_peso_neto,
    se.despacho_peso_tara                                                             AS despacho_peso_tara,
    CASE WHEN (se.despacho_h + se.despacho_m + se.despacho_x) > 0
         THEN se.despacho_peso_neto / (se.despacho_h + se.despacho_m + se.despacho_x)
         ELSE 0 END                                                                   AS despacho_promedio_peso_ave,
    se.tipo_alimento,
    se.peso_prom_hembras,
    se.peso_prom_machos,
    se.uniformidad_hembras,
    se.uniformidad_machos,
    se.cv_hembras,
    se.cv_machos,
    se.consumo_agua_diario,
    se.consumo_agua_ph,
    se.consumo_agua_orp,
    se.consumo_agua_temperatura,
    se.observaciones,
    se.ciclo,
    se.metadata,
    se.items_adicionales,
    se.historico_consumo_alimento,
    se.created_by_user_id
FROM seg_enriquecido se
CROSS JOIN aves_iniciales ai
WINDOW
    -- FIX v4: seg_id puede ser NULL (movs sin seguimiento) → COALESCE para orden estable
    w_ord  AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    w_prev AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
ORDER BY se.fecha, COALESCE(se.seg_id, 0);
$$;";
    }
}
