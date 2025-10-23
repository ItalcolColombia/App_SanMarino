using System.Text.Json;

namespace ZooSanMarino.Domain.Entities;

public class CatalogItem
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
