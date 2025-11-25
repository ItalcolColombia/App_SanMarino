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
    /// Obtiene la lista de sublotes para un lote base dado
    /// </summary>
    Task<List<string>> ObtenerSublotesAsync(string loteNombreBase, CancellationToken ct = default);
}

