namespace ZooSanMarino.Application.DTOs.Tickets;

// ───────────────────────── Entrada ─────────────────────────

/// <summary>Crea un ticket. País/empresa/autor se infieren del contexto, no del body.</summary>
public record CreateTicketRequest(
    string Titulo,
    string Tipo,
    string Descripcion,
    List<TicketImagenInput>? Imagenes
);

/// <summary>Imagen entrante (ya comprimida en el frontend) en Base64.</summary>
public record TicketImagenInput(
    string Base64,
    string? FileName,
    string? ContentType,
    int? SizeBytes
);

public record AddTicketImagenesRequest(List<TicketImagenInput> Imagenes);

public record CambiarEstadoTicketRequest(string Estado, string? Nota);

public record CreateTicketNotaRequest(string Nota, bool EsInterna = false);

/// <summary>Filtros de búsqueda paginada (compartidos por mis-tickets / gestión / admin).</summary>
public record TicketSearchRequest(
    int? Anio = null,
    string? Estado = null,
    string? Tipo = null,
    int? PaisId = null,
    int? CompanyId = null,
    int Page = 1,
    int PageSize = 20
);

// ──────────────── Salida — LISTADO (ligero, sin Base64) ────────────────

public record TicketListItemDto(
    long Id,
    string? Codigo,
    string Titulo,
    string Tipo,
    string Estado,
    int PaisId,
    int CreatedByUserId,
    int? AssignedToUserId,
    DateTime CreatedAt,
    int CantidadImagenes,
    int CantidadNotas
);

// ──────────── Salida — DETALLE (metadata de imágenes, sin Base64 inline) ────────────

public record TicketDetailDto(
    long Id,
    string? Codigo,
    string Titulo,
    string Tipo,
    string Estado,
    string Descripcion,
    int PaisId,
    int CreatedByUserId,
    int? AssignedToUserId,
    DateTime CreatedAt,
    DateTime? FechaPrimeraApertura,
    DateTime? FechaSolucion,
    IReadOnlyList<TicketNotaDto> Notas,
    IReadOnlyList<TicketImagenMetaDto> Imagenes
);

public record TicketNotaDto(
    long Id,
    int UserId,
    string Nota,
    string? EstadoResultante,
    bool EsInterna,
    DateTime CreatedAt
);

public record TicketImagenMetaDto(
    long Id,
    string? FileName,
    string? ContentType,
    int? SizeBytes,
    DateTime CreatedAt
);

// ──────────── Salida — UNA imagen (on-demand) ────────────

public record TicketImagenDto(
    long Id,
    string ImagenBase64,
    string? ContentType,
    string? FileName
);
