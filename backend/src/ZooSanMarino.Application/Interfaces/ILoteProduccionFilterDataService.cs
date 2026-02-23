// src/ZooSanMarino.Application/Interfaces/ILoteProduccionFilterDataService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio que devuelve en una sola llamada los datos para los filtros
/// del módulo Seguimiento Diario de Producción: granjas, núcleos, galpones y lotes desde lote_postura_produccion.
/// </summary>
public interface ILoteProduccionFilterDataService
{
    Task<SeguimientoProduccionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);
}
