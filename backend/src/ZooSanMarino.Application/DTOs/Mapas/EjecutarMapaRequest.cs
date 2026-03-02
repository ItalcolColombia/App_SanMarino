namespace ZooSanMarino.Application.DTOs.Mapas;

public class EjecutarMapaRequest
{
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public List<int>? GranjaIds { get; set; }
    public string? TipoDato { get; set; }
    /// <summary>Formato de exportación: excel o pdf. Por defecto excel.</summary>
    public string FormatoExport { get; set; } = "excel";
}
