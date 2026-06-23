using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Gestión de perfiles de atención del módulo de tickets.
/// Determina quién atiende qué tipo de ticket en qué país, el nivel del solicitante
/// y los defaults por rol.
/// </summary>
[ApiController]
[Route("api/ticket-perfiles")]
[Produces("application/json")]
public class TicketPerfilesController : ControllerBase
{
    private readonly ITicketPerfilService _svc;
    public TicketPerfilesController(ITicketPerfilService svc) => _svc = svc;

    // ── Solicitante al crear ──────────────────────────────────────────────

    /// <summary>Tipos que el usuario actual puede crear + usuarios asignables por tipo.</summary>
    [HttpGet("tipos-permitidos")]
    [ProducesResponseType(typeof(IEnumerable<TipoPermitidoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TipoPermitidoDto>>> TiposPermitidos(CancellationToken ct)
        => Ok(await _svc.GetTiposPermitidosAsync(ct));

    /// <summary>Usuarios asignables para un tipo y país concreto.</summary>
    [HttpGet("asignables")]
    [ProducesResponseType(typeof(IEnumerable<AsignableDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AsignableDto>>> Asignables(
        [FromQuery] string tipo, [FromQuery] int? paisId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return BadRequest("tipo requerido.");
        return Ok(await _svc.GetAsignablesAsync(tipo, paisId, ct));
    }

    // ── Perfil de usuario ────────────────────────────────────────────────

    /// <summary>Obtiene el perfil de atención de un usuario (nivel + resolutores).</summary>
    [HttpGet("usuario/{userId:guid}")]
    [ProducesResponseType(typeof(TicketPerfilDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketPerfilDto>> GetPerfilUsuario(Guid userId, CancellationToken ct)
        => Ok(await _svc.GetPerfilUsuarioAsync(userId, ct));

    /// <summary>Crea o actualiza el perfil de atención de un usuario.</summary>
    [HttpPut("usuario/{userId:guid}")]
    [ProducesResponseType(typeof(TicketPerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketPerfilDto>> UpsertPerfilUsuario(
        Guid userId, [FromBody] UpsertTicketPerfilRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.UpsertPerfilUsuarioAsync(userId, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // ── Perfil de rol (defaults) ─────────────────────────────────────────

    /// <summary>Obtiene los perfiles de atención de un rol (defaults para sus usuarios).</summary>
    [HttpGet("rol/{roleId:int}")]
    [ProducesResponseType(typeof(TicketResolutorRolDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketResolutorRolDto>> GetPerfilRol(int roleId, CancellationToken ct)
        => Ok(await _svc.GetPerfilRolAsync(roleId, ct));

    /// <summary>Crea o actualiza los perfiles de atención de un rol.</summary>
    [HttpPut("rol/{roleId:int}")]
    [ProducesResponseType(typeof(TicketResolutorRolDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketResolutorRolDto>> UpsertPerfilRol(
        int roleId, [FromBody] UpsertTicketResolutorRolRequest req, CancellationToken ct)
        => Ok(await _svc.UpsertPerfilRolAsync(roleId, req, ct));

    /// <summary>
    /// Siembra perfiles de resolutor de un rol en un usuario (al asignarle el rol).
    /// </summary>
    [HttpPost("usuario/{userId:guid}/seed-desde-rol/{roleId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SeedDesdeRol(Guid userId, int roleId, CancellationToken ct)
    {
        await _svc.SeedPerfilDesdeRolAsync(userId, roleId, ct);
        return NoContent();
    }

    /// <summary>
    /// Re-aplica la plantilla de resolutor del rol a todos sus usuarios en la empresa activa.
    /// Idempotente: solo agrega lo faltante, no elimina ajustes hechos por usuario.
    /// </summary>
    [HttpPost("rol/{roleId:int}/reaplicar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReaplicarPlantillaRol(int roleId, CancellationToken ct)
    {
        await _svc.ReaplicarPlantillaRolAsync(roleId, ct);
        return NoContent();
    }
}
