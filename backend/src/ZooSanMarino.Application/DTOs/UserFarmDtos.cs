namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para representar la asociación entre un usuario y una granja
/// </summary>
public record UserFarmDto(
    Guid UserId,
    int FarmId,
    string UserName,
    string FarmName,
    bool IsAdmin,
    bool IsDefault,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

/// <summary>
/// DTO para crear una nueva asociación usuario-granja
/// </summary>
public record CreateUserFarmDto(
    Guid UserId,
    int FarmId,
    bool IsAdmin = false,
    bool IsDefault = false
);

/// <summary>
/// DTO para actualizar una asociación usuario-granja existente
/// </summary>
public record UpdateUserFarmDto(
    bool? IsAdmin,
    bool? IsDefault
);

/// <summary>
/// DTO para asociar múltiples granjas a un usuario
/// </summary>
public record AssociateUserFarmsDto(
    Guid UserId,
    int[] FarmIds,
    bool IsAdmin = false,
    bool IsDefault = false
);

/// <summary>
/// DTO para asociar múltiples usuarios a una granja
/// </summary>
public record AssociateFarmUsersDto(
    int FarmId,
    Guid[] UserIds,
    bool IsAdmin = false
);

/// <summary>
/// DTO para obtener las granjas de un usuario
/// </summary>
public record UserFarmsResponseDto(
    Guid UserId,
    string UserName,
    UserFarmDto[] Farms
);

/// <summary>
/// DTO para obtener los usuarios de una granja
/// </summary>
public record FarmUsersResponseDto(
    int FarmId,
    string FarmName,
    UserFarmDto[] Users
);

/// <summary>
/// DTO simplificado para mostrar información básica de granja en contexto de usuario
/// </summary>
public record UserFarmLiteDto(
    int FarmId,
    string FarmName,
    bool IsAdmin,
    bool IsDefault
);
