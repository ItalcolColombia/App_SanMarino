namespace ZooSanMarino.Application.DTOs.Mapas;

public class MapaDetailDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? CodigoPlantilla { get; set; }
    public bool IsActive { get; set; }
    public int CompanyId { get; set; }
    public int? PaisId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<MapaPasoDto> Pasos { get; set; } = new();
    public int TotalEjecuciones { get; set; }
}
