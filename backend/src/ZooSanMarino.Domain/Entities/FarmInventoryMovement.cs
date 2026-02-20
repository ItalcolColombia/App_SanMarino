// src/ZooSanMarino.Domain/Entities/FarmInventoryMovement.cs
using System.Text.Json;
using ZooSanMarino.Domain.Enums;

namespace ZooSanMarino.Domain.Entities;

public class FarmInventoryMovement
{
    public int Id { get; set; }
    public int FarmId { get; set; }
    public int CatalogItemId { get; set; }
    
    // Tipo de item del catálogo (alimento, vacuna, medicamento, etc.)
    public string? ItemType { get; set; }
    
    // Empresa y País
    public int CompanyId { get; set; }
    public int PaisId { get; set; }
    
    public decimal Quantity { get; set; }      // positiva
    public InventoryMovementType MovementType { get; set; }
    public string Unit { get; set; } = "kg";
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public string? Origin { get; set; }           // Origen para entradas (ej: "Planta Sanmarino", "Planta Itacol")
    public string? Destination { get; set; }      // Destino para salidas (ej: "Venta", "Movimiento", "Devolución")
    public Guid? TransferGroupId { get; set; }
    
    // Campos específicos para movimiento de alimento
    public string? DocumentoOrigen { get; set; }      // Autoconsumo, RVN (Remisión facturada), EAN (Entrada de inventario)
    public string? TipoEntrada { get; set; }          // Entrada Nueva, Traslado entre galpon, Traslados entre granjas
    public string? GalponDestinoId { get; set; }      // ID del galpón destino
    public DateTimeOffset? FechaMovimiento { get; set; } // Fecha del movimiento (puede ser diferente a created_at)
    
    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
    public string? ResponsibleUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Farm Farm { get; set; } = null!;
    public CatalogItem CatalogItem { get; set; } = null!;
    public Company Company { get; set; } = null!;
    public Pais Pais { get; set; } = null!;
}
