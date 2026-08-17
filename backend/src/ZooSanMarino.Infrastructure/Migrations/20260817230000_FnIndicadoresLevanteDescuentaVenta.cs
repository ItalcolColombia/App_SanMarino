using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FnIndicadoresLevanteDescuentaVenta : Migration
    {
        // Plan: fase_de_desarrollo/saldo_levante_una_sola_formula_plan.md
        //
        // El saldo de aves de levante tenia CUATRO consumidores y DOS formulas. Descontaban la venta
        // fn_reporte_semanal_levante_extras y fn_resumen_semanal_ra_pesadas_levante (comentario propio
        // de esta ultima: "el saldo tiene que descontarla o el reporte sobrestima el lote"); NO la
        // descontaban fn_indicadores_levante_postura y ReporteTecnicoService. La especificacion
        // ejecutable -SaldoAvesLevanteCalculos.BajasNetas- si la incluye desde siempre.
        //
        // Resultado: el mismo lote y la misma semana mostraban dos conteos de aves segun la pantalla.
        // Medido antes del cambio: lote 143 sem 24 -> 10.619 en Indicadores contra 10.329 en el
        // reporte semanal; lote 143 sem 23 -> 10.626 contra 10.476; lote 142 sem 24 -> 10.646 contra
        // 10.450. La diferencia es EXACTAMENTE la venta acumulada. Violaba "una sola formula por
        // numero" (CLAUDE.md) y no se notaba solo porque casi nadie habia registrado ventas de
        // levante todavia (2 lotes en toda la base).
        //
        // Esta migracion pone la venta en el saldo de fn_indicadores_levante_postura, en el mixto y
        // por sexo. Por sexo se usan los splits dedicados (venta_aves_hembras/machos), no el total
        // venta_aves_cantidad, que es el mismo criterio de fn_resumen_semanal_ra_pesadas_levante; el
        // mixto se arma como h+m igual que mort/sel/err/traslados en el resto de la fn.
        //
        // Se agrega ademas la venta al predicado que descarta las filas de "puro traslado" posteriores
        // a la semana 25, en sus DOS copias (el armado de la serie y el fallback v_first_ing_*), que
        // la propia fn documenta como obligatoriamente identicas: una fila que trae venta no es puro
        // traslado y descartarla perderia esas aves.
        //
        // VERIFICACION (backend/sql/verificar_paridad_saldo_levante.sql, corrido antes y despues):
        //   * fn_indicadores_levante_postura vs fn_reporte_semanal_levante_extras, TODOS los lotes:
        //     antes 3 filas desalineadas (peor 290 aves) -> despues 0, en las dos empresas con lotes.
        //   * Cambiaron EXACTAMENTE 3 filas, las de los 2 lotes con venta. Demo: 0 filas.
        //   * peso_cierre y consumo_total_semana intactos en las 3. mortalidad_sem se movio en 1
        //     (lote 143 sem 24: 0,065876 -> 0,066819) y es correcto: su denominador son las aves al
        //     inicio de la semana, o sea el cierre de la semana 23, que bajo por la venta.
        //
        // La firma no cambia, asi que CREATE OR REPLACE alcanza; el script conserva el DROP FUNCTION
        // previo que ya traia el .sql.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnIndicadoresLevanteConVenta);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnIndicadoresLevanteSinVenta);
        }
    }
}
