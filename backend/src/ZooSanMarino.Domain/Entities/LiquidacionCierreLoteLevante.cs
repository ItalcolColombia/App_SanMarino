using System.Text.Json;

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Registro de liquidación técnica al cerrar un lote de levante (semana 25).
/// Tabla: liquidacion_cierre_lote_levante
/// </summary>
public class LiquidacionCierreLoteLevante
{
    public int Id { get; set; }
    public int LotePosturaLevanteId { get; set; }
    public DateTime FechaCierre { get; set; } = DateTime.UtcNow;

    // Usuario que ejecutó el cierre
    public int? ClosedByUserId { get; set; }

    // Aves encasetadas
    public int? HembrasEncasetadas { get; set; }
    public int? MachosEncasetados { get; set; }

    // Métricas reales acumuladas hembras (semana 25)
    public decimal PorcentajeMortalidadHembras { get; set; }
    public decimal PorcentajeSeleccionHembras { get; set; }
    public decimal PorcentajeErrorSexajeHembras { get; set; }
    /// <summary>Mort + Selección + Error sexaje acumulado hembras.</summary>
    public decimal PorcentajeRetiroAcumulado { get; set; }

    // Consumo — real
    public decimal ConsumoAlimentoRealGramos { get; set; }

    // Consumo — guía genética
    /// <summary>ConsAcH de la guía: consumo acumulado (g/ave) semanas 1-25.</summary>
    public decimal? ConsumoAlimentoGuiaGramos { get; set; }
    /// <summary>GrAveDiaH de la guía: consumo diario (g/ave) en la semana 25 específicamente.</summary>
    public decimal? ConsumoGrAveDiaSemana25Guia { get; set; }
    public decimal? PorcentajeDiferenciaConsumo { get; set; }

    // Peso semana 25
    public decimal? PesoSemana25Real { get; set; }
    public decimal? PesoSemana25Guia { get; set; }
    public decimal? PorcentajeDiferenciaPeso { get; set; }

    // Uniformidad
    public decimal? UniformidadReal { get; set; }
    public decimal? UniformidadGuia { get; set; }
    public decimal? PorcentajeDiferenciaUniformidad { get; set; }

    // % Retiro guía genética
    public decimal? PorcentajeRetiroGuia { get; set; }

    // Info guía usada
    public string? RazaGuia { get; set; }
    public int? AnoGuia { get; set; }

    /// <summary>JSON para campos adicionales (machos, semana exacta, etc.).</summary>
    public JsonDocument? Metadata { get; set; }

    // Auditoría
    public int CompanyId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public LotePosturaLevante LotePosturaLevante { get; set; } = null!;
}
