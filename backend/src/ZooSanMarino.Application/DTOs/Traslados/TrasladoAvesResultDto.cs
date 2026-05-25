namespace ZooSanMarino.Application.DTOs.Traslados;

/// <summary>Resultado luego de ejecutar el traslado de aves desde seguimiento diario.</summary>
public record TrasladoAvesResultDto(
    bool Exitoso,
    string Mensaje,
    int? MovimientoAvesId,
    int AvesHActualOrigen,
    int AvesMActualOrigen
);
