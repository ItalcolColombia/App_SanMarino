namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para asignar una empresa a un país
/// </summary>
public record AssignCompanyPaisDto(
    int CompanyId,
    int PaisId
);

/// <summary>
/// DTO para remover la asignación de una empresa a un país
/// </summary>
public record RemoveCompanyPaisDto(
    int CompanyId,
    int PaisId
);

/// <summary>
/// DTO para asignar un usuario a una empresa en un país específico
/// </summary>
public record AssignUserCompanyPaisDto(
    Guid UserId,
    int CompanyId,
    int PaisId,
    bool IsDefault = false
);

/// <summary>
/// DTO para remover la asignación de un usuario a una empresa-país
/// </summary>
public record RemoveUserCompanyPaisDto(
    Guid UserId,
    int CompanyId,
    int PaisId
);




