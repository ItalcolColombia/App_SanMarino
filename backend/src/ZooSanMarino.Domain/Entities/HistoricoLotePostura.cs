namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Historial de lotes postura. Cada registro creado o actualizado en lote_postura_levante
/// o lote_postura_produccion genera un registro en esta tabla para auditoría e históricos.
/// </summary>
public class HistoricoLotePostura
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string TipoLote { get; set; } = null!; // "LotePosturaLevante" | "LotePosturaProduccion"
    public int? LotePosturaLevanteId { get; set; }
    public int? LotePosturaProduccionId { get; set; }
    public string TipoRegistro { get; set; } = "Creacion"; // "Creacion" | "Actualizacion"
    public DateTime FechaRegistro { get; set; }
    public int? UsuarioId { get; set; }

    /// <summary>Snapshot JSON del registro al momento de la operación.</summary>
    public System.Text.Json.JsonDocument? Snapshot { get; set; }

    public DateTime CreatedAt { get; set; }

    public LotePosturaLevante? LotePosturaLevante { get; set; }
    public LotePosturaProduccion? LotePosturaProduccion { get; set; }
}
