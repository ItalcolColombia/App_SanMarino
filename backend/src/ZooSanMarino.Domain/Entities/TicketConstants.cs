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
public static class TicketEstados
{
    public const string Abierto          = "ABIERTO";
    public const string EnAnalisis       = "EN_ANALISIS";
    public const string EnImplementacion = "EN_IMPLEMENTACION";
    public const string Solucionado      = "SOLUCIONADO";
    public const string Cerrado          = "CERRADO";
    public const string Transferido      = "TRANSFERIDO";
    public const string Suspendido       = "SUSPENDIDO";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { Abierto, EnAnalisis, EnImplementacion, Solucionado, Cerrado, Transferido, Suspendido };

    public static bool EsValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) && Todos.Contains(estado);

    /// <summary>Transiciones permitidas: estado actual → estados destino válidos.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Transiciones =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Abierto]          = new[] { EnAnalisis, Suspendido, Transferido },
            [EnAnalisis]       = new[] { EnImplementacion, Solucionado, Suspendido, Transferido },
            [EnImplementacion] = new[] { Solucionado, EnAnalisis, Suspendido, Transferido },
            [Solucionado]      = new[] { EnAnalisis, Cerrado },        // reapertura o cierre del solicitante
            [Cerrado]          = Array.Empty<string>(),               // terminal (cerrado por ambas partes)
            [Transferido]      = new[] { EnAnalisis, Suspendido },
            [Suspendido]       = new[] { EnAnalisis },                 // reactivar
        };

    public static bool PuedeTransicionar(string desde, string hacia) =>
        Transiciones.TryGetValue(desde, out var destinos) &&
        destinos.Contains(hacia, StringComparer.OrdinalIgnoreCase);
}
