// src/ZooSanMarino.Application/Interfaces/ILoteLevanteFilterDataService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio que devuelve en una sola llamada los datos para los filtros
/// del módulo Seguimiento Diario de Levante: granjas, núcleos, galpones y solo lotes en fase Levante.
/// No incluye lotes en fase Producción (tabla unificada).
/// </summary>
public interface ILoteLevanteFilterDataService
{
    Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);
}
