using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Recalcula <c>seguimiento_diario_aves_engorde.saldo_alimento_kg</c> con la formula corregida
    /// de la v12, dejando el dato GUARDADO y la GRILLA en el mismo numero.
    /// <para>
    /// Hasta ahora las dos fuentes fallaban por lados opuestos y ninguna servia sola:
    /// <list type="bullet">
    /// <item><b>La grilla</b> (<c>fn_seguimiento_diario_engorde</c>) recalcula en vivo, pero
    /// arrastraba la apertura fantasma del ciclo anterior del galpon.</item>
    /// <item><b>La columna guardada</b> no tenia esa apertura fantasma —el service de Ecuador
    /// conservaba el corte viejo— pero se congela: <c>RecalcularSaldoAlimentoPorLoteAsync</c> solo
    /// corre al crear o editar un seguimiento, asi que un ingreso posterior al ultimo dia cargado
    /// nunca la actualiza (Kilometro 61 G0037: los 10.000 kg del documento 63020 del 28/07 entraron
    /// el mismo dia del ultimo seguimiento y el saldo guardado nunca los recogio).</item>
    /// </list>
    /// </para>
    /// <para>
    /// El recalculo toma los valores de la propia fn v11, que ya quedo validada contra el stock
    /// fisico de inventario: asi el dato guardado pasa a ser, por construccion, identico a lo que
    /// pinta la pantalla. Eso corrige de paso los tres descuadres persistentes medidos sobre el
    /// dump de produccion (Kilometro 61 G0037 −10.000 kg, Kilometro 86 G0040 −2.400, CAROLINA
    /// G0058 +480).
    /// </para>
    /// <para>
    /// <b>Idempotente:</b> el <c>UPDATE</c> filtra por <c>IS DISTINCT FROM</c>, asi que una segunda
    /// corrida no mueve ninguna fila. El backup se llena con <c>WHERE NOT EXISTS</c>, de modo que
    /// conserva SIEMPRE el valor original aunque la migracion vuelva a ejecutarse.
    /// </para>
    /// <para>
    /// Debe correr DESPUES de <c>20260730140000_FnSeguimientoEngordeV12AperturaCorteCicloAnterior</c>:
    /// lee los valores de esa version de la funcion.
    /// </para>
    /// Plan: fase_de_desarrollo/fix_apertura_alimento_ciclo_anterior_plan.md
    /// </summary>
    public partial class RecalcularSaldoAlimentoEngordeV12 : Migration
    {
        private const string BackupTable = "_backup_saldo_alimento_engorde_v12";

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

-- 2) Recalculo desde la propia fn v11 (unica fuente de verdad del saldo).
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
