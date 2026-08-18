using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Vuelve a alinear <c>seguimiento_diario_aves_engorde.saldo_alimento_kg</c> con
    /// <c>fn_seguimiento_diario_engorde</c> despues de la v16a
    /// (<c>20260818060000_FnSeguimientoEngordeV16aMarcaInerte</c>).
    /// <para>
    /// <b>Por que va, si la v16a se declaro no-op.</b> Lo es cuando NO hay marcas: el gate multipais
    /// dio 0 diferencias en las dos empresas sobre 6.429 filas. Pero desde esta maquina <b>no se puede
    /// consultar produccion</b> (RDS en VPC privada, ECS Exec deshabilitado), asi que no se puede
    /// AFIRMAR que prod tenga 0 filas con <c>para_proximo_ciclo</c>. Si tuviera aunque sea una, la
    /// v16a cambiaria el saldo de ese galpon y la columna persistida quedaria vieja.
    /// </para>
    /// <para>
    /// <b>Y por que urge cerrar ese hueco.</b> <c>LiquidacionCongeladaAplicador</c> toma el saldo del
    /// ULTIMO dia directo de esta columna y lo escribe en la copia congelada. Una foto congelada no se
    /// reescribe: si la columna esta desalineada ese dia, el numero queda mal para siempre. Ya paso:
    /// la fn cambio dos veces sin recalculo y dejo 109 filas / 36 lotes de Panama divergentes, 6 de
    /// ellos en el ultimo dia (migracion 20260818010000).
    /// </para>
    /// <para>
    /// <b>El valor sale de la propia fn</b> (una sola formula por numero): aca no se escribe aritmetica
    /// nueva. Medido en local antes de escribir esta migracion: <b>0 filas desalineadas</b> en las dos
    /// empresas, o sea que aca no mueve nada. Cuesta nada y elimina la unica forma en que la v16a
    /// podria dejar la columna desalineada en prod.
    /// </para>
    /// <para>
    /// <b>Idempotente:</b> el <c>UPDATE</c> filtra por <c>IS DISTINCT FROM</c> y el backup se llena con
    /// <c>WHERE NOT EXISTS</c>, asi que conserva SIEMPRE el valor original aunque se vuelva a ejecutar.
    /// Molde exacto de <c>20260818010000_RecalcularSaldoAlimentoEngordePersistido</c>.
    /// </para>
    /// <para><b>No toca ninguna funcion SQL</b> ⇒ no aplica el gate de paridad multipais.</para>
    /// Plan: fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md (§3.1, migracion #2)
    /// </summary>
    public partial class RecalcularSaldoAlimentoEngordeV16a : Migration
    {
        private const string BackupTable = "_backup_saldo_alimento_engorde_v16a_20260818";

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
