using ZooSanMarino.Application.DTOs.Tickets;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Puerto del subsistema de perfiles de atención (resolutores + niveles).
/// Gestiona quién atiende qué tipo de ticket en qué país y el nivel del solicitante.
/// </summary>
public interface ITicketPerfilService
{
    // ── Consultas para el formulario de crear ticket ──────────────────────────
    /// <summary>
    /// Tipos que el usuario actual puede crear según su nivel Y que tienen
    /// al menos un resolutor disponible en su país (o global), con los asignables.
    /// </summary>
    Task<IReadOnlyList<TipoPermitidoDto>> GetTiposPermitidosAsync(CancellationToken ct);

    /// <summary>Usuarios resolutores disponibles para un tipo y país.</summary>
    Task<IReadOnlyList<AsignableDto>> GetAsignablesAsync(string tipo, int? paisId, CancellationToken ct);

    // ── Gestión de perfiles de usuario ────────────────────────────────────────
    Task<TicketPerfilDto> GetPerfilUsuarioAsync(Guid userId, CancellationToken ct);
    Task<TicketPerfilDto> UpsertPerfilUsuarioAsync(Guid userId, UpsertTicketPerfilRequest req, CancellationToken ct);

    // ── Gestión de perfiles de rol (defaults) ─────────────────────────────────
    Task<TicketResolutorRolDto> GetPerfilRolAsync(int roleId, CancellationToken ct);
    Task<TicketResolutorRolDto> UpsertPerfilRolAsync(int roleId, UpsertTicketResolutorRolRequest req, CancellationToken ct);

    /// <summary>
    /// Siembra en <c>ticket_resolutores</c> los perfiles del rol para el usuario dado,
    /// si aún no existen. Se llama al asignar un rol a un usuario.
    /// </summary>
    Task SeedPerfilDesdeRolAsync(Guid userId, int roleId, CancellationToken ct);
}
