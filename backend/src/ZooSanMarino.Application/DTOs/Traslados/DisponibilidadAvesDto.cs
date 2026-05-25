namespace ZooSanMarino.Application.DTOs.Traslados;

/// <summary>Stock actual de aves en el lote origen para validar el traslado.</summary>
public record DisponibilidadAvesDto(
    int LoteId,
    string LoteNombre,
    string TipoLote,          // "Levante" | "Produccion"
    int AvesHActual,
    int AvesMActual,
    int? GranjaId,
    string? GranjaNombre,
    string? GalponId,
    string? GalponNombre
);
