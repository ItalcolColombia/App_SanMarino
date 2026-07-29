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

    /// <summary>
    /// Hoja «RESUMEN SEMANAL» del Informe RA Pesadas — bloque Levante:
    /// una fila por lote para UNA semana calendario (WEEKNUM de Excel).
    /// </summary>
    Task<ResumenSemanalRaPesadasLevanteResponse> GenerarResumenLevanteAsync(
        ResumenSemanalRaPesadasRequest request, CancellationToken ct = default);

    /// <inheritdoc cref="GenerarResumenLevanteAsync"/>
    Task<ResumenSemanalRaPesadasProduccionResponse> GenerarResumenProduccionAsync(
        ResumenSemanalRaPesadasRequest request, CancellationToken ct = default);

    /// <summary>
    /// Curva consolidada del año (bloque «Resumen a semana N» de las hojas de
    /// gráficas): todos los lotes a lo largo de todas las edades, ponderado por
    /// saldo de hembras.
    /// </summary>
    Task<CurvaConsolidadaResponse> GenerarCurvaConsolidadaAsync(
        CurvaConsolidadaRequest request, CancellationToken ct = default);
}
