namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Definición de un mapa (documento de mapeo para exportar datos a ERP/CIESA).
/// Usa Guid para usuarios (created_by_user_id, updated_by_user_id) para coincidir con users.id (UUID).
/// </summary>
public class Mapa
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    /// <summary>Código de plantilla (ej. granjas_huevos_alimento, entrada_ciesa) para definir estructura de encabezado.</summary>
    public string? CodigoPlantilla { get; set; }
    public int CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    public int? PaisId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Pais? Pais { get; set; }
    public ICollection<MapaPaso> Pasos { get; set; } = new List<MapaPaso>();
    public ICollection<MapaEjecucion> Ejecuciones { get; set; } = new List<MapaEjecucion>();
}
