namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Insumos digitados por el usuario al liquidar un lote de pollo de engorde en Panamá.
/// Una fila por lote (lote_ave_engorde_id único). Los indicadores derivados NO se guardan aquí:
/// los calcula la función SQL fn_reporte_indicadores_panama a partir de estos 6 insumos
/// y de los agregados del seguimiento (seguimiento_diario_aves_engorde_panama).
/// Tabla: liquidacion_lote_engorde_panama.
/// </summary>
public class LiquidacionLoteEngordePanama
{
    public int Id { get; set; }
    public int LoteAveEngordeId { get; set; }

    // ─── Insumos digitados por el usuario ───
    /// <summary>Metros cuadrados del galpón/lote.</summary>
    public decimal MetrosCuadrados { get; set; }
    /// <summary>Aves finales en granja (al cierre).</summary>
    public int AvesFinalGranja { get; set; }
    /// <summary>Aves beneficiadas (enviadas a planta).</summary>
    public int AvesBeneficiada { get; set; }
    /// <summary>Producción de kilos en pie.</summary>
    public decimal ProduccionKiloPie { get; set; }
    /// <summary>Días de engorde.</summary>
    public int DiasEngorde { get; set; }
    /// <summary>Días en granja.</summary>
    public int DiasEnGranja { get; set; }

    // ─── Auditoría ───
    /// <summary>Id de usuario (Guid string) que registró la liquidación.</summary>
    public string? RegistradoPorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual LoteAveEngorde? LoteAveEngorde { get; set; }
}
