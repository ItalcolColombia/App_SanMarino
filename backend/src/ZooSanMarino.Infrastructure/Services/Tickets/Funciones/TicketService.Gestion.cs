using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Gestión del caso tipo tablero (perfil administrador): prioridad, responsable, planificación,
/// arrastre entre columnas, tablero kanban, roadmap, línea de tiempo y métricas de tiempos.
/// </summary>
/// <remarks>
/// Partial de <see cref="TicketService"/> — misma clase, mismo namespace plano, DI intacta.
/// Todo el filtrado y la agregación se resuelve en la BD: el backend orquesta, la BD filtra.
/// </remarks>
public partial class TicketService
{
    /// <summary>Permiso para operar sobre casos ajenos (resolutor o administrador).</summary>
    private bool PuedeGestionar() =>
        EsSuperAdmin() || _currentUser.Permissions.Contains("tickets.gestionar", StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Carga el caso y valida que el usuario actual pueda gestionarlo. Devuelve null si no existe
    /// (el controller responde 404) y lanza si existe pero no le corresponde tocarlo.
    /// </summary>
    private async Task<Ticket?> CargarParaGestionAsync(long id, CancellationToken ct)
    {
        var ticket = await _ctx.Tickets.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (ticket is null) return null;

        if (!PuedeGestionar())
            throw new InvalidOperationException("No tenés permisos para gestionar este caso.");
        if (EsSolicitante(ticket))
            throw new InvalidOperationException("Sos el solicitante de este caso; lo gestiona el equipo que atiende.");

        return ticket;
    }

    /// <summary>Registra una nota de sistema en la bitácora (alimenta la línea de tiempo).</summary>
    private void RegistrarEventoSistema(long ticketId, string tipoEvento, string texto, DateTime now) =>
        _ctx.TicketNotas.Add(new TicketNota
        {
            TicketId   = ticketId,
            UserId     = _currentUser.UserId,
            Nota       = texto,
            TipoEvento = tipoEvento,
            EsInterna  = false,
            CreatedAt  = now
        });

    // ───────────────────────────── PRIORIDAD ─────────────────────────────

    public async Task<TicketDetailDto?> CambiarPrioridadAsync(long id, CambiarPrioridadRequest req, CancellationToken ct)
    {
        if (!TicketPrioridades.EsValida(req.Prioridad))
            throw new InvalidOperationException("Prioridad inválida. Use: BAJA, MEDIA, ALTA o CRITICA.");

        var ticket = await CargarParaGestionAsync(id, ct);
        if (ticket is null) return null;

        var nueva = req.Prioridad.ToUpperInvariant();
        if (!string.Equals(ticket.Prioridad, nueva, StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTime.UtcNow;
            var anterior = ticket.Prioridad;
            ticket.Prioridad = nueva;
            ticket.UpdatedByUserId = _currentUser.UserId;
            ticket.UpdatedAt = now;

            RegistrarEventoSistema(ticket.Id, TicketNotaEventos.Prioridad,
                $"Prioridad: {anterior} → {nueva}.", now);
            await _ctx.SaveChangesAsync(ct);
        }

        return await GetByIdInternalAsync(id, ct);
    }

    // ───────────────────────────── RESPONSABLE ─────────────────────────────

    public async Task<TicketDetailDto?> CambiarAsignadoAsync(long id, CambiarAsignadoRequest req, CancellationToken ct)
    {
        var ticket = await CargarParaGestionAsync(id, ct);
        if (ticket is null) return null;

        if (req.AsignadoUserGuid == Guid.Empty)
            throw new InvalidOperationException("Indicá el nuevo responsable del caso.");

        var destino = await _ctx.Set<User>().AsNoTracking()
            .Where(u => u.Id == req.AsignadoUserGuid)
            .Select(u => new { u.Id, u.firstName, u.surName })
            .FirstOrDefaultAsync(ct);
        if (destino is null)
            throw new InvalidOperationException("El usuario indicado como responsable no existe.");

        if (ticket.AssignedToUserGuid == req.AsignadoUserGuid)
            return await GetByIdInternalAsync(id, ct);

        var now = DateTime.UtcNow;
        var anteriorNombre = await ResolveNombrePorGuidAsync(ticket.AssignedToUserGuid, ct);
        var nuevoNombre = $"{destino.firstName} {destino.surName}".Trim();

        ticket.AssignedToUserGuid = req.AsignadoUserGuid;
        ticket.AssignedToUserId = null;   // el int es legacy: se limpia para no dejar dos verdades
        ticket.UpdatedByUserId = _currentUser.UserId;
        ticket.UpdatedAt = now;

        var texto = string.IsNullOrWhiteSpace(req.Nota)
            ? $"Responsable: {anteriorNombre ?? "sin asignar"} → {nuevoNombre}."
            : req.Nota.Trim();
        RegistrarEventoSistema(ticket.Id, TicketNotaEventos.Asignacion, texto, now);

        await _ctx.SaveChangesAsync(ct);

        // Avisar al nuevo responsable. Si la cola falla, no bloquea la reasignación.
        try
        {
            var (email, nombre) = await ResolveSolicitanteEmailAsync(req.AsignadoUserGuid, 0, ct);
            if (!string.IsNullOrWhiteSpace(email))
            {
                var asignadorNombre = await ResolveNombrePorGuidAsync(_currentUser.UserGuid, ct);
                var body = TicketEmailTemplates.Asignado(ticket, nombre, asignadorNombre,
                    _logoUrl, _brandName, BrandLine, _applicationUrl);
                await _emailQueue.EnqueueEmailAsync(email!,
                    $"[{ticket.Codigo}] Te asignaron un caso", body, "ticket_asignado",
                    $"{{\"ticketId\":{ticket.Id},\"codigo\":\"{ticket.Codigo}\"}}");
            }
        }
        catch { /* la cola no bloquea la gestión */ }

        return await GetByIdInternalAsync(id, ct);
    }

    // ───────────────────────────── PLANIFICACIÓN ─────────────────────────────

    public async Task<TicketDetailDto?> ActualizarPlanificacionAsync(
        long id, ActualizarPlanificacionRequest req, CancellationToken ct)
    {
        var ticket = await CargarParaGestionAsync(id, ct);
        if (ticket is null) return null;

        if (req.HorasEstimadas is < 0)
            throw new InvalidOperationException("Las horas estimadas no pueden ser negativas.");

        var inicio = req.LimpiarFechaInicioPlan ? null : req.FechaInicioPlan ?? ticket.FechaInicioPlan;
        var fin    = req.LimpiarFechaFinPlan    ? null : req.FechaFinPlan    ?? ticket.FechaFinPlan;
        if (inicio is not null && fin is not null && fin < inicio)
            throw new InvalidOperationException("La fecha de fin planificada no puede ser anterior a la de inicio.");

        var now = DateTime.UtcNow;
        ticket.FechaInicioPlan = inicio;
        ticket.FechaFinPlan    = fin;
        ticket.FechaLimite     = req.LimpiarFechaLimite    ? null : req.FechaLimite    ?? ticket.FechaLimite;
        ticket.HorasEstimadas  = req.LimpiarHorasEstimadas ? null : req.HorasEstimadas ?? ticket.HorasEstimadas;
        ticket.UpdatedByUserId = _currentUser.UserId;
        ticket.UpdatedAt = now;

        RegistrarEventoSistema(ticket.Id, TicketNotaEventos.Planificacion,
            DescribirPlanificacion(ticket), now);

        await _ctx.SaveChangesAsync(ct);
        return await GetByIdInternalAsync(id, ct);
    }

    private static string DescribirPlanificacion(Ticket t)
    {
        var partes = new List<string>();
        if (t.FechaInicioPlan is { } i) partes.Add($"inicio {i:dd/MM/yyyy}");
        if (t.FechaFinPlan is { } f)    partes.Add($"fin {f:dd/MM/yyyy}");
        if (t.FechaLimite is { } l)     partes.Add($"compromiso {l:dd/MM/yyyy}");
        if (t.HorasEstimadas is { } h)  partes.Add($"estimación {h:0.##} h");
        return partes.Count == 0
            ? "Planificación limpiada."
            : $"Planificación: {string.Join(" · ", partes)}.";
    }

    // ───────────────────────────── MOVER EN EL TABLERO ─────────────────────────────

    public async Task<TicketDetailDto?> MoverAsync(long id, MoverTicketRequest req, CancellationToken ct)
    {
        if (!TicketEstados.EsValido(req.Estado))
            throw new InvalidOperationException("Estado inválido.");

        var destino = req.Estado.ToUpperInvariant();
        var ticket = await CargarParaGestionAsync(id, ct);
        if (ticket is null) return null;

        // Los dos estados con ceremonia propia no se alcanzan arrastrando: SOLUCIONADO exige la
        // descripción de la solución y CERRADO lo confirma el solicitante.
        if (destino == TicketEstados.Solucionado && !string.Equals(ticket.Estado, destino, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Para solucionar el caso usá la acción «Solucionar»: hay que registrar la descripción de la solución.");
        if (destino == TicketEstados.Cerrado && !string.Equals(ticket.Estado, destino, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El cierre lo confirma el solicitante.");

        var cambiaEstado = !string.Equals(ticket.Estado, destino, StringComparison.OrdinalIgnoreCase);
        if (cambiaEstado && !TicketEstados.PuedeTransicionar(ticket.Estado, destino))
            throw new InvalidOperationException($"Transición inválida: {ticket.Estado} → {destino}.");

        var now = DateTime.UtcNow;
        var estadoAnterior = ticket.Estado;

        // Reordenamiento: se traen solo (id, estado, orden) de los casos vivos — proyección liviana.
        var posiciones = await _ctx.Tickets.AsNoTracking()
            .Where(x => x.DeletedAt == null && (x.Estado == estadoAnterior || x.Estado == destino))
            .Select(x => new TicketTareaCalculos.Posicion(x.Id, x.Estado, x.OrdenTablero))
            .ToListAsync(ct);

        var cambios = TicketTareaCalculos.Reordenar(posiciones, id, destino, req.Indice);
        if (cambios.Count > 0)
        {
            var ids = cambios.Select(c => c.Id).ToList();
            var afectados = await _ctx.Tickets.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
            foreach (var c in cambios)
            {
                var afectado = afectados.FirstOrDefault(a => a.Id == c.Id);
                if (afectado is null) continue;
                afectado.OrdenTablero = c.Orden;
                if (afectado.Id == id) afectado.Estado = c.Estado;
            }
        }
        else
        {
            ticket.OrdenTablero = req.Indice < 0 ? 0 : req.Indice;
            ticket.Estado = destino;
        }

        if (cambiaEstado)
        {
            ticket.FechaPrimeraApertura ??= now;
            _ctx.TicketNotas.Add(new TicketNota
            {
                TicketId         = ticket.Id,
                UserId           = _currentUser.UserId,
                Nota             = string.IsNullOrWhiteSpace(req.Nota)
                    ? $"Estado cambiado a {destino} desde el tablero."
                    : req.Nota.Trim(),
                EstadoResultante = destino,
                CreatedAt        = now
            });
        }

        ticket.UpdatedByUserId = _currentUser.UserId;
        ticket.UpdatedAt = now;
        await _ctx.SaveChangesAsync(ct);

        return await GetByIdInternalAsync(id, ct);
    }

    // ───────────────────────────── TABLERO ─────────────────────────────

    public async Task<TicketTableroDto> GetTableroAsync(TicketTableroFiltro filtro, CancellationToken ct)
    {
        var query = AplicarFiltroTablero(filtro);
        var max = filtro.MaxPorColumna is < 1 or > 200 ? 60 : filtro.MaxPorColumna;

        // 1) Conteo real por columna (la BD agrupa; no se traen filas para contar).
        var conteos = await query
            .GroupBy(x => x.Estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToDictionaryAsync(g => g.Estado, g => g.Total, StringComparer.OrdinalIgnoreCase, ct);

        // 2) Las primeras `max` tarjetas de cada columna, en una sola pasada de mapeo de identidad.
        var filas = new List<TicketRow>();
        foreach (var estado in TicketEstados.ColumnasTablero)
        {
            if (!conteos.ContainsKey(estado)) continue;
            var deColumna = await ProyectarFilasAsync(
                query.Where(x => x.Estado == estado)
                     .OrderBy(x => x.OrdenTablero)
                     .ThenByDescending(x => x.CreatedAt)
                     .Take(max),
                ct);
            filas.AddRange(deColumna);
        }

        var items = await MapearItemsAsync(filas, ct);
        var porEstado = items.GroupBy(i => i.Estado, StringComparer.OrdinalIgnoreCase)
                             .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var columnas = TicketEstados.ColumnasTablero.Select(estado => new TicketTableroColumnaDto(
            estado,
            TicketTimelineCalculos.EtiquetaEstado(estado),
            conteos.GetValueOrDefault(estado),
            porEstado.GetValueOrDefault(estado) ?? new List<TicketListItemDto>())).ToList();

        return new TicketTableroDto(columnas, await ConstruirResumenTableroAsync(query, conteos, ct));
    }

    /// <summary>Indicadores de cabecera del tablero, todos resueltos con agregados en la BD.</summary>
    private async Task<TicketTableroResumenDto> ConstruirResumenTableroAsync(
        IQueryable<Ticket> query, Dictionary<string, int> conteos, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var limitePorVencer = ahora.AddHours(TicketMetricasCalculos.HorasUmbralPorVencer);

        var vencidos = await query.CountAsync(x =>
            x.FechaLimite != null && x.FechaSolucion == null && x.FechaLimite < ahora, ct);
        var porVencer = await query.CountAsync(x =>
            x.FechaLimite != null && x.FechaSolucion == null &&
            x.FechaLimite >= ahora && x.FechaLimite <= limitePorVencer, ct);
        var sinAsignar = await query.CountAsync(x => x.AssignedToUserGuid == null, ct);

        var ids = query.Select(x => x.Id);
        var horas = await _ctx.TicketTiempos.AsNoTracking()
            .Where(t => t.DeletedAt == null && ids.Contains(t.TicketId))
            .SumAsync(t => (decimal?)t.Horas, ct) ?? 0m;

        var enCurso = TicketEstados.FasesTrabajo.Sum(f => conteos.GetValueOrDefault(f));

        return new TicketTableroResumenDto(
            Total:         conteos.Values.Sum(),
            Abiertos:      conteos.GetValueOrDefault(TicketEstados.Abierto),
            EnCurso:       enCurso,
            Solucionados:  conteos.GetValueOrDefault(TicketEstados.Solucionado),
            Cerrados:      conteos.GetValueOrDefault(TicketEstados.Cerrado),
            Vencidos:      vencidos,
            PorVencer:     porVencer,
            SinAsignar:    sinAsignar,
            HorasRegistradas: horas);
    }

    /// <summary>
    /// Alcance del tablero: administración global (todas las empresas) para <c>tickets.admin</c>;
    /// para un resolutor, solo los casos que tiene asignados. Fail-closed: sin Guid ni permiso,
    /// no se devuelve nada.
    /// </summary>
    private IQueryable<Ticket> AplicarFiltroTablero(TicketTableroFiltro filtro)
    {
        var query = _ctx.Tickets.AsNoTracking().Where(x => x.DeletedAt == null);

        if (!EsSuperAdmin())
        {
            var miGuid = _currentUser.UserGuid;
            query = miGuid.HasValue
                ? query.Where(x => x.AssignedToUserGuid == miGuid.Value)
                : query.Where(_ => false);
        }

        if (filtro.PaisId.HasValue)    query = query.Where(x => x.PaisId == filtro.PaisId.Value);
        if (filtro.CompanyId.HasValue) query = query.Where(x => x.CompanyId == filtro.CompanyId.Value);
        if (filtro.AssignedToGuid.HasValue)
            query = query.Where(x => x.AssignedToUserGuid == filtro.AssignedToGuid.Value);

        return ApplyFilters(query, new TicketSearchRequest(
            Anio: filtro.Anio, Tipo: filtro.Tipo, Prioridad: filtro.Prioridad, Texto: filtro.Texto));
    }

    // ───────────────────────────── ROADMAP ─────────────────────────────

    /// <summary>Tope de casos del roadmap: más barras que esto no se leen en pantalla.</summary>
    private const int MaxItemsRoadmap = 200;

    public async Task<TicketRoadmapDto> GetRoadmapAsync(TicketTableroFiltro filtro, CancellationToken ct)
    {
        var query = AplicarFiltroTablero(filtro);

        var casos = await query
            .OrderBy(x => x.FechaInicioPlan ?? DateOnly.FromDateTime(x.CreatedAt))
            .ThenByDescending(x => x.CreatedAt)
            .Take(MaxItemsRoadmap)
            .Select(x => new
            {
                x.Id, x.Codigo, x.Titulo, x.Tipo, x.Estado, x.Prioridad, x.CompanyId,
                x.AssignedToUserGuid, x.FechaInicioPlan, x.FechaFinPlan, x.FechaLimite,
                x.CreatedAt, x.FechaSolucion,
                Tareas = x.Tareas.Where(t => t.DeletedAt == null)
                    .OrderBy(t => t.FechaInicioPlan ?? DateOnly.FromDateTime(t.CreatedAt))
                    .Select(t => new
                    {
                        t.Id, t.Codigo, t.Titulo, t.Tipo, t.Estado, t.AsignadoUserGuid,
                        t.FechaInicioPlan, t.FechaFinPlan
                    }).ToList(),
                TotalTareas = x.Tareas.Count(t => t.DeletedAt == null),
                TareasListas = x.Tareas.Count(t => t.DeletedAt == null && t.Estado == TicketTareaEstados.Listo)
            })
            .ToListAsync(ct);

        var refs = new List<(Guid, int)>();
        foreach (var c in casos)
        {
            if (c.AssignedToUserGuid.HasValue) refs.Add((c.AssignedToUserGuid.Value, c.CompanyId));
            foreach (var t in c.Tareas)
                if (t.AsignadoUserGuid.HasValue) refs.Add((t.AsignadoUserGuid.Value, c.CompanyId));
        }
        var (users, _) = await BuildUserInfoAsync(refs, ct);
        var ahora = DateTime.UtcNow;

        var items = casos.Select(c => new TicketRoadmapItemDto(
            c.Id, c.Codigo, c.Titulo, c.Tipo, c.Estado, c.Prioridad,
            NombreDe(users, c.AssignedToUserGuid),
            c.FechaInicioPlan, c.FechaFinPlan, c.FechaLimite, c.CreatedAt, c.FechaSolucion,
            TicketMetricasCalculos.PorcentajeAvanceTareas(c.TotalTareas, c.TareasListas),
            TicketMetricasCalculos.EstadoSla(c.FechaLimite, c.FechaSolucion, ahora),
            c.Tareas.Select(t => new TicketRoadmapTareaDto(
                t.Id, t.Codigo, t.Titulo, t.Tipo, t.Estado,
                NombreDe(users, t.AsignadoUserGuid), t.FechaInicioPlan, t.FechaFinPlan)).ToList()))
            .ToList();

        // Ventana visible: se calcula sobre lo que realmente tiene fechas.
        var fechas = items
            .SelectMany(i => new[] { i.FechaInicioPlan, i.FechaFinPlan }
                .Concat(i.Tareas.SelectMany(t => new[] { t.FechaInicioPlan, t.FechaFinPlan })))
            .Where(f => f.HasValue).Select(f => f!.Value).ToList();

        return new TicketRoadmapDto(
            fechas.Count == 0 ? null : fechas.Min(),
            fechas.Count == 0 ? null : fechas.Max(),
            items);
    }

    // ───────────────────────────── LÍNEA DE TIEMPO ─────────────────────────────

    public async Task<IReadOnlyList<TicketTimelineEventoDto>> GetTimelineAsync(long id, CancellationToken ct)
    {
        var t = await _ctx.Tickets.AsNoTracking()
            .Where(x => x.Id == id && x.DeletedAt == null)
            .Select(x => new
            {
                x.Id, x.CompanyId, x.PaisId, x.Tipo, x.CreatedAt, x.CreatedByUserId, x.CreatedByUserGuid,
                x.AssignedToUserGuid, x.AssignedToUserId, x.SolicitanteUserGuid, x.SolicitanteUserId,
                x.FechaPrimeraApertura, x.FechaSolucion, x.SolucionDescripcion,
                x.FechaCierreSolicitante, x.FechaNotificacionCorreo, x.CorreoNotificadoA,
                Notas = x.Notas.Select(n => new
                    { n.Id, n.UserId, n.Nota, n.EstadoResultante, n.EsInterna, n.TipoEvento, n.CreatedAt }).ToList(),
                Adjuntos = x.Adjuntos.Select(a => new
                    { a.Id, a.Tipo, a.FileName, a.Titulo, a.CreatedByUserId, a.CreatedAt }).ToList(),
                Tareas = x.Tareas.Where(k => k.DeletedAt == null).Select(k => new
                    { k.Id, k.Codigo, k.Titulo, k.Estado, k.FechaFinReal, k.CreatedByUserId, k.CreatedAt }).ToList(),
                Tiempos = x.Tiempos.Where(w => w.DeletedAt == null).Select(w => new
                    { w.Id, w.Horas, w.Descripcion, w.UserGuid, w.UserId, w.CreatedAt }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (t is null) return Array.Empty<TicketTimelineEventoDto>();

        if (!await PuedeVerTicketAsync(t.PaisId, t.Tipo, t.CreatedByUserId, t.CreatedByUserGuid,
                                       t.AssignedToUserGuid, t.SolicitanteUserGuid, t.SolicitanteUserId, ct))
            return Array.Empty<TicketTimelineEventoDto>();

        // Identidad de todos los actores que aparecen en la línea de tiempo.
        var cedulas = t.Notas.Select(n => n.UserId)
            .Concat(t.Adjuntos.Select(a => a.CreatedByUserId))
            .Concat(t.Tareas.Select(k => k.CreatedByUserId))
            .Concat(t.Tiempos.Select(w => w.UserId))
            .Append(t.CreatedByUserId)
            .Append(t.AssignedToUserId ?? 0)
            .Append(t.SolicitanteUserId ?? 0)
            .Where(x => x != 0).Distinct().ToList();
        var cedInfo = await BuildNotaUserInfoAsync(cedulas, t.CompanyId, ct);

        var refs = new List<(Guid, int)>();
        if (t.CreatedByUserGuid.HasValue)   refs.Add((t.CreatedByUserGuid.Value, t.CompanyId));
        if (t.AssignedToUserGuid.HasValue)  refs.Add((t.AssignedToUserGuid.Value, t.CompanyId));
        if (t.SolicitanteUserGuid.HasValue) refs.Add((t.SolicitanteUserGuid.Value, t.CompanyId));
        var (users, _) = await BuildUserInfoAsync(refs, ct);

        string? PorCedula(int cedula) => cedula != 0 && cedInfo.TryGetValue(cedula, out var i) ? i.Nombre : null;
        string? PorGuid(Guid? g, int cedulaFallback) => NombreDe(users, g) ?? PorCedula(cedulaFallback);

        var creadoPor = PorGuid(t.CreatedByUserGuid, t.CreatedByUserId);
        var solicitante = t.SolicitanteUserGuid.HasValue || t.SolicitanteUserId.HasValue
            ? PorGuid(t.SolicitanteUserGuid, t.SolicitanteUserId ?? 0)
            : creadoPor;

        var cabecera = new TicketTimelineCalculos.CabeceraCaso(
            t.CreatedAt, creadoPor, solicitante,
            PorGuid(t.AssignedToUserGuid, t.AssignedToUserId ?? 0),
            t.FechaPrimeraApertura, t.FechaSolucion, t.SolucionDescripcion,
            t.FechaCierreSolicitante, t.FechaNotificacionCorreo, t.CorreoNotificadoA);

        // Las notas internas son del equipo: el solicitante que no gestiona no las ve.
        var incluirInternas = PuedeGestionar();

        var eventos = TicketTimelineCalculos.Construir(
            cabecera,
            t.Notas.Select(n => new TicketTimelineCalculos.NotaTimeline(
                n.Id, n.CreatedAt, n.Nota, n.EstadoResultante, n.EsInterna, n.TipoEvento, PorCedula(n.UserId))),
            t.Adjuntos.Select(a => new TicketTimelineCalculos.AdjuntoTimeline(
                a.Id, a.CreatedAt, a.Tipo, a.FileName ?? a.Titulo, PorCedula(a.CreatedByUserId))),
            t.Tareas.Select(k => new TicketTimelineCalculos.TareaTimeline(
                k.Id, k.CreatedAt, k.Codigo, k.Titulo, k.Estado, k.FechaFinReal, PorCedula(k.CreatedByUserId))),
            t.Tiempos.Select(w => new TicketTimelineCalculos.TiempoTimeline(
                w.Id, w.CreatedAt, w.Horas, w.Descripcion, PorGuid(w.UserGuid, w.UserId))),
            incluirInternas);

        return eventos.Select(e => new TicketTimelineEventoDto(
            e.Momento, e.Tipo, e.Titulo, e.Detalle, e.Autor, e.EstadoResultante, e.EsInterna, e.ReferenciaId))
            .ToList();
    }

    // ───────────────────────────── MÉTRICAS ─────────────────────────────

    public async Task<TicketMetricasDto?> GetMetricasAsync(long id, CancellationToken ct)
    {
        var t = await _ctx.Tickets.AsNoTracking()
            .Where(x => x.Id == id && x.DeletedAt == null)
            .Select(x => new
            {
                x.PaisId, x.Tipo, x.Estado, x.CreatedAt, x.CreatedByUserId, x.CreatedByUserGuid,
                x.AssignedToUserGuid, x.SolicitanteUserGuid, x.SolicitanteUserId,
                x.FechaPrimeraApertura, x.FechaSolucion, x.FechaCierreSolicitante,
                x.FechaLimite, x.HorasEstimadas,
                HorasRegistradas = x.Tiempos.Where(w => w.DeletedAt == null).Sum(w => (decimal?)w.Horas) ?? 0m,
                TotalTareas = x.Tareas.Count(k => k.DeletedAt == null),
                TareasListas = x.Tareas.Count(k => k.DeletedAt == null && k.Estado == TicketTareaEstados.Listo),
                Cambios = x.Notas.Where(n => n.EstadoResultante != null)
                    .Select(n => new { n.EstadoResultante, n.CreatedAt }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (t is null) return null;

        if (!await PuedeVerTicketAsync(t.PaisId, t.Tipo, t.CreatedByUserId, t.CreatedByUserGuid,
                                       t.AssignedToUserGuid, t.SolicitanteUserGuid, t.SolicitanteUserId, ct))
            return null;

        return ConstruirMetricas(
            t.CreatedAt, t.FechaPrimeraApertura, t.FechaSolucion, t.FechaCierreSolicitante,
            t.FechaLimite, t.Estado, t.HorasEstimadas, t.HorasRegistradas,
            t.TotalTareas, t.TareasListas,
            t.Cambios.Select(c => new TicketMetricasCalculos.CambioEstado(c.EstadoResultante!, c.CreatedAt)));
    }

    /// <summary>Ensambla el DTO de métricas delegando TODO el cálculo en <see cref="TicketMetricasCalculos"/>.</summary>
    private static TicketMetricasDto ConstruirMetricas(
        DateTime creado, DateTime? primeraApertura, DateTime? fechaSolucion, DateTime? fechaCierre,
        DateTime? fechaLimite, string estado, decimal? horasEstimadas, decimal horasRegistradas,
        int totalTareas, int tareasListas, IEnumerable<TicketMetricasCalculos.CambioEstado> cambios)
    {
        var ahora = DateTime.UtcNow;
        // El reloj de permanencia se detiene en el cierre: después ya no corre tiempo del caso.
        var corte = fechaCierre ?? ahora;

        return new TicketMetricasDto(
            TicketMetricasCalculos.HorasPrimeraRespuesta(creado, primeraApertura),
            TicketMetricasCalculos.HorasResolucion(creado, fechaSolucion, ahora),
            TicketMetricasCalculos.HorasConfirmacionCierre(fechaSolucion, fechaCierre),
            TicketMetricasCalculos.EstadoSla(fechaLimite, fechaSolucion, ahora),
            TicketMetricasCalculos.HorasParaVencer(fechaLimite, fechaSolucion, ahora),
            TicketMetricasCalculos.PorcentajeAvanceTareas(totalTareas, tareasListas),
            TicketMetricasCalculos.PorcentajeAvanceFlujo(estado),
            totalTareas, tareasListas, horasRegistradas, horasEstimadas,
            TicketMetricasCalculos.DesvioHoras(horasEstimadas, horasRegistradas),
            TicketMetricasCalculos.PermanenciaPorEstado(creado, cambios, corte)
                .Select(p => new TicketPermanenciaEstadoDto(p.Estado, p.Horas)).ToList());
    }

    // ───────────────────────────── SOLICITANTES ("a nombre de") ─────────────────────────────

    /// <summary>Tope del buscador de solicitantes: es un autocompletar, no un listado.</summary>
    private const int MaxSolicitantes = 30;

    public async Task<IReadOnlyList<SolicitanteCandidatoDto>> GetSolicitantesAsync(string? texto, CancellationToken ct)
    {
        // Fail-closed: sin el permiso global no se devuelve el padrón de usuarios.
        if (!EsSuperAdmin()) return Array.Empty<SolicitanteCandidatoDto>();

        var query = _ctx.Set<User>().AsNoTracking().Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.firstName, patron) ||
                EF.Functions.ILike(u.surName, patron) ||
                EF.Functions.ILike(u.cedula, patron) ||
                u.UserLogins.Any(ul => EF.Functions.ILike(ul.Login.email, patron)));
        }

        var rows = await query
            .OrderBy(u => u.firstName).ThenBy(u => u.surName)
            .Take(MaxSolicitantes)
            .Select(u => new
            {
                u.Id, u.firstName, u.surName, u.cedula,
                Email = u.UserLogins.Select(ul => ul.Login.email).FirstOrDefault(),
                Rol = u.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault(),
                Empresa = u.UserCompanies.Select(uc => uc.Company.Name).FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(u => new SolicitanteCandidatoDto(
            u.Id, $"{u.firstName} {u.surName}".Trim(), u.Email, u.Rol, u.Empresa, u.cedula)).ToList();
    }
}
