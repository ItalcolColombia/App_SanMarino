using ZooSanMarino.Application.DTOs.Tickets;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Puerto del subsistema de configuración de tickets: quién puede ABRIR (nivel del usuario y de sus
/// roles) y quién ATIENDE (resolutores por usuario o por rol, con alcance EMPRESA o GLOBAL).
/// </summary>
/// <remarks>
/// Las escrituras lanzan <see cref="UnauthorizedAccessException"/> (403) cuando la sesión no puede
/// configurar ese destino, e <see cref="InvalidOperationException"/> (400) ante datos inválidos o una
/// empresa ambigua. Ver <c>TicketPerfilAutorizacionCalculos</c> y <c>TicketPerfilEmpresaCalculos</c>.
/// </remarks>
public interface ITicketPerfilService
{
    // ── Consultas para el formulario de crear ticket ──────────────────────────
    /// <summary>
    /// Tipos que el usuario actual puede crear según su nivel efectivo Y que tienen
    /// al menos un resolutor en la empresa activa (o global), con los asignables.
    /// </summary>
    Task<IReadOnlyList<TipoPermitidoDto>> GetTiposPermitidosAsync(CancellationToken ct);

    /// <summary>Usuarios resolutores disponibles para un tipo y país, en la empresa activa.</summary>
    Task<IReadOnlyList<AsignableDto>> GetAsignablesAsync(string tipo, int? paisId, CancellationToken ct);

    // ── Perfil de usuario ────────────────────────────────────────────────────
    /// <param name="companyId">Null = la empresa del usuario.</param>
    Task<TicketPerfilDto> GetPerfilUsuarioAsync(Guid userId, int? companyId, CancellationToken ct);
    Task<TicketPerfilDto> UpsertPerfilUsuarioAsync(Guid userId, UpsertTicketPerfilRequest req, CancellationToken ct);

    // ── Configuración de rol (abrir + atender) ────────────────────────────────
    /// <param name="companyId">Null = la empresa del rol.</param>
    Task<TicketResolutorRolDto> GetPerfilRolAsync(int roleId, int? companyId, CancellationToken ct);
    Task<TicketResolutorRolDto> UpsertPerfilRolAsync(int roleId, UpsertTicketResolutorRolRequest req, CancellationToken ct);
}
