namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Catálogo de tipos de ticket. Persistidos como string (patrón <c>Lesion.ModuloOrigen</c>),
/// no como enum mapeado a BD.
/// </summary>
public static class TicketTipos
{
    public const string Soporte       = "SOPORTE";
    public const string Desarrollo    = "DESARROLLO";
    public const string Requerimiento = "REQUERIMIENTO";
    public const string Dudas         = "DUDAS";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { Soporte, Desarrollo, Requerimiento, Dudas };

    public static bool EsValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && Todos.Contains(tipo);
}

/// <summary>
/// Estados del ticket + máquina de transiciones válidas. La validación de transición
/// vive en el dominio para mantener una sola fuente de verdad.
/// </summary>
/// <remarks>
/// Las cuatro <see cref="FasesTrabajo"/> (análisis, documentación, implementación, revisión) se
/// mueven libremente entre sí: es lo que permite arrastrar una tarjeta en el tablero. La ampliación
/// es estrictamente ADITIVA — toda transición válida antes de agregar EN_DOCUMENTACION / EN_REVISION
/// lo sigue siendo (cubierto por <c>TicketEstadosTransicionesTests</c>).
/// </remarks>
public static class TicketEstados
{
    public const string Abierto          = "ABIERTO";
    public const string EnAnalisis       = "EN_ANALISIS";
    public const string EnDocumentacion  = "EN_DOCUMENTACION";
    public const string EnImplementacion = "EN_IMPLEMENTACION";
    public const string EnRevision       = "EN_REVISION";
    public const string Solucionado      = "SOLUCIONADO";
    public const string Cerrado          = "CERRADO";
    public const string Transferido      = "TRANSFERIDO";
    public const string Suspendido       = "SUSPENDIDO";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { Abierto, EnAnalisis, EnDocumentacion, EnImplementacion, EnRevision,
          Solucionado, Cerrado, Transferido, Suspendido };

    /// <summary>Fases en las que el caso está siendo trabajado por el equipo (columnas del tablero).</summary>
    public static readonly string[] FasesTrabajo =
        { EnAnalisis, EnDocumentacion, EnImplementacion, EnRevision };

    /// <summary>Orden lineal del flujo — alimenta el stepper y el cálculo de avance.</summary>
    public static readonly string[] FlujoLineal =
        { Abierto, EnAnalisis, EnDocumentacion, EnImplementacion, EnRevision, Solucionado, Cerrado };

    /// <summary>Columnas del tablero kanban de casos, en orden de izquierda a derecha.</summary>
    public static readonly string[] ColumnasTablero =
        { Abierto, EnAnalisis, EnDocumentacion, EnImplementacion, EnRevision, Solucionado, Cerrado };

    public static bool EsValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) && Todos.Contains(estado);

    /// <summary>Posición en el flujo lineal (-1 si el estado es especial: transferido/suspendido).</summary>
    public static int OrdenDe(string? estado) =>
        estado is null ? -1 : Array.FindIndex(FlujoLineal, e => e.Equals(estado, StringComparison.OrdinalIgnoreCase));

    /// <summary>Transiciones permitidas: estado actual → estados destino válidos.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Transiciones = ConstruirTransiciones();

    private static Dictionary<string, string[]> ConstruirTransiciones()
    {
        // Desde cualquier fase de trabajo: las otras tres fases + solucionar + suspender + transferir.
        string[] DesdeFase(string actual) =>
            FasesTrabajo.Where(f => !f.Equals(actual, StringComparison.OrdinalIgnoreCase))
                        .Concat(new[] { Solucionado, Suspendido, Transferido })
                        .ToArray();

        var mapa = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Abierto]     = FasesTrabajo.Concat(new[] { Suspendido, Transferido }).ToArray(),
            [Solucionado] = new[] { EnAnalisis, Cerrado },        // reapertura o cierre del solicitante
            [Cerrado]     = Array.Empty<string>(),               // terminal (cerrado por ambas partes)
            [Transferido] = FasesTrabajo.Concat(new[] { Suspendido }).ToArray(),
            [Suspendido]  = FasesTrabajo.ToArray(),               // reactivar en cualquier fase
        };

        foreach (var fase in FasesTrabajo)
            mapa[fase] = DesdeFase(fase);

        return mapa;
    }

    public static bool PuedeTransicionar(string desde, string hacia) =>
        Transiciones.TryGetValue(desde, out var destinos) &&
        destinos.Contains(hacia, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Prioridad del caso. Ordena el tablero y define qué tan pronto vence el compromiso de atención.
/// Default neutro <see cref="Media"/> para que los casos preexistentes no cambien de lectura.
/// </summary>
public static class TicketPrioridades
{
    public const string Baja    = "BAJA";
    public const string Media   = "MEDIA";
    public const string Alta    = "ALTA";
    public const string Critica = "CRITICA";

    public static readonly IReadOnlySet<string> Todas =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Baja, Media, Alta, Critica };

    public static bool EsValida(string? prioridad) =>
        !string.IsNullOrWhiteSpace(prioridad) && Todas.Contains(prioridad);

    /// <summary>Peso para ordenar (crítica primero). Desconocida cae al peso de <see cref="Media"/>.</summary>
    public static int Peso(string? prioridad) => prioridad?.ToUpperInvariant() switch
    {
        Critica => 0,
        Alta    => 1,
        Media   => 2,
        Baja    => 3,
        _       => 2,
    };
}
