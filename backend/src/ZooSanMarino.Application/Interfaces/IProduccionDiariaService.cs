using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IProduccionDiariaService
{
    Task<IEnumerable<ProduccionDiariaDto>> GetAllAsync();
    Task<IEnumerable<ProduccionDiariaDto>> GetByLoteIdAsync(int loteId);
    Task<ProduccionDiariaDto> CreateAsync(CreateProduccionDiariaDto dto);
    Task<ProduccionDiariaDto?> UpdateAsync(UpdateProduccionDiariaDto dto);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<ProduccionDiariaDto>> FilterAsync(FilterProduccionDiariaDto filter);
    Task<bool> HasProduccionLoteConfigAsync(string loteId);
}