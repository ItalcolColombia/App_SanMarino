// src/ZooSanMarino.Domain/Entities/InventarioGastoDetalle.cs
// Líneas de un gasto de inventario (ítem + cantidad).

namespace ZooSanMarino.Domain.Entities;

public class InventarioGastoDetalle
{
    public int Id { get; set; }
    public int InventarioGastoId { get; set; }
    public int ItemInventarioEcuadorId { get; set; }

    /// <summary>Snapshot del concepto del ítem en el momento del gasto.</summary>
    public string? Concepto { get; set; }
    public decimal Cantidad { get; set; }
    /// <summary>Snapshot de la unidad (por defecto del ítem).</summary>
    public string Unidad { get; set; } = "kg";

    public decimal? StockAntes { get; set; }
    public decimal? StockDespues { get; set; }

    public InventarioGasto InventarioGasto { get; set; } = null!;
    public ItemInventarioEcuador ItemInventarioEcuador { get; set; } = null!;
}

