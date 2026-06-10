namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Adjunto de un <see cref="Ticket"/>: documento (Excel/PDF en Base64) o link externo.
/// Calca el patrón de <see cref="TicketImagen"/> para los archivos (carga pesada on-demand),
/// y agrega el caso LINK (solo URL + título).
/// </summary>
public class TicketAdjunto
{
    public long Id { get; set; }
    public long TicketId { get; set; }

    /// <summary>ARCHIVO | LINK — ver <see cref="TicketAdjuntoTipos"/>.</summary>
    public string Tipo { get; set; } = TicketAdjuntoTipos.Archivo;

    // ── Caso ARCHIVO (Excel / PDF) ──────────────────────────────
    /// <summary>Contenido del archivo en Base64. NULL para LINK. CARGA PESADA: nunca en listados.</summary>
    public string? ContenidoBase64 { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public int? SizeBytes { get; set; }

    // ── Caso LINK ───────────────────────────────────────────────
    /// <summary>URL del documento externo (Drive, SharePoint, etc.). NULL para ARCHIVO.</summary>
    public string? Url { get; set; }
    public string? Titulo { get; set; }

    /// <summary>Cédula/identificación de quien adjuntó (de <c>ICurrentUser.UserId</c>).</summary>
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket? Ticket { get; set; }
}

/// <summary>Tipos de adjunto del ticket.</summary>
public static class TicketAdjuntoTipos
{
    public const string Archivo = "ARCHIVO";
    public const string Link    = "LINK";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Archivo, Link };

    public static bool EsValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && Todos.Contains(tipo);
}
