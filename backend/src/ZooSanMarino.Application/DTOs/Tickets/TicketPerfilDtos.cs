namespace ZooSanMarino.Application.DTOs.Tickets;

// ───────────────────────── Entrada ─────────────────────────

/// <summary>Upsert completo del perfil de atención de un usuario (resolutores + nivel).</summary>
public record UpsertTicketPerfilRequest(
    /// <summary>Nivel del solicitante: NORMAL | IMPLEMENTADOR</summary>
    string Nivel,
    /// <summary>Lista de perfiles de resolutor que tendrá el usuario.</summary>
    List<ResolutorItemRequest> Resolutores
);

/// <summary>Un perfil de resolutor: tipo + país opcional (null = global).</summary>
public record ResolutorItemRequest(
    string Tipo,
    int? PaisId
);

/// <summary>Upsert de perfiles de atención de un ROL (defaults).</summary>
public record UpsertTicketResolutorRolRequest(
    List<ResolutorItemRequest> Resolutores
);

// ───────────────────────── Salida ─────────────────────────

public record TicketPerfilDto(
    Guid UserId,
    string Nivel,
    IReadOnlyList<ResolutorItemDto> Resolutores,
    bool HasProfile = false
);

public record ResolutorItemDto(
    long Id,
    string Tipo,
    int? PaisId,
    bool Activo
);

/// <summary>Usuario asignable para un tipo+país dado (para el select de asignado).</summary>
public record AsignableDto(
    Guid UserId,
    string NombreCompleto,
    string? PaisLabel
);

/// <summary>Tipo permitido con la lista de usuarios asignables disponibles.</summary>
public record TipoPermitidoDto(
    string Tipo,
    string Label,
    IReadOnlyList<AsignableDto> Asignables
);

/// <summary>Perfil del rol (defaults).</summary>
public record TicketResolutorRolDto(
    int RoleId,
    IReadOnlyList<ResolutorItemDto> Resolutores
);
