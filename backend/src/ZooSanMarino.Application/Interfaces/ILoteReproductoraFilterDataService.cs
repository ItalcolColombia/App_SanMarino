// src/ZooSanMarino.Application/Interfaces/ILoteReproductoraFilterDataService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio que devuelve en una sola llamada los datos para los filtros
/// del módulo Lote Reproductora: granjas, núcleos, galpones y lotes.
/// </summary>
public interface ILoteReproductoraFilterDataService
{
    Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);
}
