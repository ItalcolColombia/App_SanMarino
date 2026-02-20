// src/ZooSanMarino.Domain/Entities/ProduccionSeguimiento.cs
namespace ZooSanMarino.Domain.Entities;

public class ProduccionSeguimiento : AuditableEntity
{
    public int Id { get; set; }
    /// <summary>Lote en fase Producción (lote hijo o mismo lote si está en producción).</summary>
    public int LoteId { get; set; }
    public DateTime FechaRegistro { get; set; }
    
    // Mortalidad
    public int MortalidadH { get; set; }
    public int MortalidadM { get; set; }
    
    // Consumo
    public decimal ConsumoKg { get; set; }
    
    // Producción de huevos
    public int HuevosTotales { get; set; }
    public int HuevosIncubables { get; set; }
    public decimal PesoHuevo { get; set; }
    
    // Observaciones
    public string? Observaciones { get; set; }
    
    // Navegación
    public Lote Lote { get; set; } = null!;
}



