namespace ZooSanMarino.Application.DTOs.Tickets;

// ───────────────────────── Entrada ─────────────────────────

/// <summary>Crea un ticket. País/empresa/autor se infieren del contexto, no del body.</summary>
public record CreateTicketRequest(
    string Titulo,
    string Tipo,
    string Descripcion,
    /// <summary>Guid del resolutor obligatorio — debe ser asignable para (Tipo, País).</summary>
    Guid AssignedToUserGuid,
    List<TicketImagenInput>? Imagenes
);

/// <summary>Transfiere un ticket de REQUERIMIENTO a DESARROLLO, reasignándolo.</summary>
public record TransferirTicketRequest(
    Guid NuevoAsignadoGuid,
    string? Nota
);

/// <summary>Imagen entrante (ya comprimida en el frontend) en Base64.</summary>
public record TicketImagenInput(
    string Base64,
    string? FileName,
    string? ContentType,
    int? SizeBytes
);

public record AddTicketImagenesRequest(List<TicketImagenInput> Imagenes);

/// <summary>Cambia el estado. <c>SolucionDescripcion</c> es obligatoria al pasar a SOLUCIONADO.</summary>
public record CambiarEstadoTicketRequest(string Estado, string? Nota, string? SolucionDescripcion = null);

/// <summary>El solicitante confirma el cierre de un ticket SOLUCIONADO → CERRADO.</summary>
public record ConfirmarCierreRequest(string? Nota);

public record CreateTicketNotaRequest(string Nota, bool EsInterna = false);

// ──────────────── Adjuntos: documentos (Excel/PDF) y links ────────────────

/// <summary>Documento entrante (Excel/PDF) en Base64.</summary>
public record AddTicketDocumentoRequest(string Base64, string? FileName, string? ContentType, int? SizeBytes);

/// <summary>Link de documento externo.</summary>
public record AddTicketLinkRequest(string Url, string? Titulo);

/// <summary>Metadata de un adjunto (sin Base64) — para listar.</summary>
public record TicketAdjuntoDto(
    long Id, string Tipo, string? FileName, string? ContentType, int? SizeBytes,
    string? Url, string? Titulo, int CreatedByUserId, DateTime CreatedAt,
    string? CreatedByNombre = null);

/// <summary>Documento on-demand (con Base64) para descargar.</summary>
public record TicketDocumentoDto(long Id, string ContenidoBase64, string? ContentType, string? FileName);

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
    int CantidadNotas,
    // Identidad legible (nombre completo + rol en la empresa del ticket)
    string? CreatedByNombre,
    string? CreatedByRol,
    string? AssignedToNombre,
    string? AssignedToRol,
    string? PaisNombre = null
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
    IReadOnlyList<TicketImagenMetaDto> Imagenes,
    // Identidad legible (nombre completo + rol en la empresa del ticket)
    string? CreatedByNombre,
    string? CreatedByRol,
    string? AssignedToNombre,
    string? AssignedToRol,
    string? PaisNombre = null,
    string? CreatedByEmail = null,
    string? AssignedToEmail = null,
    /// <summary>True si el usuario actual es el creador (oculta "Tomar" en el front).</summary>
    bool SoyCreador = false,
    // Cierre + notificación
    string? SolucionDescripcion = null,
    DateTime? FechaCierreSolicitante = null,
    bool NotificadoCorreo = false,
    DateTime? FechaNotificacionCorreo = null,
    string? CorreoNotificadoA = null,
    IReadOnlyList<TicketAdjuntoDto>? Adjuntos = null
);

public record TicketNotaDto(
    long Id,
    int UserId,
    string Nota,
    string? EstadoResultante,
    bool EsInterna,
    DateTime CreatedAt,
    // Identidad legible del autor de la nota
    string? UserNombre = null,
    string? UserRol = null,
    string? UserEmail = null,
    /// <summary>True si la nota la escribió el usuario actual (chat: burbuja a la derecha).</summary>
    bool EsMio = false
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
