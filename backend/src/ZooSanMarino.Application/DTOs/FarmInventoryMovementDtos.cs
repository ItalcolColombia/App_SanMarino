// src/ZooSanMarino.Application/DTOs/InventoryMovementDtos.cs
using System.Text.Json;

namespace ZooSanMarino.Application.DTOs;

public class InventoryMovementDto
{
    public int Id { get; set; }
    public int FarmId { get; set; }
    public int CatalogItemId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string MovementType { get; set; } = null!; // Entry|Exit|TransferIn|TransferOut|Adjust
    public string Unit { get; set; } = "kg";
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public Guid? TransferGroupId { get; set; }
    public JsonDocument? Metadata { get; set; }
    public string? ResponsibleUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class InventoryEntryRequest
{
    public int? CatalogItemId { get; set; }
    public string? Codigo { get; set; }
    public decimal Quantity { get; set; }      // positivo
    public string? Unit { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public string? Origin { get; set; }        // Origen para entradas (ej: "Planta Sanmarino", "Planta Itacol")
    public JsonDocument? Metadata { get; set; }
}
public class InventoryExitRequest : InventoryEntryRequest 
{ 
    public string? Destination { get; set; }    // Destino para salidas (ej: "Venta", "Movimiento", "Devolución")
}
public class InventoryTransferRequest : InventoryEntryRequest
{
    public int ToFarmId { get; set; }
}
public class InventoryAdjustRequest : InventoryEntryRequest
{
    // Quantity puede ser + o −
}

public class MovementQuery
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? CatalogItemId { get; set; }
    public string? Codigo { get; set; }
    public string? Type { get; set; } // Entry|Exit|TransferIn|TransferOut|Adjust
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class KardexItemDto
{
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = null!;
    public string? Referencia { get; set; }
    public decimal Cantidad { get; set; }   // +entrada / -salida
    public string Unidad { get; set; } = "kg";
    public decimal Saldo { get; set; }      // acumulado
    public string? Motivo { get; set; }
}

public class StockCountRequest
{
    public List<StockCountItemRequest> Items { get; set; } = new();
}
public class StockCountItemRequest
{
    public int CatalogItemId { get; set; }
    public decimal Conteo { get; set; }
}
