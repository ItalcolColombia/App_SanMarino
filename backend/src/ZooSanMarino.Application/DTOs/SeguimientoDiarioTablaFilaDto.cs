namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Proyección de fn_seguimiento_diario_engorde(p_lote_id) para el módulo Ecuador.
/// Las columnas JSONB se exponen como string para compatibilidad con SqlQueryRaw.
/// </summary>
public class SeguimientoDiarioTablaFilaDto
{
    // Identificación
    public long SegId { get; set; }
    public DateTime Fecha { get; set; }

    // Tiempo
    public int EdadDia { get; set; }
    public short Semana { get; set; }

    // Seguimiento crudo
    public int MortalidadHembras { get; set; }
    public int MortalidadMachos { get; set; }
    public int SelH { get; set; }
    public int SelM { get; set; }
    public int ErrorSexajeHembras { get; set; }
    public int ErrorSexajeMachos { get; set; }

    // Calculados simples
    public int TotalMortSelDia { get; set; }
    public int PerdidasTotalesDia { get; set; }
    public double ConsumoKgHembras { get; set; }
    public double ConsumoKgMachos { get; set; }
    public double ConsumoDiaKg { get; set; }

    // Acumulados (window functions)
    public double AcumConsumoKg { get; set; }
    public int SaldoAves { get; set; }
    public double? PctPerdidasDia { get; set; }

    // Saldo alimento persistido (NULL si el registro aún no tiene dato en tabla)
    public double? SaldoAlimentoKg { get; set; }

    // Histórico de alimento por fecha
    public double IngresoAlimentoKg { get; set; }
    public double TrasladoEntradaKg { get; set; }
    public double TrasladoSalidaKg { get; set; }
    public double ConsumoBodegaKg { get; set; }

    // Movimientos de aves y documentos
    public string? Documento { get; set; }
    public int DespachoHembras { get; set; }
    public int DespachoMachos { get; set; }
    public int DespachoMixtas { get; set; }

    // Mediciones del seguimiento
    public string? TipoAlimento { get; set; }
    public double? PesoPromHembras { get; set; }
    public double? PesoPromMachos { get; set; }
    public double? UniformidadHembras { get; set; }
    public double? UniformidadMachos { get; set; }
    public double? CvHembras { get; set; }
    public double? CvMachos { get; set; }
    public double? ConsumoAguaDiario { get; set; }
    public double? ConsumoAguaPh { get; set; }
    public double? ConsumoAguaOrp { get; set; }
    public double? ConsumoAguaTemperatura { get; set; }
    public string? Observaciones { get; set; }
    public string? Ciclo { get; set; }

    // JSONB expuesto como string
    public string? Metadata { get; set; }
    public string? ItemsAdicionales { get; set; }
    public string? HistoricoConsumoAlimento { get; set; }

    public string? CreatedByUserId { get; set; }
}
