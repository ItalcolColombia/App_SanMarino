// src/ZooSanMarino.Application/Interfaces/IReporteDiarioCostosPosturaService.cs
using ZooSanMarino.Application.DTOs.ReporteDiarioCostosPostura;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Reporte Diario Área de Costos de POSTURA (levante + producción), por lote:galpón.
/// No tiene relación con el reporte homónimo de engorde: otras fuentes y otras reglas.
/// </summary>
public interface IReporteDiarioCostosPosturaService
{
    Task<ReporteDiarioCostosPosturaReporteDto> GenerarAsync(
        ReporteDiarioCostosPosturaRequest request,
        CancellationToken ct = default);
}
