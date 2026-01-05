// file: src/ZooSanMarino.Application/Interfaces/IFarmService.cs
using ZooSanMarino.Application.DTOs;               // FarmDto, Create/Update

using CommonDtos = ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.DTOs.Farms; // ⟵ alias para PagedResult<>

namespace ZooSanMarino.Application.Interfaces;

public interface IFarmService
{
    Task<IEnumerable<FarmDto>> GetAllAsync(Guid? userId = null, int? companyId = null);
    Task<FarmDto?>             GetByIdAsync(int id);
    Task<FarmDto>              CreateAsync(CreateFarmDto dto);
    Task<FarmDto?>             UpdateAsync(UpdateFarmDto dto);
    Task<bool>                 DeleteAsync(int id);

    Task<CommonDtos.PagedResult<FarmDetailDto>> SearchAsync(FarmSearchRequest req);
    Task<FarmDetailDto?>                        GetDetailByIdAsync(int id);
    Task<FarmTreeDto?>                          GetTreeByIdAsync(int farmId, bool soloActivos = true);
    Task<bool>                                           HardDeleteAsync(int id);
}
