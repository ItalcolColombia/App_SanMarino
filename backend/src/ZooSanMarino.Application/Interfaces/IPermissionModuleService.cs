using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Módulos de permisos: catálogo (qué permisos agrupa cada módulo) y asignación por empresa. Todo
/// cambio se materializa en <c>company_permissions</c>, que es lo que leen el login y los gates.
/// Reglas en <c>PermisoModuloCalculos</c>.
/// </summary>
public interface IPermissionModuleService
{
    Task<IReadOnlyList<PermissionModuleDto>> GetAllAsync();

    /// <returns><c>null</c> si ya existe un módulo con esa key.</returns>
    Task<PermissionModuleDto?> CreateAsync(CreatePermissionModuleDto dto);

    /// <returns><c>null</c> si el módulo no existe.</returns>
    Task<PermissionModuleDto?> UpdateAsync(int id, UpdatePermissionModuleDto dto);

    Task<EliminarModuloResultado> DeleteAsync(int id);

    /// <summary>
    /// Reemplaza la clasificación del módulo y recalcula <c>company_permissions</c> en todas las empresas
    /// que tienen configuración de módulos.
    /// </summary>
    /// <returns><c>null</c> si el módulo no existe.</returns>
    Task<CambioPermisosDto?> SetPermissionsAsync(int moduleId, SetPermissionModulePermissionsRequest request);

    Task<IReadOnlyList<CompanyPermissionModuleItemDto>> GetForCompanyAsync(int companyId);

    /// <summary>Fija los módulos prendidos de la empresa y materializa sus permisos.</summary>
    /// <returns><c>null</c> si la empresa no existe.</returns>
    Task<CambioPermisosDto?> SetForCompanyAsync(int companyId, SetCompanyPermissionModulesRequest request);
}
