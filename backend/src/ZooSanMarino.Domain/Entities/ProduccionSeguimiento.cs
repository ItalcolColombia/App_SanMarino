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

    // Campos de traslado de aves (R3)
    public int? TrasladoHembras { get; set; }
    public int? TrasladoMachos { get; set; }
    public int? LoteDestinoId { get; set; }
    public int? GranjaDestinoId { get; set; }
    public DateTime? FechaTraslado { get; set; }
    public string? TrasladoObservaciones { get; set; }

    // Feature 14 — splits H/M dedicados (separan traslado de mortalidad)
    public int TrasladoIngresoHembras { get; set; }
    public int TrasladoIngresoMachos { get; set; }
    public int TrasladoSalidaHembras { get; set; }
    public int TrasladoSalidaMachos { get; set; }

    // Feature 14 — marcado de traslado (igual que Levante)
    public bool EsTraslado { get; set; }
    public int? TrasladoLoteContraparteId { get; set; }
    public int? TrasladoGranjaContraparteId { get; set; }
    public string? TrasladoDireccion { get; set; }

    // Feature 14 — selección y error de sexaje (alineado con Levante)
    public int SelH { get; set; }
    public int SelM { get; set; }
    public int ErrorSexajeHembras { get; set; }
    public int ErrorSexajeMachos { get; set; }

    // Nota: UpdatedByUserId (int?) ya viene de AuditableEntity, mapeado a
    //       updated_by_user_id INTEGER. CreatedByUserId también es int (legacy).

    // Navegación
    public Lote Lote { get; set; } = null!;
}



