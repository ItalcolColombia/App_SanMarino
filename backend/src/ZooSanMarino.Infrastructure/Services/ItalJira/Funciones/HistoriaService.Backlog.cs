using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// ItalJira — proyecciones agregadas: backlog (árbol), tablero (kanban de historias) y roadmap.
/// </summary>
/// <remarks>
/// Las tres vistas se arman sobre las MISMAS dos proyecciones (<c>ProyectarHistoriasAsync</c> y
/// <c>ProyectarCasosAsync</c>) para que el avance y las horas de una historia sean iguales en las
/// tres pantallas. Es la regla de «una sola fórmula por número» aplicada a la UI.
///
/// El filtro siempre baja a la BD (por historia, por estado, por texto); la agregación de avance y
/// horas se resuelve en memoria sobre las filas ya filtradas. El universo de ItalJira es de decenas
/// de historias y cientos de trabajos — no es la agregación multipaís que obliga a empujar todo a
/// SQL, y a cambio evita que una traducción LINQ frágil (<c>Min</c> sobre <c>DateOnly?</c>
/// agrupado) explote en runtime.
/// </remarks>
public partial class HistoriaService
{
    /// <summary>Datos mínimos de un trabajo (tarea o caso) para calcular avance, horas y rango.</summary>
    private readonly record struct TrabajoAgregable(
        long HistoriaId, string EstadoTarea, DateOnly? Inicio, DateOnly? Fin, decimal Horas);

    // ───────────────────────── Proyección de historias ─────────────────────────

