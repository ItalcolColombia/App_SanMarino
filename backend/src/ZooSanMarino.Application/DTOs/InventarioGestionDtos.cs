// src/ZooSanMarino.Application/DTOs/InventarioGestionDtos.cs
using ZooSanMarino.Application.DTOs.Shared;

namespace ZooSanMarino.Application.DTOs;

/// <summary>Datos para filtros del módulo Gestión de Inventario (Granja → Núcleo → Galpón).</summary>
public sealed record InventarioGestionFilterDataDto(
    IEnumerable<FarmDto> Farms,
    IEnumerable<NucleoDto> Nucleos,
    IEnumerable<GalponLiteDto> Galpones
);

/// <summary>Stock de un ítem en una ubicación (granja o granja+núcleo+galpón). Ítem desde item_inventario_ecuador.</summary>
public sealed record InventarioGestionStockDto(
    int Id,
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int ItemInventarioEcuadorId,
    string ItemCodigo,
    string ItemNombre,
    string ItemType,
    decimal Quantity,
    string Unit,
    string? GranjaNombre = null,
    string? NucleoNombre = null,
    string? GalponNombre = null
);

/// <summary>Request para registrar un ingreso. ItemInventarioEcuadorId referencia a config/item-inventario-ecuador.</summary>
public sealed record InventarioGestionIngresoRequest(
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int ItemInventarioEcuadorId,
    decimal Quantity,
    string Unit,
    string? Reference,
    string? Reason,
    /// <summary>Origen para estado en histórico: "planta" → Entrada planta, "granja" → Entrada granja.</summary>
    string? OrigenTipo = null
);

/// <summary>Request para registrar un traslado.</summary>
public sealed record InventarioGestionTrasladoRequest(
    int FromFarmId,
    string? FromNucleoId,
    string? FromGalponId,
    int ToFarmId,
    string? ToNucleoId,
    string? ToGalponId,
    int ItemInventarioEcuadorId,
    decimal Quantity,
    string Unit,
    string? Reference,
    string? Reason,
    /// <summary>Destino para estado en histórico: "granja" → Transferencia a granja, "planta" → Transferencia a planta.</summary>
    string? DestinoTipo = null
);

/// <summary>Registro del histórico de movimientos (entradas, salidas, traslados).</summary>
public sealed record InventarioGestionMovimientoDto(
    int Id,
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int ItemInventarioEcuadorId,
    string ItemCodigo,
    string ItemNombre,
    string ItemType,
    decimal Quantity,
    string Unit,
    string MovementType,
    string? Estado,
    int? FromFarmId,
    string? FromNucleoId,
    string? FromGalponId,
    string? Reference,
    string? Reason,
    DateTimeOffset CreatedAt,
    string? GranjaNombre,
    string? NucleoNombre,
    string? GalponNombre
);

/// <summary>Request para registrar consumo (reduce stock). Usado desde Seguimiento Diario.</summary>
public sealed record InventarioGestionConsumoRequest(
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int ItemInventarioEcuadorId,
    decimal Quantity,
    string Unit,
    string? Reference,
    string? Reason
);
