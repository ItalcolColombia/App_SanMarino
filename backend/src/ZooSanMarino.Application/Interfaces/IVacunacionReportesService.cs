// src/ZooSanMarino.Application/Interfaces/IVacunacionReportesService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IVacunacionReportesService
{
    /// <summary>Cumplimiento por lote (fn_vacunacion_cumplimiento_lote), filtrado por granja/núcleo/galpón/lote/línea/fecha.</summary>
    Task<List<VacunacionCumplimientoLoteDto>> GetCumplimientoAsync(VacunacionCumplimientoFiltroRequest req, CancellationToken ct = default);
}
