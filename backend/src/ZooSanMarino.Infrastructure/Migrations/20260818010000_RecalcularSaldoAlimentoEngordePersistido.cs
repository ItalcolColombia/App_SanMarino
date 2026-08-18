using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Vuelve a alinear <c>seguimiento_diario_aves_engorde.saldo_alimento_kg</c> con
    /// <c>fn_seguimiento_diario_engorde</c>, que es la dueña del número.
    /// <para>
    /// <b>Por qué hace falta otra vez.</b> Las migraciones de v11 y v12 dejaron las dos fuentes
    /// iguales, pero desde entonces la fn cambió dos veces sin que ninguna recalculara la columna, y
    /// el recálculo del service no cubre todos los caminos que mueven un día ya cargado. Medido el
    /// 17-ago-2026 sobre el dump de producción: <b>109 filas / 36 lotes de ItalcolPanama</b> con la
    /// columna desalineada (la peor por 23.355 kg); <b>ItalcolEcuador en 0</b> de sus 5.189 filas.
    /// </para>
    /// <para>
    /// <b>Y por qué urge.</b> <c>LiquidacionCongeladaAplicador</c> toma el saldo del ÚLTIMO día
    /// directo de esta columna y lo escribe en la copia congelada de la liquidación. Una foto
    /// congelada no se reescribe: si la columna está desalineada ese día, el número queda mal para
    /// siempre, y de ahí lo leen Costos, el modal de liquidación y el reporte de «liquidados con
    /// alimento sin trasladar». <b>6 lotes de Panamá tienen hoy el último día divergente</b>, el peor
    /// por 9.844 kg.
    /// </para>
    /// <para>
    /// <b>El valor sale de la propia fn</b> (una sola fórmula por número): acá no se escribe
    /// aritmética nueva. Simulado en transacción y revertido antes de escribir esta migración: cambia
    /// 109 filas, todas de ItalcolPanama, 0 de ItalcolEcuador, y deja 0 divergencias.
    /// </para>
    /// <para>
    /// <b>Idempotente:</b> el <c>UPDATE</c> filtra por <c>IS DISTINCT FROM</c>, así que una segunda
    /// corrida no mueve ninguna fila. El backup se llena con <c>WHERE NOT EXISTS</c>, de modo que
    /// conserva SIEMPRE el valor original aunque la migración vuelva a ejecutarse.
    /// </para>
    /// <para>
    /// <b>No toca ninguna función SQL</b> ⇒ no aplica el gate de paridad multipaís; igualmente
    /// Ecuador queda byte a byte por construcción (0 filas cambian).
    /// </para>
    /// Plan: fase_de_desarrollo/saldo_alimento_persistido_vs_fn_panama_plan.md
    /// </summary>
    public partial class RecalcularSaldoAlimentoEngordePersistido : Migration
    {
        private const string BackupTable = "_backup_saldo_alimento_engorde_20260818";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
-- 1) Backup del valor original, una sola vez (idempotente).
CREATE TABLE IF NOT EXISTS {BackupTable} (
    seg_id                BIGINT PRIMARY KEY,
    lote_ave_engorde_id   INT,
    fecha                 TIMESTAMPTZ,
    saldo_alimento_kg     NUMERIC(18,3),
    respaldado_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO {BackupTable} (seg_id, lote_ave_engorde_id, fecha, saldo_alimento_kg)
SELECT s.id, s.lote_ave_engorde_id, s.fecha, s.saldo_alimento_kg
  FROM seguimiento_diario_aves_engorde s
 WHERE NOT EXISTS (SELECT 1 FROM {BackupTable} b WHERE b.seg_id = s.id);

-- 2) Recalculo desde la propia fn (unica fuente de verdad del saldo).
--    `IS DISTINCT FROM` cubre los NULL y hace la migracion idempotente.
WITH nuevos AS (
    SELECT f.seg_id,
           ROUND(f.saldo_alimento_kg::numeric, 3) AS saldo
      FROM lote_ave_engorde l
      CROSS JOIN LATERAL fn_seguimiento_diario_engorde(l.lote_ave_engorde_id) f
     WHERE l.deleted_at IS NULL
       AND f.seg_id IS NOT NULL
)
UPDATE seguimiento_diario_aves_engorde s
   SET saldo_alimento_kg = n.saldo
  FROM nuevos n
 WHERE s.id = n.seg_id
   AND s.saldo_alimento_kg IS DISTINCT FROM n.saldo;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaura el valor original desde el backup y lo deja disponible por si hay que repetir.
            migrationBuilder.Sql($@"
DO $$
BEGIN
    IF to_regclass('public.{BackupTable}') IS NOT NULL THEN
        UPDATE seguimiento_diario_aves_engorde s
           SET saldo_alimento_kg = b.saldo_alimento_kg
          FROM {BackupTable} b
         WHERE b.seg_id = s.id
           AND s.saldo_alimento_kg IS DISTINCT FROM b.saldo_alimento_kg;
    END IF;
END $$;
");
        }
    }
}
