// src/ZooSanMarino.Application/Calculos/TicketMetricasCalculos.cs
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica pura de control de tiempos del módulo de tickets (sin EF ni estado): tiempo de primera
/// respuesta, tiempo de resolución, permanencia por estado, semáforo de SLA y avance por tareas.
/// El service solo consulta filas y delega acá — esta clase es la especificación ejecutable.
/// </summary>
public static class TicketMetricasCalculos
{
    // ── Semáforo de SLA ──────────────────────────────────────────────────────

    public const string SlaSinCompromiso = "SIN_SLA";
    public const string SlaEnTiempo      = "EN_TIEMPO";
    public const string SlaPorVencer     = "POR_VENCER";
    public const string SlaVencido       = "VENCIDO";
    public const string SlaCumplido      = "CUMPLIDO";
    public const string SlaIncumplido    = "INCUMPLIDO";

    /// <summary>Umbral (en horas restantes) a partir del cual un caso se marca POR_VENCER.</summary>
    public const int HorasUmbralPorVencer = 24;

    /// <summary>
    /// Estado del compromiso de solución.
    /// Sin <paramref name="fechaLimite"/> no hay SLA que evaluar. Si el caso ya se solucionó, el
    /// veredicto es definitivo (CUMPLIDO/INCUMPLIDO comparando contra la fecha de solución, no
    /// contra "ahora"): un caso resuelto a tiempo no puede volverse vencido por el paso del reloj.
    /// </summary>
    public static string EstadoSla(DateTime? fechaLimite, DateTime? fechaSolucion, DateTime ahora)
    {
        if (fechaLimite is null) return SlaSinCompromiso;

        if (fechaSolucion is not null)
            return fechaSolucion.Value <= fechaLimite.Value ? SlaCumplido : SlaIncumplido;

        if (ahora > fechaLimite.Value) return SlaVencido;
        return (fechaLimite.Value - ahora).TotalHours <= HorasUmbralPorVencer
            ? SlaPorVencer
            : SlaEnTiempo;
    }

    /// <summary>
    /// Horas que faltan para el vencimiento (negativas si ya venció). Null si no hay compromiso.
    /// Se congela en la fecha de solución cuando el caso ya está resuelto.
    /// </summary>
    public static double? HorasParaVencer(DateTime? fechaLimite, DateTime? fechaSolucion, DateTime ahora)
    {
        if (fechaLimite is null) return null;
        var referencia = fechaSolucion ?? ahora;
        return Math.Round((fechaLimite.Value - referencia).TotalHours, 2);
    }

    // ── Tiempos del caso ─────────────────────────────────────────────────────

    /// <summary>
    /// Horas entre la creación y la primera vez que el equipo abrió el caso.
    /// Null mientras nadie lo haya tomado. Nunca negativo (datos inconsistentes ⇒ 0).
    /// </summary>
    public static double? HorasPrimeraRespuesta(DateTime creado, DateTime? primeraApertura) =>
        primeraApertura is null ? null : HorasEntre(creado, primeraApertura.Value);

    /// <summary>
    /// Horas entre la creación y la solución. Mientras no esté solucionado devuelve el
    /// transcurrido contra <paramref name="ahora"/> (el caso sigue corriendo).
    /// </summary>
    public static double HorasResolucion(DateTime creado, DateTime? fechaSolucion, DateTime ahora) =>
        HorasEntre(creado, fechaSolucion ?? ahora);

    /// <summary>Horas entre la solución y el cierre confirmado por el solicitante.</summary>
    public static double? HorasConfirmacionCierre(DateTime? fechaSolucion, DateTime? fechaCierre) =>
        fechaSolucion is null || fechaCierre is null ? null : HorasEntre(fechaSolucion.Value, fechaCierre.Value);

    private static double HorasEntre(DateTime desde, DateTime hasta) =>
        Math.Round(Math.Max(0, (hasta - desde).TotalHours), 2);

    // ── Permanencia por estado ───────────────────────────────────────────────

