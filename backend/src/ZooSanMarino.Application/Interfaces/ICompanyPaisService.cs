using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para gestionar relaciones empresa-país y usuario-empresa-país
/// </summary>
public interface ICompanyPaisService
{
    /// <summary>
    /// Asigna una empresa a un país
    /// </summary>
    Task<CompanyPaisDto> AssignCompanyToPaisAsync(AssignCompanyPaisDto dto);

    /// <summary>
    /// Remueve la asignación de una empresa a un país
    /// </summary>
    Task<bool> RemoveCompanyFromPaisAsync(RemoveCompanyPaisDto dto);

    /// <summary>
    /// Obtiene todas las empresas asignadas a un país
    /// </summary>
    Task<List<CompanyDto>> GetCompaniesByPaisAsync(int paisId);

    /// <summary>
    /// Obtiene todos los países asignados a una empresa
    /// </summary>
    Task<List<PaisDto>> GetPaisesByCompanyAsync(int companyId);

    /// <summary>
    /// Obtiene todas las relaciones empresa-país
    /// </summary>
    Task<List<CompanyPaisDto>> GetAllCompanyPaisAsync();

    /// <summary>
    /// Asigna un usuario a una empresa en un país específico
    /// </summary>
    Task<CompanyPaisDto> AssignUserToCompanyPaisAsync(AssignUserCompanyPaisDto dto);

    /// <summary>
    /// Remueve la asignación de un usuario a una empresa-país
    /// </summary>
    Task<bool> RemoveUserFromCompanyPaisAsync(RemoveUserCompanyPaisDto dto);

    /// <summary>
    /// Obtiene todas las combinaciones empresa-país de un usuario
    /// </summary>
    Task<List<CompanyPaisDto>> GetUserCompanyPaisAsync(Guid userId);

    /// <summary>
    /// Valida que una empresa pertenece a un país
    /// </summary>
    Task<bool> ValidateCompanyPaisAsync(int companyId, int paisId);
}





