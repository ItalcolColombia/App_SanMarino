// Seguimiento diario por lote reproductora aves de engorde. Persiste en seguimiento_diario_lote_reproductora_aves_engorde.
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ISeguimientoDiarioLoteReproductoraService
{
    Task<IEnumerable<SeguimientoLoteLevanteDto>> GetByLoteReproductoraAsync(int loteReproductoraId);
    Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id);
    Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteReproductoraId, DateTime? desde, DateTime? hasta);
    Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto);
    Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto);
    Task<bool> DeleteAsync(int id);
}
