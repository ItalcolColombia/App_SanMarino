using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Migración del fix #14 (plan: fase_de_desarrollo/14_deploy_aws_y_movs_sin_seguimiento.md).
    /// Reemplaza la función `fn_seguimiento_diario_engorde` con la versión v4 que incluye:
    ///   * Fix #10: castear rango_seg a DATE, ABS() para INV_TRASLADO_SALIDA, saldo dinámico,
    ///     consumo_bodega_kg = consumo_dia_kg.
    ///   * Fix #12: apertura filtrada por fecha_encaset (no hereda lote anterior), subconsulta
    ///     correlacionada hist_alimento para movs en días sin seguimiento.
    ///   * Fix #14: para lotes ABIERTOS no aplica tope superior (movs post-último-seg visibles).
    ///     Nuevo CTE fechas_universo: UNION de fechas con seguimiento + fechas con movs sin seg.
    ///     Permite mostrar en la tabla los movimientos de alimento/venta sin que el usuario
    ///     tenga que crear un seguimiento ese día.
    /// Después de reemplazar la función, ejecuta el UPDATE masivo de saldo_alimento_kg en
    /// `seguimiento_diario_aves_engorde` para todos los lotes activos (idempotente: solo
    /// actualiza filas donde el persistido difiere del cálculo correcto).
    /// </summary>
    public partial class FixFnSeguimientoEngordeYRecalcularSaldosMasivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─────────────────────────────────────────────────────────────────────
            // 1. DROP explícito de la función previa (garantiza limpieza ante
            //    cualquier cambio de signatura / nombres de columnas RETURNS TABLE
            //    y evita errores "cannot change return type of existing function").
            //    Aunque CREATE OR REPLACE FUNCTION dentro de FN_V4_SQL ya remplaza
            //    el cuerpo, el DROP IF EXISTS hace el contrato explícito al deploy.
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS fn_seguimiento_diario_engorde(INT);",
                suppressTransaction: true);

            // ─────────────────────────────────────────────────────────────────────
            // 2. Crea la función fn_seguimiento_diario_engorde versión v4
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(FN_V4_SQL, suppressTransaction: true);

            // ─────────────────────────────────────────────────────────────────────
            // 3. Snapshot persistente + UPDATE masivo de saldo_alimento_kg
            //    Para auditoría y rollback de emergencia.
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(MIGRACION_MASIVA_SQL, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaurar saldos desde snapshot (si existe)
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables
               WHERE table_name = '_migracion_saldo_alimento_2026_05_28') THEN
        UPDATE seguimiento_diario_aves_engorde p
        SET saldo_alimento_kg = b.saldo_antes,
            updated_at = b.updated_at_antes
        FROM _migracion_saldo_alimento_2026_05_28 b
        WHERE p.id = b.seg_id;
    END IF;
END $$;
", suppressTransaction: true);

            // Eliminar la función (la migración previa AddFnSeguimientoDiarioEngorde la recreará en v1
            // si se hace re-up de las migraciones desde 20260520140828).
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_seguimiento_diario_engorde(INT);", suppressTransaction: true);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // SQL de la función fn_seguimiento_diario_engorde — versión v4
        // (sincronizado con backend/sql/fn_seguimiento_diario_engorde.sql)
        // ═════════════════════════════════════════════════════════════════════════
        private const string FN_V4_SQL = @"
