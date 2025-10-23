// file: backend/src/ZooSanMarino.Application/Interfaces/ISeguimientoLoteLevanteService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ISeguimientoLoteLevanteService
{
    Task<IEnumerable<SeguimientoLoteLevanteDto>> GetByLoteAsync(int loteId);
    Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteId, DateTime? desde, DateTime? hasta);
    Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto);
    Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto);
    Task<bool> DeleteAsync(int id);
    
     // NUEVO: cálculo on-demand + lectura del snapshot
    Task<ResultadoLevanteResponse> GetResultadoAsync(int loteId, DateTime? desde, DateTime? hasta, bool recalcular = true);
}
