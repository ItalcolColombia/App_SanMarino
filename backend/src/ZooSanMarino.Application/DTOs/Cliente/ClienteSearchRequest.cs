namespace ZooSanMarino.Application.DTOs.Cliente;

public sealed record ClienteSearchRequest(
    string? Search        = null,
    string? TipoCliente   = null,
    string? Pais          = null,
    string? TipoDocumento = null,
    string? Zona          = null,   // 'Zona 1' | 'Zona 2' (Panamá)
    bool    SoloActivos   = true,
    string  SortBy        = "nombre",
    bool    SortDesc      = false,
    int     Page          = 1,
    int     PageSize      = 20
);
