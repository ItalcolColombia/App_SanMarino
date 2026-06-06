using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.DTOs.Tickets;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Puerto del módulo de tickets. El scoping multi-tenant (company) y por país/usuario
/// se resuelve dentro de la implementación a partir de <see cref="ICurrentUser"/>.
/// </summary>
public interface ITicketService
{
    // ── Solicitante ──────────────────────────────────────────────
    Task<TicketDetailDto> CreateAsync(CreateTicketRequest req, CancellationToken ct);
    Task<PagedResult<TicketListItemDto>> SearchMisTicketsAsync(TicketSearchRequest req, CancellationToken ct);
    Task<TicketDetailDto?> GetByIdAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<TicketImagenMetaDto>> GetImagenesMetaAsync(long ticketId, CancellationToken ct);
    Task<TicketImagenDto?> GetImagenAsync(long ticketId, long imagenId, CancellationToken ct);
    Task<int> AddImagenesAsync(long ticketId, AddTicketImagenesRequest req, CancellationToken ct);
    Task<TicketNotaDto?> AddNotaAsync(long ticketId, CreateTicketNotaRequest req, CancellationToken ct);

    // ── Adjuntos (documentos Excel/PDF + links) ──────────────────
    Task<IReadOnlyList<TicketAdjuntoDto>> GetAdjuntosAsync(long ticketId, CancellationToken ct);
    Task<TicketAdjuntoDto?> AddDocumentoAsync(long ticketId, AddTicketDocumentoRequest req, CancellationToken ct);
    Task<TicketAdjuntoDto?> AddLinkAsync(long ticketId, AddTicketLinkRequest req, CancellationToken ct);
    Task<TicketDocumentoDto?> GetDocumentoAsync(long ticketId, long adjuntoId, CancellationToken ct);
    Task<bool> DeleteAdjuntoAsync(long ticketId, long adjuntoId, CancellationToken ct);

    // ── Resolutor ────────────────────────────────────────────────
    Task<PagedResult<TicketListItemDto>> SearchGestionAsync(TicketSearchRequest req, CancellationToken ct);
    Task<TicketDetailDto?> TomarAsync(long id, CancellationToken ct);
    Task<TicketDetailDto?> CambiarEstadoAsync(long id, CambiarEstadoTicketRequest req, CancellationToken ct);

    /// <summary>El solicitante confirma el cierre de un ticket SOLUCIONADO → CERRADO.</summary>
    Task<TicketDetailDto?> ConfirmarCierreAsync(long id, ConfirmarCierreRequest req, CancellationToken ct);

    // ── Super Admin ──────────────────────────────────────────────
    Task<PagedResult<TicketListItemDto>> SearchAdminAsync(TicketSearchRequest req, CancellationToken ct);

    // ── Transferir (REQUERIMIENTO -> DESARROLLO) ─────────────────
    Task<TicketDetailDto?> TransferirAsync(long id, TransferirTicketRequest req, CancellationToken ct);

    // ── Bandeja personal del resolutor ────────────────────────────
    Task<PagedResult<TicketListItemDto>> GetAsignadosAsync(TicketSearchRequest req, CancellationToken ct);

    // ── Común ────────────────────────────────────────────────────────
    Task<bool> DeleteAsync(long id, CancellationToken ct);
}
