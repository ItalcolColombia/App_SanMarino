// src/ZooSanMarino.Domain/Entities/ProduccionSeguimiento.cs
namespace ZooSanMarino.Domain.Entities;

public class ProduccionSeguimiento : AuditableEntity
{
    public int Id { get; set; }
    public int ProduccionLoteId { get; set; }
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
    public ProduccionLote ProduccionLote { get; set; } = null!;
}



