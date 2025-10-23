// src/ZooSanMarino.Domain/Entities/ProduccionLote.cs
namespace ZooSanMarino.Domain.Entities;

public class ProduccionLote : AuditableEntity
{
    public int Id { get; set; }
    public int LoteId { get; set; }
    public DateTime FechaInicio { get; set; }
    
    // Aves iniciales
    public int AvesInicialesH { get; set; }
    public int AvesInicialesM { get; set; }
    
    // Observaciones
    public string? Observaciones { get; set; }
    
    // Navegaciones
    public Lote Lote { get; set; } = null!;
    public ICollection<ProduccionSeguimiento> Seguimientos { get; set; } = new List<ProduccionSeguimiento>();
}
