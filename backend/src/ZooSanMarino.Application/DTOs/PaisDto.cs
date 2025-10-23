namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para información de país
/// </summary>
public record PaisDto(
    int PaisId,
    string PaisNombre
);

/// <summary>
/// DTO para información básica de usuario (sin roles ni empresas)
/// </summary>
public record UserBasicDto(
    Guid Id,
    string SurName,
    string FirstName,
    string Cedula,
    string Telefono,
    string Ubicacion,
    bool IsActive,
    bool IsLocked,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

/// <summary>
/// DTO para crear país
/// </summary>
public record CreatePaisDto(
    string PaisNombre
);

/// <summary>
/// DTO para actualizar país
/// </summary>
public record UpdatePaisDto(
    int PaisId,
    string PaisNombre
);