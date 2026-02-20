using System.Text.Json;

namespace ZooSanMarino.Domain.Entities;

public class CatalogItem
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string ItemType { get; set; } = "alimento"; // Tipo de item: alimento, vacuna, medicamento, etc.
    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
    public bool Activo { get; set; } = true;
    
    // Campos para filtrado por empresa y país
    public int CompanyId { get; set; }
    public int PaisId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
