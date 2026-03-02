namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Historial de una ejecución de un mapa.
/// </summary>
public class MapaEjecucion
{
    public int Id { get; set; }
    public int MapaId { get; set; }
    public Guid UsuarioId { get; set; }
    public int CompanyId { get; set; }
    public string Parametros { get; set; } = "{}"; // JSON: rango fechas, granjas, tipo dato
    public string? TipoArchivo { get; set; }      // pdf, excel
    public string? ResultadoJson { get; set; }    // JSON: payload con el que se generó el archivo
    public string Estado { get; set; } = "en_proceso"; // en_proceso, completado, error
    public string? MensajeError { get; set; }
    /// <summary>Mensaje de progreso durante la ejecución (ej. "Paso 2/5: Extracción").</summary>
    public string? MensajeEstado { get; set; }
    /// <summary>Número de paso actual (1-based) cuando estado es en_proceso.</summary>
    public int? PasoActual { get; set; }
    /// <summary>Total de pasos con script, para barra de progreso.</summary>
    public int? TotalPasos { get; set; }
    public DateTime FechaEjecucion { get; set; }

    public Mapa Mapa { get; set; } = null!;
    public User Usuario { get; set; } = null!;
}