CREATE OR REPLACE FUNCTION fn_seguimiento_diario_engorde(p_lote_id INT)
RETURNS TABLE (
    seg_id                      BIGINT,
    fecha                       DATE,
    edad_dia                    INT,
    semana                      SMALLINT,
    mortalidad_hembras          INT,
    mortalidad_machos           INT,
    sel_h                       INT,
    sel_m                       INT,
    error_sexaje_hembras        INT,
    error_sexaje_machos         INT,
    total_mort_sel_dia          INT,
    perdidas_totales_dia        INT,
    consumo_kg_hembras          DOUBLE PRECISION,
    consumo_kg_machos           DOUBLE PRECISION,
    consumo_dia_kg              DOUBLE PRECISION,
    acum_consumo_kg             DOUBLE PRECISION,
    saldo_aves                  INT,
    pct_perdidas_dia            DOUBLE PRECISION,
    saldo_alimento_kg           DOUBLE PRECISION,
    ingreso_alimento_kg         DOUBLE PRECISION,
    traslado_entrada_kg         DOUBLE PRECISION,
    traslado_salida_kg          DOUBLE PRECISION,
    consumo_bodega_kg           DOUBLE PRECISION,
    documento                   TEXT,
    despacho_hembras            INT,
    despacho_machos             INT,
    despacho_mixtas             INT,
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
salidas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) +
        COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) +
        COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0)
    ), 0) AS bajas_seguimiento
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),
ventas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)
    ), 0) AS total_ventas
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
),
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
ventas_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion) AS fecha,
        COALESCE(SUM(
            COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)
        ), 0) AS ventas_dia,
        COALESCE(SUM(COALESCE(h.cantidad_hembras, 0)), 0) AS despacho_h,
        COALESCE(SUM(COALESCE(h.cantidad_machos,  0)), 0) AS despacho_m,
        COALESCE(SUM(COALESCE(h.cantidad_mixtas,  0)), 0) AS despacho_x
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
    GROUP BY DATE(h.fecha_operacion)
),
hist_alimento AS (
    SELECT
        DATE(h.fecha_operacion) AS fecha,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_INGRESO'
             AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
            THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END), 0)::FLOAT8 AS ingreso_kg,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA'
            THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END), 0)::FLOAT8 AS traslado_entrada_kg,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'
            THEN ABS(COALESCE(h.cantidad_kg, 0)) ELSE 0 END), 0)::FLOAT8 AS traslado_salida_kg
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
docs_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion) AS fecha,
        STRING_AGG(
            DISTINCT NULLIF(TRIM(COALESCE(h.numero_documento, h.referencia, '')), ''),
            ', '
        ) AS documento
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
          (h.tipo_evento = 'INV_INGRESO'
           AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id)
          OR
          (h.tipo_evento = 'VENTA_AVES'
           AND h.lote_ave_engorde_id = p_lote_id)
      )
    GROUP BY DATE(h.fecha_operacion)
),
fechas_universo AS (
    SELECT DATE(s.fecha) AS fecha, s.id AS seg_id
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
    UNION ALL
    SELECT DATE(h.fecha_operacion) AS fecha, NULL::BIGINT AS seg_id
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON TRUE
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
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id)
          OR
          (h.tipo_evento = 'VENTA_AVES' AND h.lote_ave_engorde_id = p_lote_id)
      )
      AND (li.fecha_encaset IS NULL OR DATE(h.fecha_operacion) >= li.fecha_encaset::DATE)
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
      AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max)
      AND NOT EXISTS (
          SELECT 1 FROM seguimiento_diario_aves_engorde s2
          WHERE s2.lote_ave_engorde_id = p_lote_id
            AND DATE(s2.fecha) = DATE(h.fecha_operacion)
      )
    GROUP BY DATE(h.fecha_operacion)
),
seg_enriquecido AS (
    SELECT
        s.id AS seg_id,
        fu.fecha AS fecha,
        CASE WHEN li.fecha_encaset IS NOT NULL
             THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
             ELSE 0 END AS edad_dia,
        LEAST(8, GREATEST(1,
            CEIL((CASE WHEN li.fecha_encaset IS NOT NULL
                       THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
                       ELSE 0 END + 1) / 7.0)
        ))::SMALLINT AS semana,
        COALESCE(s.mortalidad_hembras,   0) AS mortalidad_hembras,
        COALESCE(s.mortalidad_machos,    0) AS mortalidad_machos,
        COALESCE(s.sel_h,                0) AS sel_h,
        COALESCE(s.sel_m,                0) AS sel_m,
        COALESCE(s.error_sexaje_hembras, 0) AS error_sexaje_hembras,
        COALESCE(s.error_sexaje_machos,  0) AS error_sexaje_machos,
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
            + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) AS total_mort_sel_dia,
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
            + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)
            + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_totales_dia,
        COALESCE(s.consumo_kg_hembras, 0)::FLOAT8 AS consumo_kg_hembras,
        COALESCE(s.consumo_kg_machos,  0)::FLOAT8 AS consumo_kg_machos,
        (COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS consumo_dia_kg,
        s.saldo_alimento_kg::FLOAT8 AS saldo_alimento_kg,
        s.tipo_alimento,
        s.peso_prom_hembras::FLOAT8 AS peso_prom_hembras,
        s.peso_prom_machos::FLOAT8 AS peso_prom_machos,
        s.uniformidad_hembras::FLOAT8 AS uniformidad_hembras,
        s.uniformidad_machos::FLOAT8 AS uniformidad_machos,
        s.cv_hembras::FLOAT8 AS cv_hembras,
        s.cv_machos::FLOAT8 AS cv_machos,
        s.consumo_agua_diario::FLOAT8 AS consumo_agua_diario,
        s.consumo_agua_ph::FLOAT8 AS consumo_agua_ph,
        s.consumo_agua_orp::FLOAT8 AS consumo_agua_orp,
        s.consumo_agua_temperatura::FLOAT8 AS consumo_agua_temperatura,
        s.observaciones,
        s.ciclo,
        s.metadata,
        s.items_adicionales,
        s.historico_consumo_alimento,
        s.created_by_user_id,
        COALESCE(vpf.ventas_dia, 0) AS ventas_dia,
        COALESCE(vpf.despacho_h, 0) AS despacho_h,
        COALESCE(vpf.despacho_m, 0) AS despacho_m,
        COALESCE(vpf.despacho_x, 0) AS despacho_x,
        COALESCE(ha.ingreso_kg,          0) AS ingreso_alimento_kg,
        COALESCE(ha.traslado_entrada_kg, 0) AS traslado_entrada_kg,
        COALESCE(ha.traslado_salida_kg,  0) AS traslado_salida_kg,
        dpf.documento
    FROM fechas_universo fu
    CROSS JOIN lote_info li
    LEFT JOIN seguimiento_diario_aves_engorde s ON s.id = fu.seg_id
    LEFT JOIN ventas_por_fecha vpf ON vpf.fecha = fu.fecha
    LEFT JOIN hist_alimento    ha  ON ha.fecha  = fu.fecha
    LEFT JOIN docs_por_fecha   dpf ON dpf.fecha = fu.fecha
)
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
    SUM(se.consumo_dia_kg) OVER w_ord AS acum_consumo_kg,
    GREATEST(0,
        ai.inicial - SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_ord
    )::INT AS saldo_aves,
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
    END AS pct_perdidas_dia,
    GREATEST(0,
        (SELECT apertura_kg FROM apertura_alimento)
        + COALESCE((SELECT SUM(ha2.ingreso_kg + ha2.traslado_entrada_kg - ha2.traslado_salida_kg)
                    FROM hist_alimento ha2
                    WHERE ha2.fecha <= se.fecha), 0)
        - SUM(se.consumo_dia_kg) OVER w_ord
    )::FLOAT8 AS saldo_alimento_kg,
    se.ingreso_alimento_kg,
    se.traslado_entrada_kg,
    se.traslado_salida_kg,
    se.consumo_dia_kg AS consumo_bodega_kg,
    se.documento,
    se.despacho_h AS despacho_hembras,
    se.despacho_m AS despacho_machos,
    se.despacho_x AS despacho_mixtas,
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
    w_ord  AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    w_prev AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
