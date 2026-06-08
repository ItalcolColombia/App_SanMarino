using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Gestión de permisos por objeto (grants) de DB Studio. Solo admin.
/// </summary>
public interface IDbStudioPermissionService
{
    Task<IEnumerable<ObjectGrantDto>> GetGrantsByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ObjectGrantDto>> GetAllGrantsAsync(CancellationToken ct = default);
    Task<ObjectGrantDto> UpsertGrantAsync(GrantRequest request, CancellationToken ct = default);
    Task RevokeGrantAsync(long grantId, CancellationToken ct = default);
    Task RevokeGrantAsync(Guid userId, string schema, string objectName, CancellationToken ct = default);
}
