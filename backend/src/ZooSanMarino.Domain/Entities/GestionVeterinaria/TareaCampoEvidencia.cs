namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Fotografía de cumplimiento de una tarea. El contenido pesado se consulta on-demand; los listados
/// sólo proyectan metadata. Queda preparada para migrar el contenido a S3 sin cambiar la tarea.
/// </summary>
public class TareaCampoEvidencia
{
    public long Id { get; set; }
    public long TareaId { get; set; }
    public string ImagenBase64 { get; set; } = null!;
    public string? FileName { get; set; }
    public string ContentType { get; set; } = null!;
    public int SizeBytes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TareaCampo Tarea { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
