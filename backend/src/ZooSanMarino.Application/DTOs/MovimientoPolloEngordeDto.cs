// src/ZooSanMarino.Application/DTOs/MovimientoPolloEngordeDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO de lectura para movimiento de pollo engorde
/// </summary>
public record MovimientoPolloEngordeDto(
    int Id,
    string NumeroMovimiento,
    DateTime FechaMovimiento,
    string TipoMovimiento,
    string? TipoLoteOrigen,
    int? LoteOrigenId,
    string? LoteOrigenNombre,
    string? TipoLoteDestino,
    int? LoteDestinoId,
    string? LoteDestinoNombre,
    int? GranjaOrigenId,
    string? GranjaOrigenNombre,
    int? GranjaDestinoId,
    string? GranjaDestinoNombre,
    int CantidadHembras,
    int CantidadMachos,
    int CantidadMixtas,
    int TotalAves,
    string Estado,
    string? MotivoMovimiento,
    string? Observaciones,
    int UsuarioMovimientoId,
    string? UsuarioNombre,
    DateTime? FechaProcesamiento,
    DateTime? FechaCancelacion,
    DateTime CreatedAt,
    // Despacho / salida
    string? NumeroDespacho = null,
    int? EdadAves = null,
    int? TotalPollosGalpon = null,
    string? Raza = null,
    string? Placa = null,
    TimeOnly? HoraSalida = null,
    string? GuiaAgrocalidad = null,
    string? Sellos = null,
    string? Ayuno = null,
    string? Conductor = null,
    double? PesoBruto = null,
    double? PesoTara = null,
    double? PesoNeto = null,
    double? PromedioPesoAve = null
);

/// <summary>
/// DTO para crear movimiento de pollo engorde
/// </summary>
public sealed class CreateMovimientoPolloEngordeDto
{
    public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;
    public string TipoMovimiento { get; set; } = "Traslado";

    public int? LoteAveEngordeOrigenId { get; set; }
    public int? LoteReproductoraAveEngordeOrigenId { get; set; }
    public int? GranjaOrigenId { get; set; }
    public string? NucleoOrigenId { get; set; }
    public string? GalponOrigenId { get; set; }

    public int? LoteAveEngordeDestinoId { get; set; }
    public int? LoteReproductoraAveEngordeDestinoId { get; set; }
    public int? GranjaDestinoId { get; set; }
    public string? NucleoDestinoId { get; set; }
    public string? GalponDestinoId { get; set; }
    public string? PlantaDestino { get; set; }

    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }
    public string? MotivoMovimiento { get; set; }
    public string? Descripcion { get; set; }
    public string? Observaciones { get; set; }
    public int UsuarioMovimientoId { get; set; }
    // Despacho
    public string? NumeroDespacho { get; set; }
    public int? EdadAves { get; set; }
    public int? TotalPollosGalpon { get; set; }
    public string? Raza { get; set; }
    public string? Placa { get; set; }
    public TimeOnly? HoraSalida { get; set; }
    public string? GuiaAgrocalidad { get; set; }
    public string? Sellos { get; set; }
    public string? Ayuno { get; set; }
    public string? Conductor { get; set; }
    public double? PesoBruto { get; set; }
    public double? PesoTara { get; set; }
}

/// <summary>
/// DTO para actualizar movimiento de pollo engorde
/// </summary>
public sealed class UpdateMovimientoPolloEngordeDto
{
    public DateTime? FechaMovimiento { get; init; }
    public string? TipoMovimiento { get; init; }
    public int? GranjaOrigenId { get; init; }
    public string? NucleoOrigenId { get; init; }
    public string? GalponOrigenId { get; init; }
    public int? GranjaDestinoId { get; init; }
    public string? NucleoDestinoId { get; init; }
    public string? GalponDestinoId { get; init; }
    public string? PlantaDestino { get; init; }
    public int? CantidadHembras { get; init; }
    public int? CantidadMachos { get; init; }
    public int? CantidadMixtas { get; init; }
    public string? MotivoMovimiento { get; init; }
    public string? Observaciones { get; init; }
    public string? NumeroDespacho { get; init; }
    public int? EdadAves { get; init; }
    public int? TotalPollosGalpon { get; init; }
    public string? Raza { get; init; }
    public string? Placa { get; init; }
    public TimeOnly? HoraSalida { get; init; }
    public string? GuiaAgrocalidad { get; init; }
    public string? Sellos { get; init; }
    public string? Ayuno { get; init; }
    public string? Conductor { get; init; }
    public double? PesoBruto { get; init; }
    public double? PesoTara { get; init; }
}

/// <summary>
/// Búsqueda de movimientos de pollo engorde
/// </summary>
public sealed record MovimientoPolloEngordeSearchRequest(
    string? NumeroMovimiento = null,
    string? TipoMovimiento = null,
    string? Estado = null,
    int? LoteAveEngordeOrigenId = null,
    int? LoteReproductoraAveEngordeOrigenId = null,
    /// <summary>Filtro por granja de origen (incluye movimientos vinculados por lote aunque GranjaOrigenId venga null en registros antiguos).</summary>
    int? GranjaOrigenId = null,
    string? NucleoOrigenId = null,
    string? GalponOrigenId = null,
    /// <summary>Solo movimientos cuyo origen no tiene galpón asignado (movimiento o lote).</summary>
    bool? GalponOrigenSinAsignar = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    string SortBy = "FechaMovimiento",
    bool SortDesc = true,
    int Page = 1,
    int PageSize = 20
);

/// <summary>
/// Resumen de aves por lote para reportes: cuántas iniciaron, cuántas salieron (ventas/traslados completados), cuántas hay actuales.
/// </summary>
public record ResumenAvesLoteDto(
    string TipoLote,
    int LoteId,
    string? NombreLote,
    int AvesInicioHembras,
    int AvesInicioMachos,
    int AvesInicioMixtas,
    int AvesInicioTotal,
    int AvesSalidasTotal,
    int AvesVendidasTotal,
    int AvesActualesHembras,
    int AvesActualesMachos,
    int AvesActualesMixtas,
    int AvesActualesTotal
);