ORDER BY se.fecha, COALESCE(se.seg_id, 0);
$$;
";

        // ═════════════════════════════════════════════════════════════════════════
        // Snapshot + UPDATE masivo de saldo_alimento_kg (idempotente)
        // ═════════════════════════════════════════════════════════════════════════
        private const string MIGRACION_MASIVA_SQL = @"
-- Crea snapshot persistente (si no existe) con el estado ANTES de la migración.
-- Sin PRIMARY KEY por compatibilidad con instancias donde la tabla fue creada
-- manualmente antes (caso del fix #13 aplicado en local).
CREATE TABLE IF NOT EXISTS _migracion_saldo_alimento_2026_05_28 (
    seg_id            BIGINT,
    lote_id           INT,
    fecha             DATE,
    saldo_antes       NUMERIC(18,3),
    updated_at_antes  TIMESTAMP WITH TIME ZONE,
    migrated_at       TIMESTAMP WITH TIME ZONE
);

-- Inserción idempotente: solo agrega filas para seg_id que aún no estén en el snapshot
INSERT INTO _migracion_saldo_alimento_2026_05_28 (seg_id, lote_id, fecha, saldo_antes, updated_at_antes, migrated_at)
SELECT
    s.id, s.lote_ave_engorde_id, DATE(s.fecha), s.saldo_alimento_kg, s.updated_at,
    (now() AT TIME ZONE 'utc')
FROM seguimiento_diario_aves_engorde s
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
WHERE l.deleted_at IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM _migracion_saldo_alimento_2026_05_28 b WHERE b.seg_id = s.id
  );

-- UPDATE masivo: solo actualiza filas donde el saldo persistido difiere del cálculo correcto
WITH nuevos_saldos AS (
    SELECT
        l.lote_ave_engorde_id AS lote_id,
        fn.seg_id,
        fn.saldo_alimento_kg::numeric(18,3) AS saldo_nuevo
    FROM lote_ave_engorde l
    CROSS JOIN LATERAL fn_seguimiento_diario_engorde(l.lote_ave_engorde_id) fn
    WHERE l.deleted_at IS NULL
      AND fn.seg_id IS NOT NULL
)
UPDATE seguimiento_diario_aves_engorde p
SET
    saldo_alimento_kg = n.saldo_nuevo,
    updated_at        = (now() AT TIME ZONE 'utc')
FROM nuevos_saldos n
WHERE p.id = n.seg_id
  AND (
       p.saldo_alimento_kg IS NULL
    OR ABS(p.saldo_alimento_kg - n.saldo_nuevo) >= 0.001
  );
";
    }
}
