using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para gestionar permisos y validaciones de usuario
/// </summary>
public interface IUserPermissionService
{
    /// <summary>
    /// Obtiene los países asignados al usuario basado en sus empresas
    /// </summary>
    Task<IEnumerable<PaisDto>> GetAssignedCountriesAsync(int userId);
    
    /// <summary>
    /// Valida si el usuario puede crear granjas en el país especificado
    /// </summary>
    Task<bool> CanCreateFarmInCountryAsync(int userId, int paisId);
    
    /// <summary>
    /// Obtiene los usuarios que pertenecen a las empresas asignadas al usuario actual
    /// </summary>
    Task<IEnumerable<UserBasicDto>> GetUsersFromAssignedCompaniesAsync(int userId);
    
    /// <summary>
    /// Valida si el usuario puede asignar otros usuarios a granjas
    /// </summary>
    Task<bool> CanAssignUserToFarmAsync(int currentUserId, Guid targetUserId);
}
