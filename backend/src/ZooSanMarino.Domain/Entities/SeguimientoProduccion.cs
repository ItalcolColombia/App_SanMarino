/// file: backend/src/ZooSanMarino.Domain/Entities/SeguimientoProduccion.cs
namespace ZooSanMarino.Domain.Entities;

public class SeguimientoProduccion
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int LoteId { get; set; }
    
    public int MortalidadH { get; set; }
    public int MortalidadM { get; set; }
    public int SelH { get; set; }
    
    public decimal ConsKgH { get; set; }
    public decimal ConsKgM { get; set; }
    
    public int HuevoTot { get; set; }
    public int HuevoInc { get; set; }
    
    public string TipoAlimento { get; set; } = null!;
    public string? Observaciones { get; set; }
    
    public decimal PesoHuevo { get; set; }
    public int Etapa { get; set; }
    
    // Relación con Lote
    public Lote Lote { get; set; } = null!;
}



