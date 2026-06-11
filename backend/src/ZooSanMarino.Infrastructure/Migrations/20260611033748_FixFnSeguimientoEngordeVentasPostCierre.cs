using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// v7 de fn_seguimiento_diario_engorde: las VENTA_AVES del lote ya no se acotan por
    /// fecha_min/fecha_max en fechas_universo ni docs_por_fecha (solo por fecha_encaset).
    /// En lotes cerrados aves_iniciales = bajas + ventas_totales (sin tope de fecha); una
    /// venta posterior al corte por alimento=0 (v5) inflaba el inicial sin fila que la
    /// restara, dejando saldo_aves residual fantasma (caso lote 23 "2601": venta de 8
    /// machos el 2026-05-13 con corte 2026-03-18 mostraba saldo 8 en vez de 0).
    /// Idempotente: CREATE OR REPLACE. SQL sincronizado con backend/sql/fn_seguimiento_diario_engorde.sql.
    /// </summary>
    public partial class FixFnSeguimientoEngordeVentasPostCierre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FN_SQL, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La función queda en v7. Para revertirla, re-aplicar el SQL de
            // 20260531194613_FixFnSeguimientoEngordeM1SaldoAlimento (v6).
        }

        private const string FN_SQL = @"
-- =============================================================================
-- fn_seguimiento_diario_engorde(p_lote_id INT)
-- Devuelve la tabla diaria de seguimiento de un lote de pollo engorde.
-- Tabla fuente: seguimiento_diario_aves_engorde
--
-- v7 (2026-06-10) — Fix: VENTA_AVES fuera del rango [fecha_min, fecha_max].
--   * Problema: en lotes CERRADOS `aves_iniciales = bajas + ventas_totales` (cierre en 0
--     por construcción) y `ventas_totales` NO tiene tope de fecha, pero `fechas_universo`
--     SÍ acotaba por fecha_max (cierre por alimento=0, v5). Una venta posterior al corte
--     (ej. lote 23: venta de 8 machos el 2026-05-13, reapertura ""PARA TRASLADO"", con corte
--     2026-03-18) inflaba `inicial` sin fila que la restara → saldo_aves residual fantasma
--     (el lote ""cerrado"" mostraba saldo 8 en vez de 0).
--   * Solución: en `fechas_universo` y `docs_por_fecha` las VENTA_AVES del lote NO se
--     acotan por fecha_min/fecha_max (solo por fecha_encaset). Las ventas pertenecen al
--     lote (no al galpón), no hay riesgo de contaminación de otro ciclo. Los eventos de
--     alimento (scope galpón) conservan el tope v5. La venta tardía aparece como fila
--     propia y el saldo del lote cerrado cierra en 0.
--
-- v6 (2026-05-31) — Fix: saldo_alimento_kg con PISO 0 + RESETEO DE BASE (modelo M1).
--   * Problema: la fn calculaba el saldo como GREATEST(0, acumulado) sobre el cumulativo
--     desde apertura (modelo M0). Eso ARRASTRABA el déficit transitorio: cuando el consumo
--     se adelantaba a un ingreso registrado tarde, el acumulado se volvía negativo, se
--     mostraba 0, y al llegar el ingreso ""rebotaba"" → el usuario lo veía como ""no cuadra""
--     (caso lote 75, 29→30 may-2026). Además, M0 NO coincidía con la lógica canónica del
--     frontend (computeSaldoAlimentoKgPorSeguimiento) ni del backend C#
--     (RecalcularSaldoAlimentoPorLoteAsync), que SÍ usan piso 0 con reseteo.
--   * Solución: nuevo CTE `pt_calc` expone P_t (saldo SIN piso). La columna final aplica la
--     forma cerrada de la recursión de Lindley:  saldo_t = P_t − LEAST(0, MIN(P_t) acumulado).
--     Equivale a saldo_t = max(0, saldo_{t-1} + ingreso − consumo). Alinea la fn con front/back.
--   * Nota: para lotes sanos (que nunca van a negativo) M1 == M0. Solo cambia los que dipean.
--
-- v5 (2026-05-30) — Fix: corte por CIERRE EFECTIVO de alimento.
--   * Problema: lotes que terminaron de hecho (alimento llegó a 0 al cerrar) pero NO
--     se marcaron 'Cerrado' tenían fecha_max=NULL, así la tabla mostraba ingresos de
--     alimento del SIGUIENTE ciclo del galpón (fechas que no aplican al lote).
--   * Solución: nuevo CTE `saldo_close` detecta la primera fecha >= último seguimiento
--     donde el saldo de alimento llega a 0 (lote vaciado). `fecha_max` se acota ahí,
--     sin depender del flag estado_operativo_lote. Se incluye ESE registro de cierre
--     (la salida que deja el alimento en 0) y se excluye todo lo posterior.
--   * Fallback: si el saldo nunca llega a 0 tras el último seg → lote 'cerrado' usa
--     MAX(seg) (comportamiento previo); lote 'abierto' usa NULL (sigue mostrando movs).
--
-- v4 (plan #14): fechas_universo = seguimientos ∪ movimientos; fecha_max NULL en abiertos.
-- v3 (plan #12): apertura filtrada por fecha_encaset.
-- v2 (2026-05-28): rango_seg ::DATE; INV_TRASLADO_SALIDA con ABS(); saldo dinámico.
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

-- 2. Rango base del ciclo: primer y último seguimiento + estado
rango_seg AS (
    SELECT
        MIN(s.fecha)::DATE AS fecha_min,
        MAX(s.fecha)::DATE AS last_seg,
        (SELECT LOWER(COALESCE(estado_operativo_lote, ''))
         FROM lote_ave_engorde
         WHERE lote_ave_engorde_id = p_lote_id AND deleted_at IS NULL) AS estado
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 3. Saldo de apertura del galpón ANTES del primer seguimiento (v2/v3).
--    ⭐ v6: PISO 0 POR PASO (Lindley), alineado con el frontend/C# (la apertura nunca abre
--    en negativo: una salida sin ingreso previo suficiente se clampa a 0). Antes era un SUM
--    plano que podía dar apertura negativa y desalinear el saldo vs la pantalla (ej. lote 7).
apert_mov AS (
    SELECT DATE(h.fecha_operacion) AS f, h.created_at AS ts,
        CASE h.tipo_evento
            WHEN 'INV_INGRESO'          THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_ENTRADA' THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
            ELSE 0
        END AS delta
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
apert_run AS (
    SELECT
        SUM(delta) OVER (ORDER BY f, ts ROWS UNBOUNDED PRECEDING) AS p,
        ROW_NUMBER()    OVER (ORDER BY f DESC, ts DESC)           AS rn_desc
    FROM apert_mov
),
apertura_alimento AS (
    -- saldo de apertura pisado = P final − LEAST(0, MIN(P))  (forma cerrada de Lindley).
    SELECT COALESCE(
        (SELECT p FROM apert_run WHERE rn_desc = 1)
        - LEAST(0, (SELECT MIN(p) FROM apert_run))
    , 0)::FLOAT8 AS apertura_kg
),

-- 3b. ⭐ v5: movimientos de alimento del galpón por fecha, SIN tope superior
--     (para detectar la fecha de cierre = saldo a 0). Neto firmado igual que el saldo.
hist_full AS (
    SELECT
        DATE(h.fecha_operacion) AS fecha,
        SUM(CASE
            WHEN h.tipo_evento = 'INV_INGRESO'
             AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
                 THEN COALESCE(h.cantidad_kg, 0)
            WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA' THEN COALESCE(h.cantidad_kg, 0)
            WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
            ELSE 0
        END)::FLOAT8 AS neto_kg
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON TRUE
    WHERE NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
    GROUP BY DATE(h.fecha_operacion)
),

-- 3c. Consumo del seguimiento por fecha
consumo_por_fecha AS (
    SELECT DATE(s.fecha) AS fecha,
           SUM(COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS cons_kg
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
    GROUP BY DATE(s.fecha)
),

-- 3d. ⭐ v5: saldo de alimento corriente (misma fórmula que la columna saldo_alimento_kg)
--     evaluado sobre TODO el histórico (sin tope) para detectar el cierre.
saldo_running AS (
    SELECT sf.fecha,
        GREATEST(0,
            (SELECT apertura_kg FROM apertura_alimento)
            + COALESCE(SUM(hf.neto_kg) OVER (ORDER BY sf.fecha ROWS UNBOUNDED PRECEDING), 0)
            - COALESCE(SUM(cf.cons_kg) OVER (ORDER BY sf.fecha ROWS UNBOUNDED PRECEDING), 0)
        ) AS saldo
    FROM (SELECT fecha FROM hist_full UNION SELECT fecha FROM consumo_por_fecha) sf
    LEFT JOIN hist_full          hf ON hf.fecha = sf.fecha
    LEFT JOIN consumo_por_fecha  cf ON cf.fecha = sf.fecha
),

-- 3e. ⭐ v5: fecha de cierre = primera fecha >= último seguimiento con saldo en 0
--     (lote vaciado de alimento). NULL si el saldo nunca llega a 0 (lote aún activo).
saldo_close AS (
    SELECT MIN(sr.fecha) AS close_date
    FROM saldo_running sr, rango_seg rs
    WHERE rs.last_seg IS NOT NULL
      AND sr.fecha >= rs.last_seg
      AND sr.saldo <= 0.5
),

-- 4. ⭐ v5: rango final. fecha_max = cierre efectivo (saldo 0) o, si no lo hay,
--    MAX(seg) para lotes 'cerrado' (fallback) o NULL para 'abierto' aún activo.
rango_final AS (
    SELECT
        rs.fecha_min,
        COALESCE(
            sc.close_date,
            CASE WHEN rs.estado = 'cerrado' THEN rs.last_seg ELSE NULL END
        ) AS fecha_max
    FROM rango_seg rs, saldo_close sc
),

-- 5. Bajas totales en seguimiento
salidas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) +
        COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) +
        COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0)
    ), 0) AS bajas_seguimiento
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 6. Ventas totales de aves (VENTA_AVES)
ventas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)
    ), 0) AS total_ventas
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
),

