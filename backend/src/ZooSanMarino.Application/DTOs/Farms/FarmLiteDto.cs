// src/ZooSanMarino.Application/DTOs/Farms/FarmLiteDto.cs
namespace ZooSanMarino.Application.DTOs.Farms;

public sealed record FarmLiteDto(
    int    Id,
    string Name,
    int?   RegionalId,      // nullable
    int    DepartamentoId,
    int    CiudadId         // desde entidad.MunicipioId
);
