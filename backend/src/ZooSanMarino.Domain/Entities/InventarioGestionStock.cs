// src/ZooSanMarino.Domain/Entities/InventarioGestionStock.cs
// Stock del módulo Gestión de Inventario (Panama/Ecuador).
// Para item tipo "alimento": ubicación Granja -> Núcleo -> Galpón.
// Para otros tipos: solo Granja (NucleoId y GalponId null).

namespace ZooSanMarino.Domain.Entities;

public class InventarioGestionStock
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public int PaisId { get; set; }
    public int FarmId { get; set; }
    /// <summary>Requerido para alimento; null para otros tipos (stock a nivel granja).</summary>
    public string? NucleoId { get; set; }
    /// <summary>Requerido para alimento; null para otros tipos.</summary>
    public string? GalponId { get; set; }
    public int ItemInventarioEcuadorId { get; set; }

    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "kg";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public Pais Pais { get; set; } = null!;
    public Farm Farm { get; set; } = null!;
    public ItemInventarioEcuador ItemInventarioEcuador { get; set; } = null!;
}
