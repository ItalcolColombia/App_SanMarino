// src/ZooSanMarino.Domain/Entities/MovimientoPolloEngorde.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Movimiento o traslado de pollo engorde entre lotes.
/// Origen/destino pueden ser LoteAveEngorde o LoteReproductoraAveEngorde.
/// </summary>
public class MovimientoPolloEngorde : AuditableEntity
{
    public int Id { get; set; }

    public string NumeroMovimiento { get; set; } = string.Empty;
    public DateTime FechaMovimiento { get; set; }
    public string TipoMovimiento { get; set; } = null!; // Traslado, Ajuste, Liquidacion, Venta, Retiro

    /// <summary>
    /// Identificador único de la factura/despacho (Parte C / R3.3). Todas las líneas de un
    /// mismo despacho multi-lote comparten este UID. Se genera automáticamente al crear el
    /// despacho (independiente de <see cref="NumeroDespacho"/>, que es una referencia legible opcional).
    /// </summary>
    public Guid? FacturaId { get; set; }

    // Origen: exactamente uno de los dos debe estar definido
    public int? LoteAveEngordeOrigenId { get; set; }
    public int? LoteReproductoraAveEngordeOrigenId { get; set; }
    public int? GranjaOrigenId { get; set; }
    public string? NucleoOrigenId { get; set; }
    public string? GalponOrigenId { get; set; }

    // Destino: pueden ser ambos null (venta/retiro) o uno de los dos
    public int? LoteAveEngordeDestinoId { get; set; }
    public int? LoteReproductoraAveEngordeDestinoId { get; set; }
    public int? GranjaDestinoId { get; set; }
    public string? NucleoDestinoId { get; set; }
    public string? GalponDestinoId { get; set; }
    public string? PlantaDestino { get; set; }

    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }

    public string? MotivoMovimiento { get; set; }
    public string? Descripcion { get; set; }
    public string? Observaciones { get; set; }
    public string Estado { get; set; } = "Pendiente"; // Pendiente, Completado, Cancelado, Anulado (eliminación con soft-delete)

    public int UsuarioMovimientoId { get; set; }
    public string? UsuarioNombre { get; set; }
    public DateTime? FechaProcesamiento { get; set; }
    public DateTime? FechaCancelacion { get; set; }

    // Campos de despacho / salida (venta de aves)
    public string? NumeroDespacho { get; set; }
    public int? EdadAves { get; set; }
    public int? TotalPollosGalpon { get; set; }
    public string? Raza { get; set; }
    public string? Placa { get; set; }
    public TimeOnly? HoraSalida { get; set; }
    public string? GuiaAgrocalidad { get; set; }
    public string? Sellos { get; set; }
    public string? Ayuno { get; set; }
    public string? Conductor { get; set; }
    public double? PesoBruto { get; set; }
    public double? PesoTara { get; set; }

    // Peso global del despacho: mismo valor en todos los movimientos generados para un despacho multi-galpón.
    public double? PesoBrutoGlobal { get; set; }
    public double? PesoTaraGlobal { get; set; }
    public double? PesoNetoGlobal { get; set; }

    // Peso individual prorrateado: proporcional a las aves de cada movimiento dentro del despacho.
    public double? PesoBrutoReal { get; set; }
    public double? PesoTaraReal { get; set; }
    public double? PesoNeto { get; set; }
    public double? PromedioPesoAve { get; set; }

    /// <summary>Aves de este movimiento que fueron sobrante (excedente sobre el disponible al vender). Parte C/B (R2).</summary>
    public int AvesSobrante { get; set; }

    /// <summary>
    /// Venta Panamá (R-Panamá): el split <see cref="CantidadHembras"/>/<see cref="CantidadMachos"/>
    /// se asignó sobre las MIXTAS del lote. El stock se descuenta/devuelve sobre mixtas (H+M), no
    /// sobre hembras/machos; el reporte muestra el split en H/M. Ver CantidadesEfectivasEnLote().
    /// </summary>
    public bool EsVentaMixta { get; set; }

    public int TotalAves => CantidadHembras + CantidadMachos + CantidadMixtas;

    // Navegación
    public LoteAveEngorde? LoteAveEngordeOrigen { get; set; }
    public LoteReproductoraAveEngorde? LoteReproductoraAveEngordeOrigen { get; set; }
    public LoteAveEngorde? LoteAveEngordeDestino { get; set; }
    public LoteReproductoraAveEngorde? LoteReproductoraAveEngordeDestino { get; set; }
    public Farm? GranjaOrigen { get; set; }
    public Farm? GranjaDestino { get; set; }
}
