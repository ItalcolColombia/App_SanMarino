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
    public string Estado { get; set; } = "Pendiente"; // Pendiente, Completado, Cancelado

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

    public int TotalAves => CantidadHembras + CantidadMachos + CantidadMixtas;

    public double? PesoNeto => PesoBruto.HasValue && PesoTara.HasValue ? PesoBruto.Value - PesoTara.Value : null;
    public double? PromedioPesoAve => PesoNeto.HasValue && TotalAves > 0 ? PesoNeto.Value / TotalAves : null;

    // Navegación
    public LoteAveEngorde? LoteAveEngordeOrigen { get; set; }
    public LoteReproductoraAveEngorde? LoteReproductoraAveEngordeOrigen { get; set; }
    public LoteAveEngorde? LoteAveEngordeDestino { get; set; }
    public LoteReproductoraAveEngorde? LoteReproductoraAveEngordeDestino { get; set; }
    public Farm? GranjaOrigen { get; set; }
    public Farm? GranjaDestino { get; set; }
}
