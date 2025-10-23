using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ISeguimientoProduccionService
{
    Task<IEnumerable<SeguimientoProduccionDto>> GetAllAsync();
    Task<SeguimientoProduccionDto?> GetByLoteIdAsync(int loteId);
    Task<SeguimientoProduccionDto> CreateAsync(CreateSeguimientoProduccionDto dto);
    Task<SeguimientoProduccionDto?> UpdateAsync(UpdateSeguimientoProduccionDto dto);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<SeguimientoProduccionDto>> FilterAsync(FilterSeguimientoProduccionDto filter);
}



