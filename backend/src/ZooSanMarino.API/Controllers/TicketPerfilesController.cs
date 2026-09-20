using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Configuración de tickets: quién puede ABRIR (nivel del usuario y de sus roles) y quién ATIENDE
/// (resolutores por usuario o por rol, con alcance EMPRESA o GLOBAL).
/// </summary>
/// <remarks>
/// Las escrituras exigen admin global o <c>tickets.admin</c> de la empresa del destino, y tocar GLOBAL
/// solo el admin global (<c>TicketPerfilAutorizacionCalculos</c>) ⇒ 403. Empresa ambigua o datos
/// inválidos ⇒ 400. Hasta el 19-sep-2026 no había ningún gate: cualquier sesión podía hacerse
/// Implementador o resolutor con un PUT sobre su propio id.
/// </remarks>
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

    /// <summary>
    /// Perfil de tickets de un usuario en SU empresa (o en <paramref name="companyId"/>, solo para el
    /// admin global u otra empresa activa válida).
    /// </summary>
    [HttpGet("usuario/{userId:guid}")]
    [ProducesResponseType(typeof(TicketPerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TicketPerfilDto>> GetPerfilUsuario(
        Guid userId, [FromQuery] int? companyId = null, CancellationToken ct = default)
    {
        try { return Ok(await _svc.GetPerfilUsuarioAsync(userId, companyId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
    }

    /// <summary>Crea o actualiza el perfil de tickets de un usuario.</summary>
    [HttpPut("usuario/{userId:guid}")]
    [ProducesResponseType(typeof(TicketPerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TicketPerfilDto>> UpsertPerfilUsuario(
        Guid userId, [FromBody] UpsertTicketPerfilRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.UpsertPerfilUsuarioAsync(userId, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
    }

    // ── Configuración de rol (abrir + atender) ───────────────────────────

    /// <summary>Qué puede ABRIR y qué ATIENDE quien tenga el rol, en la empresa del rol.</summary>
    [HttpGet("rol/{roleId:int}")]
    [ProducesResponseType(typeof(TicketResolutorRolDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TicketResolutorRolDto>> GetPerfilRol(
        int roleId, [FromQuery] int? companyId = null, CancellationToken ct = default)
    {
        try { return Ok(await _svc.GetPerfilRolAsync(roleId, companyId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
    }

    /// <summary>Crea o actualiza la configuración de tickets de un rol.</summary>
    [HttpPut("rol/{roleId:int}")]
    [ProducesResponseType(typeof(TicketResolutorRolDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TicketResolutorRolDto>> UpsertPerfilRol(
        int roleId, [FromBody] UpsertTicketResolutorRolRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.UpsertPerfilRolAsync(roleId, req, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
    }
}
