namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Paso de un mapa: head, extraction, transformation, execute, export.
/// </summary>
public class MapaPaso
{
    public int Id { get; set; }
    public int MapaId { get; set; }
    public int Orden { get; set; } = 1;
    public string Tipo { get; set; } = null!; // head, extraction, transformation, execute, export
    public string? NombreEtiqueta { get; set; }
    public string? ScriptSql { get; set; }
    public string? Opciones { get; set; } // JSON: formato export pdf/excel, etc.

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Mapa Mapa { get; set; } = null!;
}
