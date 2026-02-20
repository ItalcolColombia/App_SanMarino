// src/ZooSanMarino.Application/Interfaces/IMasterListService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IMasterListService
{
    Task<IEnumerable<MasterListDto>> GetAllAsync(int? companyId = null, int? countryId = null);
    Task<MasterListDto?>             GetByIdAsync(int id);
    Task<MasterListDto?>             GetByKeyAsync(string key, int? companyId = null, int? countryId = null);
    Task<MasterListDto>              CreateAsync(CreateMasterListDto dto);
    Task<MasterListDto?>             UpdateAsync(UpdateMasterListDto dto);
    Task<bool>                       DeleteAsync(int id);
}
