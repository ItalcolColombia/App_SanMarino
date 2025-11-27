namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para validar que una empresa pertenece a un país y que un usuario tiene acceso a esa combinación
/// </summary>
public interface ICompanyPaisValidator
{
    /// <summary>
    /// Valida que una empresa pertenece a un país
    /// </summary>
    Task<bool> ValidateCompanyPaisAsync(int companyId, int paisId);

    /// <summary>
    /// Valida que un usuario tiene acceso a una empresa en un país específico
    /// </summary>
    Task<bool> ValidateUserCompanyPaisAsync(Guid userId, int companyId, int paisId);

    /// <summary>
    /// Obtiene los países asociados a una empresa
    /// </summary>
    Task<List<int>> GetPaisesByCompanyAsync(int companyId);

    /// <summary>
    /// Obtiene las empresas asociadas a un país
    /// </summary>
    Task<List<int>> GetCompaniesByPaisAsync(int paisId);

    /// <summary>
    /// Obtiene las combinaciones empresa-país de un usuario
    /// </summary>
    Task<List<(int CompanyId, int PaisId, string CompanyName, string PaisNombre)>> GetUserCompanyPaisAsync(Guid userId);
}





