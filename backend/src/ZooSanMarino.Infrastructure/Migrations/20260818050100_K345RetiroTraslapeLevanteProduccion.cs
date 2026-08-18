using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only): retira los días que viven a la vez en
    /// <c>seguimiento_diario_levante</c> y en <c>seguimiento_diario_produccion</c>, después de
    /// rescatar lo que solo existe del lado de levante.
    ///
    /// <para>
    /// <b>Caso (18-ago-2026).</b> K345A (lote 13) y K345B (lote 14) son los <b>únicos</b> de la base
    /// con días duplicados: 15 en total. El guard ya impide crear nuevos; estos son el residuo. 14
    /// son la semana de transición de julio 2025 —<b>la misma mortalidad en los dos lados</b> (1/1,
    /// 0/0, 2/2), levante con el alimento y producción con los huevos, que arrancan 33 → 1.595— y 1
    /// es el 7-abr-2026, donde la fila de levante está vacía (mort 0, kg 0,000) y dice
    /// <c>observaciones = 'pruebas sistemas'</c>, sobre un día real de 4.277 huevos.
    /// </para>
    ///
    /// <para>
    /// <b>Decisión del usuario (18-ago-2026):</b> producción manda desde el primer huevo. La
    /// mortalidad deja de estar contada dos veces.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>No es un DELETE pelado, y esa es la parte importante.</b> Medido antes de escribir esto:
    /// el alimento YA está en producción con el mismo valor o mayor (927,7 = 927,7 · 1.259 = 1.259 ·
    /// K345B 23-jul producción 1.279,9 vs levante 1.259,9), así que no hay nada que reasignar. Pero
    /// <c>sel_m</c> (<b>21 + 112 = 133 machos seleccionados</b>), el C.V. y la uniformidad viven SOLO
    /// en levante, y <c>produccion_resultado_levante</c> NO los preserva: su <c>ac_sel_m</c> llega a
    /// 8 cuando el total de <c>sel_m</c> de levante del lote 13 es 241, y el lote 14 ni figura en esa
    /// tabla. Borrar sin rescatar perdería 133 aves. Por eso el paso 1 rescata y el paso 2 borra.
    /// </para>
    ///
    /// <para>
    /// El rescate <b>jamás pisa</b> un valor que producción ya tenga (de ahí los <c>COALESCE</c> y el
    /// <c>CASE</c>): <c>peso_h</c> conserva sus 3.341,40 y 3.307,20, que ya coincidían.
    /// </para>
    ///
    /// <para>
    /// El <c>DELETE</c> es duro —<c>seguimiento_diario_levante</c> no tiene soft-delete— así que
    /// antes se copian las filas completas a <c>_backup_traslape_levante_k345_20260818</c>, que es de
    /// donde las repone el <c>Down()</c>. El trigger
    /// <c>trg_tombstone_seguimiento_diario_levante</c> deja cada borrado en <c>sync_tombstones</c>,
    /// así que los clientes offline se enteran.
    /// </para>
    ///
    /// <para><b>Idempotente</b>: la 2ª corrida no encuentra traslape y afecta 0 filas.</para>
    ///
    /// <para>
    /// Verificado en transacción con ROLLBACK: 15 → 0 días traslapados · <c>SUM(sel_m)</c> de esos
    /// días se conserva en <b>133</b> · <c>SUM(cons_kg_h + cons_kg_m)</c> de producción sin cambio
    /// (18.159,0) · +15 tombstones. SQL trazable en
    /// <c>backend/sql/correccion_traslape_levante_produccion.sql</c>.
    /// </para>
    /// </summary>
    public partial class K345RetiroTraslapeLevanteProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS _backup_traslape_levante_k345_20260818 AS
                SELECT sl.* FROM seguimiento_diario_levante sl
                JOIN seguimiento_diario_produccion sp
                  ON sp.lote_id::text = sl.lote_id::text AND sp.fecha_registro::date = sl.fecha::date;

                CREATE TEMP TABLE _traslape ON COMMIT DROP AS
                SELECT sl.id AS lev_id, sp.id AS pro_id
                FROM seguimiento_diario_levante sl
                JOIN seguimiento_diario_produccion sp
                  ON sp.lote_id::text = sl.lote_id::text AND sp.fecha_registro::date = sl.fecha::date;

                UPDATE seguimiento_diario_produccion sp SET
                    sel_h              = CASE WHEN COALESCE(sp.sel_h,0) = 0 THEN COALESCE(sl.sel_h, sp.sel_h) ELSE sp.sel_h END,
                    sel_m              = CASE WHEN COALESCE(sp.sel_m,0) = 0 THEN COALESCE(sl.sel_m, sp.sel_m) ELSE sp.sel_m END,
                    cv_hembras         = COALESCE(sp.cv_hembras, sl.cv_hembras),
                    cv_machos          = COALESCE(sp.cv_machos, sl.cv_machos),
                    uniformidad        = COALESCE(sp.uniformidad, sl.uniformidad_hembras),
                    uniformidad_machos = COALESCE(sp.uniformidad_machos, sl.uniformidad_machos),
                    metadata           = COALESCE(sp.metadata, sl.metadata)
                FROM seguimiento_diario_levante sl, _traslape t
                WHERE t.pro_id = sp.id AND t.lev_id = sl.id;

                DELETE FROM seguimiento_diario_levante WHERE id IN (SELECT lev_id FROM _traslape);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Repone las filas de levante desde el respaldo. NO deshace el rescate a producción:
            // esos campos estaban vacíos y volver a vaciarlos borraría dato bueno.
            migrationBuilder.Sql("""
                INSERT INTO seguimiento_diario_levante
                SELECT * FROM _backup_traslape_levante_k345_20260818 b
                WHERE NOT EXISTS (SELECT 1 FROM seguimiento_diario_levante s WHERE s.id = b.id);
                """);
        }
    }
}
