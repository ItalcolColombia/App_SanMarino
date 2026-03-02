namespace ZooSanMarino.Application.DTOs.Mapas;

public class MapaEjecucionEstadoDto
{
    public int Id { get; set; }
    public int MapaId { get; set; }
    public string Estado { get; set; } = null!;
    public string? MensajeError { get; set; }
    /// <summary>Progreso actual durante la ejecución (ej. "Paso 2/5: Extracción").</summary>
    public string? MensajeEstado { get; set; }
    public int? PasoActual { get; set; }
    public int? TotalPasos { get; set; }
    public string? TipoArchivo { get; set; }
    public DateTime FechaEjecucion { get; set; }
    /// <summary>True si estado es completado y hay archivo para descargar.</summary>
    public bool PuedeDescargar { get; set; }
}
