// src/ZooSanMarino.Application/DTOs/InventarioAvesDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para mostrar información del inventario de aves
/// </summary>
public record InventarioAvesDto(
    int Id,
    int LoteId,
    string LoteNombre,
    int GranjaId,
    string GranjaNombre,
    string? NucleoId,
    string? NucleoNombre,
    string? GalponId,
    string? GalponNombre,
    int CantidadHembras,
    int CantidadMachos,
    int CantidadMixtas,
    int TotalAves,
    DateTime FechaActualizacion,
    string Estado,
    string? Observaciones,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// DTO para crear un nuevo inventario de aves
/// </summary>
public sealed class CreateInventarioAvesDto
{
    public int LoteId { get; set; }
    public int GranjaId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// DTO para actualizar un inventario existente
/// </summary>
public sealed class UpdateInventarioAvesDto
{
    public int Id { get; set; }
    public int? GranjaId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public int? CantidadHembras { get; set; }
    public int? CantidadMachos { get; set; }
    public int? CantidadMixtas { get; set; }
    public string? Estado { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// DTO para búsqueda y filtrado de inventarios
/// </summary>
public sealed record InventarioAvesSearchRequest(
    int? LoteId = null,
    int? GranjaId = null,
    string? NucleoId = null,
    string? GalponId = null,
    string? Estado = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    bool SoloActivos = true,
    string SortBy = "lote_id",
    bool SortDesc = false,
    int Page = 1,
    int PageSize = 20
);

/// <summary>
/// DTO para resumen de inventario por ubicación
/// </summary>
public record ResumenInventarioDto(
    int GranjaId,
    string GranjaNombre,
    string? NucleoId,
    string? NucleoNombre,
    string? GalponId,
    string? GalponNombre,
    int TotalLotes,
    int TotalHembras,
    int TotalMachos,
    int TotalMixtas,
    int TotalAves,
    DateTime UltimaActualizacion
);

/// <summary>
/// DTO para el estado actual de un lote
/// </summary>
public record EstadoLoteDto(
    int LoteId,
    string LoteNombre,
    List<UbicacionLoteDto> Ubicaciones,
    int TotalHembras,
    int TotalMachos,
    int TotalMixtas,
    int TotalAves,
    DateTime UltimaActualizacion
);

/// <summary>
/// DTO para ubicaciones de un lote
/// </summary>
public record UbicacionLoteDto(
    int InventarioId,
    int GranjaId,
    string GranjaNombre,
    string? NucleoId,
    string? NucleoNombre,
    string? GalponId,
    string? GalponNombre,
    int CantidadHembras,
    int CantidadMachos,
    int CantidadMixtas,
    int TotalAves,
    string Estado
);
