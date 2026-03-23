// src/ZooSanMarino.Application/DTOs/InventarioGestionDtos.cs
using ZooSanMarino.Application.DTOs.Shared;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Datos para filtros del módulo Gestión de Inventario (Granja → Núcleo → Galpón).
/// <list type="bullet">
/// <item><term>FarmsOrigen</term> Granjas asignadas al usuario (user_farms) dentro de la empresa activa.</item>
/// <item><term>FarmsDestino</term> Todas las granjas de la empresa (p. ej. destino de traslado inter-granja o granja de procedencia en ingreso).</item>
/// </list>
/// </summary>
public sealed record InventarioGestionFilterDataDto(
    IEnumerable<FarmDto> FarmsOrigen,
    IEnumerable<FarmDto> FarmsDestino,
    IEnumerable<NucleoDto> NucleosOrigen,
    IEnumerable<NucleoDto> NucleosDestino,
    IEnumerable<GalponLiteDto> GalponesOrigen,
    IEnumerable<GalponLiteDto> GalponesDestino
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
    /// <summary>Origen para estado en histórico: planta | granja | bodega.</summary>
    string? OrigenTipo = null,
    /// <summary>Si OrigenTipo es "granja", granja de procedencia (debe ser distinta a FarmId).</summary>
    int? OrigenFarmId = null,
    /// <summary>Si OrigenTipo es "bodega", texto opcional (nombre/referencia de la bodega de procedencia).</summary>
    string? OrigenBodegaDescripcion = null
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
    string? GalponNombre,
    /// <summary>Agrupa salida/entrada de un mismo traslado (incl. inter-granja en tránsito).</summary>
    Guid? TransferGroupId = null,
    /// <summary>Nombre granja en el otro extremo (origen de traslado hacia esta fila, o destino según tipo).</summary>
    string? FromGranjaNombre = null,
    string? FromNucleoNombre = null,
    string? FromGalponNombre = null,
    /// <summary>Etiqueta de operación para reportes (ingreso, consumo, traslado, etc.).</summary>
    string? TipoOperacion = null
);

/// <summary>Recepción en granja destino de un traslado inter-granja que quedó en tránsito.</summary>
public sealed record InventarioGestionRecepcionTransitoRequest(
    Guid TransferGroupId,
    int ToFarmId,
    string? ToNucleoId,
    string? ToGalponId
);

/// <summary>Rechazo en destino de una solicitud inter-granja pendiente (no descuenta origen).</summary>
public sealed record InventarioGestionRechazoTransitoRequest(
    Guid TransferGroupId,
    string? Reason
);

/// <summary>Salida inter-granja pendiente de recepción (inventario en tránsito hacia ToFarmId).</summary>
public sealed record InventarioGestionTransitoPendienteDto(
    Guid TransferGroupId,
    int SalidaMovimientoId,
    int FromFarmId,
    string? FromGranjaNombre,
    int ToFarmId,
    string? ToGranjaNombre,
    string? FromNucleoId,
    string? FromGalponId,
    string? DestinoNucleoIdHint,
    string? DestinoGalponIdHint,
    int ItemInventarioEcuadorId,
    string ItemCodigo,
    string ItemNombre,
    decimal Quantity,
    string Unit,
    DateTimeOffset CreatedAt,
    /// <summary>True: la solicitud aún no descontó stock en origen; al confirmar recepción se descuenta. False: registro previo (stock ya descontado al enviar).</summary>
    bool PendienteDespachoOrigen = true
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
