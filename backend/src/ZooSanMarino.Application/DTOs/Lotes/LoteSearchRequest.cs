namespace ZooSanMarino.Application.DTOs.Lotes;

public sealed record LoteSearchRequest(
    string?   Search       = null,
    int?      GranjaId     = null,
    string?   NucleoId     = null,
    string?   GalponId     = null,
    DateTime? FechaDesde   = null,
    DateTime? FechaHasta   = null,
    string?   TipoLinea    = null,
    string?   Raza         = null,
    string?   Tecnico      = null,
    bool      SoloActivos  = true,
    string    SortBy       = "fecha_encaset", // lote_nombre | fecha_encaset | lote_id
    bool      SortDesc     = true,
    int       Page         = 1,
    int       PageSize     = 20
);