    /// <summary>
    /// Proyecta historias a DTO agregando avance, horas y rango efectivo desde sus trabajos vivos.
    /// </summary>
    private async Task<IReadOnlyList<HistoriaDto>> ProyectarHistoriasAsync(
        IQueryable<Historia> universo, CancellationToken ct)
    {
        var filas = await universo
            .Where(h => h.DeletedAt == null)
            .OrderBy(h => h.Estado).ThenBy(h => h.Orden).ThenBy(h => h.Id)
            .Select(h => new
            {
                h.Id, h.Codigo, h.Titulo, h.Descripcion, h.Estado, h.Prioridad,
                h.ResponsableUserGuid, h.Orden, h.HorasEstimadas,
                h.FechaInicioPlan, h.FechaFinPlan, h.FechaInicioReal, h.FechaFinReal,
                h.Etiquetas, h.CreatedAt, h.CreatedByUserId
            })
            .ToListAsync(ct);

        if (filas.Count == 0) return Array.Empty<HistoriaDto>();

        var ids = filas.Select(f => f.Id).ToList();
        var trabajos = await CargarTrabajosAgregablesAsync(ids, ct);
        var porHistoria = trabajos.GroupBy(t => t.HistoriaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var nombres = await NombresPorGuidAsync(
            filas.Where(f => f.ResponsableUserGuid.HasValue).Select(f => f.ResponsableUserGuid!.Value), ct);
        var autores = await NombresPorCedulaAsync(filas.Select(f => f.CreatedByUserId), ct);

        return filas.Select(f =>
        {
            var míos = porHistoria.GetValueOrDefault(f.Id) ?? new List<TrabajoAgregable>();
            var estados = míos.Select(t => t.EstadoTarea).ToList();
            var (terminados, totales) = HistoriaCalculos.ConteoAvance(estados);

            var (inicioEfectivo, finEfectivo) = HistoriaCalculos.RangoEfectivo(
                f.FechaInicioPlan, f.FechaFinPlan, míos.Select(t => (t.Inicio, t.Fin)));

            return new HistoriaDto(
                f.Id, f.Codigo, f.Titulo, f.Descripcion, f.Estado, f.Prioridad,
                f.ResponsableUserGuid,
                f.ResponsableUserGuid.HasValue ? nombres.GetValueOrDefault(f.ResponsableUserGuid.Value) : null,
                f.Orden, f.HorasEstimadas, míos.Sum(t => t.Horas),
                f.FechaInicioPlan, f.FechaFinPlan, f.FechaInicioReal, f.FechaFinReal,
                f.Etiquetas,
                HistoriaCalculos.AvancePorTareas(estados, f.Estado), terminados, totales,
                f.CreatedAt, autores.GetValueOrDefault(f.CreatedByUserId),
                inicioEfectivo, finEfectivo);
        }).ToList();
    }

    /// <summary>
    /// Carga los trabajos (tareas + casos) de un conjunto de historias, ya traducidos al vocabulario
    /// de las tareas y con sus horas imputadas. El filtro por historia lo resuelve la BD.
    /// </summary>
    private async Task<List<TrabajoAgregable>> CargarTrabajosAgregablesAsync(
        List<long> historiaIds, CancellationToken ct)
    {
        if (historiaIds.Count == 0) return new List<TrabajoAgregable>();

        var tareas = await _ctx.TicketTareas.AsNoTracking()
            .Where(t => t.HistoriaId != null && historiaIds.Contains(t.HistoriaId.Value) && t.DeletedAt == null)
            .Select(t => new
            {
                HistoriaId = t.HistoriaId!.Value,
                t.Estado, t.FechaInicioPlan, t.FechaFinPlan,
                Horas = t.Tiempos.Where(w => w.DeletedAt == null).Sum(w => (decimal?)w.Horas) ?? 0m
            })
            .ToListAsync(ct);

        var casos = await _ctx.Tickets.AsNoTracking()
            .Where(t => t.HistoriaId != null && historiaIds.Contains(t.HistoriaId.Value) && t.DeletedAt == null)
            .Select(t => new
            {
                HistoriaId = t.HistoriaId!.Value,
                t.Estado, t.FechaInicioPlan, t.FechaFinPlan,
                // Solo el tiempo imputado AL CASO: el de sus tareas ya viaja por la fila de la tarea
                // cuando la tarea pertenece a la historia. Sumar los dos duplicaría las horas.
                Horas = t.Tiempos.Where(w => w.DeletedAt == null && w.TareaId == null)
                                 .Sum(w => (decimal?)w.Horas) ?? 0m
            })
            .ToListAsync(ct);

        var resultado = new List<TrabajoAgregable>(tareas.Count + casos.Count);

        resultado.AddRange(tareas.Select(t => new TrabajoAgregable(
            t.HistoriaId, t.Estado, t.FechaInicioPlan, t.FechaFinPlan, t.Horas)));

        resultado.AddRange(casos.Select(c => new TrabajoAgregable(
            c.HistoriaId, HistoriaCalculos.EstadoTrabajoDeCaso(c.Estado),
            c.FechaInicioPlan, c.FechaFinPlan, c.Horas)));

        return resultado;
    }

    // ───────────────────────── Proyección de casos ─────────────────────────

    /// <summary>Proyecta casos al DTO liviano que consume el árbol de ItalJira.</summary>
    private async Task<IReadOnlyList<ItalJiraCasoDto>> ProyectarCasosAsync(
        IQueryable<Ticket> universo, CancellationToken ct)
    {
        var filas = await universo
            .Where(t => t.DeletedAt == null)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Select(t => new
            {
                t.Id, t.Codigo, t.Titulo, t.Tipo, t.Estado, t.Prioridad,
                t.AssignedToUserGuid, t.HistoriaId, t.HorasEstimadas,
                t.FechaInicioPlan, t.FechaFinPlan, t.FechaLimite, t.CreatedAt,
                Horas = t.Tiempos.Where(w => w.DeletedAt == null).Sum(w => (decimal?)w.Horas) ?? 0m,
                Tareas = t.Tareas.Count(x => x.DeletedAt == null)
            })
            .ToListAsync(ct);

        var nombres = await NombresPorGuidAsync(
            filas.Where(f => f.AssignedToUserGuid.HasValue).Select(f => f.AssignedToUserGuid!.Value), ct);

        return filas.Select(f => new ItalJiraCasoDto(
            f.Id, f.Codigo, f.Titulo, f.Tipo, f.Estado, f.Prioridad,
            f.AssignedToUserGuid,
            f.AssignedToUserGuid.HasValue ? nombres.GetValueOrDefault(f.AssignedToUserGuid.Value) : null,
            f.HistoriaId, f.HorasEstimadas, f.Horas,
            f.FechaInicioPlan, f.FechaFinPlan, f.FechaLimite, f.CreatedAt, f.Tareas)).ToList();
    }

    // ───────────────────────── Proyección de tareas por historia ─────────────────────────

    /// <summary>
    /// Tareas de varias historias en una sola pasada, indexadas por historia.
    /// Reusa la proyección canónica de tareas (<c>ITicketTareaService</c>) a través de la misma
    /// consulta base, para no tener dos formas de leer <c>ticket_tareas</c>.
    /// </summary>
    private async Task<Dictionary<long, List<TicketTareaDto>>> ProyectarTareasDeHistoriasAsync(
        IReadOnlyCollection<long> historiaIds, CancellationToken ct)
    {
        var resultado = new Dictionary<long, List<TicketTareaDto>>();
        if (historiaIds.Count == 0) return resultado;

        var ids = historiaIds.ToList();

        var filas = await _ctx.TicketTareas.AsNoTracking()
            .Where(t => t.HistoriaId != null && ids.Contains(t.HistoriaId.Value) && t.DeletedAt == null)
            .OrderBy(t => t.Estado).ThenBy(t => t.Orden).ThenBy(t => t.Id)
            .Select(t => new
            {
                t.Id, t.TicketId, t.HistoriaId, t.Codigo, t.Tipo, t.Estado, t.Prioridad,
                t.Titulo, t.Descripcion, t.AsignadoUserGuid, t.ParentTareaId, t.Orden,
                t.HorasEstimadas, t.FechaInicioPlan, t.FechaFinPlan,
                t.FechaInicioReal, t.FechaFinReal, t.Etiquetas, t.CreatedAt, t.CreatedByUserId,
                CodigoCaso = t.Ticket != null ? t.Ticket.Codigo : null,
                HorasRegistradas = t.Tiempos.Where(w => w.DeletedAt == null).Sum(w => (decimal?)w.Horas) ?? 0m,
                Subtareas = _ctx.TicketTareas.Count(s => s.ParentTareaId == t.Id && s.DeletedAt == null)
            })
            .ToListAsync(ct);

        var nombres = await NombresPorGuidAsync(
            filas.Where(f => f.AsignadoUserGuid.HasValue).Select(f => f.AsignadoUserGuid!.Value), ct);
        var autores = await NombresPorCedulaAsync(filas.Select(f => f.CreatedByUserId), ct);

        foreach (var f in filas)
        {
            var dto = new TicketTareaDto(
                f.Id, f.TicketId, f.Codigo, f.Tipo, f.Estado, f.Prioridad, f.Titulo, f.Descripcion,
                f.AsignadoUserGuid,
                f.AsignadoUserGuid.HasValue ? nombres.GetValueOrDefault(f.AsignadoUserGuid.Value) : null,
                f.ParentTareaId, f.Orden, f.HorasEstimadas, f.HorasRegistradas,
                f.FechaInicioPlan, f.FechaFinPlan, f.FechaInicioReal, f.FechaFinReal,
                f.Etiquetas, f.CreatedAt, autores.GetValueOrDefault(f.CreatedByUserId),
                f.Subtareas, f.HistoriaId, f.CodigoCaso);

            if (!resultado.TryGetValue(f.HistoriaId!.Value, out var lista))
                resultado[f.HistoriaId!.Value] = lista = new List<TicketTareaDto>();
            lista.Add(dto);
        }

        return resultado;
    }

    // ───────────────────────────── BACKLOG ─────────────────────────────

    public async Task<ItalJiraBacklogDto> GetBacklogAsync(ItalJiraFiltro filtro, CancellationToken ct)
    {
        if (!PuedeGestionar())
            return new ItalJiraBacklogDto(
                Array.Empty<HistoriaDetalleDto>(), Array.Empty<ItalJiraCasoDto>(),
                Array.Empty<TicketTareaDto>(), ResumenVacio());

        var historias = await ProyectarHistoriasAsync(AplicarFiltro(filtro), ct);
        var ids = historias.Select(h => h.Id).ToList();

        var tareasPorHistoria = await ProyectarTareasDeHistoriasAsync(ids, ct);

        var casosDeHistorias = ids.Count == 0
            ? Array.Empty<ItalJiraCasoDto>()
            : await ProyectarCasosAsync(
                _ctx.Tickets.AsNoTracking().Where(t => t.HistoriaId != null && ids.Contains(t.HistoriaId.Value)), ct);
        var casosPorHistoria = casosDeHistorias
            .Where(c => c.HistoriaId.HasValue)
            .GroupBy(c => c.HistoriaId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ItalJiraCasoDto>)g.ToList());

        var detalles = historias.Select(h => new HistoriaDetalleDto(
            h,
            tareasPorHistoria.GetValueOrDefault(h.Id) is { } ts
                ? (IReadOnlyList<TicketTareaDto>)ts
                : Array.Empty<TicketTareaDto>(),
            casosPorHistoria.GetValueOrDefault(h.Id) ?? Array.Empty<ItalJiraCasoDto>()
        )).ToList();

        // La bandeja de entrada: lo que registran los usuarios nace SIEMPRE sin historia.
        var casosSinHistoria = await ProyectarCasosAsync(
            _ctx.Tickets.AsNoTracking().Where(t => t.HistoriaId == null), ct);

        var tareasSinHistoria = await ProyectarTareasSinHistoriaAsync(ct);

        var resumen = await ArmarResumenAsync(historias, casosSinHistoria.Count, ct);

        return new ItalJiraBacklogDto(detalles, casosSinHistoria, tareasSinHistoria, resumen);
    }

