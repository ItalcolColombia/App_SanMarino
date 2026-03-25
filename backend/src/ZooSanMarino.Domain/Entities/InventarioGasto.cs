// src/ZooSanMarino.Domain/Entities/InventarioGasto.cs
// Cabecera de gasto/consumo de inventario (Ecuador). El stock se descuenta a nivel granja.

namespace ZooSanMarino.Domain.Entities;

public class InventarioGasto
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public int PaisId { get; set; }

    public int FarmId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public int? LoteAveEngordeId { get; set; }

    public DateTime Fecha { get; set; }
    public string? Observaciones { get; set; }

    /// <summary>Activo | Eliminado</summary>
    public string Estado { get; set; } = "Activo";

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public Company Company { get; set; } = null!;
    public Pais Pais { get; set; } = null!;
    public Farm Farm { get; set; } = null!;
    public LoteAveEngorde? LoteAveEngorde { get; set; }

    public List<InventarioGastoDetalle> Detalles { get; set; } = new();
}

