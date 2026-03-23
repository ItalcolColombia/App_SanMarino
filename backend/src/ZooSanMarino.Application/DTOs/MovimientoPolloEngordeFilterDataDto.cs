using ZooSanMarino.Application.DTOs.Farms;
using ZooSanMarino.Application.DTOs.Galpones;
using ZooSanMarino.Application.DTOs.LoteAveEngorde;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Catálogos para filtros en cascada (granja → núcleo → galpón → lote) en una sola respuesta.
/// Granjas: solo asignadas al usuario (UserFarms) y empresa/país activos, alineado con Lote form-data.
/// Lotes: solo Ave Engorde (no reproductoras).
/// </summary>
public sealed record MovimientoPolloEngordeFilterDataDto(
    IReadOnlyList<FarmDto> Farms,
    IReadOnlyList<NucleoDto> Nucleos,
    IReadOnlyList<GalponDetailDto> Galpones,
    IReadOnlyList<LoteAveEngordeDetailDto> LotesAveEngorde
);
