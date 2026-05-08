// src/ZooSanMarino.Application/Interfaces/IExportacionExcelService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IExportacionExcelService
{
    byte[] ExportarReporteLevante(ReporteTecnicoLevanteCompletoDto reporte, ExportarExcelMetaDto meta);
    byte[] ExportarReporteProduccionTabs(ReporteTecnicoProduccionTabsDto reporte, ExportarExcelMetaDto meta);
}
