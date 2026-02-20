using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ILoteReproductoraAveEngordeFilterDataService
{
    Task<LoteReproductoraAveEngordeFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);
}
