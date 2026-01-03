// src/ZooSanMarino.Application/DTOs/FarmInventoryDtos.cs
using System.Text.Json;

namespace ZooSanMarino.Application.DTOs;

public class FarmInventoryDto
{
    public int Id { get; set; }
    public int FarmId { get; set; }

    public int CatalogItemId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;

    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "kg";
    public string? Location { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public decimal? UnitCost { get; set; }
    public JsonDocument? Metadata { get; set; } // Metadata del inventario
    public JsonDocument? CatalogItemMetadata { get; set; } // Metadata del CatalogItem (incluye type_item, especie, raza, etc.)
    public bool Active { get; set; }

    public string? ResponsibleUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class FarmInventoryCreateRequest
{
    // Puedes enviar CatalogItemId o Codigo (uno de los dos)
    public int? CatalogItemId { get; set; }
    public string? Codigo { get; set; }

    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "kg";
    public string? Location { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public decimal? UnitCost { get; set; }

    public JsonDocument? Metadata { get; set; }
    public bool Active { get; set; } = true;

    // Opcional: si no hay usuario autenticado
    public string? ResponsibleUserId { get; set; }
}

public class FarmInventoryUpdateRequest
{
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "kg";
    public string? Location { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public decimal? UnitCost { get; set; }
    public JsonDocument? Metadata { get; set; }
    public bool Active { get; set; } = true;

    public string? ResponsibleUserId { get; set; }
}
