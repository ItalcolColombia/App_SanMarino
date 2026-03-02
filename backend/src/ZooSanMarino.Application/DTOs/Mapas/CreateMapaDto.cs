namespace ZooSanMarino.Application.DTOs.Mapas;

public class CreateMapaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    /// <summary>Código de plantilla: null, granjas_huevos_alimento, entrada_ciesa.</summary>
    public string? CodigoPlantilla { get; set; }
    public int? PaisId { get; set; }
    public bool IsActive { get; set; } = true;
}
