// src/ZooSanMarino.Domain/Entities/FarmProductInventory.cs
using System.Text.Json;

namespace ZooSanMarino.Domain.Entities;

public class FarmProductInventory
{
    public int Id { get; set; }

    // Claves
    public int FarmId { get; set; }
    public int CatalogItemId { get; set; }

    // Datos de inventario
    public decimal Quantity { get; set; }        // numeric(18,3)
    public string Unit { get; set; } = "kg";     // p.ej. kg, und, l
    public string? Location { get; set; }        // bodega/galpón/estante
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public decimal? UnitCost { get; set; }       // numeric(18,2)

    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
    public bool Active { get; set; } = true;

    // Usuario responsable (normalmente viene del JWT)
    public string? ResponsibleUserId { get; set; }

    // Timestamps
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navs
    public Farm Farm { get; set; } = null!;
    public CatalogItem CatalogItem { get; set; } = null!;
}
