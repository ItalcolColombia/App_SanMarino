namespace ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;

/// <summary>
/// Fila cruda devuelta por fn_reporte_semanal_levante_extras (cálculo en la BD).
/// Nombres de propiedades = snake_case de la función, mapeados por EF SqlQueryRaw
/// (mismo patrón que IndicadorSemanalLevanteDto / IndicadorProduccionSemanalBdRow).
/// Conteos y kg POR SEXO con la misma semántica de semana/saldo que
/// fn_indicadores_levante_postura; los % y acumulados los deriva
/// ReporteTecnicoSemanalCalculos (base FIJA de aves iniciales, como el Excel).
/// </summary>
public sealed class ReporteSemanalLevanteExtrasRow
{
    public int Semana { get; set; }
    public DateTime FechaFinSemana { get; set; }
    public int DiasConRegistro { get; set; }

    public double BaseHembras { get; set; }
    public double BaseMachos { get; set; }
    public double AvesHembrasInicio { get; set; }
    public double AvesHembrasFin { get; set; }
    public double AvesMachosInicio { get; set; }
    public double AvesMachosFin { get; set; }

    public int MortalidadHembrasSem { get; set; }
    public int MortalidadMachosSem { get; set; }
    public int SeleccionHembrasSem { get; set; }
    public int SeleccionMachosSem { get; set; }
    public int ErrorHembrasSem { get; set; }
    public int ErrorMachosSem { get; set; }
    public int TrasladoIngresoHembrasSem { get; set; }
    public int TrasladoIngresoMachosSem { get; set; }
    public int TrasladoSalidaHembrasSem { get; set; }
    public int TrasladoSalidaMachosSem { get; set; }

    public double ConsumoKgHembrasSem { get; set; }
    public double ConsumoKgMachosSem { get; set; }

    public double? KcalAlimentoHembras { get; set; }
    public double? ProtAlimentoHembras { get; set; }

    public double? UniformidadHembras { get; set; }
    public double? UniformidadMachos { get; set; }
    public double? CvHembras { get; set; }
    public double? CvMachos { get; set; }

    /// <summary>Peso prom hembras (arrastre del último pesaje conocido; NULL si nunca hubo pesaje).</summary>
    public double? PesoHembrasSem { get; set; }
    /// <summary>Peso prom machos (arrastre del último pesaje conocido; NULL si nunca hubo pesaje).</summary>
    public double? PesoMachosSem { get; set; }
}
