// src/ZooSanMarino.Domain/Entities/SeguimientoDiarioAvesEngorde.cs
using System.Text.Json;

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Seguimiento diario por lote aves de engorde. Tabla: seguimiento_diario_aves_engorde.
/// Un registro por lote_ave_engorde_id por fecha.
/// </summary>
public class SeguimientoDiarioAvesEngorde
{
    public long Id { get; set; }
    public int LoteAveEngordeId { get; set; }
    public DateTime Fecha { get; set; }

    public int? MortalidadHembras { get; set; }
    public int? MortalidadMachos { get; set; }
    public int? SelH { get; set; }
    public int? SelM { get; set; }
    public int? ErrorSexajeHembras { get; set; }
    public int? ErrorSexajeMachos { get; set; }
    public decimal? ConsumoKgHembras { get; set; }
    public decimal? ConsumoKgMachos { get; set; }
    public string? TipoAlimento { get; set; }
    public string? Observaciones { get; set; }
    public string? Ciclo { get; set; }

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

    public JsonDocument? Metadata { get; set; }
    public JsonDocument? ItemsAdicionales { get; set; }

    public double? KcalAlH { get; set; }
    public double? ProtAlH { get; set; }
    public double? KcalAveH { get; set; }
    public double? ProtAveH { get; set; }

    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual LoteAveEngorde? LoteAveEngorde { get; set; }
}
