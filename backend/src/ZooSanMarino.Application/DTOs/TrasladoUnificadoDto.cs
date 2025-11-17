// src/ZooSanMarino.Application/DTOs/TrasladoUnificadoDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO unificado para representar tanto traslados de aves como de huevos
/// </summary>
public record TrasladoUnificadoDto(
    // Identificación
    int Id,
    string NumeroTraslado,
    DateTime FechaTraslado,
    string TipoOperacion, // "Venta", "Traslado", etc.
    string TipoTraslado, // "Aves" o "Huevos"
    
    // Lote origen
    string LoteIdOrigen, // String para compatibilidad
    int? LoteIdOrigenInt, // Int para compatibilidad con MovimientoAves
    int GranjaOrigenId,
    string? GranjaOrigenNombre,
    
    // Lote destino (opcional)
    string? LoteIdDestino,
    int? LoteIdDestinoInt,
    int? GranjaDestinoId,
    string? GranjaDestinoNombre,
    string? TipoDestino, // "Granja", "Planta", null
    
    // Cantidades (para aves)
    int? CantidadHembras,
    int? CantidadMachos,
    int? TotalAves,
    
    // Cantidades (para huevos)
    int? TotalHuevos,
    int? CantidadLimpio,
    int? CantidadTratado,
    int? CantidadSucio,
    int? CantidadDeforme,
    int? CantidadBlanco,
    int? CantidadDobleYema,
    int? CantidadPiso,
    int? CantidadPequeno,
    int? CantidadRoto,
    int? CantidadDesecho,
    int? CantidadOtro,
    
    // Estado e información
    string Estado,
    string? Motivo,
    string? Descripcion,
    string? Observaciones,
    
    // Usuario responsable
    int UsuarioTrasladoId,
    string? UsuarioNombre,
    
    // Fechas de control
    DateTime? FechaProcesamiento,
    DateTime? FechaCancelacion,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    
    // Información del lote (fase)
    string? FaseLote, // "Levante" o "Produccion"
    bool TieneSeguimientoProduccion
);




