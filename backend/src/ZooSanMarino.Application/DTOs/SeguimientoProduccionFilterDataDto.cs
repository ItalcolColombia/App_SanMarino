// src/ZooSanMarino.Application/DTOs/SeguimientoProduccionFilterDataDto.cs
// Datos para filtros del módulo Seguimiento Diario de Producción (lotes desde lote_postura_produccion).

using ZooSanMarino.Application.DTOs.Shared;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Item de lote_postura_produccion para el filtro de Seguimiento Diario Producción.
/// Incluye aves actuales, iniciales y estado para mostrar en la ficha del lote.
/// </summary>
public sealed record LotePosturaProduccionFilterItemDto(
    int LotePosturaProduccionId,
    string LoteNombre,
    int GranjaId,
    string? NucleoId,
    string? GalponId,
    int? AvesHInicial,
    int? AvesMInicial,
    int? AvesHActual,
    int? AvesMActual,
    string? EstadoCierre,
    DateTime? FechaEncaset
);

/// <summary>
/// Payload para filtros del módulo Seguimiento Diario de Producción.
/// Lotes provienen de lote_postura_produccion (abiertos o cerrados, de la empresa).
/// </summary>
public sealed record SeguimientoProduccionFilterDataDto(
    IEnumerable<FarmDto> Farms,
    IEnumerable<NucleoDto> Nucleos,
    IEnumerable<GalponLiteDto> Galpones,
    IEnumerable<LotePosturaProduccionFilterItemDto> Lotes
);
