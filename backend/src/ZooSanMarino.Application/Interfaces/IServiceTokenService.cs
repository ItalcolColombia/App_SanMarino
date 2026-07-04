using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Emisión / revocación / validación de tokens de servicio (PAT) de larga duración.
/// El plano se devuelve UNA sola vez al emitir; en BD solo vive el SHA-256 hex.
/// </summary>
public interface IServiceTokenService
{
    /// <summary>
    /// Emite un token nuevo para <paramref name="ownerUserId"/>. Devuelve el plano (mostrar una vez)
    /// y la metadata. Persiste solo el hash.
    /// </summary>
    Task<(string PlainToken, ServiceTokenDto Dto)> IssueAsync(
        string name, Guid ownerUserId, string scopes, DateTime? expiresAt, CancellationToken ct);

    /// <summary>Revoca (marca RevokedAt) el token. Devuelve false si no existe o ya estaba revocado.</summary>
    Task<bool> RevokeAsync(long id, CancellationToken ct);

    /// <summary>
    /// Valida un token plano: hash coincidente, no revocado, no expirado.
    /// Si es válido actualiza LastUsedAt y devuelve la entidad; si no, null.
    /// </summary>
    Task<ServiceToken?> ValidateAsync(string plainToken, CancellationToken ct);
}
