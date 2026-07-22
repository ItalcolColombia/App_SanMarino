// src/ZooSanMarino.Application/Interfaces/IReporteDiarioCostosEngordeService.cs
using ZooSanMarino.Application.DTOs.ReporteDiarioCostosEngorde;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Reporte Diario Costos de pollo engorde: unifica por fecha los lotes de una granja
/// (todos o los amarrados a un lote base), con alimento por día, mortalidad+selección
/// y aves vivas por galpón. Fuente: fn_reporte_diario_costos_engorde (que reusa
/// fn_seguimiento_diario_engorde por lote).
/// </summary>
public interface IReporteDiarioCostosEngordeService
{
    Task<ReporteDiarioCostosReporteDto> GenerarAsync(ReporteDiarioCostosRequest request, CancellationToken ct = default);
}
