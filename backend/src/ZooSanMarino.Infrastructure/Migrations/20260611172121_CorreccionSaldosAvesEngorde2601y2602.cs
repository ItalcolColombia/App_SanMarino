using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS de saldos de aves en lotes pollo engorde (Ecuador, company 3),
    /// validada primero en local vía los endpoints de LoteAveEngordeController
    /// (aves-disponibles/validar + corregir) y empaquetada como migración para que PROD
    /// quede alineada en el deploy:
    ///  0. CHECK ck_hlpe_tipo_registro admite 'AjusteResync'.
    ///  A. Confirma las 23 ventas con factura que quedaron 'Pendiente' (lotes 72/73 "2602"):
    ///     despachos físicamente ejecutados (constan en histórico/tabla diaria) que nunca
    ///     descontaron el maestro. Réplica de CompleteAsync; guard estado='Pendiente'.
    ///  B. Lote 5 "2602": re-sync del maestro (29 ventas de abril no descontaron por bug
    ///     previo a may-2026): −10.738 H / −12.892 M. Marcador 'AjusteResync'.
    ///  C. Lotes "2601" cerrados con disponibles fantasma → 0 (14, 19, 20, 23, 28, 33, 55, 56).
    ///     Marcador 'Ajuste' por lote; solo si el lote sigue Cerrado.
    /// IDEMPOTENTE (guards/marcadores): en BD ya corregida es no-op; respeta confirmaciones
    /// manuales parciales. SQL sincronizado con backend/sql/correccion_saldos_aves_engorde_2601_2602.sql.
    /// Planes: fase_de_desarrollo/correccion_aves_disponibles_engorde_2601_plan.md ·
    ///         fase_de_desarrollo/correccion_saldos_engorde_2602_global_plan.md
    /// </summary>
    public partial class CorreccionSaldosAvesEngorde2601y2602 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DATA_FIX_SQL);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Corrección de datos: no se revierte automáticamente (revertir falsificaría
            // ventas confirmadas reales). Los marcadores 'Ajuste'/'AjusteResync' en
            // historial_lote_pollo_engorde documentan lo aplicado por lote.
        }

        private const string DATA_FIX_SQL = @"
-- =============================================================================
-- Corrección de saldos de aves — lotes pollo engorde ""2601"" y ""2602"" (Ecuador, company 3)
-- Canónico: migración EF CorreccionSaldosAvesEngorde2601y2602 (se aplica sola en el deploy).
-- Este archivo es la copia legible/testeable del cuerpo SQL.
--
-- Diagnóstico: fase_de_desarrollo/correccion_aves_disponibles_engorde_2601_plan.md
--              fase_de_desarrollo/correccion_saldos_engorde_2602_global_plan.md
--
-- IDEMPOTENTE: cada parte tiene guard (estado/realidad o marcador en historial_lote_pollo_engorde).
--   · Ya aplicada (BD local corregida vía endpoint) → no-op.
--   · Prod con confirmaciones manuales parciales → solo aplica lo que falte.
-- =============================================================================

-- ── 0) CHECK de tipo_registro: admitir 'AjusteResync' ───────────────────────
-- 'Ajuste'       = descuento por aves fantasma (participa en la conservación).
-- 'AjusteResync' = sustituye el descuento de ventas Completadas que no descontaron
--                  el maestro (NO participa en la conservación; evita re-aplicarse).
ALTER TABLE historial_lote_pollo_engorde DROP CONSTRAINT IF EXISTS ck_hlpe_tipo_registro;
ALTER TABLE historial_lote_pollo_engorde ADD CONSTRAINT ck_hlpe_tipo_registro
    CHECK (tipo_registro IN ('Inicio', 'Ajuste', 'AjusteResync'));

-- ── A) Confirmar las 23 ventas con factura que quedaron 'Pendiente' (lotes 72/73 ""2602"") ──
-- Despachos físicamente ejecutados (02–04 jun: constan en el histórico unificado y en la
-- tabla diaria) que nunca se confirmaron → no descontaron el maestro. Réplica exacta de
-- MovimientoPolloEngordeService.CompleteAsync: estado→Completado + fecha_procesamiento +
-- descuento del maestro con piso 0. Guard estado='Pendiente' → idempotente y respeta
-- confirmaciones manuales hechas entre el respaldo y el deploy. (Los 23 tienen mixtas=0.)
WITH flipped AS (
    UPDATE movimiento_pollo_engorde m
    SET estado              = 'Completado',
        fecha_procesamiento = (NOW() AT TIME ZONE 'utc'),
        updated_at          = (NOW() AT TIME ZONE 'utc')
    WHERE m.id IN (
            -- lote 72 (2602, G0039): 5.225 H + 10.893 M
            1353,1354,1355,1356,1358,1359,1360,1361,1362,1363,1364,1365,1366,1367,
            -- lote 73 (2602, G0040): 5.224 H + 10.676 M
            1344,1345,1348,1351,1357,1368,1371,1372,1373)
      AND m.estado = 'Pendiente'
      AND m.deleted_at IS NULL
    RETURNING m.lote_ave_engorde_origen_id AS lote_id, m.cantidad_hembras AS h, m.cantidad_machos AS mch
),
agg AS (
    SELECT lote_id, SUM(h) AS h, SUM(mch) AS mch FROM flipped GROUP BY lote_id
)
UPDATE lote_ave_engorde l
SET hembras_l  = GREATEST(0, COALESCE(l.hembras_l, 0) - a.h),
    machos_l   = GREATEST(0, COALESCE(l.machos_l, 0) - a.mch),
    updated_at = (NOW() AT TIME ZONE 'utc')
