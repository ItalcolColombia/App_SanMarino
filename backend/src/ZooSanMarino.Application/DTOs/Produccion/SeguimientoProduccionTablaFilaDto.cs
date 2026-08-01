// src/ZooSanMarino.Application/DTOs/Produccion/SeguimientoProduccionTablaFilaDto.cs
namespace ZooSanMarino.Application.DTOs.Produccion;

/// <summary>
/// Fila de <c>fn_seguimiento_diario_produccion</c> (SqlQueryRaw). Tipo NO mapeado: las columnas
/// snake_case de la fn se enlazan por <c>UseSnakeCaseNamingConvention</c> (mismo patrón que
/// <c>SeguimientoDiarioTablaFilaDto</c> de engorde). Las columnas JSONB se exponen como
/// <c>string?</c> por compatibilidad con SqlQueryRaw.
/// <para>
/// <see cref="SegId"/> NULL = fila movimiento-only (día con movimiento de aves pero sin registro
/// diario). <see cref="SaldoAvesH"/>/<see cref="SaldoAvesM"/> NULL = rama legacy sin LPP.
/// peso_h/peso_m/uniformidad/coeficiente_variacion viajan como NUMERIC (decimal exacto, mismos
/// tipos que la tabla); el resto de medidas como double.
/// </para>
/// </summary>
public class SeguimientoProduccionTablaFilaDto
{
    public long? SegId { get; set; }
    public DateTime Fecha { get; set; }
    public DateTime? FechaTs { get; set; }
    public string Fuente { get; set; } = "";
    /// <summary>v2: fila TSD del lote base con lpp NULL en rama LPP (visible en la grilla; las fns semanales la excluyen).</summary>
    public bool FilaSinLpp { get; set; }
    public int? LoteId { get; set; }
    public int? LotePosturaProduccionId { get; set; }
    public int? CompanyId { get; set; }

    public int? EdadDias { get; set; }
    public int? Semana { get; set; }

    public int? MortalidadHembras { get; set; }
    public int? MortalidadMachos { get; set; }
    public int? SelH { get; set; }
    public int? SelM { get; set; }
    public int? ErrorSexajeHembras { get; set; }
    public int? ErrorSexajeMachos { get; set; }

    public double? ConsKgH { get; set; }
    public double? ConsKgM { get; set; }
    public double ConsumoTotalKg { get; set; }
    public string? TipoAlimento { get; set; }

    public int? HuevoTot { get; set; }
    public int? HuevoInc { get; set; }
    public int? HuevoLimpio { get; set; }
    public int? HuevoTratado { get; set; }
    public int? HuevoSucio { get; set; }
    public int? HuevoDeforme { get; set; }
    public int? HuevoBlanco { get; set; }
    public int? HuevoDobleYema { get; set; }
    public int? HuevoPiso { get; set; }
    public int? HuevoPequeno { get; set; }
    public int? HuevoRoto { get; set; }
    public int? HuevoDesecho { get; set; }
    public int? HuevoOtro { get; set; }
    public double? PesoHuevo { get; set; }

    public long HuevoTotAcum { get; set; }
    public long HuevoIncAcum { get; set; }
    public double? PctPosturaDia { get; set; }

    public int MovVentaH { get; set; }
    public int MovVentaM { get; set; }
    public int MovRetiroH { get; set; }
    public int MovRetiroM { get; set; }
    public int MovTrasladoInH { get; set; }
    public int MovTrasladoInM { get; set; }
    public int MovTrasladoOutH { get; set; }
    public int MovTrasladoOutM { get; set; }

    public int? AvesHInicioDia { get; set; }
    public int? AvesMInicioDia { get; set; }
    public int? SaldoAvesH { get; set; }
    public int? SaldoAvesM { get; set; }

    public bool EsTraslado { get; set; }
    public string? TrasladoDireccion { get; set; }
    public int? TrasladoIngresoHembras { get; set; }
    public int? TrasladoIngresoMachos { get; set; }
    public int? TrasladoSalidaHembras { get; set; }
    public int? TrasladoSalidaMachos { get; set; }
    public int? LoteDestinoId { get; set; }
    public int? GranjaDestinoId { get; set; }

    public decimal? PesoH { get; set; }
    public decimal? PesoM { get; set; }
    public decimal? Uniformidad { get; set; }
    public decimal? CoeficienteVariacion { get; set; }
    public double? UniformidadHembras { get; set; }
    public double? UniformidadMachos { get; set; }
    public double? CvHembras { get; set; }
    public double? CvMachos { get; set; }
    public string? ObservacionesPesaje { get; set; }

    public double? ConsumoAguaDiario { get; set; }
    public double? ConsumoAguaPh { get; set; }
    public double? ConsumoAguaOrp { get; set; }
    public double? ConsumoAguaTemperatura { get; set; }

    public int? Etapa { get; set; }
    public string? Ciclo { get; set; }
    public string? Observaciones { get; set; }
    public string? Metadata { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
