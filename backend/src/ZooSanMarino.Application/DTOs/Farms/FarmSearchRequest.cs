// src/ZooSanMarino.Application/DTOs/Farms/FarmSearchRequest.cs
namespace ZooSanMarino.Application.DTOs.Farms;

public sealed record FarmSearchRequest(
    string? Search          = null,   // name/id
    int?    RegionalId      = null,
    int?    DepartamentoId  = null,   // filtro por Departamento
    int?    CiudadId        = null,   // filtro por Ciudad (Municipio)
    int?    PaisId          = null,   // NUEVO: País (via Departamento.PaisId)
    string? Status          = null,   // "A" | "I"
    bool    SoloActivos     = true,   // DeletedAt == null
    string  SortBy          = "name", // name|regional_id|departamento_id|ciudad_id|created_at
    bool    SortDesc        = false,
    int     Page            = 1,
    int     PageSize        = 20
);
