using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Devuelve datos de filtro para Reporte Técnico Levante (granjas, núcleos, galpones, lotes).
/// Los lotes provienen de lote_postura_levante. LoteId en cada item = lotePosturaLevanteId.
/// </summary>
public interface IReporteTecnicoLevanteFilterDataService
{
    Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);
}
