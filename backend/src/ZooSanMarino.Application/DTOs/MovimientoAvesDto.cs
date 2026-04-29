// src/ZooSanMarino.Application/DTOs/MovimientoAvesDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para mostrar información de movimientos de aves
/// </summary>
public record MovimientoAvesDto(
    int Id,
    string NumeroMovimiento,
    DateTime FechaMovimiento,
    string TipoMovimiento,
    
    // Origen
    UbicacionMovimientoDto? Origen,
    
    // Destino
    UbicacionMovimientoDto? Destino,
    
    // Cantidades
    int CantidadHembras,
    int CantidadMachos,
    int CantidadMixtas,
    int TotalAves,
    
    // Estado y información
    string Estado,
    string? MotivoMovimiento,
    string? Descripcion,
    string? PlantaDestino,
    string? Observaciones,
    
    // Usuario
    int UsuarioMovimientoId,
    string? UsuarioNombre,
    
    // Fechas
    DateTime? FechaProcesamiento,
    DateTime? FechaCancelacion,
    DateTime CreatedAt,
    
    // Campos específicos para despacho (Ecuador)
    int? EdadAves,
    string? Raza,
    string? Placa,
    TimeOnly? HoraSalida,
    string? GuiaAgrocalidad,
    string? Sellos,
    string? Ayuno,
    string? Conductor,
    int? TotalPollosGalpon,
    double? PesoBruto,
    double? PesoTara,
    double? PesoNeto,
    double? PromedioPesoAve
);

/// <summary>
/// DTO para ubicación en movimientos
/// </summary>
public record UbicacionMovimientoDto(
    int? LoteId,
    string? LoteNombre,
    int? GranjaId,
    string? GranjaNombre,
    string? NucleoId,
    string? NucleoNombre,
    string? GalponId,
    string? GalponNombre
);

/// <summary>
/// DTO para crear un nuevo movimiento de aves
/// </summary>
public sealed class CreateMovimientoAvesDto
{
    public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;
    public string TipoMovimiento { get; set; } = "Traslado"; // Traslado, Ajuste, Liquidacion
    
    // Origen
    public int? InventarioOrigenId { get; set; }
    public int? LoteOrigenId { get; set; }  // Changed from string? to int?
    public int? GranjaOrigenId { get; set; }
    public string? NucleoOrigenId { get; set; }
    public string? GalponOrigenId { get; set; }
    
    // Destino
    public int? InventarioDestinoId { get; set; }
    public int? LoteDestinoId { get; set; }  // Changed from string? to int?
    public int? GranjaDestinoId { get; set; }
    public string? NucleoDestinoId { get; set; }
    public string? GalponDestinoId { get; set; }
    public string? PlantaDestino { get; set; }  // Para traslados a plantas
    
    // Cantidades a mover
    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }
    
    // Información adicional
    public string? MotivoMovimiento { get; set; }
    public string? Descripcion { get; set; }  // Para ventas
    public string? Observaciones { get; set; }
    
    // Campos específicos para despacho (Ecuador)
    public int? EdadAves { get; set; } // Edad de las aves en días
    public string? Raza { get; set; } // Raza de las aves
    public string? Placa { get; set; } // Placa del vehículo
    public TimeOnly? HoraSalida { get; set; } // Hora de salida
    public string? GuiaAgrocalidad { get; set; } // Guía Agrocalidad
    public string? Sellos { get; set; } // Información de sellos
    public string? Ayuno { get; set; } // Información sobre ayuno (horas o indicador)
    public string? Conductor { get; set; } // Nombre del conductor
    public int? TotalPollosGalpon { get; set; } // Total de pollos por galpón
    public double? PesoBruto { get; set; } // Peso bruto en kg
    public double? PesoTara { get; set; } // Peso tara en kg
    
    // Se auto-completa con el usuario actual
    public int UsuarioMovimientoId { get; set; }
}

/// <summary>
/// DTO para actualizar un movimiento de aves
/// </summary>
public sealed class ActualizarMovimientoAvesDto
{
    public DateTime? FechaMovimiento { get; init; }
    public string? TipoMovimiento { get; init; }
    public int? LoteOrigenId { get; init; }
    public int? GranjaOrigenId { get; init; }
    public string? NucleoOrigenId { get; init; }
    public string? GalponOrigenId { get; init; }
    public int? LoteDestinoId { get; init; }
    public int? GranjaDestinoId { get; init; }
    public string? NucleoDestinoId { get; init; }
    public string? GalponDestinoId { get; init; }
    public string? PlantaDestino { get; init; }  // Para traslados a plantas
    public int? CantidadHembras { get; init; }
    public int? CantidadMachos { get; init; }
    public int? CantidadMixtas { get; init; }
    public string? MotivoMovimiento { get; init; }
    public string? Descripcion { get; init; }  // Para ventas
    public string? Observaciones { get; init; }
    
