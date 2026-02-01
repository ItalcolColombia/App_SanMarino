// src/ZooSanMarino.Application/DTOs/Traslados/ActualizarTrasladoHuevosDto.cs
namespace ZooSanMarino.Application.DTOs.Traslados;

/// <summary>
/// DTO para actualizar un traslado de huevos existente
/// </summary>
public record ActualizarTrasladoHuevosDto
{
    public DateTime? FechaTraslado { get; init; }
    public string? TipoOperacion { get; init; } // "Venta" o "Traslado"
    
    // Cantidades por tipo de huevo
    public int? CantidadLimpio { get; init; }
    public int? CantidadTratado { get; init; }
    public int? CantidadSucio { get; init; }
    public int? CantidadDeforme { get; init; }
    public int? CantidadBlanco { get; init; }
    public int? CantidadDobleYema { get; init; }
    public int? CantidadPiso { get; init; }
    public int? CantidadPequeno { get; init; }
    public int? CantidadRoto { get; init; }
    public int? CantidadDesecho { get; init; }
    public int? CantidadOtro { get; init; }
    
    // Destino (si es traslado)
    public int? GranjaDestinoId { get; init; }
    public string? LoteDestinoId { get; init; }
    public string? TipoDestino { get; init; } // "Granja" o "Planta"
    
    // Motivo y descripción (especialmente para venta)
    public string? Motivo { get; init; }
    public string? Descripcion { get; init; }
    public string? Observaciones { get; init; }
}
