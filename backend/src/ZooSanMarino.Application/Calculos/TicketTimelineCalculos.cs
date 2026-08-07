// src/ZooSanMarino.Application/Calculos/TicketTimelineCalculos.cs
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Arma la línea de tiempo de un caso fusionando lo que YA existe en la base: creación, primera
/// apertura, notas (comentarios y cambios de estado), adjuntos, tareas y registros de tiempo.
/// </summary>
/// <remarks>
/// Deliberadamente NO hay una tabla de eventos: derivar la línea de tiempo de los datos existentes
/// evita un backfill y hace que los casos anteriores a esta funcionalidad se vean completos desde
/// el primer deploy. Lógica pura, sin EF ni estado ⇒ testeable en `TicketTimelineCalculosTests`.
/// </remarks>
public static class TicketTimelineCalculos
{
    // Tipos de evento (los consume el front para elegir icono y color)
    public const string EvCreado       = "CREADO";
    public const string EvAsignado     = "ASIGNADO";
    public const string EvApertura     = "APERTURA";
    public const string EvEstado       = "ESTADO";
    public const string EvComentario   = "COMENTARIO";
    public const string EvSistema      = "SISTEMA";
    public const string EvAdjunto      = "ADJUNTO";
    public const string EvTarea        = "TAREA";
    public const string EvTiempo       = "TIEMPO";
    public const string EvSolucion     = "SOLUCION";
    public const string EvCierre       = "CIERRE";
    public const string EvNotificacion = "NOTIFICACION";

    // ── Entradas (proyecciones planas que arma el service) ───────────────────

    public readonly record struct CabeceraCaso(
        DateTime CreatedAt,
        string? CreadoPorNombre,
        string? SolicitanteNombre,
        string? AsignadoNombre,
        DateTime? FechaPrimeraApertura,
        DateTime? FechaSolucion,
        string? SolucionDescripcion,
        DateTime? FechaCierreSolicitante,
        DateTime? FechaNotificacionCorreo,
        string? CorreoNotificadoA);

    public readonly record struct NotaTimeline(
        long Id, DateTime CreatedAt, string Nota, string? EstadoResultante,
        bool EsInterna, string? TipoEvento, string? AutorNombre);

    public readonly record struct AdjuntoTimeline(
        long Id, DateTime CreatedAt, string Tipo, string? Nombre, string? AutorNombre);

    public readonly record struct TareaTimeline(
        long Id, DateTime CreatedAt, string? Codigo, string Titulo, string Estado,
        DateTime? FechaFinReal, string? AutorNombre);

    public readonly record struct TiempoTimeline(
        long Id, DateTime CreatedAt, decimal Horas, string? Descripcion, string? AutorNombre);

    // ── Salida ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Un punto de la línea de tiempo. <paramref name="EsInterna"/> viaja para que el service
    /// pueda filtrar los eventos que el solicitante no debe ver.
    /// </summary>
    public readonly record struct EventoTimeline(
        DateTime Momento,
        string Tipo,
        string Titulo,
        string? Detalle,
        string? Autor,
        string? EstadoResultante,
        bool EsInterna,
        long? ReferenciaId);