    /// <summary>
    /// Tareas nacidas en desarrollo que todavía no se agruparon (ni historia ni caso).
    /// Incluye las subtareas: si se filtraran por <c>ParentTareaId == null</c>, el trabajo que cae
    /// acá al borrar una historia quedaría invisible en las tres pantallas. El front las anida.
    /// </summary>
    private async Task<IReadOnlyList<TicketTareaDto>> ProyectarTareasSinHistoriaAsync(CancellationToken ct)
    {
        var filas = await _ctx.TicketTareas.AsNoTracking()
            .Where(t => t.HistoriaId == null && t.TicketId == null && t.DeletedAt == null)
            .OrderBy(t => t.Estado).ThenBy(t => t.Orden).ThenBy(t => t.Id)
            .Select(t => new
            {
                t.Id, t.TicketId, t.HistoriaId, t.Codigo, t.Tipo, t.Estado, t.Prioridad,
                t.Titulo, t.Descripcion, t.AsignadoUserGuid, t.ParentTareaId, t.Orden,
                t.HorasEstimadas, t.FechaInicioPlan, t.FechaFinPlan,
                t.FechaInicioReal, t.FechaFinReal, t.Etiquetas, t.CreatedAt, t.CreatedByUserId,
                HorasRegistradas = t.Tiempos.Where(w => w.DeletedAt == null).Sum(w => (decimal?)w.Horas) ?? 0m,
                Subtareas = _ctx.TicketTareas.Count(s => s.ParentTareaId == t.Id && s.DeletedAt == null)
            })
            .ToListAsync(ct);

        var nombres = await NombresPorGuidAsync(
            filas.Where(f => f.AsignadoUserGuid.HasValue).Select(f => f.AsignadoUserGuid!.Value), ct);
        var autores = await NombresPorCedulaAsync(filas.Select(f => f.CreatedByUserId), ct);

        return filas.Select(f => new TicketTareaDto(
            f.Id, f.TicketId, f.Codigo, f.Tipo, f.Estado, f.Prioridad, f.Titulo, f.Descripcion,
            f.AsignadoUserGuid,
            f.AsignadoUserGuid.HasValue ? nombres.GetValueOrDefault(f.AsignadoUserGuid.Value) : null,
            f.ParentTareaId, f.Orden, f.HorasEstimadas, f.HorasRegistradas,
            f.FechaInicioPlan, f.FechaFinPlan, f.FechaInicioReal, f.FechaFinReal,
            f.Etiquetas, f.CreatedAt, autores.GetValueOrDefault(f.CreatedByUserId),
            f.Subtareas, f.HistoriaId, null)).ToList();
    }

