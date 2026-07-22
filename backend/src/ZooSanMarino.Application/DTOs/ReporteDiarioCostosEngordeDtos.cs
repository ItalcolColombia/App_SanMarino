// src/ZooSanMarino.Application/DTOs/ReporteDiarioCostosEngordeDtos.cs
namespace ZooSanMarino.Application.DTOs.ReporteDiarioCostosEngorde;

/// <summary>Filtros del Reporte Diario Costos de pollo engorde (por granja).</summary>
public sealed record ReporteDiarioCostosRequest(
    int GranjaId,
    /// <summary>Lote base global opcional. NULL = todos los lotes de la granja.</summary>
    int? LoteBaseEngordeId = null,
    /// <summary>NULL = regla del segundo lote (encaset del lote más reciente del alcance).</summary>
    DateTime? FechaInicio = null,
    /// <summary>NULL = hoy.</summary>
    DateTime? FechaFin = null
);

/// <summary>Desglose de alimento del día (granja completa). StockKg NULL = sin snapshot (fila vieja sin histórico).</summary>
public sealed record ReporteDiarioCostosAlimentoDto(string NombreAlimento, double? StockKg, double ConsumoKg);

/// <summary>Métricas del día para un galpón.</summary>
public sealed record ReporteDiarioCostosGalponDiaDto(
    string GalponId,
    string GalponNombre,
    int Mortalidad,
    int Seleccion,
    int ErrSexaje,
    int MortSel,
    double ConsumoKg,
    int AvesVivas
);

/// <summary>Una fila del reporte = una fecha (todos los lotes del alcance unificados).</summary>
public sealed record ReporteDiarioCostosFilaDto(
    DateTime Fecha,
    double ConsumoTotalKg,
    int MortSelTotal,
    int AvesVivasTotal,
    IReadOnlyList<ReporteDiarioCostosAlimentoDto> Alimentos,
    IReadOnlyList<ReporteDiarioCostosGalponDiaDto> Galpones
);

/// <summary>Lote del alcance (cabecera: "lote nombre", "lote nombre 2", …).</summary>
public sealed record ReporteDiarioCostosLoteDto(
    int LoteAveEngordeId,
    string LoteNombre,
    string GalponId,
    string GalponNombre,
    DateTime? FechaEncaset,
    string? EstadoOperativoLote
);

/// <summary>Galpón del alcance (columna dinámica) con los lotes que lo componen.</summary>
public sealed record ReporteDiarioCostosGalponHeaderDto(
    string GalponId,
    string GalponNombre,
    IReadOnlyList<string> Lotes
);

public sealed record ReporteDiarioCostosAlimentoTotalDto(string NombreAlimento, double ConsumoKg);

public sealed record ReporteDiarioCostosGalponTotalDto(
    string GalponId,
    string GalponNombre,
    int Mortalidad,
    int Seleccion,
    int ErrSexaje,
    int MortSel
);

/// <summary>Footer del reporte: SUMA TOTAL global, por alimento y por galpón.</summary>
public sealed record ReporteDiarioCostosTotalesDto(
    double ConsumoTotalKg,
    int MortSelTotal,
    IReadOnlyList<ReporteDiarioCostosAlimentoTotalDto> Alimentos,
    IReadOnlyList<ReporteDiarioCostosGalponTotalDto> PorGalpon
);

/// <summary>Aves vivas "actuales" (última fecha del reporte) por galpón.</summary>
public sealed record ReporteDiarioCostosAvesActualesDto(string GalponId, string GalponNombre, int AvesVivas);

public sealed record ReporteDiarioCostosReporteDto(
    ReporteDiarioCostosRequest FiltrosAplicados,
    DateTime? FechaInicioEfectiva,
    DateTime? FechaFinEfectiva,
    int GranjaId,
    string GranjaNombre,
    int? LoteBaseEngordeId,
    string? LoteBaseNombre,
    IReadOnlyList<ReporteDiarioCostosLoteDto> Lotes,
    IReadOnlyList<ReporteDiarioCostosGalponHeaderDto> Galpones,
    IReadOnlyList<ReporteDiarioCostosAvesActualesDto> AvesVivasActuales,
    int AvesVivasActualesTotal,
    IReadOnlyList<ReporteDiarioCostosFilaDto> Filas,
    ReporteDiarioCostosTotalesDto Totales
);

/// <summary>
/// Fila cruda devuelta por <c>fn_reporte_diario_costos_engorde</c> (SqlQueryRaw). Props en PascalCase
/// que mapean 1:1 a las columnas snake_case de la función. Alimentos/Galpones llegan como JSON (text).
/// </summary>
public sealed class ReporteDiarioCostosRow
{
    public DateTime Fecha { get; set; }
    public double ConsumoTotalKg { get; set; }
    public int MortSelTotal { get; set; }
    public int AvesVivasTotal { get; set; }
    public string? Alimentos { get; set; }
    public string? Galpones { get; set; }
}
