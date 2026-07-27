// src/ZooSanMarino.Application/DTOs/CreateNucleoDto.cs
namespace ZooSanMarino.Application.DTOs;
public record CreateNucleoDto(
    int    GranjaId,
    string NucleoId,
    string NucleoNombre,
    // Códigos ERP avícolas (empresas con manejaCodigosErpAvicola = true)
    string? CodigoBodega      = null,
    string? DescripcionBodega = null
);