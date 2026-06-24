namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Filtros del Informe Semanal Pollo de Engorde (Panamá).
/// CompanyId se toma del usuario autenticado (no del request).
/// </summary>
public record InformeSemanalRequest(
    int[]? GranjaIds,          // null o vacío = todas las granjas
    string? NucleoId,          // null = todos
    string? GalponId,          // null = todos
    int? LoteId,               // null = todos los que pasen el resto
    DateOnly? FechaDesde,      // semana calendario (overlap); null = sin tope
    DateOnly? FechaHasta
);

/// <summary>
/// Proyección 1:1 de fn_informe_semanal_pollo_engorde(...). Mapeada por SqlQueryRaw
/// (snake_case → PascalCase). Las columnas *Tabla / Pct* salen NULL (placeholder, sin conectar).
/// </summary>
public class InformeSemanalRow
{
    public int CompanyId { get; set; }
    public int GranjaId { get; set; }
    public string GranjaNombre { get; set; } = "";
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public string? GalponNombre { get; set; }
    public int LoteAveEngordeId { get; set; }
    public string LoteNombre { get; set; } = "";
    public DateOnly? FechaEncaset { get; set; }
    public int Semana { get; set; }
    public int EdadDiaFin { get; set; }
    public DateOnly? FechaInicioSemana { get; set; }
    public DateOnly? FechaFinSemana { get; set; }
    // Aves
    public int AvesEncasetadas { get; set; }
    public int SaldoInicioSemana { get; set; }
    public int SaldoFinSemana { get; set; }
    public int MortNaturalUnid { get; set; }
    public int SeleccionUnid { get; set; }
    public int VentasUnid { get; set; }
    public decimal MortNaturalPct { get; set; }
    public decimal SeleccionPct { get; set; }
    public decimal MortalidadTotalPct { get; set; }
    // Consumo
    public decimal ConsumoSemanaKg { get; set; }
    public decimal ConsumoAcumKg { get; set; }
    public decimal ConsumoRealGAve { get; set; }
    // Peso / ganancia / conversión (reales)
    public decimal? PesoRealG { get; set; }
    public decimal? PesoAnteriorG { get; set; }
    public decimal? PesoLlegadaG { get; set; }
    public decimal? GananciaRealG { get; set; }
    public decimal? ConversionReal { get; set; }
    // Ventas
    public decimal VentasKg { get; set; }
    // Agua
    public decimal AguaMl { get; set; }
    public decimal? RelacionAgua { get; set; }
    // >>> Tabla genética (placeholder NULL — no conectado) <<<
    public decimal? ConsumoTablaG { get; set; }
    public decimal? PesoTablaG { get; set; }
    public decimal? GananciaTablaG { get; set; }
    public decimal? ConversionTabla { get; set; }
    public decimal? MortalidadTablaPct { get; set; }
    public decimal? PctConsumo { get; set; }
    public decimal? PctPeso { get; set; }
    public decimal? PctConversion { get; set; }

    public InformeSemanalFilaDto ToDto() => new(
        GranjaId, GranjaNombre, NucleoId, GalponId, GalponNombre,
        LoteAveEngordeId, LoteNombre, FechaEncaset, Semana, EdadDiaFin,
        FechaInicioSemana, FechaFinSemana,
        AvesEncasetadas, SaldoInicioSemana, SaldoFinSemana,
        MortNaturalUnid, SeleccionUnid, VentasUnid,
        MortNaturalPct, SeleccionPct, MortalidadTotalPct,
        ConsumoSemanaKg, ConsumoAcumKg, ConsumoRealGAve,
        PesoRealG, PesoAnteriorG, PesoLlegadaG, GananciaRealG, ConversionReal,
        VentasKg, AguaMl, RelacionAgua,
        ConsumoTablaG, PesoTablaG, GananciaTablaG, ConversionTabla, MortalidadTablaPct,
        PctConsumo, PctPeso, PctConversion);
}

/// <summary>Una fila del informe = (lote, semana de vida). Reales + Tabla (placeholder).</summary>
public record InformeSemanalFilaDto(
    int GranjaId,
    string GranjaNombre,
    string? NucleoId,
    string? GalponId,
    string? GalponNombre,
    int LoteAveEngordeId,
    string LoteNombre,
    DateOnly? FechaEncaset,
    int Semana,
    int EdadDiaFin,
    DateOnly? FechaInicioSemana,
    DateOnly? FechaFinSemana,
    int AvesEncasetadas,
    int SaldoInicioSemana,
    int SaldoFinSemana,
    int MortNaturalUnid,
    int SeleccionUnid,
    int VentasUnid,
    decimal MortNaturalPct,
    decimal SeleccionPct,
    decimal MortalidadTotalPct,
    decimal ConsumoSemanaKg,
    decimal ConsumoAcumKg,
    decimal ConsumoRealGAve,
    decimal? PesoRealG,
    decimal? PesoAnteriorG,
    decimal? PesoLlegadaG,
    decimal? GananciaRealG,
    decimal? ConversionReal,
    decimal VentasKg,
    decimal AguaMl,
    decimal? RelacionAgua,
    decimal? ConsumoTablaG,
    decimal? PesoTablaG,
    decimal? GananciaTablaG,
    decimal? ConversionTabla,
    decimal? MortalidadTablaPct,
    decimal? PctConsumo,
    decimal? PctPeso,
    decimal? PctConversion
);

/// <summary>Fila CONSOLIDADO de una semana: AVES = suma; tasas/pesos/consumo = promedio.</summary>
public record InformeSemanalConsolidadoDto(
    int Semana,
    int CantidadLotes,
    int AvesTotales,
    decimal ConsumoRealGAveProm,
    decimal? PesoRealGProm,
    decimal? GananciaRealGProm,
    decimal? ConversionRealProm,
    decimal MortNaturalPctProm,
    decimal SeleccionPctProm,
    decimal MortalidadTotalPctProm,
    decimal ConsumoSemanaKgTotal,
    decimal VentasKgTotal,
    int VentasUnidTotal,
    // Tabla genética (promedio entre lotes; NULL si ningún lote tiene guía configurada)
    decimal? ConsumoTablaGProm,
    decimal? PesoTablaGProm,
    decimal? GananciaTablaGProm,
    decimal? ConversionTablaProm,
    decimal? MortalidadTablaPctProm,
    decimal? PctConsumoProm,
    decimal? PctPesoProm,
    decimal? PctConversionProm
);

/// <summary>Bloque de una semana de vida: filas por lote + consolidado.</summary>
public record InformeSemanalGrupoSemanaDto(
    int Semana,
    IReadOnlyList<InformeSemanalFilaDto> Filas,
    InformeSemanalConsolidadoDto Consolidado
);

/// <summary>Respuesta completa: filtros aplicados + grupos por semana de vida.</summary>
public record InformeSemanalReporteDto(
    InformeSemanalRequest FiltrosAplicados,
    int TotalFilas,
    IReadOnlyList<InformeSemanalGrupoSemanaDto> Semanas
);
