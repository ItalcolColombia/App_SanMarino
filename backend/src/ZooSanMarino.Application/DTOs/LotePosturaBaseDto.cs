namespace ZooSanMarino.Application.DTOs;

public record LotePosturaBaseDto(
    int      LotePosturaBaseId,
    string   LoteNombre,
    string?  CodigoErp,
    int      CantidadHembras,
    int      CantidadMachos,
    int      CantidadMixtas,
    int      CompanyId,
    int      CreatedByUserId,
    int?     PaisId,
    DateTime CreatedAt
);

public record CreateLotePosturaBaseDto(
    string  LoteNombre,
    string? CodigoErp,
    int     CantidadHembras,
    int     CantidadMachos,
    int     CantidadMixtas
);

