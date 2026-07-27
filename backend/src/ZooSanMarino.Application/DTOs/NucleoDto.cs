// src/ZooSanMarino.Application/DTOs/NucleoDto.cs
namespace ZooSanMarino.Application.DTOs;
public record NucleoDto(
    string  NucleoId,
    int     GranjaId,
    string  NucleoNombre,
    string? GranjaNombre  = null,
    string? CompanyNombre = null,
    int?    CompanyId     = null,
    // Códigos ERP avícolas (empresas con manejaCodigosErpAvicola = true)
    string? CodigoBodega      = null,
    string? DescripcionBodega = null
);