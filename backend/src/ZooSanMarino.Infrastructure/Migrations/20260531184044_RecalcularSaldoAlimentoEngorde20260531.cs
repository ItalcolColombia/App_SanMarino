using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Re-ejecuta el recálculo masivo de `saldo_alimento_kg` en
    /// `seguimiento_diario_aves_engorde` usando la versión FINAL de la función
    /// `fn_seguimiento_diario_engorde` (la dejada por 20260531034622_FixFnSeguimientoEngordeCortePorCierreAlimento,
    /// "corte por cierre de alimento").
    ///
    /// Motivo: la migración 20260528212753 recalculó los saldos con la función v4, pero el fix
    /// posterior 20260531034622 cambió la lógica de la función SIN volver a recalcular los datos
    /// persistidos. Por eso los saldos quedaban con la lógica vieja. Esta migración corrige eso.
    ///
    /// Dependencias garantizadas por el orden de migraciones (esta corre al final):
    ///   * 20260531034622 → función final ya creada.
    ///   * 20260531180558 → funciones helper (weeknum_iso, fn_acumulado_entradas_alimento, etc.) ya creadas.
    ///
    /// Seguridad: respalda el saldo actual ANTES de actualizar en
    /// `_migracion_saldo_alimento_2026_05_31` (columna saldo_antes). Idempotente:
    ///   * la tabla de respaldo se crea solo si no existe;
    ///   * el snapshot inserta solo seg_id que aún no estén respaldados;
    ///   * el UPDATE solo toca filas donde el saldo persistido difiere del cálculo correcto (>= 0.001).
    /// </summary>
    public partial class RecalcularSaldoAlimentoEngorde20260531 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RECALCULO_MASIVO_SQL, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaurar saldos desde el snapshot de esta migración (si existe).
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables
               WHERE table_name = '_migracion_saldo_alimento_2026_05_31') THEN
        UPDATE seguimiento_diario_aves_engorde p
        SET saldo_alimento_kg = b.saldo_antes,
            updated_at = b.updated_at_antes
        FROM _migracion_saldo_alimento_2026_05_31 b
        WHERE p.id = b.seg_id;
    END IF;
END $$;
", suppressTransaction: true);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // Snapshot + UPDATE masivo de saldo_alimento_kg con la función FINAL (idempotente)
        // ═════════════════════════════════════════════════════════════════════════
        private const string RECALCULO_MASIVO_SQL = @"
-- Snapshot persistente con el estado ANTES de este recálculo (para auditoría y rollback).
CREATE TABLE IF NOT EXISTS _migracion_saldo_alimento_2026_05_31 (
    seg_id            BIGINT,
    lote_id           INT,
    fecha             DATE,
    saldo_antes       NUMERIC(18,3),
    updated_at_antes  TIMESTAMP WITH TIME ZONE,
    migrated_at       TIMESTAMP WITH TIME ZONE
);

-- Inserción idempotente: solo agrega filas para seg_id que aún no estén en el snapshot.
INSERT INTO _migracion_saldo_alimento_2026_05_31 (seg_id, lote_id, fecha, saldo_antes, updated_at_antes, migrated_at)
SELECT
    s.id, s.lote_ave_engorde_id, DATE(s.fecha), s.saldo_alimento_kg, s.updated_at,
    (now() AT TIME ZONE 'utc')
FROM seguimiento_diario_aves_engorde s
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
WHERE l.deleted_at IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM _migracion_saldo_alimento_2026_05_31 b WHERE b.seg_id = s.id
  );

-- UPDATE masivo: recalcula con la función FINAL; solo actualiza filas donde el saldo
-- persistido difiere del cálculo correcto.
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
