// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionReportesDtos.cs
namespace ZooSanMarino.Application.DTOs;

public record VacunacionCumplimientoFiltroRequest(
    List<int>? GranjaIds = null,
    string? NucleoId = null,
    string? GalponId = null,
    List<int>? LoteIds = null,
    string? LineaProductiva = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null
);

/// <summary>Una fila por lote: % a tiempo / tardío (1 semana vs 2+) / no aplicado + promedio de días de atraso.</summary>
public record VacunacionCumplimientoLoteDto(
    int LoteId,
    string LoteNombre,
    string LineaProductiva,
    int GranjaId,
    string? GranjaNombre,
    int TotalProgramadas,
    int TotalATiempo,
    int TotalTardio1Semana,
    int TotalTardio2MasSemanas,
    int TotalNoAplicado,
    int TotalPendiente,
    decimal PorcentajeATiempo,
    decimal PorcentajeTardio,
    decimal PorcentajeNoAplicado,
    decimal? PromedioDiasAtraso
);
