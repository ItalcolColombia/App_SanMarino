namespace ZooSanMarino.Application.DTOs.Mapas;

public class UpdateMapaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? CodigoPlantilla { get; set; }
    public int? PaisId { get; set; }
    public bool IsActive { get; set; } = true;
}
