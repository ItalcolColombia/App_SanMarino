// file: src/ZooSanMarino.Application/Interfaces/INucleoService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Nucleos;
using CommonDtos = ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.Application.Interfaces;

public interface INucleoService
{
    /// <summary>
    /// Núcleos de la empresa y solo de las granjas indicadas (sin bypass de admin / todo el país).
    /// </summary>
    Task<IEnumerable<NucleoDto>> GetByFarmIdsForCompanyAsync(IReadOnlyList<int> farmIds, int companyId, CancellationToken ct = default);

    // Compat
    Task<IEnumerable<NucleoDto>> GetAllAsync();
    Task<NucleoDto?>             GetByIdAsync(string nucleoId, int granjaId);
    Task<IEnumerable<NucleoDto>> GetByGranjaAsync(int granjaId);
    Task<NucleoDto>              CreateAsync(CreateNucleoDto dto);
    Task<NucleoDto?>             UpdateAsync(UpdateNucleoDto dto);
    Task<bool>                   DeleteAsync(string nucleoId, int granjaId);
    Task<bool>                   HardDeleteAsync(string nucleoId, int granjaId);

    // Avanzado
    Task<CommonDtos.PagedResult<NucleoDetailDto>> SearchAsync(NucleoSearchRequest req);
    Task<NucleoDetailDto?>                        GetDetailByIdAsync(string nucleoId, int granjaId);
}
