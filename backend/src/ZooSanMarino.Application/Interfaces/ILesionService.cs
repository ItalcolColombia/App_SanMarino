using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.DTOs.Lesiones;

namespace ZooSanMarino.Application.Interfaces;

public interface ILesionService
{
    Task<LesionDto?> GetByIdAsync(long id, CancellationToken ct);
    Task<PagedResult<LesionDto>> SearchAsync(LesionSearchRequest req, CancellationToken ct);
    Task<LesionDto> CreateAsync(CreateLesionRequest req, CancellationToken ct);
    Task<LesionDto?> UpdateAsync(long id, UpdateLesionRequest req, CancellationToken ct);
    Task<bool> DeleteAsync(long id, CancellationToken ct);
    Task<IEnumerable<LesionResumenDto>> GetResumenAsync(
        string? moduloOrigen,
        int?    clienteId,
        int?    farmId,
        int?    loteId,
        string? galponId,
        string? loteReproductoraId,
        CancellationToken ct);
}
