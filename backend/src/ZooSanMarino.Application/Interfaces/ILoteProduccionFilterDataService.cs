// src/ZooSanMarino.Application/Interfaces/ILoteProduccionFilterDataService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio que devuelve en una sola llamada los datos para los filtros del Reporte Técnico de Producción:
/// granjas, núcleos, galpones, lotes (lote_postura_produccion) y lotes base (lote_postura_base).
/// </summary>
public interface ILoteProduccionFilterDataService
{
    Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);
}
