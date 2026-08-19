// Endpoint liviano de sesión: heartbeat autenticado usado por el front para
//   (a) detectar pérdida de conexión con el backend, y
//   (b) capturar la expiración real del token (responde 401 cuando el JWT venció).
// Requiere token válido (FallbackPolicy = RequireAuthenticatedUser). Está EXCLUIDO del
// rate limiter (ver RateLimitingMiddleware) para no bloquear IPs compartidas (oficinas NAT).
//
// Desde B1 cuelgan de acá los endpoints de sesiones (mías / de un usuario / revocar). Van bajo
// `/api/session` a propósito: ese prefijo YA está en EXCLUIDOS de la lista cacheable del front
// (un `/api/sesiones` nuevo cortaría el gate de CI) y no contiene «admin», que el WAF bloquea.
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Infrastructure.Services;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SessionController : ControllerBase
{
    private readonly ISesionActivaService _sesiones;
    private readonly ICurrentUser _current;
    private readonly ZooSanMarinoContext _ctx;

    public SessionController(ISesionActivaService sesiones, ICurrentUser current, ZooSanMarinoContext ctx)
    {
        _sesiones = sesiones;
        _current = current;
        _ctx = ctx;
    }

    /// <summary>Heartbeat de sesión. 200 si el token es válido; 401 si expiró/es inválido.</summary>
    /// <remarks>
    /// La respuesta es la misma de siempre (`ok` + `serverTimeUtc`). Lo que se agregó es el efecto:
    /// marca el último contacto real del dispositivo (con throttle de 5 min) y aprovecha el paso
    /// para la limpieza perezosa de sesiones vencidas hace más de 30 días.
    /// </remarks>
    [HttpGet("heartbeat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Heartbeat(CancellationToken ct)
    {
        await _sesiones.TocarAsync(JtiActual(), ct);
        await _sesiones.LimpiarVencidasAsync(ct);
        return Ok(new { ok = true, serverTimeUtc = DateTime.UtcNow });
    }

    /// <summary>Mis dispositivos: las sesiones abiertas con mi usuario. La actual viene marcada.</summary>
    [HttpGet("mias")]
    [ProducesResponseType(typeof(IReadOnlyList<SesionActivaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SesionActivaDto>>> Mias(
        [FromQuery] bool incluirRevocadas = false, CancellationToken ct = default)
    {
        var yo = _current.UserGuid;
        if (yo is null || yo.Value == Guid.Empty)
            return BadRequest("No se pudo identificar al usuario actual.");

        return Ok(await _sesiones.ListarDeUsuarioAsync(yo.Value, JtiActual(), incluirRevocadas, ct));
    }

    /// <summary>
    /// Cierro una sesión MÍA (la de la tablet que perdí, sin esperar a un administrador).
    /// Sólo sobre filas de mi propio usuario: la comparación es contra el Guid del token ya
    /// validado, nunca contra un id del body.
    /// </summary>
    [HttpDelete("mias/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CerrarMia(long id, [FromBody] RevocarSesionRequest? req, CancellationToken ct)
    {
        var dueno = await _sesiones.ObtenerDuenoAsync(id, ct);
        if (dueno is null) return NotFound();

        if (!RevocacionSesionCalculos.PuedeRevocarSesionPropia(_current.UserGuid, dueno.Value.UserId))
            return Forbid();

        var ok = await _sesiones.RevocarAsync(
            id, _current.UserGuid, req?.Motivo ?? "Cerrada por el propio usuario", ct);

        return ok ? NoContent() : NotFound();
    }

    /// <summary>Sesiones de un usuario cualquiera. Super admin o permiso <c>usuarios.revocar_sesion</c>.</summary>
    [HttpGet("de-usuario/{userId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<SesionActivaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SesionActivaDto>>> DeUsuario(
        Guid userId, [FromQuery] bool incluirRevocadas = false, CancellationToken ct = default)
    {
        // Mis propias sesiones no exigen permiso de administración.
        if (!RevocacionSesionCalculos.PuedeRevocarSesionPropia(_current.UserGuid, userId) &&
            !await PuedeAdministrarAsync(ct))
            return Forbid();

        return Ok(await _sesiones.ListarDeUsuarioAsync(userId, JtiActual(), incluirRevocadas, ct));
    }

    /// <summary>
    /// Revoca la sesión de cualquiera. Es lo que se usa cuando se pierde una tablet.
    /// <b>Surte efecto en menos de un minuto</b> desde que el dispositivo toque la red (la caché de
    /// verificación dura 60 s por tarea); no es instantáneo y no hay que prometerlo como tal.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revocar(long id, [FromBody] RevocarSesionRequest? req, CancellationToken ct)
    {
        var dueno = await _sesiones.ObtenerDuenoAsync(id, ct);
        if (dueno is null) return NotFound();

        var esPropia = RevocacionSesionCalculos.PuedeRevocarSesionPropia(_current.UserGuid, dueno.Value.UserId);
        if (!esPropia && !await PuedeAdministrarAsync(ct))
            return Forbid();

        var ok = await _sesiones.RevocarAsync(id, _current.UserGuid, req?.Motivo, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>
    /// Revoca TODAS las sesiones de un usuario. Devuelve cuántas apagó.
    /// </summary>
    [HttpDelete("de-usuario/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevocarTodas(
        Guid userId, [FromBody] RevocarSesionRequest? req, CancellationToken ct)
    {
        if (!RevocacionSesionCalculos.PuedeRevocarSesionPropia(_current.UserGuid, userId) &&
            !await PuedeAdministrarAsync(ct))
            return Forbid();

        var cuantas = await _sesiones.RevocarTodasDelUsuarioAsync(userId, _current.UserGuid, req?.Motivo, ct);
        return Ok(new { revocadas = cuantas });
    }

    /// <summary>
    /// ¿Puede administrar sesiones ajenas? La decisión es pura; acá sólo se traen los dos datos.
    /// No se usa <c>[Authorize(Roles="Admin")]</c>: ese atajo ya quedó anotado como deuda en
    /// <c>ServiceTokensController</c> y no se replica.
    /// </summary>
    private async Task<bool> PuedeAdministrarAsync(CancellationToken ct)
    {
        var esSuperAdmin = await SuperAdminLookup.EsSuperAdminAsync(_ctx, _current.UserGuid, ct);
        return RevocacionSesionCalculos.PuedeRevocarSesionDeOtro(esSuperAdmin, _current.Permissions);
    }

    /// <summary>El <c>jti</c> del token con el que se está pidiendo. Nulo en tokens anteriores a B1.</summary>
    private string? JtiActual() => User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
}
