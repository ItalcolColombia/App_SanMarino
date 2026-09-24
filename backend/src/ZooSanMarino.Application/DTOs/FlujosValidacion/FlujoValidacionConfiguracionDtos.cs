namespace ZooSanMarino.Application.DTOs.FlujosValidacion;

/// <summary>Un proceso del catálogo global, con su disponibilidad para la empresa consultada.</summary>
/// <param name="Key">SEGUIMIENTO_LEVANTE, SEGUIMIENTO_PRODUCCION, etc.</param>
/// <param name="TieneFlujoPublicado">True si ya existe una versión PUBLICADO para esa empresa.</param>
public record ProcesoValidacionDto(
    int Id,
    string Key,
    string Nombre,
    string? Descripcion,
    string? MenuRoute,
    bool TieneFlujoPublicado
);

/// <param name="Tipo">ROL | USUARIO.</param>
/// <param name="RoleId">Obligatorio si Tipo = ROL.</param>
/// <param name="UserId">Obligatorio si Tipo = USUARIO.</param>
/// <param name="NombreLegible">Nombre del rol o del usuario, resuelto para la UI.</param>
public record FlujoAsignadoDto(
    int? Id,
    string Tipo,
    int? RoleId,
    Guid? UserId,
    string NombreLegible
);

public record FlujoPasoDto(
    int? Id,
    int Orden,
    string Nombre,
    string? Descripcion,
    int AprobacionesRequeridas,
    IReadOnlyList<FlujoAsignadoDto> Asignados
);

/// <summary>Definición completa de un flujo (borrador o publicado) para editar/mostrar en la UI.</summary>
public record FlujoValidacionDto(
    int Id,
    int CompanyId,
    int ProcesoId,
    string ProcesoKey,
    int Version,
    string Nombre,
    string Estado,
    int PlazoTotalHoras,
    bool RequierePersonasDistintas,
    bool PermiteAprobacionCreador,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    DateTime? RetiredAt,
    IReadOnlyList<FlujoPasoDto> Pasos
);

/// <param name="Nombre">Nombre legible de la secuencia.</param>
public record CrearBorradorFlujoCommand(
    int CompanyId,
    string ProcesoKey,
    string Nombre,
    int PlazoTotalHoras,
    bool RequierePersonasDistintas,
    bool PermiteAprobacionCreador,
    IReadOnlyList<FlujoPasoDto> Pasos
);

public record ActualizarBorradorFlujoCommand(
    string Nombre,
    int PlazoTotalHoras,
    bool RequierePersonasDistintas,
    bool PermiteAprobacionCreador,
    IReadOnlyList<FlujoPasoDto> Pasos
);

/// <summary>Resultado del preflight antes de publicar: vacío = publicable.</summary>
public record PreflightPublicacionDto(bool EsPublicable, IReadOnlyList<string> Problemas);

/// <summary>Un candidato disponible para armar el selector Rol/Usuario, acotado a la empresa.</summary>
public record AsignableFlujoDto(string Tipo, int? RoleId, Guid? UserId, string NombreLegible);
