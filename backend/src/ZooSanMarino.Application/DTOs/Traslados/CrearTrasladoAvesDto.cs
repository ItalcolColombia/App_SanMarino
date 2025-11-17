// src/ZooSanMarino.Application/DTOs/Traslados/CrearTrasladoAvesDto.cs
namespace ZooSanMarino.Application.DTOs.Traslados;

/// <summary>
/// DTO para crear un traslado de aves
/// </summary>
public record CrearTrasladoAvesDto
{
    public string LoteId { get; init; } = string.Empty;
    public DateTime FechaTraslado { get; init; }
    public string TipoOperacion { get; init; } = string.Empty; // "Venta" o "Traslado"
    
    // Cantidades
    public int CantidadHembras { get; init; }
    public int CantidadMachos { get; init; }
    
    // Destino (si es traslado)
    public int? GranjaDestinoId { get; init; }
    public string? LoteDestinoId { get; init; }
    public string? TipoDestino { get; init; } // "Granja" o "Planta"
    
    // Motivo y descripción (especialmente para venta)
    public string? Motivo { get; init; }
    public string? Descripcion { get; init; }
    public string? Observaciones { get; init; }
}




