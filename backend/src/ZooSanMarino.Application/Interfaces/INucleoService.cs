// file: src/ZooSanMarino.Application/Interfaces/INucleoService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Nucleos;
using CommonDtos = ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.Application.Interfaces;

public interface INucleoService
{
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