-- 7. Aves iniciales (espejo de avesInicialesLote() del frontend)
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

-- 8. Ventas VENTA_AVES por fecha (despachos y saldo aves)
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

-- 9. kg de alimento por fecha (scope: granja + nucleo + galpon), acotado a rango_final
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
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
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

-- 10. Documento por fecha (INV_INGRESO scope galpón + VENTA_AVES scope lote), acotado a rango_final
docs_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        STRING_AGG(
            DISTINCT NULLIF(TRIM(COALESCE(h.numero_documento, h.referencia, '')), ''),
            ', '
        )                                                                              AS documento
    FROM lote_registro_historico_unificado h
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (
          (h.tipo_evento = 'INV_INGRESO'
           AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
           AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
           AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max))
          OR
          -- ⭐ v7: VENTA_AVES del lote SIN tope fecha_min/fecha_max (ver cabecera).
          (h.tipo_evento = 'VENTA_AVES'
           AND h.lote_ave_engorde_id = p_lote_id)
      )
    GROUP BY DATE(h.fecha_operacion)
),

-- 11. UNIVERSO DE FECHAS = fechas con seguimiento ∪ fechas con movimientos (acotado a rango_final)
fechas_universo AS (
    SELECT DATE(s.fecha) AS fecha, s.id AS seg_id
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
    UNION ALL
    SELECT DATE(h.fecha_operacion) AS fecha, NULL::BIGINT AS seg_id
    FROM lote_registro_historico_unificado h
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (
          (h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
           AND NOT (h.tipo_evento = 'INV_INGRESO'
                    AND h.referencia IS NOT NULL
                    AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
           AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
           AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max))
          OR
          -- ⭐ v7: VENTA_AVES del lote SIN tope fecha_min/fecha_max → toda venta (incluso
          -- posterior al cierre por alimento) genera su fila y el saldo cierra en 0.
          (h.tipo_evento = 'VENTA_AVES' AND h.lote_ave_engorde_id = p_lote_id)
      )
      AND (li.fecha_encaset IS NULL OR DATE(h.fecha_operacion) >= li.fecha_encaset::DATE)
      AND NOT EXISTS (
          SELECT 1 FROM seguimiento_diario_aves_engorde s2
          WHERE s2.lote_ave_engorde_id = p_lote_id
            AND DATE(s2.fecha) = DATE(h.fecha_operacion)
      )
    GROUP BY DATE(h.fecha_operacion)
),

