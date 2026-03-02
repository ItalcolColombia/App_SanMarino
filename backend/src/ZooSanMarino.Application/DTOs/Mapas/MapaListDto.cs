namespace ZooSanMarino.Application.DTOs.Mapas;

public class MapaListDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? CodigoPlantilla { get; set; }
    public bool IsActive { get; set; }
    public int? PaisId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalEjecuciones { get; set; }
    public DateTime? UltimaEjecucionAt { get; set; }
}
