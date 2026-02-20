// src/ZooSanMarino.Application/Interfaces/ISeguimientoAvesEngordeFilterDataService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Datos para filtros del módulo Seguimiento Diario Aves de Engorde.
/// Devuelve la misma forma que LoteReproductoraFilterDataDto pero con Lotes = lotes de lote_ave_engorde
/// (LoteId = lote_ave_engorde_id) para que el front no cambie.
/// </summary>
public interface ISeguimientoAvesEngordeFilterDataService
{
    Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);
}
