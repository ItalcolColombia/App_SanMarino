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

    /// <summary>
    /// Catálogo del filtro «Lote base», armado por DÓNDE ESTÁN SUS LOTES (no por
    /// <c>lote_postura_base.farm_id</c>) y recortado a las granjas asignadas al usuario.
    /// Un lote base puede aparecer bajo varias granjas: es el caso del lote cuyo levante se hizo en
    /// una granja y su producción en otra.
    /// </summary>
    Task<IReadOnlyList<ReporteDiarioCostosPosturaLoteBaseOpcionDto>> LotesBaseAsync(
        CancellationToken ct = default);
}
