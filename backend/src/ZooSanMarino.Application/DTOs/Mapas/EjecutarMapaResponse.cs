namespace ZooSanMarino.Application.DTOs.Mapas;

public class EjecutarMapaResponse
{
    public int EjecucionId { get; set; }
    public string Estado { get; set; } = "en_proceso";
    public string? Mensaje { get; set; }
}
