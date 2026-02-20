// src/ZooSanMarino.Application/DTOs/LoteReproductoraAveEngordeFilterDataDto.cs
using ZooSanMarino.Application.DTOs.Shared;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Item mínimo de lote ave engorde para el filtro en cascada.
/// </summary>
public sealed record LoteAveEngordeFilterItemDto(
    int LoteAveEngordeId,
    string LoteNombre,
    int GranjaId,
    string? NucleoId,
    string? GalponId
);

/// <summary>
/// Payload con granjas, núcleos, galpones y lotes ave engorde para los filtros del módulo Lote Reproductora Aves de Engorde.
/// </summary>
public sealed record LoteReproductoraAveEngordeFilterDataDto(
    IEnumerable<FarmDto> Farms,
    IEnumerable<NucleoDto> Nucleos,
    IEnumerable<GalponLiteDto> Galpones,
    IEnumerable<LoteAveEngordeFilterItemDto> LotesAveEngorde
);
