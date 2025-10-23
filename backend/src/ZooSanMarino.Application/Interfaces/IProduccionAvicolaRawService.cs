// src/ZooSanMarino.Application/Interfaces/IProduccionAvicolaRawService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.Application.Interfaces;

public interface IProduccionAvicolaRawService
{
    Task<ProduccionAvicolaRawDto> CreateAsync(CreateProduccionAvicolaRawDto dto);
    Task<IEnumerable<ProduccionAvicolaRawDto>> GetAllAsync();
    Task<ProduccionAvicolaRawDto?> GetByIdAsync(int id);
    Task<ProduccionAvicolaRawDto> UpdateAsync(UpdateProduccionAvicolaRawDto dto);
    Task<bool> DeleteAsync(int id);
    Task<ZooSanMarino.Application.DTOs.Common.PagedResult<ProduccionAvicolaRawDto>> SearchAsync(ProduccionAvicolaRawSearchRequest request);
}
