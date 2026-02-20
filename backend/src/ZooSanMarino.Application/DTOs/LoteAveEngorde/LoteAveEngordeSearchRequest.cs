namespace ZooSanMarino.Application.DTOs.LoteAveEngorde;

public sealed record LoteAveEngordeSearchRequest(
    string? Search = null,
    int? GranjaId = null,
    string? NucleoId = null,
    string? GalponId = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    string? TipoLinea = null,
    string? Raza = null,
    string? Tecnico = null,
    bool SoloActivos = true,
    string SortBy = "fecha_encaset",
    bool SortDesc = true,
    int Page = 1,
    int PageSize = 20
);
