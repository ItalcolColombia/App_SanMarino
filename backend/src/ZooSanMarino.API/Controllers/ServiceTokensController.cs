using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Administración de tokens de servicio (PAT) para clientes headless (crones que llaman /api/tickets).
/// El token plano se muestra UNA sola vez al emitir; en BD solo vive su SHA-256.
/// </summary>
/// <remarks>
/// Ruta "service-tokens" (NO "admin"): AWS WAF AdminProtection bloquea cualquier path con /admin.
/// Emisión/revocación restringida a rol "Admin" (mismo criterio que ConfigurationController).
/// </remarks>
[ApiController]
[Route("api/service-tokens")]
[Produces("application/json")]
[Authorize(Roles = "Admin")] // TODO revisar policy: hoy no hay policy de permiso dedicada; se usa el rol Admin como en ConfigurationController.
public class ServiceTokensController : ControllerBase
{
    private readonly IServiceTokenService _service;
    private readonly ICurrentUser _current;

    public ServiceTokensController(IServiceTokenService service, ICurrentUser current)
    {
        _service = service;
        _current = current;
    }

    /// <summary>
    /// Emite un token nuevo. El dueño (owner) es SIEMPRE el usuario actual (ICurrentUser); nunca el body.
    /// Devuelve el token plano una única vez + la metadata.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(IssueServiceTokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IssueServiceTokenResponse>> Issue(
        [FromBody] IssueServiceTokenRequest req, CancellationToken ct)
    {
        var owner = _current.UserGuid;
        if (owner is null || owner.Value == Guid.Empty)
            return BadRequest("No se pudo identificar al usuario actual (UserGuid).");

        try
        {
            var (plain, dto) = await _service.IssueAsync(
                req.Name, owner.Value, req.Scopes ?? string.Empty, req.ExpiresAt, ct);

            return CreatedAtAction(nameof(Issue), new { id = dto.Id },
                new IssueServiceTokenResponse(plain, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Revoca un token por Id. 404 si no existe o ya estaba revocado.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(long id, CancellationToken ct)
    {
        var ok = await _service.RevokeAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}

/// <summary>Body para emitir un token. El owner NO viaja en el body (se toma de ICurrentUser).</summary>
public sealed record IssueServiceTokenRequest(string Name, string? Scopes, DateTime? ExpiresAt);

/// <summary>Respuesta de emisión: token plano (mostrar una vez) + metadata.</summary>
public sealed record IssueServiceTokenResponse(string Token, ServiceTokenDto Metadata);
