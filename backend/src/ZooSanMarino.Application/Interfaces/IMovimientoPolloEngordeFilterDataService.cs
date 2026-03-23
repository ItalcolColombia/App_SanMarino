using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IMovimientoPolloEngordeFilterDataService
{
    /// <summary>
    /// Granjas asignadas, núcleos, galpones y lotes Ave Engorde — una sola llamada.
    /// </summary>
    Task<MovimientoPolloEngordeFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);
}
