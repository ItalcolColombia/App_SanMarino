// src/ZooSanMarino.API/Infrastructure/GuiaGeneticaEscrituraGuard.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>
/// Guarda de ESCRITURA de las guías genéticas, compartida por los tres controllers que escriben una:
/// <c>GuiaGeneticaSantaReyesController</c> (tabla reducida), <c>ProduccionAvicolaRawController</c> y
/// <c>ExcelImportController</c> (tabla compartida).
///
/// <para>
/// Son <b>dos puertas</b> y hay que pasar las dos:
/// </para>
/// <list type="number">
///   <item><description>
///     <see cref="ExigirPermisoGuiaGenetica"/> — <i>quién</i>. El permiso sale del claim
///     <c>permission</c> del JWT, al que se llega por <c>ControllerBase.User</c> (la misma fuente que
///     usa <c>HttpCurrentUser</c>), así que ningún controller cambia su constructor para adoptarlo.
///     Es el mismo criterio por el que <c>VentanaFechaRegistroGuard</c> es una extensión y no un
///     servicio inyectado.
///   </description></item>
///   <item><description>
///     <see cref="ExigirPerfilGuiaGeneticaAsync"/> — <i>dónde</i>. Fail-closed en los dos sentidos:
///     una empresa de perfil <c>reducida</c> no escribe la tabla compartida y una de perfil
///     <c>sanmarino</c> no escribe la reducida. Nunca se cae al otro perfil: hacer INALCANZABLE el
///     estado malo es mejor que manejarlo.
///   </description></item>
/// </list>
///
/// <para>
/// <b>Por qué 403 con cuerpo y no <c>Forbid()</c>:</b> el status es el mismo, pero <c>Forbid()</c>
/// devuelve el cuerpo vacío y las pantallas del repo leen <c>err.error?.message</c> /
/// <c>err.error?.error</c> — el usuario vería un toast en blanco. El cuerpo lleva el mismo texto en
/// las dos claves a propósito, por el mismo motivo que <c>VentanaFechaRegistroGuard.Rechazo</c>.
/// </para>
/// </summary>
public static class GuiaGeneticaEscrituraGuard
{
    /// <summary>
    /// Rechaza si al usuario le falta <c>guia_genetica.gestionar</c>. Devuelve el 403 ya armado, o
    /// <c>null</c> si puede escribir.
    /// </summary>
    public static ActionResult? ExigirPermisoGuiaGenetica(this ControllerBase controller)
    {
        var permisos = controller.User.FindAll("permission").Select(c => c.Value);

        return GuiaGeneticaEscrituraAutorizacionCalculos.PuedeGestionar(permisos)
            ? null
            : Rechazo(GuiaGeneticaEscrituraAutorizacionCalculos.MensajeSinPermiso);
    }

    /// <summary>
    /// Rechaza si la empresa activa no usa el modelo de guía que administra esta tabla. Devuelve el
    /// 403 ya armado, o <c>null</c> si el perfil coincide.
    /// </summary>
    /// <param name="perfilDeLaTabla">
    /// <c>GuiaGeneticaPerfilCalculos.Reducida</c> para el módulo de la tabla plana;
    /// <c>GuiaGeneticaPerfilCalculos.Sanmarino</c> para el de la tabla ancha compartida.
    /// </param>
    public static async Task<ActionResult?> ExigirPerfilGuiaGeneticaAsync(
        this ControllerBase controller,
        IGuiaGeneticaPerfilResolver resolver,
        string perfilDeLaTabla,
        CancellationToken ct = default)
    {
        var perfilEmpresa = await resolver.PerfilEmpresaActivaAsync(ct);

        return GuiaGeneticaEscrituraAutorizacionCalculos.PuedeEscribirEnPerfil(perfilEmpresa, perfilDeLaTabla)
            ? null
            : Rechazo(GuiaGeneticaEscrituraAutorizacionCalculos.MensajePerfilIncorrecto(perfilDeLaTabla));
    }

    /// <summary>
    /// Las dos puertas de una vez, en el orden en que le sirven al usuario: primero el perfil (te
    /// equivocaste de módulo) y después el permiso (te falta autorización).
    /// </summary>
    public static async Task<ActionResult?> ExigirEscrituraGuiaGeneticaAsync(
        this ControllerBase controller,
        IGuiaGeneticaPerfilResolver resolver,
        string perfilDeLaTabla,
        CancellationToken ct = default)
        => await controller.ExigirPerfilGuiaGeneticaAsync(resolver, perfilDeLaTabla, ct)
           ?? controller.ExigirPermisoGuiaGenetica();

    private static ActionResult Rechazo(string mensaje) =>
        new ObjectResult(new { message = mensaje, error = mensaje })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
}
