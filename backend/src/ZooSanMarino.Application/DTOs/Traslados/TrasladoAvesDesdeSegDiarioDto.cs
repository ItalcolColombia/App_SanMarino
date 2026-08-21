namespace ZooSanMarino.Application.DTOs.Traslados;

/// <summary>Datos del traslado de aves ejecutado desde el registro de seguimiento diario.</summary>
public class TrasladoAvesDesdeSegDiarioDto
{
    /// <summary>ID del LotePosturaLevante o LotePosturaProduccion origen.</summary>
    public int LoteOrigenId { get; set; }

    /// <summary>"Levante" o "Produccion".</summary>
    public string TipoOrigen { get; set; } = null!;

    /// <summary>Fecha del registro de seguimiento donde se registra el traslado.</summary>
    public DateTime FechaSeguimiento { get; set; }

    public int TrasladoHembras { get; set; }
    public int TrasladoMachos { get; set; }

    /// <summary>ID del lote destino (LotePosturaLevante o LotePosturaProduccion).</summary>
    public int LoteDestinoId { get; set; }

    /// <summary>"Levante" o "Produccion".</summary>
    public string TipoDestino { get; set; } = null!;

    public int? GranjaDestinoId { get; set; }
    public string? Observaciones { get; set; }

    /// <summary>Placa del vehículo de transporte. Opcional (postura, Santa Reyes).</summary>
    public string? Placa { get; set; }
    /// <summary>Nombre del conductor. Opcional (postura, Santa Reyes).</summary>
    public string? Conductor { get; set; }
    /// <summary>Precinto/sellos de seguridad del transporte. Opcional (postura, Santa Reyes).</summary>
    public string? Sellos { get; set; }
}
