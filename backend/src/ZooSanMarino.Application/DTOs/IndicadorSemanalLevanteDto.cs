namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Indicador semanal de levante (postura Colombia) calculado en la BD por
/// fn_indicadores_levante_postura. El front solo lo pinta (no calcula).
/// Nombres de columnas = snake_case de la función (mapeo por EF SqlQueryRaw).
/// </summary>
public sealed class IndicadorSemanalLevanteDto
{
    public int Semana { get; set; }
    public double AvesInicioSemana { get; set; }
    public double AvesFinSemana { get; set; }
    public double ConsumoDiario { get; set; }
    public double ConsumoTabla { get; set; }
    public double ConsumoTotalSemana { get; set; }
    public double ConversionAlimenticia { get; set; }
    public double PesoTabla { get; set; }
    public double UnifReal { get; set; }
    public double UnifTabla { get; set; }
    public double MortTabla { get; set; }
    public double DifPesoPct { get; set; }
    public double GananciaSemana { get; set; }
    public double GananciaDiariaAcumulada { get; set; }
    public double GananciaTabla { get; set; }
    public double MortalidadSem { get; set; }
    public double SeleccionSem { get; set; }
    public double ErrorSexajeSem { get; set; }
    public double MortalidadMasSeleccion { get; set; }
    public double Eficiencia { get; set; }
    public double Ip { get; set; }
    public double Vpi { get; set; }
    public double SaldoAvesSemanal { get; set; }
    public double MortalidadAcum { get; set; }
    public double SeleccionAcum { get; set; }
    public double MortalidadMasSeleccionAcum { get; set; }
    public bool PisoTermicoVisible { get; set; }
    public double PesoInicial { get; set; }
    public double PesoCierre { get; set; }
    public int DiasConRegistro { get; set; }
}
