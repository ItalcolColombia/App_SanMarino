// file: src/ZooSanMarino.Application/DTOs/Nucleos/NucleoSearchRequest.cs
namespace ZooSanMarino.Application.DTOs.Nucleos;

public sealed record NucleoSearchRequest(
    string? Search      = null,  // nucleo_id o nucleo_nombre
    int?    GranjaId    = null,
    bool    SoloActivos = true,
    string  SortBy      = "nucleo_nombre", // nucleo_id | nucleo_nombre | granja_id
    bool    SortDesc    = false,
    int     Page        = 1,
    int     PageSize    = 20
);
