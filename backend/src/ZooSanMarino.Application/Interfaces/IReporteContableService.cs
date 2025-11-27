// src/ZooSanMarino.Application/Interfaces/IReporteContableService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IReporteContableService
{
    /// <summary>
    /// Genera el reporte contable para un lote padre
    /// </summary>
    Task<ReporteContableCompletoDto> GenerarReporteAsync(
        GenerarReporteContableRequestDto request,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene las semanas contables disponibles para un lote padre
    /// La semana contable inicia cuando llega el primer lote/sublote y dura 7 días calendario
    /// </summary>
    Task<List<int>> ObtenerSemanasContablesAsync(
        int lotePadreId,
        CancellationToken ct = default);
}

