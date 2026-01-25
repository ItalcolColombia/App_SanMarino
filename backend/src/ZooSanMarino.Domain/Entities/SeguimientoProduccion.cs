/// file: backend/src/ZooSanMarino.Domain/Entities/SeguimientoProduccion.cs
namespace ZooSanMarino.Domain.Entities;

public class SeguimientoProduccion
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string LoteId { get; set; } = null!; // text en BD
    
    public int MortalidadH { get; set; }
    public int MortalidadM { get; set; }
    public int SelH { get; set; }
    public int SelM { get; set; }
    
    public decimal ConsKgH { get; set; }
    public decimal ConsKgM { get; set; }
    
    public int HuevoTot { get; set; }
    public int HuevoInc { get; set; }
    
    // Campos de Clasificadora de Huevos
    // (Limpio, Tratado) = HuevoInc +
    public int HuevoLimpio { get; set; }
    public int HuevoTratado { get; set; }
    
    // (Sucio, Deforme, Blanco, Doble Yema, Piso, Pequeño, Roto, Desecho, Otro) = Huevo Total
    public int HuevoSucio { get; set; }
    public int HuevoDeforme { get; set; }
    public int HuevoBlanco { get; set; }
    public int HuevoDobleYema { get; set; }
    public int HuevoPiso { get; set; }
    public int HuevoPequeno { get; set; }
    public int HuevoRoto { get; set; }
    public int HuevoDesecho { get; set; }
    public int HuevoOtro { get; set; }
    
    public string TipoAlimento { get; set; } = null!;
    public string? Observaciones { get; set; }
    
    public decimal PesoHuevo { get; set; }
    public int Etapa { get; set; }
    
    // Campos de Pesaje Semanal (registro una vez por semana)
    public decimal? PesoH { get; set; } // Peso promedio hembras (kg)
    public decimal? PesoM { get; set; } // Peso promedio machos (kg)
    public decimal? Uniformidad { get; set; } // Uniformidad del lote (%)
    public decimal? CoeficienteVariacion { get; set; } // Coeficiente de variación (CV)
    public string? ObservacionesPesaje { get; set; } // Observaciones específicas del pesaje
    
    // Metadata JSONB para campos adicionales (consumo original, tipo de ítem, etc.)
    public System.Text.Json.JsonDocument? Metadata { get; set; }
    
    // NOTA: No hay relación de navegación con Lote porque:
    // - LoteId aquí es string (text en BD)
    // - Lote.LoteId es int?
    // - Son tipos incompatibles para foreign key
    // Si necesitas acceder al Lote, hazlo manualmente convirtiendo el string a int
}



