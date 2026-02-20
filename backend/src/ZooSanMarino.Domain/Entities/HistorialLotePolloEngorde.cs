namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Historial de lotes de pollo engorde: aves con que inicia cada lote (Lote Ave Engorde o Lote Reproductora)
/// para reportes: cuántas aves empezaron, cuántas se vendieron, cuántas hay disponibles.
/// </summary>
public class HistorialLotePolloEngorde
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string TipoLote { get; set; } = null!; // "LoteAveEngorde" | "LoteReproductoraAveEngorde"
    public int? LoteAveEngordeId { get; set; }
    public int? LoteReproductoraAveEngordeId { get; set; }
    public string TipoRegistro { get; set; } = "Inicio"; // "Inicio" | "Ajuste"
    public int AvesHembras { get; set; }
    public int AvesMachos { get; set; }
    public int AvesMixtas { get; set; }
    public DateTime FechaRegistro { get; set; }
    public int? MovimientoId { get; set; }
    public DateTime CreatedAt { get; set; }

    public int TotalAves => AvesHembras + AvesMachos + AvesMixtas;

    public LoteAveEngorde? LoteAveEngorde { get; set; }
    public LoteReproductoraAveEngorde? LoteReproductoraAveEngorde { get; set; }
    public MovimientoPolloEngorde? Movimiento { get; set; }
}