    // ───────────────────────────── TABLERO ─────────────────────────────

    public async Task<ItalJiraTableroDto> GetTableroAsync(ItalJiraFiltro filtro, CancellationToken ct)
    {
        if (!PuedeGestionar())
            return new ItalJiraTableroDto(Array.Empty<ItalJiraTableroColumnaDto>(), ResumenVacio());

        var historias = await ProyectarHistoriasAsync(AplicarFiltro(filtro), ct);

        var columnas = HistoriaEstados.Columnas
            .Select(estado => new ItalJiraTableroColumnaDto(
                estado,
                historias.Where(h => h.Estado.Equals(estado, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(h => h.Orden).ThenBy(h => h.Id)
                         .ToList()))
            .ToList();

        var casosSinHistoria = await _ctx.Tickets.AsNoTracking()
            .CountAsync(t => t.HistoriaId == null && t.DeletedAt == null, ct);

        return new ItalJiraTableroDto(columnas, await ArmarResumenAsync(historias, casosSinHistoria, ct));
    }

    // ───────────────────────────── ROADMAP ─────────────────────────────

    public async Task<ItalJiraRoadmapDto> GetRoadmapAsync(ItalJiraFiltro filtro, CancellationToken ct)
    {
        if (!PuedeGestionar())
            return new ItalJiraRoadmapDto(null, null, Array.Empty<ItalJiraRoadmapItemDto>());

        var historias = await ProyectarHistoriasAsync(AplicarFiltro(filtro), ct);
        var ids = historias.Select(h => h.Id).ToList();

        var tareasPorHistoria = await ProyectarTareasDeHistoriasAsync(ids, ct);
        var casosDeHistorias = ids.Count == 0
            ? Array.Empty<ItalJiraCasoDto>()
            : await ProyectarCasosAsync(
                _ctx.Tickets.AsNoTracking().Where(t => t.HistoriaId != null && ids.Contains(t.HistoriaId.Value)), ct);

        var casosPorHistoria = casosDeHistorias
            .Where(c => c.HistoriaId.HasValue)
            .GroupBy(c => c.HistoriaId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = historias.Select(h =>
        {
            var barras = new List<ItalJiraRoadmapBarraDto>();

            foreach (var t in tareasPorHistoria.GetValueOrDefault(h.Id) ?? new List<TicketTareaDto>())
                barras.Add(new ItalJiraRoadmapBarraDto(
                    "TAREA", t.Id, t.Codigo, t.Titulo, t.Estado, t.Prioridad, t.AsignadoNombre,
                    t.FechaInicioPlan, t.FechaFinPlan));

            foreach (var c in casosPorHistoria.GetValueOrDefault(h.Id) ?? new List<ItalJiraCasoDto>())
                barras.Add(new ItalJiraRoadmapBarraDto(
                    "CASO", c.Id, c.Codigo, c.Titulo, c.Estado, c.Prioridad, c.AssignedToNombre,
                    c.FechaInicioPlan, c.FechaFinPlan));

            return new ItalJiraRoadmapItemDto(h, barras);
        }).ToList();

        // Ventana visible: los extremos de todo lo dibujable. Sin fechas ⇒ el front decide.
        var (desde, hasta) = HistoriaCalculos.RangoPlanDerivado(
            items.Select(i => (i.Historia.InicioEfectivo, i.Historia.FinEfectivo)));

        return new ItalJiraRoadmapDto(desde, hasta, items);
    }

    // ───────────────────────────── RESUMEN ─────────────────────────────

    private static ItalJiraResumenDto ResumenVacio() =>
        new(0, 0, 0, 0, 0, 0, 0m, null);

    private async Task<ItalJiraResumenDto> ArmarResumenAsync(
        IReadOnlyList<HistoriaDto> historias, int casosSinHistoria, CancellationToken ct)
    {
        var tareas = await _ctx.TicketTareas.AsNoTracking()
            .Where(t => t.DeletedAt == null)
            .GroupBy(t => t.Estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        var totalTareas = tareas.Sum(t => t.Total);
        var listas = tareas.Where(t => TicketTareaEstados.EsTerminal(t.Estado)).Sum(t => t.Total);

        return new ItalJiraResumenDto(
            Historias:         historias.Count,
            HistoriasEnCurso:  historias.Count(h => !TicketTareaEstados.EsTerminal(h.Estado) &&
                                                    !h.Estado.Equals(HistoriaEstados.Backlog, StringComparison.OrdinalIgnoreCase)),
            HistoriasListas:   historias.Count(h => TicketTareaEstados.EsTerminal(h.Estado)),
            Tareas:            totalTareas,
            TareasListas:      listas,
            CasosSinHistoria:  casosSinHistoria,
            HorasRegistradas:  historias.Sum(h => h.HorasRegistradas),
            HorasEstimadas:    historias.Any(h => h.HorasEstimadas.HasValue)
                                   ? historias.Sum(h => h.HorasEstimadas ?? 0m)
                                   : null);
    }
}
