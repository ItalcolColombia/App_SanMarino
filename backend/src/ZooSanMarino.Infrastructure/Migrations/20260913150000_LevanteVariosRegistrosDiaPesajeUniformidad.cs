using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// LEVANTE con VARIOS registros el mismo día (Santa Reyes, flag
    /// <c>permite_multiples_seguimientos_diarios</c>): lo NO aditivo se agrupa con el mismo criterio que
    /// <c>fn_seguimiento_diario_produccion</c> v4 y que el pesaje de <c>fn_indicadores_levante_postura</c>
    /// (ambos de <c>20260913120000</c>).
    ///
    /// <list type="bullet">
    /// <item><c>fn_seguimiento_diario_levante</c> v2, SOLO la rama <c>seg_dias_agrupado</c> (flag ON):
    /// uniformidad/CV = último registro que la trae (un NULL posterior la tapaba), desempate del «último»
    /// por id (los forms graban a mediodía y el timestamp empataba) y peso/kcal/proteína = promedio de los
    /// registros que midieron (&gt; 0). Lo hereda sin cambio de código <c>sp_recalcular_seguimiento_levante</c>
    /// (modal «Cálculos»).</item>
    /// <item><c>fn_reporte_semanal_levante_extras</c> (Reporte Técnico Semanal) y
    /// <c>fn_resumen_semanal_ra_pesadas_levante</c> (Informe RA Pesadas): el PESAJE semanal se arma por
    /// día — último día con pesaje; peso por sexo = promedio de los registros de ese día que pesaron;
    /// uniformidad/CV = último registro del día que la trae. Antes tomaban UN registro.</item>
    /// </list>
    ///
    /// Firmas de retorno iguales ⇒ alcanza <c>CREATE OR REPLACE</c>. Con un registro por día cada fórmula
    /// da lo mismo que antes: medido con <c>backend/sql/verificar_paridad_levante_varios_registros_dia.sql</c>.
    /// Down = versiones previas verbatim (las de <c>20260905035704</c>, iguales a lo desplegado).
    /// Espejos: backend/sql/fn_seguimiento_diario_levante.sql, fn_reporte_semanal_levante_extras.sql y
    /// fn_resumen_semanal_ra_pesadas_levante.sql.
    /// Plan: fase_de_desarrollo/levante_varios_registros_dia_pesaje_uniformidad_plan.md.
    /// </summary>
    public partial class LevanteVariosRegistrosDiaPesajeUniformidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioLevanteV2);
            migrationBuilder.Sql(FnReporteSemanalExtrasNueva);
            migrationBuilder.Sql(FnResumenRaPesadasNueva);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnResumenRaPesadasPrev);
            migrationBuilder.Sql(FnReporteSemanalExtrasPrev);
            migrationBuilder.Sql(FnSeguimientoDiarioLevanteV1);
        }
    }
}
