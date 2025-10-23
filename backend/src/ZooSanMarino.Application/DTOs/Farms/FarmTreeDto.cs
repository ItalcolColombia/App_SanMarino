// src/ZooSanMarino.Application/DTOs/Farms/FarmTreeDto.cs
using System.Collections.Generic;

namespace ZooSanMarino.Application.DTOs.Farms;

public sealed record FarmTreeDto(
    FarmLiteDto                     Farm,
    IReadOnlyList<NucleoNodeDto>    Nucleos
);
