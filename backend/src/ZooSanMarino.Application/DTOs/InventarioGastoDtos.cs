// src/ZooSanMarino.Application/DTOs/InventarioGastoDtos.cs
using ZooSanMarino.Application.DTOs.Shared;

namespace ZooSanMarino.Application.DTOs;

public sealed record InventarioGastoLineaRequest(
    int ItemInventarioEcuadorId,
    decimal Cantidad
);

public sealed record CreateInventarioGastoRequest(
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int? LoteAveEngordeId,
    DateTime Fecha,
    string? Observaciones,
    string Concepto,
    IReadOnlyList<InventarioGastoLineaRequest> Lineas
);

public sealed record InventarioGastoDetalleDto(
    int Id,
    int ItemInventarioEcuadorId,
    string ItemCodigo,
    string ItemNombre,
    string ItemType,
    string? Concepto,
    decimal Cantidad,
    string Unidad,
    decimal? StockAntes,
    decimal? StockDespues
);

public sealed record InventarioGastoDto(
    int Id,
    DateTime Fecha,
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int? LoteAveEngordeId,
    string? LoteNombre,
    string? Observaciones,
    string Estado,
    DateTimeOffset CreatedAt,
    string? CreatedByUserId,
    DateTimeOffset? DeletedAt,
    string? DeletedByUserId,
    IReadOnlyList<InventarioGastoDetalleDto> Detalles
);

public sealed record InventarioGastoListItemDto(
    int Id,
    DateTime Fecha,
    int FarmId,
    string? GranjaNombre,
    string? NucleoId,
    string? NucleoNombre,
    string? GalponId,
    string? GalponNombre,
    int? LoteAveEngordeId,
    string? LoteNombre,
    string? Observaciones,
    string Estado,
    int Lineas,
    decimal TotalCantidad,
    string? Unidad,
    DateTimeOffset CreatedAt,
    string? CreatedByUserId
);

public sealed record InventarioGastoSearchRequest(
    int? FarmId = null,
    string? NucleoId = null,
    string? GalponId = null,
    int? LoteAveEngordeId = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    string? Concepto = null,
    string? Search = null,
    string? Estado = null
);

public sealed record InventarioGastoItemStockDto(
    int ItemInventarioEcuadorId,
    string Codigo,
    string Nombre,
    string TipoItem,
    string Unidad,
    string? Concepto,
    decimal StockCantidad
);

/// <summary>Una fila por línea de detalle (export Excel/CSV).</summary>
public sealed record InventarioGastoExportRowDto(
    int InventarioGastoId,
    DateTime Fecha,
    string Estado,
    string? ObservacionesCabecera,
    int FarmId,
    string GranjaNombre,
    string? NucleoId,
    string? NucleoNombre,
    string? GalponId,
    string? GalponNombre,
    int? LoteAveEngordeId,
    string? LoteNombre,
    int DetalleId,
    int ItemInventarioEcuadorId,
    string ItemCodigo,
    string ItemNombre,
    string ItemTipo,
    string? ConceptoLinea,
    decimal Cantidad,
    string Unidad,
    decimal? StockAntes,
    decimal? StockDespues,
    DateTimeOffset CreatedAt,
    string? CreatedByUserId,
    DateTimeOffset? DeletedAt,
    string? DeletedByUserId
);

