namespace ZooSanMarino.Application.DTOs.Lesiones;

public sealed record LesionDto(
    long      Id,
    int?      ClienteId,
    int       FarmId,
    string?   GalponId,
    int?      LoteId,
    string?   LoteReproductoraId,
    int?      EdadDias,
    int?      AvesMacho,
    int?      AvesHembra,
    int?      AvesMixtas,
    string    TipoLesion,
    string?   Observaciones,
    DateTime  FechaRegistro,
    string    ModuloOrigen,
    string    Status,
    int       CompanyId,
    int       CreatedByUserId,
    DateTime  CreatedAt,
    int?      UpdatedByUserId,
    DateTime? UpdatedAt
);
