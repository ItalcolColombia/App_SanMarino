// src/ZooSanMarino.Application/DTOs/HistorialInventarioDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para mostrar el historial de cambios en inventario
/// </summary>
public record HistorialInventarioDto(
    int Id,
    int InventarioId,
    int LoteId,
    string LoteNombre,
    DateTime FechaCambio,
    string TipoCambio,
    int? MovimientoId,
    string? NumeroMovimiento,
    
    // Cantidades antes
    int CantidadHembrasAnterior,
    int CantidadMachosAnterior,
    int CantidadMixtasAnterior,
    int TotalAnterior,
    
    // Cantidades después
    int CantidadHembrasNueva,
    int CantidadMachosNueva,
    int CantidadMixtasNueva,
    int TotalNuevo,
    
    // Diferencias
    int DiferenciaHembras,
    int DiferenciaMachos,
    int DiferenciaMixtas,
    int DiferenciaTotal,
    
    // Ubicación
    UbicacionMovimientoDto Ubicacion,
    
    // Usuario y observaciones
    int UsuarioCambioId,
    string? UsuarioNombre,
    string? Motivo,
    string? Observaciones
);

/// <summary>
/// DTO para búsqueda de historial
/// </summary>
public sealed record HistorialInventarioSearchRequest(
    int? InventarioId = null,
    int? LoteId = null,
    string? TipoCambio = null,
    int? MovimientoId = null,
    int? GranjaId = null,
    string? NucleoId = null,
    string? GalponId = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    int? UsuarioCambioId = null,
    string SortBy = "fecha_cambio",
    bool SortDesc = true,
    int Page = 1,
    int PageSize = 20
);

/// <summary>
/// DTO para resumen de cambios por período
/// </summary>
public record ResumenCambiosDto(
    DateTime FechaInicio,
    DateTime FechaFin,
    int TotalCambios,
    int TotalEntradas,
    int TotalSalidas,
    int TotalAjustes,
    int AvesEntradasTotal,
    int AvesSalidasTotal,
    int DiferenciaNeta,
    List<ResumenCambioPorTipoDto> CambiosPorTipo
);

/// <summary>
/// DTO para resumen por tipo de cambio
/// </summary>
public record ResumenCambioPorTipoDto(
    string TipoCambio,
    int Cantidad,
    int AvesTotales,
    decimal Porcentaje
);

/// <summary>
/// DTO para trazabilidad completa de un lote
/// </summary>
public record TrazabilidadLoteDto(
    int LoteId,  // Changed from string to int
    string LoteNombre,
    DateTime FechaInicio,
    DateTime? FechaFin,
    List<EventoTrazabilidadDto> Eventos,
    EstadoActualTrazabilidadDto EstadoActual
);

/// <summary>
/// DTO para eventos en la trazabilidad
/// </summary>
public record EventoTrazabilidadDto(
    DateTime Fecha,
    string TipoEvento,
    string Descripcion,
    UbicacionMovimientoDto? UbicacionAnterior,
    UbicacionMovimientoDto? UbicacionNueva,
    int? AvesAntes,
    int? AvesDespues,
    int? Diferencia,
    string? Usuario,
    string? Observaciones
);

/// <summary>
/// DTO para estado actual en trazabilidad
/// </summary>
public record EstadoActualTrazabilidadDto(
    List<UbicacionLoteDto> Ubicaciones,
    int TotalAves,
    DateTime UltimaActualizacion,
    string Estado
);
