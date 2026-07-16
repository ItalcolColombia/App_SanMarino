// src/ZooSanMarino.Application/Interfaces/IVacunacionReportesService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IVacunacionReportesService
{
    /// <summary>Cumplimiento por lote (fn_vacunacion_cumplimiento_lote), filtrado por granja/núcleo/galpón/lote/línea/fecha.
    /// Acotado a las granjas asignadas al usuario.</summary>
    Task<List<VacunacionCumplimientoLoteDto>> GetCumplimientoAsync(VacunacionCumplimientoFiltroRequest req, CancellationToken ct = default);

    /// <summary>Detalle ítem a ítem (fn_vacunacion_cumplimiento_detalle), mismos filtros que el cumplimiento.
    /// Acotado a las granjas asignadas al usuario.</summary>
    Task<List<VacunacionCumplimientoDetalleDto>> GetCumplimientoDetalleAsync(VacunacionCumplimientoFiltroRequest req, CancellationToken ct = default);
}
