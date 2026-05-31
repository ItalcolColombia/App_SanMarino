namespace ZooSanMarino.Application.DTOs.Lesiones;

public sealed record UpdateLesionRequest(
    int?    ClienteId,
    int     FarmId,
    string? GalponId,
    int?    LoteId,
    string? LoteReproductoraId,
    int?    EdadDias,
    int?    AvesMacho,
    int?    AvesHembra,
    int?    AvesMixtas,
    string  TipoLesion,
    string? Observaciones,
    string  ModuloOrigen,   // 'REPRODUCTORA' | 'APOYO' | 'ENGORDE'
    string  Status
);
