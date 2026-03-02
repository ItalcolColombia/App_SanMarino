namespace ZooSanMarino.Application.DTOs.Mapas;

public class SaveMapaPasosDto
{
    public int MapaId { get; set; }
    public List<MapaPasoDto> Pasos { get; set; } = new();
}
