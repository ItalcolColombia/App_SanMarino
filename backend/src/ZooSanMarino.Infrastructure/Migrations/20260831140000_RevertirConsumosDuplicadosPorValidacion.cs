using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Revierte los consumos de alimento que <c>ValidarAsync</c> aplicó <b>dos veces</b>: borra la
    /// copia sobrante de cada par y le devuelve los kilos al stock del galpón. Medido en la copia de
    /// producción: <b>8 pares, 19.677,24 kg de más en 7 galpones de ItalcolPanama</b>.
    /// </summary>
    /// <remarks>
    /// <b>Qué lo produjo.</b> <c>ValidacionSeguimientoService.ValidarAsync</c> leía el estado del
    /// registro y sus reservas <i>fuera</i> de la transacción y sin bloqueo, así que dos requests
    /// solapadas —un doble clic en el botón ✓, que no se deshabilitaba mientras la petición estaba en
    /// vuelo— leían las dos <c>validado = false</c> y la MISMA reserva activa, y cada una emitía su
    /// consumo. Cada seguimiento afectado tiene <b>una sola</b> fila en
    /// <c>seguimiento_reserva_alimento</c> contra <b>dos</b> movimientos en el kardex. La carrera se
    /// cierra en el mismo commit que trae esta migración (patrón «marcar primero, aplicar después»);
    /// esto repara lo que ya quedó escrito, que ningún cambio de código deshace.
    ///
    /// <b>Por qué borra y no compensa con un ingreso.</b> Así es como lo repararía
    /// <c>DesvalidarAsync</c>, pero un ingreso suelto aparecería en el histórico como una entrada de
    /// alimento de ese día y le mentiría al cuadre del galpón: acá no hubo una entrada, hubo una
    /// salida que nunca debió existir.
    ///
    /// <b>Los dos pasos, y por qué son dos.</b> Simulado antes en una transacción revertida sobre la
    /// copia de producción: el <c>DELETE</c> del movimiento <b>sí</b> deja su fila de
    /// <c>lote_registro_historico_unificado</c> en <c>anulado = true</c> —lo hace el trigger
    /// <c>trg_inventario_gestion_movimiento_lote_hist_del</c>—, pero <b>no</b> devuelve el stock. Es
    /// el mismo patrón que ya mordió una vez en este repo: dos caminos para lo mismo, uno revierte y
    /// el otro no. Por eso la migración hace las dos cosas en la misma transacción.
    ///
    /// <b>Identificación por firma, no por ids.</b> Se agrupa por <c>reference</c> + ítem + granja +
    /// núcleo + galpón + silo + cantidad con <c>count(*) &gt; 1</c>, y se conserva el de <b>menor
    /// id</b>. Los ids de local y producción no tienen por qué coincidir. La regla es la misma que
    /// fija <c>DuplicadosValidacionCalculos</c>, con sus tests: dos galpones que consumen el mismo día
    /// el mismo ítem NO son un duplicado, y dos cantidades distintas del mismo seguimiento tampoco.
    ///
    /// <b>Reversible.</b> Antes de borrar, cada movimiento se copia entero a
    /// <c>_backup_consumos_duplicados_validacion_20260831</c>. El <c>Down</c> los reinserta con su id
    /// original, deja el histórico como estaba y vuelve a restar los kilos.
    ///
    /// <b>Idempotente.</b> Corrida dos veces, la segunda no encuentra ningún grupo con
    /// <c>count(*) &gt; 1</c> y no mueve una fila.
    ///
    /// Plan: <c>fase_de_desarrollo/validar_seguimiento_doble_descuento_plan.md</c>.
    /// Migración DATA-ONLY salvo la tabla de respaldo: Designer clonado, ModelSnapshot intacto.
    /// </remarks>
    public partial class RevertirConsumosDuplicadosPorValidacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(REVERSION_SQL);
        }

        /// <summary>
        /// Reinserta los movimientos respaldados con su id original, deja el histórico como estaba
        /// —des-anula la fila original y descarta la que el trigger de alta vuelve a crear— y resta
        /// otra vez los kilos del stock.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RESTAURAR_SQL);
        }
    }
}
