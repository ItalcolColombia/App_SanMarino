namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Un grant de ubicación dentro de una granja. <c>Level</c> ∈ {"nucleo","galpon","lote"} y solo el
/// id de ese nivel va no nulo. <c>Nombre</c> es display (solo en GET; se ignora en PUT).
/// </summary>
public record UserFarmScopeItemDto(
    string Level,
    string? NucleoId,
    string? GalponId,
    int? LoteId,
    string? Nombre = null
);

/// <summary>Configuración de alcance de un usuario sobre una granja.</summary>
public record UserFarmScopeConfigDto(
    Guid UserId,
    int FarmId,
    string FarmName,
    bool RestrictLocations,
    UserFarmScopeItemDto[] Items
);

/// <summary>
/// Reemplazo transaccional del alcance. Con <c>RestrictLocations=false</c> los items se ignoran y se
/// eliminan los existentes (vuelve a acceso global). Con <c>true</c>, Items puede ser vacío
/// (usuario no ve nada de la granja hasta que se le asigne algo — fail-closed).
/// </summary>
public record UpdateUserFarmScopeDto(
    bool RestrictLocations,
    UserFarmScopeItemDto[]? Items
);

// ── Árbol de ubicaciones de una granja (para el modal de administración) ──

public record FarmLocationLoteNodeDto(int LoteId, string LoteNombre, string? Fase);

public record FarmLocationGalponNodeDto(
    string GalponId,
    string GalponNombre,
    FarmLocationLoteNodeDto[] Lotes
);

public record FarmLocationNucleoNodeDto(
    string NucleoId,
    string NucleoNombre,
    FarmLocationGalponNodeDto[] Galpones,
    FarmLocationLoteNodeDto[] LotesSinGalpon
);

public record FarmLocationsTreeDto(
    int FarmId,
    string FarmName,
    FarmLocationNucleoNodeDto[] Nucleos,
    FarmLocationLoteNodeDto[] LotesSinNucleo
);
