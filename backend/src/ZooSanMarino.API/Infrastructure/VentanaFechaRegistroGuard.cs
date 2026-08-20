// src/ZooSanMarino.API/Infrastructure/VentanaFechaRegistroGuard.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>
/// Guarda de la ventana de fechas de los registros cargados A MANO, compartida por todos los
/// controllers del alcance (gestión de inventario, gastos, movimientos de alimento, movimientos de
/// aves, movimientos y ventas de pollo engorde, traslados de aves y de huevos).
///
/// <para>
/// <b>Por qué es una extensión de <see cref="ControllerBase"/> y no un servicio inyectado:</b> el
/// permiso sale del claim <c>permission</c> del JWT, al que se llega por <c>ControllerBase.User</c>
/// —la misma fuente que usa <c>HttpCurrentUser</c>—, así que ningún controller tiene que cambiar su
/// constructor ni su DI para adoptar la guarda.
/// </para>
///
/// <para>
/// <b>Por qué vive en la capa API y no en los services:</b> los services los comparten la carga
/// masiva, las devoluciones de alimento al editar o borrar un seguimiento diario y la anulación de
/// gastos, que fechan histórico A PROPÓSITO. El controller es la única frontera «esto lo tipeó una
/// persona en una pantalla».
/// </para>
/// </summary>
public static class VentanaFechaRegistroGuard
{
    /// <summary>
    /// ¿El usuario de la request tiene el permiso de fecha retroactiva
    /// (<see cref="VentanaFechaRegistroCalculos.PermisoFechaRetroactiva"/>)?
    /// </summary>
    public static bool PuedeFecharRetroactivo(this ControllerBase controller) =>
        VentanaFechaRegistroCalculos.TienePermisoRetroactivo(
            controller.User.FindAll("permission").Select(c => c.Value));

    /// <summary>Día operativo (UTC−5) en el que se está evaluando la request.</summary>
    public static DateTime DiaOperativoActual(this ControllerBase _) =>
        VentanaFechaRegistroCalculos.DiaOperativo(DateTimeOffset.UtcNow);

    /// <summary>
    /// Valida la fecha contra la ventana admitida. Devuelve el <c>400</c> ya armado, o <c>null</c> si
    /// la fecha es aceptable (incluido <c>null</c>, que significa «sin fecha explícita» y lo resuelve
    /// el servicio con la hora actual).
    /// <para>
    /// Devuelve <see cref="ActionResult"/> y no <see cref="IActionResult"/> para que el mismo
    /// <c>return</c> sirva en las acciones que declaran <c>Task&lt;IActionResult&gt;</c> y en las que
    /// declaran <c>Task&lt;ActionResult&lt;T&gt;&gt;</c> (esta última convierte desde
    /// <c>ActionResult</c>, no desde la interfaz).
    /// </para>
    /// </summary>
    public static ActionResult? ValidarVentanaFechaRegistro(this ControllerBase controller, DateTime? fecha)
    {
        var puedeRetroactivar = controller.PuedeFecharRetroactivo();
        var hoy = controller.DiaOperativoActual();

        return VentanaFechaRegistroCalculos.EsFechaPermitida(fecha, hoy, puedeRetroactivar)
            ? null
            : Rechazo(controller, VentanaFechaRegistroCalculos.MensajeFueraDeVentana(hoy, puedeRetroactivar));
    }

    /// <summary>
    /// Igual que la sobrecarga de <see cref="DateTime"/>, para los DTOs que declaran la fecha como
    /// <see cref="DateTimeOffset"/> (los movimientos de inventario de granja).
    /// <para>
    /// Se compara <c>fecha.Date</c>, que es el día tal como lo escribió el usuario dentro de su propio
    /// offset. Convertirlo a UTC primero correría un día hacia atrás cualquier fecha enviada a
    /// medianoche con offset negativo — que es exactamente lo que manda un <c>input[type=date]</c>.
    /// </para>
    /// </summary>
    public static ActionResult? ValidarVentanaFechaRegistro(this ControllerBase controller, DateTimeOffset? fecha) =>
        controller.ValidarVentanaFechaRegistro(fecha?.Date);

    /// <summary>
    /// Cuerpo del rechazo. Lleva el mismo texto en <c>message</c> y en <c>error</c> a propósito: los
    /// formularios del alcance leen unos <c>err.error?.message</c> y otros <c>err.error?.error</c>, y
    /// con una sola clave la mitad de las pantallas mostraría un toast vacío.
    /// </summary>
    private static ActionResult Rechazo(ControllerBase controller, string mensaje) =>
        controller.BadRequest(new { message = mensaje, error = mensaje });
}
