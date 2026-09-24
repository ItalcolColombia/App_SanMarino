namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Materializa el estado de cada etapa de una instancia, con snapshot de los valores de la
/// definición (orden/nombre/aprobaciones) para consulta rápida y auditoría aunque el flujo
/// cambie de versión después.
/// </summary>
public class ValidacionInstanciaPaso
{
    public long Id { get; set; }
    public Guid InstanciaId { get; set; }
    public int PasoDefinicionId { get; set; }

    public int Orden { get; set; }
    public string Nombre { get; set; } = null!;
    public int AprobacionesRequeridas { get; set; } = 1;

    /// <summary>Ver <see cref="EstadoInstanciaPaso"/>.</summary>
    public string Estado { get; set; } = EstadoInstanciaPaso.Bloqueada;

    public DateTime? OpenedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ValidacionInstancia Instancia { get; set; } = null!;
    public ValidacionFlujoPaso PasoDefinicion { get; set; } = null!;
}

public static class EstadoInstanciaPaso
{
    public const string Bloqueada = "BLOQUEADA";
    public const string Pendiente = "PENDIENTE";
    public const string Aprobada = "APROBADA";
    public const string Devuelta = "DEVUELTA";
    public const string EsperandoCorreccion = "ESPERANDO_CORRECCION";
    public const string Cancelada = "CANCELADA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { Bloqueada, Pendiente, Aprobada, Devuelta, EsperandoCorreccion, Cancelada };

    public static bool EsValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) && Todos.Contains(estado);
}
