// src/ZooSanMarino.Application/DTOs/SeguimientoDiarioLoteReproductoraFilterDataDto.cs
using ZooSanMarino.Application.DTOs.Shared;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Item mínimo de lote reproductora ave engorde para el filtro (selección de lote reproductora en seguimiento diario).
/// </summary>
public sealed record LoteReproductoraSeguimientoFilterItemDto(
    int Id,
    string NombreLote,
    int LoteAveEngordeId
);

/// <summary>
/// Payload para filtros del módulo Seguimiento Diario Lote Reproductora.
/// Lotes = lotes ave engorde (misma forma que LoteFilterItemDto) para el 4º dropdown; LotesReproductora para el 5º.
/// </summary>
public sealed record SeguimientoDiarioLoteReproductoraFilterDataDto(
    IEnumerable<FarmDto> Farms,
    IEnumerable<NucleoDto> Nucleos,
    IEnumerable<GalponLiteDto> Galpones,
    IEnumerable<LoteFilterItemDto> Lotes,
    IEnumerable<LoteReproductoraSeguimientoFilterItemDto> LotesReproductora
);