-- 12. Seguimiento enriquecido
seg_enriquecido AS (
    SELECT
        s.id                                                                           AS seg_id,
        fu.fecha                                                                       AS fecha,
        CASE WHEN li.fecha_encaset IS NOT NULL
             THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
             ELSE 0 END                                                                AS edad_dia,
        LEAST(8, GREATEST(1,
            CEIL((CASE WHEN li.fecha_encaset IS NOT NULL
                       THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
                       ELSE 0 END + 1) / 7.0)
        ))::SMALLINT                                                                   AS semana,
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
),

-- 12b. ⭐ v6 (M1): P_t = saldo SIN piso (apertura + ingresos acumulados − consumo acumulado).
--      Se materializa aquí para poder tomar MIN(P_t) acumulado en el SELECT final (la
--      forma cerrada de Lindley necesita una ventana sobre otra ventana).
pt_calc AS (
    SELECT se.*,
        (
            (SELECT apertura_kg FROM apertura_alimento)
            + COALESCE((SELECT SUM(ha2.ingreso_kg + ha2.traslado_entrada_kg - ha2.traslado_salida_kg)
                        FROM hist_alimento ha2
                        WHERE ha2.fecha <= se.fecha), 0)
            - SUM(se.consumo_dia_kg) OVER (ORDER BY se.fecha, COALESCE(se.seg_id, 0)
                                           ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)
        )::FLOAT8 AS pt
    FROM seg_enriquecido se
)

-- 13. Query final
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
    SUM(se.consumo_dia_kg) OVER w_ord                                                 AS acum_consumo_kg,
    GREATEST(0,
        ai.inicial - SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_ord
    )::INT                                                                             AS saldo_aves,
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
    -- ⭐ v6 (M1): piso 0 con reseteo de base (Lindley). Alinea la fn con la lógica canónica
    -- del frontend (computeSaldoAlimentoKgPorSeguimiento) y del backend C#
    -- (RecalcularSaldoAlimentoPorLoteAsync): saldo_t = max(0, saldo_{t-1} + ingreso − consumo).
    -- Forma cerrada: P_t − LEAST(0, MIN(P_t) acumulado). ""Olvida"" el déficit transitorio de timing.
    (se.pt - LEAST(0, MIN(se.pt) OVER w_ord))::FLOAT8                                  AS saldo_alimento_kg,
    se.ingreso_alimento_kg,
    se.traslado_entrada_kg,
    se.traslado_salida_kg,
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
FROM pt_calc se
CROSS JOIN aves_iniciales ai
WINDOW
    w_ord  AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    w_prev AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
ORDER BY se.fecha, COALESCE(se.seg_id, 0);
$$;
";
    }
}
