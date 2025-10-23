// file: src/ZooSanMarino.Application/DTOs/Galpones/GalponSearchRequest.cs
namespace ZooSanMarino.Application.DTOs.Galpones;

public sealed record GalponSearchRequest(
    string? Search      = null,          // id o nombre
    int?    GranjaId    = null,
    string? NucleoId    = null,
    string? TipoGalpon  = null,
    bool    SoloActivos = true,
    string  SortBy      = "galpon_nombre", // galpon_id | galpon_nombre | nucleo_id
    bool    SortDesc    = false,
    int     Page        = 1,
    int     PageSize    = 20
);
