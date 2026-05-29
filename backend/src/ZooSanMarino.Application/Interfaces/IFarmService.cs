// file: src/ZooSanMarino.Application/Interfaces/IFarmService.cs
using ZooSanMarino.Application.DTOs;               // FarmDto, Create/Update

using CommonDtos = ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.DTOs.Farms; // ⟵ alias para PagedResult<>

namespace ZooSanMarino.Application.Interfaces;

public interface IFarmService
{
    /// <summary>
    /// IDs de granja asignados al usuario en <c>user_farms</c> (sin filtrar por empresa).
    /// </summary>
    Task<IReadOnlyList<int>> GetAssignedFarmIdsForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// <see cref="FarmDto"/> para los IDs indicados que existan en la empresa (p. ej. tras cruzar con <see cref="GetAssignedFarmIdsForUserAsync"/>).
    /// </summary>
    Task<IReadOnlyList<FarmDto>> GetFarmDtosByIdsInCompanyAsync(IReadOnlyCollection<int> farmIds, int companyId, CancellationToken ct = default);

    /// <summary>
    /// Granjas de la empresa indicada que estén asignadas al usuario en <c>UserFarms</c>.
    /// No aplica la lógica de “admin ve todas las granjas de la compañía”: solo asignaciones explícitas.
    /// Si <paramref name="paisId"/> tiene valor, se restringe además al país (vía <c>Departamento.PaisId</c>).
    /// </summary>
    Task<IEnumerable<FarmDto>> GetAssignedFarmsForCompanyAsync(Guid userId, int companyId, int? paisId = null);

    Task<IEnumerable<FarmDto>> GetAllAsync(Guid? userId = null, int? companyId = null);

    /// <summary>
    /// Feature 13: lista de granjas válidas como destino de traslado de aves
    /// entre seguimientos diarios. Filtra automáticamente por:
    ///   • CompanyId del usuario actual (token).
    ///   • PaisId del usuario actual (header x-active-pais).
    ///   • Status='A' y DeletedAt == null (granjas activas).
    /// Endpoint compartido entre Levante y Producción.
    /// </summary>
    Task<IEnumerable<FarmDto>> GetForTrasladoSeguimientoAsync();

    Task<FarmDto?>             GetByIdAsync(int id);
    Task<FarmDto>              CreateAsync(CreateFarmDto dto);
    Task<FarmDto?>             UpdateAsync(UpdateFarmDto dto);
    Task<bool>                 DeleteAsync(int id);

    Task<CommonDtos.PagedResult<FarmDetailDto>> SearchAsync(FarmSearchRequest req);
    Task<FarmDetailDto?>                        GetDetailByIdAsync(int id);
    Task<FarmTreeDto?>                          GetTreeByIdAsync(int farmId, bool soloActivos = true);
    Task<bool>                                           HardDeleteAsync(int id);

    /// <summary>
    /// Granjas filtradas por la zona del usuario actual cuando el país activo es PANAMA.
    /// Si país != PANAMA o el usuario no tiene zona, devuelve las granjas asignadas vía UserFarm.
    /// </summary>
    Task<IEnumerable<FarmDto>> GetByZonaUsuarioAsync(string? paisActivo, CancellationToken ct = default);
}
