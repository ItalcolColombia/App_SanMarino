/// file: backend/src/ZooSanMarino.Domain/Entities/SeguimientoProduccion.cs
namespace ZooSanMarino.Domain.Entities;

public class SeguimientoProduccion
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int LoteId { get; set; } // FK a lotes (Opción B, legacy)
    /// <summary>FK a lote_postura_produccion. Registros nuevos usan este ID.</summary>
    public int? LotePosturaProduccionId { get; set; }
    
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
    
    // Campos de agua (solo para Ecuador y Panamá)
    // NOTA: Usar double? para coincidir con double precision en PostgreSQL
    public double? ConsumoAguaDiario { get; set; } // Consumo diario de agua en litros
    public double? ConsumoAguaPh { get; set; } // Nivel de PH del agua
    public double? ConsumoAguaOrp { get; set; } // Nivel de ORP (Oxidación-Reducción Potencial) del agua en mV
    public double? ConsumoAguaTemperatura { get; set; } // Temperatura del agua en °C
    
    // Navegación opcional (Opción B: lote_id es int FK a lotes; Lote.LoteId es int? por tanto no se configura FK en EF)
}



