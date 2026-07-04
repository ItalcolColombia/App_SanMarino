namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// PAT (Service Access Token) de larga duración para clientes headless (crones).
/// Mapea a un usuario existente (dueño); al validarse produce el mismo ClaimsPrincipal
/// que produciría el JWT de ese usuario. NUNCA se persiste el token plano: solo su SHA-256 hex.
/// </summary>
public class ServiceToken
{
    public long Id { get; set; }

    /// <summary>Nombre descriptivo del token (para identificarlo en la lista/administración).</summary>
    public string Name { get; set; } = null!;

    /// <summary>SHA-256 (hex) del token plano. Único. El plano solo se muestra al emitir.</summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>Usuario dueño: sus claims (roles/company/permisos) se replican al autenticar.</summary>
    public Guid UserId { get; set; }

    /// <summary>Scopes separados por espacio (ej. "tickets:read tickets:write").</summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>Expiración opcional (null = no expira por tiempo).</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Fecha de revocación (null = activo).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Último uso exitoso (se actualiza al validar).</summary>
    public DateTime? LastUsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
