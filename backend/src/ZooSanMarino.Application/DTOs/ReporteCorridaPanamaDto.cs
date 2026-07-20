namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Reporte de liquidación técnica Panamá de una CORRIDA completa.
/// En Panamá el <c>lote_nombre</c> ES el número de corrida y se repite en varios galpones
/// de la misma granja (una fila de lote por galpón): la corrida agrupa esos lotes.
/// </summary>
public sealed record ReporteCorridaPanamaDto(
    string Corrida,
    int GranjaId,
    /// <summary>Un reporte por lote/galpón de la corrida que SÍ tiene liquidación registrada.</summary>
    IReadOnlyList<ReporteCorridaPanamaItemDto> Items,
    /// <summary>Lotes/galpones de la corrida que aún no tienen liquidación registrada (aviso en el front).</summary>
    IReadOnlyList<LoteCorridaPanamaResumenDto> LotesSinLiquidacion,
    /// <summary>Consolidado de la corrida (fórmulas espejo de la fn sobre insumos sumados). Null si Items está vacío.</summary>
    ReporteIndicadoresPanamaDto? Consolidado
);

/// <summary>Reporte individual de un lote/galpón dentro de la corrida.</summary>
public sealed record ReporteCorridaPanamaItemDto(
    int LoteAveEngordeId,
    string LoteNombre,
    string? GalponId,
    DateTime? FechaEncaset,
    ReporteIndicadoresPanamaDto Reporte
);

/// <summary>Identificación mínima de un lote de la corrida (para listar los que no tienen liquidación).</summary>
public sealed record LoteCorridaPanamaResumenDto(
    int LoteAveEngordeId,
    string LoteNombre,
    string? GalponId,
    DateTime? FechaEncaset
);
