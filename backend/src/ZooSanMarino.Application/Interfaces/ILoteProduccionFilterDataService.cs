// src/ZooSanMarino.Application/Interfaces/ILoteProduccionFilterDataService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio que devuelve en una sola llamada los datos para los filtros
/// del módulo Seguimiento Diario de Producción: granjas, núcleos, galpones y lotes de producción (semana >= 26).
/// </summary>
public interface ILoteProduccionFilterDataService
{
    Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);
}
