// src/ZooSanMarino.Application/Calculos/ImplementacionCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica pura del módulo Implementación (sin EF/estado): estados de plan y tarea, % de avance,
/// vencimiento, autorización de confirmación y plantilla por defecto del cronograma de entrega.
/// </summary>
public static class ImplementacionCalculos
{
    // Estados de plan
    public const string PlanBorrador   = "borrador";
    public const string PlanEnProgreso = "en_progreso";
    public const string PlanCompletado = "completado";
    public const string PlanCancelado  = "cancelado";

    // Estados de tarea
    public const string TareaPendiente  = "pendiente";
    public const string TareaCompletada = "completada";
    public const string TareaConfirmada = "confirmada";

    public sealed record ResumenPlan(
        int TotalTareas,
        int Completadas,
        int Confirmadas,
        decimal PorcentajeAvance,
        decimal PorcentajeConfirmado);

    public sealed record PlantillaTarea(string Categoria, string Titulo, int Orden);

    /// <summary>
    /// Resumen de avance. <paramref name="completadas"/> son las tareas en estado "completada"
    /// (aún sin confirmar); las confirmadas también cuentan como avance porque ya pasaron por el
    /// check. Porcentajes 0–100 redondeados a 1 decimal (AwayFromZero); total 0 → 0 %.
    /// </summary>
    public static ResumenPlan CalcularResumen(int totalTareas, int completadas, int confirmadas)
    {
        if (totalTareas <= 0)
            return new ResumenPlan(0, 0, 0, 0m, 0m);

        var avance = completadas + confirmadas;
        var pctAvance     = Math.Round(100m * avance      / totalTareas, 1, MidpointRounding.AwayFromZero);
        var pctConfirmado = Math.Round(100m * confirmadas / totalTareas, 1, MidpointRounding.AwayFromZero);
        return new ResumenPlan(totalTareas, completadas, confirmadas, pctAvance, pctConfirmado);
    }

    /// <summary>
    /// Estado derivado del plan tras un cambio en sus tareas. "cancelado" es manual y se respeta;
    /// sin tareas → borrador; todas confirmadas → completado; algún avance (completadas o
    /// confirmadas) → en_progreso; con tareas pero sin ningún check → borrador (aún en armado).
    /// <paramref name="avance"/> = completadas + confirmadas.
    /// </summary>
    public static string DeterminarEstadoPlan(string estadoActual, int totalTareas, int confirmadas, int avance)
    {
        if (estadoActual == PlanCancelado) return PlanCancelado;
        if (totalTareas <= 0) return PlanBorrador;
        if (confirmadas >= totalTareas) return PlanCompletado;
        if (avance > 0) return PlanEnProgreso;
        return PlanBorrador;
    }

    /// <summary>Vencida = tiene fecha programada anterior a hoy y sigue pendiente (sin check).</summary>
    public static bool EsTareaVencida(DateTime? fechaProgramada, DateTime hoy, string estado)
        => fechaProgramada.HasValue
           && fechaProgramada.Value.Date < hoy.Date
           && estado == TareaPendiente;

    /// <summary>
    /// Solo el usuario asignado puede confirmar, y únicamente cuando la tarea ya fue marcada
    /// completada por el gestor (doble check). Nulls → false (fail-closed).
    /// </summary>
    public static bool PuedeConfirmar(string estadoTarea, Guid? asignadoUserId, Guid? usuarioActual)
        => estadoTarea == TareaCompletada
           && asignadoUserId.HasValue
           && usuarioActual.HasValue
           && asignadoUserId.Value == usuarioActual.Value;

    /// <summary>
    /// Checklist estándar de entrega de la aplicación, usado al crear un plan con "usar plantilla".
    /// El orden es global (1..n) para que el cronograma quede secuenciado entre categorías.
    /// </summary>
    public static IReadOnlyList<PlantillaTarea> PlantillaPorDefecto() => new[]
    {
        new PlantillaTarea("Parametrizaciones", "Parametrización de empresa, país y usuarios",          1),
        new PlantillaTarea("Parametrizaciones", "Parametrización de granjas, núcleos y galpones",       2),
        new PlantillaTarea("Parametrizaciones", "Parametrización de catálogos e inventario",            3),
        new PlantillaTarea("Parametrizaciones", "Parametrización de guías genéticas",                   4),
        new PlantillaTarea("Capacitación",      "Capacitación en módulos de configuración",             5),
        new PlantillaTarea("Capacitación",      "Capacitación en seguimiento diario",                   6),
        new PlantillaTarea("Capacitación",      "Capacitación en reportes y liquidaciones",             7),
        new PlantillaTarea("Carga de datos",    "Carga de datos iniciales (lotes activos)",             8),
        new PlantillaTarea("Carga de datos",    "Validación de datos migrados con el usuario",          9),
        new PlantillaTarea("Puesta en marcha",  "Acompañamiento primera semana de operación",          10),
        new PlantillaTarea("Puesta en marcha",  "Acta de entrega y cierre de implementación",          11),
    };
}