FROM agg a
WHERE l.lote_ave_engorde_id = a.lote_id;

-- ── B) Lote 5 (2602, G0050): re-sync del maestro ─────────────────────────────
-- 29 de sus 30 ventas Completadas (abril) nunca descontaron hembras_l/machos_l (bug de
-- escritura previo a may-2026): 10.738 H + 12.892 M sin descontar (validado contra la
-- tabla diaria: tras el ajuste disponibles = 161 = saldo del seguimiento).
-- Marcador de idempotencia: fila 'AjusteResync' del lote.
DO $do$
DECLARE v_company INT;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM historial_lote_pollo_engorde
                   WHERE lote_ave_engorde_id = 5 AND tipo_lote = 'LoteAveEngorde'
                     AND tipo_registro = 'AjusteResync') THEN
        SELECT company_id INTO v_company
        FROM lote_ave_engorde
        WHERE lote_ave_engorde_id = 5 AND deleted_at IS NULL;
        IF v_company IS NOT NULL THEN
            UPDATE lote_ave_engorde
            SET hembras_l  = GREATEST(0, COALESCE(hembras_l, 0) - 10738),
                machos_l   = GREATEST(0, COALESCE(machos_l, 0) - 12892),
                updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE lote_ave_engorde_id = 5;
            INSERT INTO historial_lote_pollo_engorde
                (company_id, tipo_lote, lote_ave_engorde_id, tipo_registro,
                 aves_hembras, aves_machos, aves_mixtas, fecha_registro, created_at)
            VALUES (v_company, 'LoteAveEngorde', 5, 'AjusteResync',
                    10738, 12892, 0, (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'));
        END IF;
    END IF;
END $do$;

-- ── C) Lotes ""2601"" CERRADOS con disponibles fantasma → 0 ───────────────────
-- Aves nunca descargadas en ningún registro (género impreciso al final del ciclo).
-- Ajustes calculados con la fórmula vigente de disponibilidad (ver plan 2601).
-- Marcador de idempotencia: fila 'Ajuste' del lote. Solo aplica si el lote sigue Cerrado.
WITH objetivo (lote_id, aj_h, aj_m) AS (
    VALUES (14, 563, 154), (19, 0, 1), (20, 0, 457), (23, 0, 24),
           (28, 8, 0), (33, 0, 290), (55, 0, 4), (56, 42, 9)
),
aplicables AS (
    SELECT o.lote_id, o.aj_h, o.aj_m, l.company_id
    FROM objetivo o
    JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = o.lote_id
    WHERE l.deleted_at IS NULL
      AND LOWER(l.estado_operativo_lote) = 'cerrado'
      AND NOT EXISTS (SELECT 1 FROM historial_lote_pollo_engorde h
                      WHERE h.lote_ave_engorde_id = o.lote_id
                        AND h.tipo_lote = 'LoteAveEngorde'
                        AND h.tipo_registro = 'Ajuste')
),
upd AS (
    UPDATE lote_ave_engorde l
    SET hembras_l  = GREATEST(0, COALESCE(l.hembras_l, 0) - a.aj_h),
        machos_l   = GREATEST(0, COALESCE(l.machos_l, 0) - a.aj_m),
        updated_at = (NOW() AT TIME ZONE 'utc')
    FROM aplicables a
    WHERE l.lote_ave_engorde_id = a.lote_id
    RETURNING l.lote_ave_engorde_id
)
INSERT INTO historial_lote_pollo_engorde
    (company_id, tipo_lote, lote_ave_engorde_id, tipo_registro,
     aves_hembras, aves_machos, aves_mixtas, fecha_registro, created_at)
SELECT a.company_id, 'LoteAveEngorde', a.lote_id, 'Ajuste',
       a.aj_h, a.aj_m, 0, (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc')
FROM aplicables a;
";
    }
}
