namespace ZooSanMarino.Domain.Entities;

/// <summary>Niveles de solicitante para el módulo de tickets.</summary>
public static class NivelTicket
{
    public const string Normal       = "NORMAL";
    public const string Implementador = "IMPLEMENTADOR";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Normal, Implementador };

    /// <summary>Tipos de ticket que puede CREAR cada nivel.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> TiposPermitidos =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Normal]        = new[] { TicketTipos.Soporte, TicketTipos.Dudas },
            [Implementador] = new[] { TicketTipos.Soporte, TicketTipos.Dudas, TicketTipos.Desarrollo, TicketTipos.Requerimiento },
        };

    public static string[] GetTiposPermitidos(string nivel) =>
        TiposPermitidos.TryGetValue(nivel, out var t) ? t : Array.Empty<string>();
}
