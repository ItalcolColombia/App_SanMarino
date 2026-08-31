using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Devuelve el alimento de los seguimientos de <b>reproductora</b> que se borraron estando ya
    /// confirmados: su consumo se aplicó al inventario y el borrado nunca lo restituyó. Medido en la
    /// copia de producción: <b>3 reservas huérfanas, 952,560 kg</b> en ItalcolPanama.
    /// </summary>
    /// <remarks>
    /// <b>Qué lo produjo.</b> Con la doble validación encendida, el borrado de un seguimiento de
    /// reproductora llamaba a <c>LiberarAsync</c>, que solo toca reservas <c>ACTIVA</c>; la de un
    /// registro confirmado está <c>APLICADA</c>, así que no se liberaba. Y el bloque que devolvía el
    /// stock quedaba inalcanzable, porque su ubicación se calculaba con <c>!separaDel</c>. Resultado:
    /// el consumo desaparecía del sistema y los kilos no volvían al galpón. Reproductora era el único
    /// de los cinco módulos sin la guarda de «no se puede eliminar un validado» — y encima el mensaje
    /// de edición mandaba, textual, a eliminar «(se retornan aves y consumo)». Las dos cosas se
    /// arreglan en el mismo commit; esto repara lo que ya quedó escrito.
    ///
    /// <b>Acá SÍ corresponde un ingreso de devolución</b>, al revés que en la reversión de consumos
    /// duplicados. Allá había una salida que nunca debió existir y se borró; acá el consumo fue
    /// legítimo cuando se hizo, y lo que falta es la contrapartida que el borrado debió registrar. Es
    /// exactamente lo que hacen <c>DesvalidarAsync</c> y el propio camino de borrado del modelo B, con
    /// la misma referencia trazable (<c>… (devolución por eliminación)</c>).
    ///
    /// <b>Se fecha en el día del seguimiento</b>, no en el de hoy: es el criterio de
    /// <c>DesvalidarAsync</c> («misma fecha que la confirmación que se está deshaciendo»), y evita que
    /// el saldo del galpón quede con un hueco entre el consumo y su devolución.
    ///
    /// <b>Las aves NO se tocan.</b> Sus reservas huérfanas también quedaron en <c>APLICADA</c>, pero
    /// en reproductora las bajas no las escribe <c>AplicarAvesAsync</c> sino el cruce que dispara la
    /// marca de confirmación, y ese cruce se rehace solo al borrar el registro. Reponerlas a mano
    /// descuadraría el maestro por partida doble. Se marcan <c>LIBERADA</c> para que no queden
    /// contando como separación viva, y nada más.
    ///
    /// <b>Idempotente.</b> Se salta cualquier reserva que ya tenga su movimiento de devolución, y solo
    /// mira reservas <c>APLICADA</c> cuyo seguimiento de origen <b>ya no existe</b>. Corrida dos veces
    /// no devuelve dos veces.
    ///
    /// Plan: <c>fase_de_desarrollo/correccion_hallazgos_auditoria_tickets_plan.md</c> (hallazgo #1).
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto.
    /// </remarks>
    public partial class DevolverAlimentoDeReproductorasBorradas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DEVOLUCION_SQL);
        }

        /// <summary>
        /// Borra los ingresos de devolución que sembró el <c>Up</c> —localizados por su referencia
        /// exacta—, vuelve a restar los kilos del stock y devuelve las reservas a <c>APLICADA</c>.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DESHACER_SQL);
        }
    }
}
