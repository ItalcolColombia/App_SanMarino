// src/ZooSanMarino.Application/DTOs/SeguimientoDiarioFilterRequest.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Parámetros de filtrado y paginación para listado de seguimiento diario.
/// </summary>
public record SeguimientoDiarioFilterRequest
{
    /// <summary>Filtrar por tipo: levante, produccion, reproductora. Null = todos.</summary>
    public string? TipoSeguimiento { get; init; }

    /// <summary>Filtrar por lote_id (string).</summary>
    public string? LoteId { get; init; }

    /// <summary>Filtrar por reproductora_id (solo aplica si tipo = reproductora).</summary>
    public string? ReproductoraId { get; init; }

    /// <summary>Fecha desde (inclusive).</summary>
    public DateTime? FechaDesde { get; init; }

    /// <summary>Fecha hasta (inclusive).</summary>
    public DateTime? FechaHasta { get; init; }

    /// <summary>Número de página (1-based).</summary>
    public int Page { get; init; } = 1;

    /// <summary>Tamaño de página.</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>Ordenar por: Fecha, LoteId, TipoSeguimiento. Default: Fecha.</summary>
    public string OrderBy { get; init; } = "Fecha";

    /// <summary>true = ascendente, false = descendente.</summary>
    public bool OrderAsc { get; init; } = false;
}
