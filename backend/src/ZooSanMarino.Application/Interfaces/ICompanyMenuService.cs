using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>Servicio para gestionar la relación empresa-menú (qué menús ve cada empresa).</summary>
public interface ICompanyMenuService
{
    /// <summary>Obtiene el árbol de menús asignados a la empresa (solo los asignados), con IsEnabled.</summary>
    Task<IEnumerable<CompanyMenuItemDto>> GetMenusForCompanyAsync(int companyId);

    /// <summary>Asigna o actualiza los menús de la empresa. Reemplaza la asignación actual.</summary>
    Task SetCompanyMenusAsync(int companyId, SetCompanyMenusRequest request);

    /// <summary>Actualiza orden y jerarquía (parent) de los menús asignados a la empresa.</summary>
    Task UpdateCompanyMenuStructureAsync(int companyId, UpdateCompanyMenuStructureRequest request);
}
