// src/ZooSanMarino.Domain/Entities/InventarioGestionMovimiento.cs
// Movimientos (ingresos y traslados) del módulo Gestión de Inventario (Panama/Ecuador).

namespace ZooSanMarino.Domain.Entities;

public class InventarioGestionMovimiento
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public int PaisId { get; set; }
    public int FarmId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public int ItemInventarioEcuadorId { get; set; }

    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "kg";
    /// <summary>Ingreso, TrasladoEntrada, TrasladoSalida, Consumo</summary>
    public string MovementType { get; set; } = "Ingreso";

    /// <summary>Estado mostrado en histórico: Entrada planta, Entrada granja, Transferencia a granja, Transferencia a planta, Consumo.</summary>
    public string? Estado { get; set; }

    /// <summary>Origen del traslado (granja). Null si es Ingreso.</summary>
    public int? FromFarmId { get; set; }
    public string? FromNucleoId { get; set; }
    public string? FromGalponId { get; set; }

    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public Guid? TransferGroupId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }

    public Farm Farm { get; set; } = null!;
    public ItemInventarioEcuador ItemInventarioEcuador { get; set; } = null!;
    public Company Company { get; set; } = null!;
    public Pais Pais { get; set; } = null!;
}
