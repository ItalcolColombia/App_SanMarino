using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ISeguimientoDiarioLoteReproductoraFilterDataService
{
    Task<SeguimientoDiarioLoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);
}
