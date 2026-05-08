// src/ZooSanMarino.Application/Interfaces/IReporteTecnicoProduccionService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IReporteTecnicoProduccionService
{
    /// <summary>
    /// Genera un reporte técnico de producción (diario o semanal, por sublote o consolidado)
    /// </summary>
    Task<ReporteTecnicoProduccionCompletoDto> GenerarReporteAsync(
        GenerarReporteTecnicoProduccionRequestDto request,
        CancellationToken ct = default);

    /// <summary>
    /// Genera reporte técnico diario de producción para un lote específico
    /// </summary>
    Task<ReporteTecnicoProduccionCompletoDto> GenerarReporteDiarioAsync(
        int loteId,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        bool consolidarSublotes = false,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene la lista de sublotes para un lote base dado
    /// </summary>
    Task<List<string>> ObtenerSublotesAsync(string loteNombreBase, CancellationToken ct = default);

    /// <summary>
    /// Genera reporte técnico "Cuadro" semanal con valores de guía genética (amarillos)
    /// </summary>
    Task<ReporteTecnicoProduccionCuadroCompletoDto> GenerarReporteCuadroAsync(
        int loteId,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        bool consolidarSublotes = false,
        CancellationToken ct = default);

    /// <summary>
    /// Genera reporte de clasificación de huevos comercio semanal con valores de guía genética (amarillos)
    /// </summary>
    Task<ReporteClasificacionHuevoComercioCompletoDto> GenerarReporteClasificacionHuevoComercioAsync(
        int loteId,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        bool consolidarSublotes = false,
        CancellationToken ct = default);

    /// <summary>
    /// Genera reporte técnico de producción navegando desde LotePosturaBase → LPL → LPP.
    /// Lee seguimientos desde la tabla produccion_diaria (SeguimientoProduccion).
    /// Si LotePosturaProduccionId está presente, genera reporte individual; si no, consolida todos.
    /// </summary>
    Task<ReporteTecnicoProduccionCompletoDto> ObtenerReporteProduccionAsync(
        ObtenerReporteProduccionRequestDto request,
        CancellationToken ct = default);

    /// <summary>
    /// Genera reporte técnico de producción con estructura de TABs (Fase 4 — SOLO PRODUCCIÓN).
    /// Retorna datos desglosados por galpón + consolidados (general), con valores guía STANDARD.
    /// </summary>
    Task<ReporteTecnicoProduccionTabsDto> ObtenerReporteProduccionTabsAsync(
        ObtenerReporteProduccionRequestDto request,
        CancellationToken ct = default);
}

