using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para gestionar las asociaciones entre usuarios y granjas
/// </summary>
public interface IUserFarmService
{
    /// <summary>
    /// Obtiene todas las granjas asociadas a un usuario
    /// </summary>
    Task<UserFarmsResponseDto> GetUserFarmsAsync(Guid userId);

    /// <summary>
    /// Obtiene todos los usuarios asociados a una granja
    /// </summary>
    Task<FarmUsersResponseDto> GetFarmUsersAsync(int farmId);

    /// <summary>
    /// Crea una nueva asociación usuario-granja
    /// </summary>
    Task<UserFarmDto> CreateUserFarmAsync(CreateUserFarmDto dto, Guid createdByUserId);

    /// <summary>
    /// Actualiza una asociación usuario-granja existente
    /// </summary>
    Task<UserFarmDto?> UpdateUserFarmAsync(Guid userId, int farmId, UpdateUserFarmDto dto);

    /// <summary>
    /// Elimina una asociación usuario-granja
    /// </summary>
    Task<bool> DeleteUserFarmAsync(Guid userId, int farmId);

    /// <summary>
    /// Asocia múltiples granjas a un usuario
    /// </summary>
    Task<UserFarmDto[]> AssociateUserFarmsAsync(AssociateUserFarmsDto dto, Guid createdByUserId);

    /// <summary>
    /// Asocia múltiples usuarios a una granja
    /// </summary>
    Task<UserFarmDto[]> AssociateFarmUsersAsync(AssociateFarmUsersDto dto, Guid createdByUserId);

    /// <summary>
    /// Reemplaza todas las granjas asociadas a un usuario
    /// </summary>
    Task<UserFarmDto[]> ReplaceUserFarmsAsync(Guid userId, int[] farmIds, Guid createdByUserId);

    /// <summary>
    /// Reemplaza todos los usuarios asociados a una granja
    /// </summary>
    Task<UserFarmDto[]> ReplaceFarmUsersAsync(int farmId, Guid[] userIds, Guid createdByUserId);

    /// <summary>
    /// Verifica si un usuario tiene acceso a una granja específica
    /// </summary>
    Task<bool> HasUserAccessToFarmAsync(Guid userId, int farmId);

    /// <summary>
    /// Obtiene las granjas a las que un usuario tiene acceso (incluyendo las de sus compañías)
    /// </summary>
    Task<UserFarmLiteDto[]> GetUserAccessibleFarmsAsync(Guid userId);
}
