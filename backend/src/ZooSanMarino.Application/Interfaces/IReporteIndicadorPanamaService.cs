using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Liquidación / reporte de indicadores técnicos para Panamá (Pollo Engorde).
/// </summary>
public interface IReporteIndicadorPanamaService
{
    /// <summary>Inserta o actualiza los 6 insumos de liquidación de un lote (1 fila por lote).</summary>
    Task<int> GuardarLiquidacionAsync(GuardarLiquidacionPanamaRequest request, CancellationToken ct = default);

    /// <summary>Genera el reporte de liquidación ejecutando fn_reporte_indicadores_panama.
    /// Devuelve null si el lote aún no tiene liquidación registrada.</summary>
    Task<ReporteIndicadoresPanamaDto?> GetReporteAsync(int loteAveEngordeId, CancellationToken ct = default);
}
