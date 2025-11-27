// src/ZooSanMarino.Application/DTOs/Traslados/CrearTrasladoHuevosDto.cs
namespace ZooSanMarino.Application.DTOs.Traslados;

/// <summary>
/// DTO para crear un traslado de huevos
/// </summary>
public record CrearTrasladoHuevosDto
{
    public string LoteId { get; init; } = string.Empty;
    public DateTime FechaTraslado { get; init; }
    public string TipoOperacion { get; init; } = string.Empty; // "Venta" o "Traslado"
    
    // Cantidades por tipo de huevo
    public int CantidadLimpio { get; init; }
    public int CantidadTratado { get; init; }
    public int CantidadSucio { get; init; }
    public int CantidadDeforme { get; init; }
    public int CantidadBlanco { get; init; }
    public int CantidadDobleYema { get; init; }
    public int CantidadPiso { get; init; }
    public int CantidadPequeno { get; init; }
    public int CantidadRoto { get; init; }
    public int CantidadDesecho { get; init; }
    public int CantidadOtro { get; init; }
    
    // Destino (si es traslado)
    public int? GranjaDestinoId { get; init; }
    public string? LoteDestinoId { get; init; }
    public string? TipoDestino { get; init; } // "Granja" o "Planta"
    
    // Motivo y descripción (especialmente para venta)
    public string? Motivo { get; init; }
    public string? Descripcion { get; init; }
    public string? Observaciones { get; init; }
    
    // Total calculado
    public int TotalHuevos => CantidadLimpio + CantidadTratado + CantidadSucio + 
                              CantidadDeforme + CantidadBlanco + CantidadDobleYema + 
                              CantidadPiso + CantidadPequeno + CantidadRoto + 
                              CantidadDesecho + CantidadOtro;
}





