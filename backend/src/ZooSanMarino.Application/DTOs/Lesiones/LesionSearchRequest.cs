namespace ZooSanMarino.Application.DTOs.Lesiones;

public sealed record LesionSearchRequest(
    string?   ModuloOrigen       = null,   // filtro por módulo
    int?      ClienteId          = null,
    int?      FarmId             = null,
    string?   GalponId           = null,
    int?      LoteId             = null,
    string?   LoteReproductoraId = null,
    string?   TipoLesion         = null,
    DateTime? FechaDesde         = null,
    DateTime? FechaHasta         = null,
    bool      SoloActivos        = true,
    string    SortBy             = "fecha_registro",
    bool      SortDesc           = true,
    int       Page               = 1,
    int       PageSize           = 20
);
