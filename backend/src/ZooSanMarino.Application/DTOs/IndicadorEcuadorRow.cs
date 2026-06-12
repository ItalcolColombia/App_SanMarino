namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Proyección 1:1 de fn_indicadores_pollo_engorde(p_lote_id, ...) (Parte A).
/// Mapeada por SqlQueryRaw (convención snake_case → PascalCase). El wrapper C#
/// aplica filtros administrativos y la mapea a IndicadorEcuadorDto.
/// </summary>
public class IndicadorEcuadorRow
{
    public int GranjaId { get; set; }
    public string GranjaNombre { get; set; } = "";
    public int LoteId { get; set; }
    public string? LoteNombre { get; set; }
    public string? GalponId { get; set; }
    public string? GalponNombre { get; set; }
    public int AvesEncasetadas { get; set; }
    public int AvesSacrificadas { get; set; }
    public int Mortalidad { get; set; }
    public decimal MortalidadPorcentaje { get; set; }
    public decimal SupervivenciaPorcentaje { get; set; }
    public decimal ConsumoTotalAlimentoKg { get; set; }
    public decimal ConsumoAveGramos { get; set; }
    public decimal KgCarnePollos { get; set; }
    public decimal PesoPromedioKilos { get; set; }
    public decimal Conversion { get; set; }
    public decimal ConversionAjustada2700 { get; set; }
    public decimal PesoAjusteVariable { get; set; }
    public decimal DivisorAjusteVariable { get; set; }
    public decimal EdadPromedio { get; set; }
    public decimal MetrosCuadrados { get; set; }
    public decimal AvesPorMetroCuadrado { get; set; }
    public decimal KgPorMetroCuadrado { get; set; }
    public decimal EficienciaAmericana { get; set; }
    public decimal EficienciaEuropea { get; set; }
    public decimal IndiceProductividad { get; set; }
    public decimal GananciaDia { get; set; }
    public DateTime? FechaInicioLote { get; set; }
    public DateTime? FechaCierreLote { get; set; }
    public bool LoteCerrado { get; set; }
    public DateTime? FechaAlistamiento { get; set; }
    // R1 (campos vacíos): NULL cuando Costos no registró merma en el lote.
    public int? MermaUnidades { get; set; }
    public decimal? MermaKilos { get; set; }
    public decimal? MermaPorcentaje { get; set; }
    public int? AjusteAves { get; set; }
    public decimal? PorcentajeAjuste { get; set; }
    public decimal ProduccionKiloEnPie { get; set; }
    public decimal? TotalKilosDespachadosCliente { get; set; }
    public int DiasEngorde { get; set; }
    public DateTime? FechaLiquidacion { get; set; }
    public int AvesSobrante { get; set; }
    // Marcadores administrativos
    public string? EstadoOperativoLote { get; set; }
    public DateTime? LiquidadoAtMarker { get; set; }
    public decimal RatioSacrificadas { get; set; }

    public IndicadorEcuadorDto ToDto() => new(
        GranjaId, GranjaNombre, LoteId, LoteNombre, GalponId, GalponNombre,
        AvesEncasetadas, AvesSacrificadas, Mortalidad, MortalidadPorcentaje, SupervivenciaPorcentaje,
        ConsumoTotalAlimentoKg, ConsumoAveGramos,
        KgCarnePollos, PesoPromedioKilos, Conversion, ConversionAjustada2700,
        PesoAjusteVariable, DivisorAjusteVariable,
        EdadPromedio, MetrosCuadrados, AvesPorMetroCuadrado, KgPorMetroCuadrado,
        EficienciaAmericana, EficienciaEuropea, IndiceProductividad, GananciaDia,
        FechaInicioLote, FechaCierreLote, LoteCerrado, FechaAlistamiento,
        MermaUnidades, MermaKilos, MermaPorcentaje, AjusteAves, PorcentajeAjuste,
        ProduccionKiloEnPie, TotalKilosDespachadosCliente, DiasEngorde, FechaLiquidacion, AvesSobrante);
}
