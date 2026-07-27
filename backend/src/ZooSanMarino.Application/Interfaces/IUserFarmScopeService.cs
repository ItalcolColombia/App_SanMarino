using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Administración del alcance granular de ubicación por usuario-granja (módulo Usuarios →
/// asignación de granjas). El enforcement en lecturas usa <see cref="ILocationScopeResolver"/>.
/// </summary>
public interface IUserFarmScopeService
{
    /// <summary>Configuración actual (flag + grants con nombres display) de un usuario en una granja.</summary>
    Task<UserFarmScopeConfigDto> GetScopeAsync(Guid userId, int farmId);

    /// <summary>
    /// Reemplazo transaccional del alcance. Valida que cada item exista y pertenezca a la granja
    /// (fail-closed: item ajeno ⇒ InvalidOperationException, nada se guarda).
    /// </summary>
    Task<UserFarmScopeConfigDto> ReplaceScopeAsync(Guid userId, int farmId, UpdateUserFarmScopeDto dto, Guid updatedByUserId);

    /// <summary>Árbol núcleos→galpones→lotes ACTIVOS de la granja para el modal de administración.</summary>
    Task<FarmLocationsTreeDto> GetFarmLocationsTreeAsync(int farmId);
}

/// <summary>
/// Resuelve el alcance de ubicación EFECTIVO del usuario ACTUAL (ICurrentUser) por granja, con caché
/// por request. Sin usuario en contexto (jobs internos) ⇒ Global (sin restricción), igual que el
/// scoping por granja existente.
/// </summary>
public interface ILocationScopeResolver
{
    /// <summary>Scope del usuario actual sobre una granja (Global si no está restringido).</summary>
    Task<UserLocationScopeCalculos.LocationScope> GetScopeAsync(int farmId);

    /// <summary>
    /// Solo las granjas RESTRINGIDAS del usuario actual entre las dadas, con su scope. Diccionario
    /// vacío ⇒ ninguna restricción aplica (caso común: cero costo para usuarios globales).
    /// </summary>
    Task<IReadOnlyDictionary<int, UserLocationScopeCalculos.LocationScope>> GetRestrictedScopesAsync(IEnumerable<int> farmIds);

    /// <summary>
    /// TODAS las granjas restringidas del usuario actual con su scope (para queries multi-granja
    /// donde no se conocen las candidatas de antemano). Vacío ⇒ sin restricciones.
    /// </summary>
    Task<IReadOnlyDictionary<int, UserLocationScopeCalculos.LocationScope>> GetAllRestrictedScopesAsync();

    /// <summary>true si el usuario actual puede ver el lote (tabla lotes; resuelve su granja).</summary>
    Task<bool> PermiteLoteAsync(int loteId);
}
