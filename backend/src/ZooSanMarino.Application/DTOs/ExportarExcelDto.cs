// src/ZooSanMarino.Application/DTOs/ExportarExcelDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>Metadatos comunes para la hoja "Información" del Excel.</summary>
public record ExportarExcelMetaDto(
    string Etapa,
    string LoteBaseNombre,
    string? LoteSubloteNombre,
    string? GranjaNombre,
    string? NucleoNombre,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    int? TotalAvesInicio,
    string? Periodicidad
);

/// <summary>Request para exportar reporte de levante a Excel.</summary>
public class ExportarExcelLevanteRequestDto
{
    public ReporteTecnicoLevanteCompletoDto Reporte { get; set; } = null!;
    public ExportarExcelMetaDto Meta { get; set; } = null!;
}

/// <summary>Request para exportar reporte de producción TABS a Excel.</summary>
public class ExportarExcelProduccionTabsRequestDto
{
    public ReporteTecnicoProduccionTabsDto Reporte { get; set; } = null!;
    public ExportarExcelMetaDto Meta { get; set; } = null!;
}
