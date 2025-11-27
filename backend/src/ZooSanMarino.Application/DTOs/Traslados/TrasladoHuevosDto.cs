// src/ZooSanMarino.Application/DTOs/Traslados/TrasladoHuevosDto.cs
namespace ZooSanMarino.Application.DTOs.Traslados;

/// <summary>
/// DTO para representar un traslado de huevos
/// </summary>
public record TrasladoHuevosDto
{
    public int Id { get; init; }
    public string NumeroTraslado { get; init; } = string.Empty;
    public DateTime FechaTraslado { get; init; }
    public string TipoOperacion { get; init; } = string.Empty;
    
    public string LoteId { get; init; } = string.Empty;
    public string LoteNombre { get; init; } = string.Empty;
    public int GranjaOrigenId { get; init; }
    public string GranjaOrigenNombre { get; init; } = string.Empty;
    
    public int? GranjaDestinoId { get; init; }
    public string? GranjaDestinoNombre { get; init; }
    public string? LoteDestinoId { get; init; }
    public string? TipoDestino { get; init; }
    
    public string? Motivo { get; init; }
    public string? Descripcion { get; init; }
    
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
    public int TotalHuevos { get; init; }
    
    public string Estado { get; init; } = string.Empty;
    public int UsuarioTrasladoId { get; init; }
    public string? UsuarioNombre { get; init; }
    public DateTime? FechaProcesamiento { get; init; }
    public DateTime? FechaCancelacion { get; init; }
    public string? Observaciones { get; init; }
    
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}





