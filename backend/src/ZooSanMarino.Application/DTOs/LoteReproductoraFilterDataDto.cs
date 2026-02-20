// src/ZooSanMarino.Application/DTOs/LoteReproductoraFilterDataDto.cs
// Datos para los filtros en cascada del módulo Lote Reproductora (Granja → Núcleo → Galpón → Lote).

using ZooSanMarino.Application.DTOs.Shared;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Item mínimo de lote para el filtro (evita enviar todo LoteDetailDto).
/// </summary>
public sealed record LoteFilterItemDto(
    int LoteId,
    string LoteNombre,
    int GranjaId,
    string? NucleoId,
    string? GalponId
);

/// <summary>
/// Payload único con granjas, núcleos, galpones y lotes para rellenar los filtros
/// del módulo Lote Reproductora en una sola llamada.
/// </summary>
public sealed record LoteReproductoraFilterDataDto(
    IEnumerable<FarmDto> Farms,
    IEnumerable<NucleoDto> Nucleos,
    IEnumerable<GalponLiteDto> Galpones,
    IEnumerable<LoteFilterItemDto> Lotes
);
