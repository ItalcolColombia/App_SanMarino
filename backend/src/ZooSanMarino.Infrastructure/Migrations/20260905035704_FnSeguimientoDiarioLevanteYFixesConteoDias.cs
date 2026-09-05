using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Levante gana su función canónica diaria (<c>fn_seguimiento_diario_levante</c>, nueva) y los
    /// 4 consumidores que hoy leen <c>seguimiento_diario_levante</c> cruda se corrigen para no
    /// sobre-contar/mal-ordenar cuando el flag <c>permite_multiples_seguimientos_diarios</c> agrupa
    /// 2+ registros del mismo lote+día:
    ///
    /// <list type="bullet">
    /// <item><c>sp_recalcular_seguimiento_levante</c>: su CTE <c>base</c> pasa a leer de la fn nueva
    /// en vez de la tabla cruda — necesario porque sus ventanas (<c>LAG(peso_prom_h)</c> para
    /// <c>gr_ave_dia_h/m</c>) comparan FILA CONSECUTIVA, no día consecutivo; con 2 filas el mismo
    /// día el delta día-a-día no tendría sentido. SUM es asociativa, así que el resto de las
    /// columnas (acumulados) no cambian.</item>
    /// <item><c>fn_indicadores_levante_postura</c>, <c>fn_reporte_semanal_levante_extras</c>,
    /// <c>fn_resumen_semanal_ra_pesadas_levante</c>: SUS agregados SEMANALES ya eran correctos ante
    /// duplicados (SUM es asociativa: sumar 2 filas del mismo día o sumar el día ya agrupado da el
    /// MISMO total). Lo único roto era <c>dias_con_registro</c>/<c>dias</c> — contaba FILAS
    /// (<c>COUNT(*)</c>) en vez de DÍAS calendario, inflando el denominador de "consumo diario
    /// g/ave/día". Fix quirúrgico: <c>COUNT(DISTINCT reg_date)</c>. Se optó por ESTE fix mínimo en
    /// vez de reapuntar las 3 fns a la nueva función canónica: son funciones multi-lote (RA
    /// Pesadas) o con muchos edge-cases finos ya parchados (REQ-002B36, matriz Verenice) — el
    /// riesgo de tocar más que el conteo no se justifica cuando el conteo es todo lo que estaba mal.
    /// </item>
    /// </list>
    ///
    /// Todas las firmas de retorno quedan IGUALES ⇒ alcanza <c>CREATE OR REPLACE</c> (mismo patrón
    /// que <c>fn_indicadores_levante_postura</c> ya usaba, con su propio <c>DROP FUNCTION IF
    /// EXISTS</c> defensivo incluido en el espejo).
    ///
    /// Espejos: backend/sql/fn_seguimiento_diario_levante.sql (nuevo),
    /// fn_indicadores_levante_postura.sql, fn_reporte_semanal_levante_extras.sql,
    /// fn_resumen_semanal_ra_pesadas_levante.sql, sp_recalcular_seguimiento_levante.sql (nuevo
    /// espejo — no tenía uno hasta ahora).
    /// Plan: fase_de_desarrollo/seguimiento_produccion_multiples_registros_dia_plan.md (§5/S6-S7).
    /// </summary>
    public partial class FnSeguimientoDiarioLevanteYFixesConteoDias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioLevante);
            migrationBuilder.Sql(FnIndicadoresLevanteNueva);
            migrationBuilder.Sql(FnReporteSemanalExtrasNueva);
            migrationBuilder.Sql(FnResumenRaPesadasNueva);
            migrationBuilder.Sql(SpRecalcularNueva);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnIndicadoresLevantePrev);
            migrationBuilder.Sql(FnReporteSemanalExtrasPrev);
            migrationBuilder.Sql(FnResumenRaPesadasPrev);
            migrationBuilder.Sql(SpRecalcularPrev);
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_seguimiento_diario_levante(text);");
        }
    }
}
