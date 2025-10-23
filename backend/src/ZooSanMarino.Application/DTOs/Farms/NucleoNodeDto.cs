// src/ZooSanMarino.Application/DTOs/Farms/NucleoNodeDto.cs
namespace ZooSanMarino.Application.DTOs.Farms;

public sealed record NucleoNodeDto(
    String    NucleoId,
    int    GranjaId,
    string NucleoNombre,
    int    GalponesCount,
    int    LotesCount
);
