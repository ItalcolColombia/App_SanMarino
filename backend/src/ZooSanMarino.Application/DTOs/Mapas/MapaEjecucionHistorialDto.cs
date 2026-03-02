namespace ZooSanMarino.Application.DTOs.Mapas;

/// <summary>Item de historial de ejecuciones de un mapa (para listado).</summary>
public class MapaEjecucionHistorialDto
{
    public int Id { get; set; }
    public int MapaId { get; set; }
    public string Estado { get; set; } = null!;
    public string? MensajeError { get; set; }
    public DateTime FechaEjecucion { get; set; }
    public bool PuedeDescargar { get; set; }
    public string? TipoArchivo { get; set; }
}