    /// <summary>Un cambio de estado registrado en la bitácora.</summary>
    public readonly record struct CambioEstado(string Estado, DateTime Momento);

    /// <summary>Horas acumuladas en un estado.</summary>
    public readonly record struct PermanenciaEstado(string Estado, double Horas);

    /// <summary>
    /// Reparte el tiempo de vida del caso entre los estados por los que pasó.
    /// Reconstruye la línea a partir de los cambios registrados: el caso nace en ABIERTO
    /// (<paramref name="creado"/>) y el último tramo corre hasta <paramref name="hasta"/>.
    /// Los cambios llegan en cualquier orden — acá se ordenan; los repetidos consecutivos se
    /// funden porque no representan un cambio real de columna.
    /// </summary>
    public static IReadOnlyList<PermanenciaEstado> PermanenciaPorEstado(
        DateTime creado, IEnumerable<CambioEstado> cambios, DateTime hasta)
    {
        var ordenados = cambios
            .Where(c => !string.IsNullOrWhiteSpace(c.Estado))
            .OrderBy(c => c.Momento)
            .ToList();

        // Tramos: (estado, desde). Arranca en ABIERTO al momento de la creación.
        var tramos = new List<(string Estado, DateTime Desde)> { (TicketEstados.Abierto, creado) };
        foreach (var c in ordenados)
        {
            var estado = c.Estado.ToUpperInvariant();
            var actual = tramos[^1].Estado;
            if (estado.Equals(actual, StringComparison.OrdinalIgnoreCase)) continue;
            // Un cambio anterior al inicio del tramo vigente no puede correr el reloj hacia atrás.
            var desde = c.Momento < tramos[^1].Desde ? tramos[^1].Desde : c.Momento;
            tramos.Add((estado, desde));
        }

        var acumulado = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < tramos.Count; i++)
        {
            var fin = i + 1 < tramos.Count ? tramos[i + 1].Desde : (hasta < tramos[i].Desde ? tramos[i].Desde : hasta);
            var horas = Math.Max(0, (fin - tramos[i].Desde).TotalHours);
            acumulado[tramos[i].Estado] = acumulado.GetValueOrDefault(tramos[i].Estado) + horas;
        }

        return acumulado
            .Select(kv => new PermanenciaEstado(kv.Key, Math.Round(kv.Value, 2)))
            .OrderByDescending(p => p.Horas)
            .ToList();
    }

    // ── Avance por tareas ────────────────────────────────────────────────────

    /// <summary>
    /// Porcentaje de tareas terminadas (0..100, un decimal). Sin tareas devuelve 0:
    /// el avance de un caso sin desglose lo cuenta su estado, no este número.
    /// </summary>
    public static decimal PorcentajeAvanceTareas(int totalTareas, int tareasListas)
    {
        if (totalTareas <= 0) return 0m;
        var listas = Math.Clamp(tareasListas, 0, totalTareas);
        return Math.Round(listas * 100m / totalTareas, 1);
    }

    /// <summary>
    /// Desvío entre lo estimado y lo registrado, en horas. Positivo = se pasó de la estimación.
    /// Null si no hay estimación contra la cual comparar.
    /// </summary>
    public static decimal? DesvioHoras(decimal? horasEstimadas, decimal horasRegistradas) =>
        horasEstimadas is null or 0 ? null : Math.Round(horasRegistradas - horasEstimadas.Value, 2);

    /// <summary>
    /// Avance del caso sobre el flujo lineal (0..100). Los estados especiales
    /// (transferido/suspendido) no tienen posición en el flujo ⇒ devuelven 0.
    /// </summary>
    public static decimal PorcentajeAvanceFlujo(string? estado)
    {
        var idx = TicketEstados.OrdenDe(estado);
        if (idx < 0) return 0m;
        var ultimo = TicketEstados.FlujoLineal.Length - 1;
        return ultimo <= 0 ? 0m : Math.Round(idx * 100m / ultimo, 1);
    }
}
