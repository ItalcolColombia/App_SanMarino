// src/ZooSanMarino.Application/DTOs/CorreccionAvesDisponiblesEngordeDtos.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>Tipos de descuadre detectables entre maestro/disponibilidad y el seguimiento diario.</summary>
public static class TipoDescuadreAvesEngorde
{
    /// <summary>Ventas/despachos en estado Pendiente con fecha pasada: ya constan en el histórico/tabla pero no descontaron el maestro.</summary>
    public const string PendientesSinConfirmar = "PendientesSinConfirmar";
    /// <summary>Ventas Completadas que nunca descontaron hembras_l/machos_l (bug histórico de escritura).</summary>
    public const string MaestroNoDescontado = "MaestroNoDescontado";
    /// <summary>Lote Cerrado/liquidado con disponibles &gt; 0 (aves nunca descargadas en ningún registro).</summary>
    public const string FantasmaCerrado = "FantasmaCerrado";
    /// <summary>Descuadre detectado pero la evidencia no cierra exacta: no se corrige automáticamente.</summary>
    public const string RevisionManual = "RevisionManual";
}

/// <summary>
/// Diagnóstico de cuadre de aves de un lote pollo engorde:
/// contabilidad por género (iniciales − bajas del seguimiento − ventas) vs disponibilidad vigente.
/// </summary>
public sealed class ValidacionAvesDisponiblesLoteDto
{
    public int LoteAveEngordeId { get; init; }
    public string? LoteNombre { get; init; }
    public int GranjaId { get; init; }
    public string? GalponId { get; init; }
    public string? EstadoOperativoLote { get; init; }
    public DateTime? LiquidadoAt { get; init; }

    /// <summary>Encaset real por género (historial "Inicio"; informativo).</summary>
    public int HembrasIniciales { get; init; }
    public int MachosIniciales { get; init; }

    /// <summary>true si el historial Inicio cuadra con aves_encasetadas (si false, el desglose por género del historial no es confiable).</summary>
    public bool HistorialInicioConfiable { get; init; }

    /// <summary>Saldo del maestro (base de la fórmula de disponibilidad; las ventas confirmadas lo descuentan).</summary>
    public int HembrasL { get; init; }
    public int MachosL { get; init; }

    /// <summary>Bajas acumuladas del seguimiento diario (mortalidad + selección + error sexaje).</summary>
    public int BajasSeguimientoHembras { get; init; }
    public int BajasSeguimientoMachos { get; init; }

    /// <summary>Ventas/despachos de aves no anulados (histórico unificado VENTA_AVES; incluye pendientes ya ejecutados).</summary>
    public int VentasHembras { get; init; }
    public int VentasMachos { get; init; }
    public int VentasMixtas { get; init; }

    /// <summary>Ventas/despachos en estado Pendiente (reserva: no han descontado el maestro).</summary>
    public int VentasPendientesHembras { get; init; }
    public int VentasPendientesMachos { get; init; }
    public int VentasPendientesMixtas { get; init; }
    /// <summary>Movimientos Pendientes con fecha pasada (candidatos a confirmación automática).</summary>
    public int MovimientosPendientesVencidos { get; init; }
    /// <summary>Aves de los movimientos pendientes vencidos (lo que la confirmación descontaría del maestro).</summary>
    public int VentasPendientesVencidasHembras { get; init; }
    public int VentasPendientesVencidasMachos { get; init; }

    /// <summary>
    /// Exceso del maestro vs lo esperado (esperado = iniciales − ventas Completadas − ajustes auditados).
    /// &gt; 0 ⇒ ventas que no descontaron. Solo calculado con historial confiable; si no, ver DriftMaestroTotal.
    /// </summary>
    public int? DriftMaestroHembras { get; init; }
    public int? DriftMaestroMachos { get; init; }
    /// <summary>Exceso total del maestro vs conservación (hl+ml+mx) − (encaset − ventasCompTotal − ajustesTotal). Calculable siempre.</summary>
    public int DriftMaestroTotal { get; init; }

    public int VentasPosterioresAlUltimoSeguimiento { get; init; }
    public DateTime? FechaUltimoSeguimiento { get; init; }
    public DateTime? FechaUltimaVenta { get; init; }

    /// <summary>Disponibilidad vigente (misma fórmula del endpoint aves-disponibles, ya neta de reserva pendiente).</summary>
    public int HembrasDisponibles { get; init; }
    public int MachosDisponibles { get; init; }

    /// <summary>"Hembras" | "Machos" | "Ambos" | null — género del sobrante contable.</summary>
    public string? GeneroSobrante { get; init; }

    /// <summary>Tipo de descuadre detectado (TipoDescuadreAvesEngorde) o null si el lote cuadra.</summary>
    public string? TipoDescuadre { get; init; }
    public bool RequiereCorreccion { get; init; }

    /// <summary>Ajuste que la corrección descontaría del maestro (re-sync o fantasma de cerrado; las confirmaciones de pendientes descuentan vía el movimiento).</summary>
    public int AjusteHembras { get; init; }
    public int AjusteMachos { get; init; }
}

/// <summary>Solicitud de corrección de saldos de aves por nombre de lote (null ⇒ todos los lotes de la company).</summary>
public sealed class CorregirAvesDisponiblesRequest
{
    public string? LoteNombre { get; set; }

    /// <summary>true (default): solo reporta lo que haría, sin modificar la base de datos.</summary>
    public bool DryRun { get; set; } = true;
}

public sealed class CorreccionAvesDisponiblesLoteDto
{
    public int LoteAveEngordeId { get; init; }
    public string? LoteNombre { get; init; }
    public string? GalponId { get; init; }
    /// <summary>Acciones aplicadas/planificadas (TipoDescuadreAvesEngorde).</summary>
    public List<string> Acciones { get; init; } = new();
    /// <summary>Movimientos Pendientes vencidos confirmados (o a confirmar en dryRun).</summary>
    public int MovimientosConfirmados { get; init; }
    public int HembrasLAntes { get; init; }
    public int MachosLAntes { get; init; }
    /// <summary>Descuento aplicado al maestro por confirmación de pendientes vencidos.</summary>
    public int ConfirmadosHembras { get; init; }
    public int ConfirmadosMachos { get; init; }
    /// <summary>Descuento aplicado al maestro por re-sync / fantasma (con auditoría "Ajuste").</summary>
    public int AjusteHembras { get; init; }
    public int AjusteMachos { get; init; }
    public int HembrasLDespues { get; init; }
    public int MachosLDespues { get; init; }
    /// <summary>false cuando la corrida es dryRun.</summary>
    public bool Corregido { get; init; }
    /// <summary>Detalle cuando la corrección requiere revisión manual.</summary>
    public string? Observacion { get; init; }
}

public sealed class CorreccionAvesDisponiblesResponse
{
    public string? LoteNombre { get; init; }
    public bool DryRun { get; init; }
    public int LotesEvaluados { get; init; }
    public int LotesConDescuadre { get; init; }
    public int LotesCorregidos { get; init; }
    public int MovimientosConfirmados { get; init; }
    public List<CorreccionAvesDisponiblesLoteDto> Items { get; init; } = new();
}
