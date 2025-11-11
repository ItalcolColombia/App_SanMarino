// src/ZooSanMarino.Domain/Entities/ProduccionLote.cs
namespace ZooSanMarino.Domain.Entities;

public class ProduccionLote : AuditableEntity
{
    public int Id { get; set; }
    public string LoteId { get; set; } = null!; // VARCHAR en la BD
    public DateTime FechaInicio { get; set; }
    
    // Aves iniciales
    public int AvesInicialesH { get; set; }
    public int AvesInicialesM { get; set; }
    
    // Campos adicionales de la BD
    public int HuevosIniciales { get; set; }
    public string TipoNido { get; set; } = "Manual";
    public int GranjaId { get; set; }
    public string NucleoId { get; set; } = null!;
    public string? NucleoP { get; set; } // Núcleo de Producción
    public string? GalponId { get; set; }
    public string Ciclo { get; set; } = "normal";
    
    // Navegaciones
    // public Lote Lote { get; set; } = null!; // Comentado porque lote_id es VARCHAR
    public ICollection<ProduccionSeguimiento> Seguimientos { get; set; } = new List<ProduccionSeguimiento>();
}
