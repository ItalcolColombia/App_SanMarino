// src/ZooSanMarino.Application/DTOs/CorreccionAvesDisponiblesEngordeDtos.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Diagnóstico de cuadre de aves disponibles de un lote pollo engorde:
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

    /// <summary>Saldo del maestro (base de la fórmula de disponibilidad; las ventas lo descuentan).</summary>
    public int HembrasL { get; init; }
    public int MachosL { get; init; }

    /// <summary>Bajas acumuladas del seguimiento diario (mortalidad + selección + error sexaje).</summary>
    public int BajasSeguimientoHembras { get; init; }
    public int BajasSeguimientoMachos { get; init; }

    /// <summary>Ventas/despachos de aves no anulados (histórico unificado VENTA_AVES).</summary>
    public int VentasHembras { get; init; }
    public int VentasMachos { get; init; }
    public int VentasMixtas { get; init; }

    /// <summary>
    /// Aves vendidas DESPUÉS del último registro de seguimiento (p. ej. reaperturas para traslado).
    /// Con la fn v6 no se mostraban en la tabla diaria y quedaban como saldo residual fantasma.
    /// </summary>
    public int VentasPosterioresAlUltimoSeguimiento { get; init; }
    public DateTime? FechaUltimoSeguimiento { get; init; }
    public DateTime? FechaUltimaVenta { get; init; }

    /// <summary>Disponibilidad vigente (misma fórmula del endpoint aves-disponibles).</summary>
    public int HembrasDisponibles { get; init; }
    public int MachosDisponibles { get; init; }

    /// <summary>Género del sobrante contable: "Hembras" | "Machos" | "Ambos" | null.</summary>
    public string? GeneroSobrante { get; init; }

    /// <summary>true = lote Cerrado con disponibles &gt; 0 (aves fantasma a corregir).</summary>
    public bool RequiereCorreccion { get; init; }
    public int AjusteHembras { get; init; }
    public int AjusteMachos { get; init; }
}

/// <summary>Solicitud de corrección de aves disponibles por nombre de lote.</summary>
public sealed class CorregirAvesDisponiblesRequest
{
    public string LoteNombre { get; set; } = null!;

    /// <summary>true (default): solo reporta lo que haría, sin modificar la base de datos.</summary>
    public bool DryRun { get; set; } = true;
}

public sealed class CorreccionAvesDisponiblesLoteDto
{
    public int LoteAveEngordeId { get; init; }
    public string? GalponId { get; init; }
    public int HembrasLAntes { get; init; }
    public int MachosLAntes { get; init; }
    public int AjusteHembras { get; init; }
    public int AjusteMachos { get; init; }
    public int HembrasLDespues { get; init; }
    public int MachosLDespues { get; init; }
    /// <summary>false cuando la corrida es dryRun.</summary>
    public bool Corregido { get; init; }
}

public sealed class CorreccionAvesDisponiblesResponse
{
    public string LoteNombre { get; init; } = null!;
    public bool DryRun { get; init; }
    public int LotesEvaluados { get; init; }
    public int LotesConDescuadre { get; init; }
    public int LotesCorregidos { get; init; }
    public List<CorreccionAvesDisponiblesLoteDto> Items { get; init; } = new();
}
