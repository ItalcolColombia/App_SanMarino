using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Indicadores y Gráfica de postura con VARIOS registros el mismo día (Santa Reyes, flag
    /// <c>permite_multiples_seguimientos_diarios</c>): la agrupación semanal respeta el DÍA.
    ///
    /// <list type="bullet">
    /// <item><c>fn_indicadores_levante_postura</c>: el PESAJE semanal se arma por día — último día con
    /// pesaje; peso por sexo = promedio de los registros de ese día que pesaron ese sexo; uniformidad =
    /// último registro del día que la trae. Antes tomaba UN registro: no promediaba y perdía el sexo que
    /// el último registro no pesó.</item>
    /// <item><c>fn_seguimiento_diario_produccion</c> v4, SOLO la rama <c>seg_dias_agrupado</c> (flag ON):
    /// peso ave/huevo promedia los registros que pesaron (el peso de huevo se guarda en 0 sin pesaje),
    /// uniformidad/CV = último que la trae, desempate del «último» por <c>seg_id</c> y
    /// <c>metadata.huevoItems</c> concatenados. Lo heredan sin cambio de código
    /// <c>fn_indicadores_produccion_postura</c> y <c>fn_clasificacion_huevo_items_produccion</c>
    /// (Primera/Pnc dejaba afuera los ítems de los demás registros del día).</item>
    /// </list>
    ///
    /// Firmas de retorno iguales ⇒ alcanza <c>CREATE OR REPLACE</c> (el espejo de levante conserva su
    /// <c>DROP FUNCTION IF EXISTS</c> defensivo, igual que <c>20260905035704</c>). Con un registro por
    /// día cada fórmula da lo mismo que antes: medido con
    /// <c>backend/sql/verificar_paridad_indicadores_semanales.sql</c> ⇒ 0 diferencias en Sanmarino y Demo.
    /// Espejos: backend/sql/fn_indicadores_levante_postura.sql y fn_seguimiento_diario_produccion.sql.
    /// Plan: fase_de_desarrollo/indicadores_semanales_varios_registros_dia_plan.md.
    /// </summary>
    public partial class IndicadoresSemanalesVariosRegistrosDia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnIndicadoresLevanteNueva);
            migrationBuilder.Sql(FnSeguimientoDiarioProduccionV4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioProduccionV3);
            migrationBuilder.Sql(FnIndicadoresLevantePrev);
        }
    }
}
