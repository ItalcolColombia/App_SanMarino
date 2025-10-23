// file: src/ZooSanMarino.Application/Interfaces/IProduccionLoteService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IProduccionLoteService
{
    Task<ProduccionLoteDto>                 CreateAsync(CreateProduccionLoteDto dto);
    Task<ProduccionLoteDto?>                UpdateAsync(UpdateProduccionLoteDto dto);
    Task<bool>                              DeleteAsync(int id);

    Task<IEnumerable<ProduccionLoteDto>>    GetAllAsync();
    Task<ProduccionLoteDto?>                GetByLoteIdAsync(int loteId);  // Changed from string to int
    Task<IEnumerable<ProduccionLoteDto>>    FilterAsync(FilterProduccionLoteDto filter);
}
