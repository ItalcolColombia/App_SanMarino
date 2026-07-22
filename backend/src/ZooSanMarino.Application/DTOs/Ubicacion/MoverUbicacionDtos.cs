// file: src/ZooSanMarino.Application/DTOs/Ubicacion/MoverUbicacionDtos.cs
// DTOs para las operaciones de "mover" (reubicación) de Lote / Galpón / Núcleo.
// Namespace plano (ZooSanMarino.Application.DTOs) para no tocar usings en interfaces/controllers.
namespace ZooSanMarino.Application.DTOs;

/// <summary>Reubica un lote (granja/núcleo/galpón). Núcleo/galpón opcionales (un lote puede quedar sin galpón).</summary>
public sealed record MoverLoteDto(
    int     LoteId,
    int     GranjaDestinoId,
    string? NucleoDestinoId,
    string? GalponDestinoId
);

/// <summary>Mueve un galpón (y todo su contenido) a otro núcleo/granja. El GalponId no cambia.</summary>
public sealed record MoverGalponDto(
    string GalponId,
    int    GranjaDestinoId,
    string NucleoDestinoId
);

/// <summary>Mueve un núcleo (re-key) de su granja origen a otra granja, arrastrando galpones y lotes.</summary>
public sealed record MoverNucleoDto(
    string NucleoId,
    int    GranjaOrigenId,
    int    GranjaDestinoId
);

/// <summary>Respuesta uniforme de las operaciones de mover: éxito + mensaje + impacto (conteos).</summary>
public sealed record MoverResultDto(
    bool   Success,
    string Message,
    int    GalponesAfectados,
    int    LotesAfectados
);
