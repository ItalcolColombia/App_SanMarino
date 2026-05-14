// src/ZooSanMarino.Application/DTOs/ReporteTecnicoProduccionTabsDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO contenedor para el sistema de TABs del reporte técnico de producción.
/// Incluye datos desglosados por galpón y consolidados (general).
/// </summary>
public class ReporteTecnicoProduccionTabsDto
{
    /// <summary>Información del lote (o primer lote consolidado).</summary>
    public required ReporteTecnicoProduccionLoteInfoDto LoteInfo { get; init; }

    /// <summary>Datos diarios desglosados por galpón (LotePosturaProduccion).</summary>
    public required List<ReporteDiarioGalponDto> DiariosGalpon { get; init; }

    /// <summary>Datos semanales desglosados por galpón.</summary>
    public required List<ReporteSemanalGalponDto> SemanalesGalpon { get; init; }

    /// <summary>Datos diarios consolidados de todos los galpones.</summary>
    public required List<ReporteGeneralDiarioDto> DiariosGeneral { get; init; }

    /// <summary>Datos semanales consolidados de todos los galpones.</summary>
    public required List<ReporteGeneralSemanalDto> SemanalesGeneral { get; init; }
}
