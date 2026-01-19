// src/ZooSanMarino.Application/Interfaces/IReporteTecnicoService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para generar reportes técnicos diarios y semanales
/// </summary>
public interface IReporteTecnicoService
{
    /// <summary>
    /// Genera reporte técnico diario para un sublote específico
    /// </summary>
    Task<ReporteTecnicoCompletoDto> GenerarReporteDiarioSubloteAsync(
        int loteId, 
        DateTime? fechaInicio = null, 
        DateTime? fechaFin = null,
        CancellationToken ct = default);

    /// <summary>
    /// Genera reporte técnico diario consolidado para un lote (todos los sublotes)
    /// </summary>
    Task<ReporteTecnicoCompletoDto> GenerarReporteDiarioConsolidadoAsync(
        string loteNombreBase, // Ej: "K326" sin el sublote (compatibilidad hacia atrás)
        DateTime? fechaInicio = null, 
        DateTime? fechaFin = null,
        int? loteId = null, // Si se proporciona, usa lógica de lote padre
        CancellationToken ct = default);

    /// <summary>
    /// Genera reporte técnico semanal para un sublote específico
    /// </summary>
    Task<ReporteTecnicoCompletoDto> GenerarReporteSemanalSubloteAsync(
        int loteId,
        int? semana = null,
        CancellationToken ct = default);

    /// <summary>
    /// Genera reporte técnico semanal consolidado para un lote
    /// Solo consolida semanas completas (7 días) de todos los sublotes
    /// </summary>
    Task<ReporteTecnicoCompletoDto> GenerarReporteSemanalConsolidadoAsync(
        string loteNombreBase, // Compatibilidad hacia atrás
        int? semana = null,
        int? loteId = null, // Si se proporciona, usa lógica de lote padre
        CancellationToken ct = default);

    /// <summary>
    /// Genera reporte técnico según los parámetros de la solicitud
    /// </summary>
    Task<ReporteTecnicoCompletoDto> GenerarReporteAsync(
        GenerarReporteTecnicoRequestDto request,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene lista de sublotes disponibles para un lote base
    /// </summary>
    Task<List<string>> ObtenerSublotesAsync(string loteNombreBase, int? loteId = null, CancellationToken ct = default);

    /// <summary>
    /// Genera reporte técnico completo de Levante con estructura Excel (25 semanas)
    /// Incluye todos los campos calculados, manuales y de guía
    /// </summary>
    Task<ReporteTecnicoLevanteCompletoDto> GenerarReporteLevanteCompletoAsync(
        int loteId,
        bool consolidarSublotes = false,
        CancellationToken ct = default);

    /// <summary>
    /// Genera reporte diario específico de MACHOS desde el seguimiento diario de levante
    /// </summary>
    Task<List<ReporteTecnicoDiarioMachosDto>> GenerarReporteDiarioMachosAsync(
        int loteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default);

    /// <summary>
    /// Genera reporte diario específico de HEMBRAS desde el seguimiento diario de levante
    /// </summary>
    Task<List<ReporteTecnicoDiarioHembrasDto>> GenerarReporteDiarioHembrasAsync(
        int loteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default);

    /// <summary>
    /// Genera reporte técnico de Levante con estructura de tabs
    /// Incluye datos diarios separados (machos y hembras) y datos semanales completos
    /// </summary>
    Task<ReporteTecnicoLevanteConTabsDto> GenerarReporteLevanteConTabsAsync(
        int loteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        bool consolidarSublotes = false,
        CancellationToken ct = default);
}


