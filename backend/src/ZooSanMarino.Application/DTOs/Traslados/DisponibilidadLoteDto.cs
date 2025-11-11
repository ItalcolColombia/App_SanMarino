// src/ZooSanMarino.Application/DTOs/Traslados/DisponibilidadLoteDto.cs
namespace ZooSanMarino.Application.DTOs.Traslados;

/// <summary>
/// Información de disponibilidad de un lote (aves o huevos según el tipo)
/// </summary>
public record DisponibilidadLoteDto
{
    public int LoteId { get; init; }
    public string LoteNombre { get; init; } = string.Empty;
    public string TipoLote { get; init; } = string.Empty; // "Levante" o "Produccion"
    
    // Información de aves (si es levante)
    public AvesDisponiblesDto? Aves { get; init; }
    
    // Información de huevos (si es producción)
    public HuevosDisponiblesDto? Huevos { get; init; }
    
    // Información del lote
    public int GranjaId { get; init; }
    public string GranjaNombre { get; init; } = string.Empty;
    public string? NucleoId { get; init; }
    public string? NucleoNombre { get; init; }
    public string? GalponId { get; init; }
    public string? GalponNombre { get; init; }
}

/// <summary>
/// Disponibilidad de aves en un lote de levante
/// </summary>
public record AvesDisponiblesDto
{
    public int HembrasVivas { get; init; }
    public int MachosVivos { get; init; }
    public int TotalAves { get; init; }
    
    // Información adicional
    public int HembrasIniciales { get; init; }
    public int MachosIniciales { get; init; }
    public int MortalidadAcumuladaHembras { get; init; }
    public int MortalidadAcumuladaMachos { get; init; }
    public int RetirosAcumuladosHembras { get; init; }
    public int RetirosAcumuladosMachos { get; init; }
}

/// <summary>
/// Disponibilidad de huevos en un lote de producción
/// </summary>
public record HuevosDisponiblesDto
{
    public int TotalHuevos { get; init; }
    public int TotalHuevosIncubables { get; init; }
    
    // Desglose por tipo de huevo
    public int Limpio { get; init; }
    public int Tratado { get; init; }
    public int Sucio { get; init; }
    public int Deforme { get; init; }
    public int Blanco { get; init; }
    public int DobleYema { get; init; }
    public int Piso { get; init; }
    public int Pequeno { get; init; }
    public int Roto { get; init; }
    public int Desecho { get; init; }
    public int Otro { get; init; }
    
    // Información adicional
    public DateTime? FechaUltimoRegistro { get; init; }
    public int DiasEnProduccion { get; init; }
}


