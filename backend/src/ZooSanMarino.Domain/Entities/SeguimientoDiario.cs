// src/ZooSanMarino.Domain/Entities/SeguimientoDiario.cs
using System.Text.Json;

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Entidad unificada de seguimiento diario (levante, producción, reproductora).
/// Tabla: public.seguimiento_diario
/// </summary>
public class SeguimientoDiario
{
    public long Id { get; set; }

    public string TipoSeguimiento { get; set; } = null!; // 'levante' | 'produccion' | 'reproductora' | 'engorde'
    public string LoteId { get; set; } = null!;
    /// <summary>FK a lote_postura_levante. Solo aplica cuando tipo = 'levante'.</summary>
    public int? LotePosturaLevanteId { get; set; }
    /// <summary>FK a lote_postura_produccion. Solo aplica cuando tipo = 'produccion'.</summary>
    public int? LotePosturaProduccionId { get; set; }
    public string? ReproductoraId { get; set; }
    public DateTime Fecha { get; set; }

    // Comunes
    public int? MortalidadHembras { get; set; }
    public int? MortalidadMachos { get; set; }
    public int? SelH { get; set; }
    public int? SelM { get; set; }
    public int? ErrorSexajeHembras { get; set; }
    public int? ErrorSexajeMachos { get; set; }
    public decimal? ConsumoKgHembras { get; set; }
    public decimal? ConsumoKgMachos { get; set; }
    public string? TipoAlimento { get; set; }
    /// <summary>Nombre del alimento asignado a Hembras ese día (levante). Independiente de Machos.</summary>
    public string? TipoAlimentoHembrasNombre { get; set; }
    /// <summary>Nombre del alimento asignado a Machos ese día (levante). Independiente de Hembras.</summary>
    public string? TipoAlimentoMachosNombre { get; set; }
    public string? Observaciones { get; set; }
    public string? Ciclo { get; set; }

    public double? PesoPromHembras { get; set; }
    public double? PesoPromMachos { get; set; }
    public double? UniformidadHembras { get; set; }
    public double? UniformidadMachos { get; set; }
    public double? CvHembras { get; set; }
    public double? CvMachos { get; set; }

    public double? ConsumoAguaDiario { get; set; }
    public double? ConsumoAguaPh { get; set; }
    public double? ConsumoAguaOrp { get; set; }
    public double? ConsumoAguaTemperatura { get; set; }

    public JsonDocument? Metadata { get; set; }
    public JsonDocument? ItemsAdicionales { get; set; }

    // Solo reproductora
    public decimal? PesoInicial { get; set; }
    public decimal? PesoFinal { get; set; }

    // Solo levante
    public double? KcalAlH { get; set; }
    public double? ProtAlH { get; set; }
    public double? KcalAveH { get; set; }
    public double? ProtAveH { get; set; }

    // Solo producción
    public int? HuevoTot { get; set; }
    public int? HuevoInc { get; set; }
    public int? HuevoLimpio { get; set; }
    public int? HuevoTratado { get; set; }
    public int? HuevoSucio { get; set; }
    public int? HuevoDeforme { get; set; }
    public int? HuevoBlanco { get; set; }
    public int? HuevoDobleYema { get; set; }
    public int? HuevoPiso { get; set; }
    public int? HuevoPequeno { get; set; }
    public int? HuevoRoto { get; set; }
    public int? HuevoDesecho { get; set; }
    public int? HuevoOtro { get; set; }
    public double? PesoHuevo { get; set; }
    public int? Etapa { get; set; }
    public decimal? PesoH { get; set; }
    public decimal? PesoM { get; set; }
    public decimal? Uniformidad { get; set; }
    public decimal? CoeficienteVariacion { get; set; }
    public string? ObservacionesPesaje { get; set; }

    // Movimientos de aves (traslados y ventas)
    public int? TrasladoAvesEntrante { get; set; }
    public int? TrasladoAvesSalida { get; set; }
    public int? VentaAvesCantidad { get; set; }
    public string? VentaAvesMotivo { get; set; }

    // ── Marcado de traslado (Feature 13) ────────────────────────────
    /// <summary>TRUE si este registro fue creado o modificado por un traslado (visual: fila amarilla).</summary>
    public bool EsTraslado { get; set; }
    /// <summary>lote_postura_levante_id (o produccion_id) del lote contraparte.</summary>
    public int? TrasladoLoteContraparteId { get; set; }
    /// <summary>granja_id del lote contraparte (para auditoría rápida).</summary>
    public int? TrasladoGranjaContraparteId { get; set; }
    /// <summary>SALIDA = aves enviadas; INGRESO = aves recibidas. Útil cuando la fila SÓLO es traslado.</summary>
    public string? TrasladoDireccion { get; set; }

    // ── Splits H/M dedicados (Feature 13 refinamiento) ─────────────
    /// <summary>Hembras recibidas por traslado en esta fila (dirección INGRESO).</summary>
    public int TrasladoIngresoHembras { get; set; }
    /// <summary>Machos recibidos por traslado en esta fila (dirección INGRESO).</summary>
    public int TrasladoIngresoMachos { get; set; }
    /// <summary>Hembras enviadas por traslado en esta fila (dirección SALIDA).</summary>
    public int TrasladoSalidaHembras { get; set; }
    /// <summary>Machos enviados por traslado en esta fila (dirección SALIDA).</summary>
    public int TrasladoSalidaMachos { get; set; }

    // Auditoría
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
