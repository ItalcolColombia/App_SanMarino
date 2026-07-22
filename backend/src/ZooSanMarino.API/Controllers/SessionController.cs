// Endpoint liviano de sesión: heartbeat autenticado usado por el front para
//   (a) detectar pérdida de conexión con el backend, y
//   (b) capturar la expiración real del token (responde 401 cuando el JWT venció).
// Requiere token válido (FallbackPolicy = RequireAuthenticatedUser). Está EXCLUIDO del
// rate limiter (ver RateLimitingMiddleware) para no bloquear IPs compartidas (oficinas NAT).
using Microsoft.AspNetCore.Mvc;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SessionController : ControllerBase
{
    /// <summary>Heartbeat de sesión. 200 si el token es válido; 401 si expiró/es inválido.</summary>
    [HttpGet("heartbeat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Heartbeat()
        => Ok(new { ok = true, serverTimeUtc = DateTime.UtcNow });
}
