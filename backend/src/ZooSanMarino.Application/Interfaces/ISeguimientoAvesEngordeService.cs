// Seguimiento diario Aves de Engorde: misma API que Levante, persiste en seguimiento_diario con tipo = 'engorde'.
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ISeguimientoAvesEngordeService
{
    Task<IEnumerable<SeguimientoLoteLevanteDto>> GetByLoteAsync(int loteId);
    Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id);
    Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteId, DateTime? desde, DateTime? hasta);
    Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto);
    Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto);
    Task<bool> DeleteAsync(int id);
    Task<ResultadoLevanteResponse> GetResultadoAsync(int loteId, DateTime? desde, DateTime? hasta, bool recalcular = true);
}
