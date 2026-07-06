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

/// <summary>Resumen de una línea/ítem consumido, para mostrarlo inline en la tabla (sin abrir el detalle).</summary>
public sealed record InventarioGastoLineaResumenDto(
    string Codigo,
    string Nombre,
    decimal Cantidad,
    string Unidad
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
    string? CreatedByUserId,
    IReadOnlyList<InventarioGastoLineaResumenDto> Items
);

/// <summary>
/// Fila cruda devuelta por <c>fn_inventario_gastos_search</c> (SqlQueryRaw). Props en PascalCase
/// que mapean 1:1 a las columnas citadas de la función. Se proyecta a <see cref="InventarioGastoListItemDto"/>.
/// </summary>
public sealed class InventarioGastoListRow
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int FarmId { get; set; }
    public string? GranjaNombre { get; set; }
    public string? NucleoId { get; set; }
    public string? NucleoNombre { get; set; }
    public string? GalponId { get; set; }
    public string? GalponNombre { get; set; }
    public int? LoteAveEngordeId { get; set; }
    public string? LoteNombre { get; set; }
    public string? Observaciones { get; set; }
    public string Estado { get; set; } = null!;
    public int Lineas { get; set; }
    public decimal TotalCantidad { get; set; }
    public string? Unidad { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    /// <summary>JSON (text) con las líneas/ítems del gasto; se deserializa a <see cref="InventarioGastoLineaResumenDto"/>.</summary>
    public string? Items { get; set; }
}

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

