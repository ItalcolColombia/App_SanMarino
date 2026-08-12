using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Configuración del eje permiso↔empresa (<c>company_permissions</c>): qué permisos del catálogo
/// global están habilitados en cada empresa.
/// </summary>
public interface ICompanyPermissionService
{
    /// <summary>
    /// Catálogo COMPLETO de permisos con el estado (habilitado / no) para la empresa, más cuántos
    /// roles de esa empresa ya usan cada uno.
    /// </summary>
    Task<IEnumerable<CompanyPermissionItemDto>> GetPermissionsForCompanyAsync(int companyId);

    /// <summary>
    /// Keys habilitadas de la empresa. Diccionario vacío para una empresa SIN CONFIGURAR — el
    /// llamador aplica fail-closed (ver <c>CompanyPermissionCalculos</c>).
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyCollection<string>>> GetEnabledKeysAsync(IEnumerable<int> companyIds);

    /// <summary>Reemplaza la configuración de la empresa por la lista recibida.</summary>
    Task SetPermissionsForCompanyAsync(int companyId, SetCompanyPermissionsRequest request);

    /// <summary>
    /// Habilita TODO el catálogo para una empresa que aún no tiene configuración (regla R4: una
    /// empresa nueva no puede nacer sin permisos, porque fail-closed bloquearía su primer rol).
    /// No toca nada si la empresa ya tiene filas.
    /// </summary>
    Task SembrarCatalogoCompletoSiVaciaAsync(int companyId);
}
