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
    string? GalponId,
    string? LoteErp = null,
    /// <summary>Solo lotes ave engorde: Abierto / Cerrado; null en otros módulos.</summary>
    string? EstadoOperativoLote = null,
    /// <summary>Solo lotes levante: ID del lote padre (LotePosturaLevantePadreId); null si es lote base.</summary>
    int? LotePosturaLevantePadreId = null,
    /// <summary>Solo lotes levante: ID del LotePosturaBase al que pertenece este levante (vía Lote.LotePosturaBaseId). Requerido por POST /levante/obtener.</summary>
    int? LotePosturaBaseId = null
);

/// <summary>
/// Item mínimo de lote_postura_base para el dropdown "Lote Base" del filtro.
/// No incluye GranjaId porque lote_postura_base no tiene campo de granja;
/// la ubicación se resuelve vía lote_postura_levante.
/// </summary>
public sealed record LoteBaseFilterItemDto(
    int     LotePosturaBaseId,
    string  LoteNombre,
    string? CodigoErp = null
);

/// <summary>
/// Payload único con granjas, núcleos, galpones, lotes (levante) y lotes base
/// para rellenar los filtros del módulo Reporte Técnico Levante en una sola llamada.
/// </summary>
public sealed record LoteReproductoraFilterDataDto(
    IEnumerable<FarmDto>              Farms,
    IEnumerable<NucleoDto>            Nucleos,
    IEnumerable<GalponLiteDto>        Galpones,
    IEnumerable<LoteFilterItemDto>    Lotes,
    IEnumerable<LoteBaseFilterItemDto> LotesBase
);
