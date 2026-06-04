namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Imagen adjunta a un <see cref="Ticket"/>, almacenada en Base64.
/// CARGA PESADA: nunca se incluye en listados; se consulta on-demand en el detalle.
/// La tabla queda preparada para migrar a S3 (agregar columna de key/url) sin romper el contrato.
/// </summary>
public class TicketImagen
{
    public long Id { get; set; }
    public long TicketId { get; set; }

    /// <summary>Contenido de la imagen en Base64 (ya comprimida desde el frontend).</summary>
    public string ImagenBase64 { get; set; } = default!;

    public string? FileName { get; set; }
    public string? ContentType { get; set; }

    /// <summary>Tamaño en bytes tras la compresión (metadato ligero para los listados de imágenes).</summary>
    public int? SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket? Ticket { get; set; }
}
