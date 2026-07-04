namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Metadata de un token de servicio (PAT). NUNCA incluye el hash ni el token plano.
/// </summary>
public sealed record ServiceTokenDto(
    long Id,
    string Name,
    string Scopes,
    DateTime? ExpiresAt,
    DateTime? RevokedAt,
    DateTime? LastUsedAt,
    DateTime CreatedAt);
