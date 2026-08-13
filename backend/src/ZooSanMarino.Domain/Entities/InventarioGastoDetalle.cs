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

    /// <summary>
    /// Silo o bodega del que salió la línea (<c>farm_silos.id</c>), en las empresas que ubican el
    /// inventario por silo. <c>null</c> = comportamiento previo (stock a nivel granja).
    /// <para>
    /// Se guarda porque la <b>anulación devuelve al mismo silo</b>: sin el dato, eliminar un gasto
    /// repondría el insumo en una ubicación que nadie descontó y el saldo del silo quedaría corto
    /// para siempre.
    /// </para>
    /// </summary>
    public int? SiloId { get; set; }

    public decimal? StockAntes { get; set; }
    public decimal? StockDespues { get; set; }

    public InventarioGasto InventarioGasto { get; set; } = null!;
    public ItemInventario ItemInventario { get; set; } = null!;
}

