namespace ZooSanMarino.Application.DTOs.Mapas;

public class MapaPasoDto
{
    public int Id { get; set; }
    public int MapaId { get; set; }
    public int Orden { get; set; }
    public string Tipo { get; set; } = null!; // head, extraction, transformation, execute, export
    public string? NombreEtiqueta { get; set; }
    public string? ScriptSql { get; set; }
    public string? Opciones { get; set; }
}
