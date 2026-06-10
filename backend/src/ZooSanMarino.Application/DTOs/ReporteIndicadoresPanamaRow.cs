namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Proyección 1:1 de fn_reporte_indicadores_panama(p_lote_id).
/// Mapeada por SqlQueryRaw (convención snake_case → PascalCase).
/// </summary>
public class ReporteIndicadoresPanamaRow
{
    public int Id { get; set; }
    public string? IdUsuarioRegistro { get; set; }
    public int IdLote { get; set; }

    public decimal MetrosCuadrados { get; set; }
    public decimal AvesFinalGranja { get; set; }
    public decimal ProduccionKiloPie { get; set; }
    public int DiasEngorde { get; set; }
    public int DiasEnGranja { get; set; }
    public int AvesBeneficiada { get; set; }

    public decimal PesoPromedio { get; set; }
    public decimal MortalidadPorc { get; set; }
    public decimal SeleccionPorc { get; set; }
    public decimal PorcMortalidadTotal { get; set; }
    public decimal Supervivencia { get; set; }
    public decimal ConsumoAve { get; set; }
    public decimal Conversion { get; set; }
    public decimal EficienciaAmericana { get; set; }
    public decimal Eef { get; set; }
    public decimal EefDos { get; set; }
    public decimal AvesMetrosCua { get; set; }
    public decimal KilosMetrosCua { get; set; }
    public decimal Productividad { get; set; }
    public decimal FaltanteSobra { get; set; }

    public decimal ConsumoAlimentoTotal { get; set; }
    public decimal TotalAvesSeleccion { get; set; }
    public decimal TotalAvesMuertas { get; set; }
    public int AvesEncasetadas { get; set; }

    public ReporteIndicadoresPanamaDto ToDto() => new(
        new LiquidacionPanamaDto(
            Id, IdUsuarioRegistro, IdLote,
            MetrosCuadrados, AvesFinalGranja, ProduccionKiloPie, DiasEngorde, DiasEnGranja, AvesBeneficiada,
            PesoPromedio, MortalidadPorc, SeleccionPorc, PorcMortalidadTotal, Supervivencia,
            ConsumoAve, Conversion, EficienciaAmericana, Eef, EefDos,
            AvesMetrosCua, KilosMetrosCua, Productividad, FaltanteSobra),
        new InfoProductivaPanamaDto(ConsumoAlimentoTotal, TotalAvesSeleccion, TotalAvesMuertas),
        AvesEncasetadas);
}
