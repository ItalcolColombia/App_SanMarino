namespace ZooSanMarino.Application.DTOs;

public record LotePosturaBaseDto(
    int       LotePosturaBaseId,
    string    LoteNombre,
    string?   CodigoErp,
    string?   DescripcionErp,
    int       CantidadHembras,
    int       CantidadMachos,
    int       CantidadMixtas,
    // Datos del lote
    string?   Raza,
    string?   TipoLinea,
    DateTime? FechaEncaset,
    // Empresa
    int       CompanyId,
    string?   CompanyNombre,
    // Usuario creador
    int       CreatedByUserId,
    // País
    int?      PaisId,
    string?   PaisNombre,
    // Granja
    int?      FarmId,
    string?   FarmNombre,
    // ERP
    DateTime? ErpCreate,
    // Auditoría
    DateTime  CreatedAt
);

public record CreateLotePosturaBaseDto(
    string    LoteNombre,
    string?   CodigoErp,
    string?   DescripcionErp,
    int       CantidadHembras,
    int       CantidadMachos,
    int       CantidadMixtas,
    string?   Raza,
    string?   TipoLinea,
    DateTime? FechaEncaset,
    int?      FarmId,
    DateTime? ErpCreate
);

public record UpdateLotePosturaBaseDto(
    string    LoteNombre,
    string?   CodigoErp,
    string?   DescripcionErp,
    int       CantidadHembras,
    int       CantidadMachos,
    int       CantidadMixtas,
    string?   Raza,
    string?   TipoLinea,
    DateTime? FechaEncaset,
    int?      FarmId,
    DateTime? ErpCreate
);