    /// <summary>
    /// Fusiona todas las fuentes y devuelve los eventos en orden cronológico ascendente.
    /// Con <paramref name="incluirInternas"/> en false se omiten las notas internas
    /// (es la vista del solicitante).
    /// </summary>
    public static IReadOnlyList<EventoTimeline> Construir(
        CabeceraCaso caso,
        IEnumerable<NotaTimeline> notas,
        IEnumerable<AdjuntoTimeline> adjuntos,
        IEnumerable<TareaTimeline> tareas,
        IEnumerable<TiempoTimeline> tiempos,
        bool incluirInternas)
    {
        var eventos = new List<EventoTimeline>();

        // 1) Creación del caso
        var tituloCreacion = string.IsNullOrWhiteSpace(caso.SolicitanteNombre) ||
                             string.Equals(caso.SolicitanteNombre, caso.CreadoPorNombre, StringComparison.OrdinalIgnoreCase)
            ? "Caso creado"
            : $"Caso creado a nombre de {caso.SolicitanteNombre}";
        eventos.Add(new EventoTimeline(
            caso.CreatedAt, EvCreado, tituloCreacion,
            string.IsNullOrWhiteSpace(caso.CreadoPorNombre) ? null : $"Registrado por {caso.CreadoPorNombre}",
            caso.CreadoPorNombre, TicketEstados.Abierto, false, null));

        // 2) Primera apertura por el equipo
        if (caso.FechaPrimeraApertura is { } apertura)
            eventos.Add(new EventoTimeline(
                apertura, EvApertura, "El equipo tomó el caso",
                string.IsNullOrWhiteSpace(caso.AsignadoNombre) ? null : $"Responsable: {caso.AsignadoNombre}",
                caso.AsignadoNombre, null, false, null));

        // 3) Notas: comentario humano, cambio de estado o evento de sistema
        foreach (var n in notas)
        {
            if (n.EsInterna && !incluirInternas) continue;

            var (tipo, titulo) = ClasificarNota(n);
            eventos.Add(new EventoTimeline(
                n.CreatedAt, tipo, titulo, n.Nota, n.AutorNombre,
                n.EstadoResultante, n.EsInterna, n.Id));
        }

        // 4) Adjuntos
        foreach (var a in adjuntos)
            eventos.Add(new EventoTimeline(
                a.CreatedAt, EvAdjunto,
                a.Tipo.Equals("LINK", StringComparison.OrdinalIgnoreCase) ? "Link agregado" : "Documento adjuntado",
                a.Nombre, a.AutorNombre, null, false, a.Id));

        // 5) Tareas: alta y, si ya terminaron, su cierre
        foreach (var t in tareas)
        {
            var etiqueta = string.IsNullOrWhiteSpace(t.Codigo) ? t.Titulo : $"{t.Codigo} · {t.Titulo}";
            eventos.Add(new EventoTimeline(
                t.CreatedAt, EvTarea, "Tarea creada", etiqueta, t.AutorNombre, null, false, t.Id));

            if (t.FechaFinReal is { } fin)
                eventos.Add(new EventoTimeline(
                    fin, EvTarea, "Tarea completada", etiqueta, t.AutorNombre, null, false, t.Id));
        }

        // 6) Registros de tiempo
        foreach (var w in tiempos)
            eventos.Add(new EventoTimeline(
                w.CreatedAt, EvTiempo, $"{FormatearHoras(w.Horas)} de trabajo registradas",
                w.Descripcion, w.AutorNombre, null, false, w.Id));

        // 7) Solución, notificación y cierre (hitos del caso)
        if (caso.FechaSolucion is { } solucion)
            eventos.Add(new EventoTimeline(
                solucion, EvSolucion, "Caso solucionado", caso.SolucionDescripcion,
                caso.AsignadoNombre, TicketEstados.Solucionado, false, null));

        if (caso.FechaNotificacionCorreo is { } notificacion)
            eventos.Add(new EventoTimeline(
                notificacion, EvNotificacion, "Solución notificada por correo",
                caso.CorreoNotificadoA, null, null, false, null));

        if (caso.FechaCierreSolicitante is { } cierre)
            eventos.Add(new EventoTimeline(
                cierre, EvCierre, "Cierre confirmado por el solicitante",
                "El caso quedó cerrado por ambas partes.",
                caso.SolicitanteNombre ?? caso.CreadoPorNombre, TicketEstados.Cerrado, false, null));

        return eventos.OrderBy(e => e.Momento).ThenBy(e => e.Tipo, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Decide con qué cara se pinta una nota: evento de sistema, cambio de estado o comentario.
    /// El orden importa — una nota de sistema puede además traer estado resultante.
    /// </summary>
    private static (string Tipo, string Titulo) ClasificarNota(NotaTimeline n)
    {
        if (TicketNotaEventos.EsDeSistema(n.TipoEvento))
            return (EvSistema, TituloDeEventoSistema(n.TipoEvento!));

        if (!string.IsNullOrWhiteSpace(n.EstadoResultante))
            return (EvEstado, $"Estado: {EtiquetaEstado(n.EstadoResultante!)}");

        return (EvComentario, n.EsInterna ? "Nota interna" : "Comentario");
    }

    private static string TituloDeEventoSistema(string tipoEvento) => tipoEvento.ToUpperInvariant() switch
    {
        TicketNotaEventos.Asignacion    => "Responsable actualizado",
        TicketNotaEventos.Prioridad     => "Prioridad actualizada",
        TicketNotaEventos.Tarea         => "Cambio en las tareas",
        TicketNotaEventos.Planificacion => "Planificación actualizada",
        TicketNotaEventos.Solicitante   => "Solicitante actualizado",
        TicketNotaEventos.Tiempo        => "Registro de tiempo",
        _                               => "Actualización del caso",
    };

    /// <summary>Etiqueta legible de un estado (el front tiene la suya; esta alimenta el detalle textual).</summary>
    public static string EtiquetaEstado(string estado) => estado.ToUpperInvariant() switch
    {
        TicketEstados.Abierto          => "Abierto",
        TicketEstados.EnAnalisis       => "En análisis",
        TicketEstados.EnDocumentacion  => "En documentación",
        TicketEstados.EnImplementacion => "En implementación",
        TicketEstados.EnRevision       => "En revisión",
        TicketEstados.Solucionado      => "Solucionado",
        TicketEstados.Cerrado          => "Cerrado",
        TicketEstados.Transferido      => "Transferido",
        TicketEstados.Suspendido       => "Suspendido",
        _                              => estado,
    };

    /// <summary>«1,5 h» / «2 h» — sin decimales cuando son horas enteras.</summary>
    private static string FormatearHoras(decimal horas) =>
        horas == Math.Truncate(horas)
            ? $"{horas:0} h"
            : $"{horas:0.##} h";
}
