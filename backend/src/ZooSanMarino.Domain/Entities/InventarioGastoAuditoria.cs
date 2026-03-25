// src/ZooSanMarino.Domain/Entities/InventarioGastoAuditoria.cs
// Auditoría de acciones sobre gastos de inventario.

namespace ZooSanMarino.Domain.Entities;

public class InventarioGastoAuditoria
{
    public int Id { get; set; }
    public int InventarioGastoId { get; set; }

    /// <summary>Crear | Eliminar | Editar</summary>
    public string Accion { get; set; } = "Crear";
    public DateTimeOffset Fecha { get; set; }
    public string UserId { get; set; } = null!;

    /// <summary>Payload mínimo (JSON) para rastreo.</summary>
    public string? Detalle { get; set; }

    public InventarioGasto InventarioGasto { get; set; } = null!;
}

