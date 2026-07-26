using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Reporte Técnico Semanal (Sanmarino postura): Levante (semanas 1-25) y
/// Producción (semana 25+) por lote base, un tab por sublote/galpón +
/// consolidado, comparado contra la guía genética cargada de la empresa.
/// </summary>
public interface IReporteTecnicoSemanalService
{
    Task<ReporteTecnicoSemanalLevanteResponse> GenerarLevanteAsync(
        ReporteTecnicoSemanalRequest request, CancellationToken ct = default);

    Task<ReporteTecnicoSemanalProduccionResponse> GenerarProduccionAsync(
        ReporteTecnicoSemanalRequest request, CancellationToken ct = default);
}