    // Campos específicos para despacho (Ecuador)
    public int? EdadAves { get; init; }
    public string? Raza { get; init; }
    public string? Placa { get; init; }
    public TimeOnly? HoraSalida { get; init; }
    public string? GuiaAgrocalidad { get; init; }
    public string? Sellos { get; init; }
    public string? Ayuno { get; init; }
    public string? Conductor { get; init; }
    public int? TotalPollosGalpon { get; init; }
    public double? PesoBruto { get; init; }
    public double? PesoTara { get; init; }
}

/// <summary>
/// DTO para procesar un movimiento
/// </summary>
public sealed class ProcesarMovimientoDto
{
    public int MovimientoId { get; set; }
    public string? ObservacionesProcesamiento { get; set; }
    public bool AutoCrearInventarioDestino { get; set; } = true;
}

/// <summary>
/// DTO para cancelar un movimiento
/// </summary>
public sealed class CancelarMovimientoDto
{
    public int MovimientoId { get; set; }
    public string MotivoCancelacion { get; set; } = null!;
}

/// <summary>
/// DTO para búsqueda de movimientos
/// </summary>
public sealed record MovimientoAvesSearchRequest(
    string? NumeroMovimiento = null,
    string? TipoMovimiento = null,
    string? Estado = null,
    int? LoteOrigenId = null,  // Changed from string? to int?
    int? LoteDestinoId = null,  // Changed from string? to int?
    int? GranjaOrigenId = null,
    int? GranjaDestinoId = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    int? UsuarioMovimientoId = null,
    string SortBy = "fecha_movimiento",
    bool SortDesc = true,
    int Page = 1,
    int PageSize = 20
);

/// <summary>
/// DTO para traslado rápido entre ubicaciones
/// </summary>
public sealed class TrasladoRapidoDto
{
    public int LoteId { get; set; }  // Changed from string to int
    
    // Origen (opcional si se detecta automáticamente)
    public int? GranjaOrigenId { get; set; }
    public string? NucleoOrigenId { get; set; }
    public string? GalponOrigenId { get; set; }
    
    // Destino (requerido)
    public int GranjaDestinoId { get; set; }
    public string? NucleoDestinoId { get; set; }
    public string? GalponDestinoId { get; set; }
    
    // Cantidades (opcional, por defecto todas las aves)
    public int? CantidadHembras { get; set; }
    public int? CantidadMachos { get; set; }
    public int? CantidadMixtas { get; set; }
    
    public string? MotivoTraslado { get; set; }
    public string? Observaciones { get; set; }
    public bool ProcesarInmediatamente { get; set; } = true;
}

/// <summary>
/// DTO para resultado de operaciones de movimiento
/// </summary>
public record ResultadoMovimientoDto(
    bool Success,
    string Message,
    int? MovimientoId,
    string? NumeroMovimiento,
    List<string> Errores,
    MovimientoAvesDto? Movimiento
);

/// <summary>Request para ejecutar una venta de aves desde el seguimiento diario.</summary>
public sealed class EjecutarVentaAvesRequest
{
    public int LoteOrigenId { get; set; }
    public long SeguimientoId { get; set; }
    public DateTime Fecha { get; set; }
    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public string? Motivo { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Request para ejecutar un traslado de aves entre lotes desde el seguimiento diario.</summary>
public sealed class EjecutarTrasladoAvesRequest
{
    public int LoteOrigenId { get; set; }
    public long SeguimientoOrigenId { get; set; }
    public int LoteDestinoId { get; set; }
    public DateTime Fecha { get; set; }
    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Request para ejecutar el traslado de aves al cerrar un lote levante hacia producción.</summary>
public sealed class TrasladoCierreLevanteRequest
{
    /// <summary>ID de LotePosturaLevante que se cierra.</summary>
    public int LotePosturaLevanteId { get; set; }
    /// <summary>ID de LotePosturaProduccion destino. Null si el traslado es a venta directa.</summary>
    public int? LotePosturaProduccionId { get; set; }
    public DateTime Fecha { get; set; }
    public int HembrasTraslado { get; set; }
    public int MachosTraslado { get; set; }
    /// <summary>ID de la liquidación de cierre ya guardada, para trazabilidad.</summary>
    public int? LiquidacionCierreId { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Granja simplificada para selector de destino.</summary>
public sealed record GranjaDestinoDto(int GranjaId, string GranjaNombre);

/// <summary>Núcleo simplificado para selector de destino.</summary>
public sealed record NucleoDestinoDto(string NucleoId, string NucleoNombre);

/// <summary>Galpón simplificado para selector de destino.</summary>
public sealed record GalponDestinoDto(string GalponId, string GalponNombre);

/// <summary>Lote simplificado para selector de destino.</summary>
public sealed record LoteDestinoDto(int LoteId, string LoteNombre, string? Fase);
