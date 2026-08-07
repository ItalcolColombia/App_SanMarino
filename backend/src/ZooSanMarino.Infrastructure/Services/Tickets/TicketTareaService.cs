using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Tareas de un caso y su registro de tiempos — el tablero tipo Jira del administrador.
/// </summary>
/// <remarks>
/// Archivo ANCLA del partial: campos, constructor, helpers de visibilidad/identidad y la interfaz.
/// El worklog vive en <c>Funciones/TicketTareaService.Tiempos.cs</c>.
/// El reordenamiento de tarjetas se delega en <see cref="TicketTareaCalculos"/> (puro y testeado).
/// </remarks>
public partial class TicketTareaService : ITicketTareaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;

    public TicketTareaService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    // ───────────────────────────── Visibilidad ─────────────────────────────

    /// <summary>Datos mínimos del caso necesarios para decidir visibilidad y auditoría.</summary>
    private sealed record CasoMeta(
        long Id, string? Codigo, int CompanyId, int PaisId, string Tipo,
        int CreatedByUserId, Guid? CreatedByUserGuid, Guid? AssignedToUserGuid,
        Guid? SolicitanteUserGuid, int? SolicitanteUserId);

    private Task<CasoMeta?> CargarCasoAsync(long ticketId, CancellationToken ct) =>
        _ctx.Tickets.AsNoTracking()
            .Where(x => x.Id == ticketId && x.DeletedAt == null)
            .Select(x => new CasoMeta(x.Id, x.Codigo, x.CompanyId, x.PaisId, x.Tipo,
                x.CreatedByUserId, x.CreatedByUserGuid, x.AssignedToUserGuid,
                x.SolicitanteUserGuid, x.SolicitanteUserId))
            .FirstOrDefaultAsync(ct)!;

    private bool EsSuperAdmin() =>
        _currentUser.Permissions.Contains("tickets.admin", StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Quién puede CREAR/EDITAR/MOVER tareas: el administrador global, un resolutor del módulo
    /// o el responsable del caso. El solicitante ve las tareas pero no las toca.
    /// </summary>
    private bool PuedeGestionar(CasoMeta caso) =>
        EsSuperAdmin()
        || _currentUser.Permissions.Contains("tickets.gestionar", StringComparer.OrdinalIgnoreCase)
        || (_currentUser.UserGuid.HasValue && caso.AssignedToUserGuid == _currentUser.UserGuid.Value);

    /// <summary>
    /// Quién puede VER las tareas: quien puede ver el caso — su creador, el solicitante a cuyo
    /// nombre se registró, el responsable, un resolutor del tipo/país o el administrador.
    /// Espeja <c>TicketService.PuedeVerTicketAsync</c>.
    /// </summary>
    private async Task<bool> PuedeVerAsync(CasoMeta caso, CancellationToken ct)
    {
        if (PuedeGestionar(caso)) return true;

        if (caso.CreatedByUserId != 0 && caso.CreatedByUserId == _currentUser.UserId) return true;
        if (caso.SolicitanteUserId is { } sol && sol != 0 && sol == _currentUser.UserId) return true;

        var miGuid = _currentUser.UserGuid;
        if (miGuid.HasValue)
        {
            if (caso.CreatedByUserGuid == miGuid.Value) return true;
            if (caso.SolicitanteUserGuid == miGuid.Value) return true;

            return await _ctx.TicketResolutores.AsNoTracking()
                .AnyAsync(r => r.UserId == miGuid.Value && r.Activo &&
                               r.Tipo == caso.Tipo &&
                               (r.PaisId == null || r.PaisId == caso.PaisId), ct);
        }

        return false;
    }

    /// <summary>Carga el caso exigiendo permiso de gestión. Null = no existe (404 en el controller).</summary>
    private async Task<CasoMeta?> CargarParaEscrituraAsync(long ticketId, CancellationToken ct)
    {
        var caso = await CargarCasoAsync(ticketId, ct);
        if (caso is null) return null;
        if (!PuedeGestionar(caso))
            throw new InvalidOperationException("No tenés permisos para gestionar las tareas de este caso.");
        return caso;
    }

    // ───────────────────────────── Identidad ─────────────────────────────

    /// <summary>Nombre completo por Guid, para los responsables de las tareas.</summary>
    private async Task<Dictionary<Guid, string>> NombresPorGuidAsync(
        IEnumerable<Guid> guids, CancellationToken ct)
    {
        var ids = guids.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return await _ctx.Set<User>().AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.firstName} {u.surName}".Trim(), ct);
    }

    /// <summary>Nombre completo por cédula (los autores se auditan con el int de <c>ICurrentUser</c>).</summary>
    private async Task<Dictionary<int, string>> NombresPorCedulaAsync(
        IEnumerable<int> userIds, CancellationToken ct)
    {
        var cedulas = userIds.Where(x => x != 0).Distinct().Select(x => x.ToString()).ToList();
        var result = new Dictionary<int, string>();
        if (cedulas.Count == 0) return result;

        var rows = await _ctx.Set<User>().AsNoTracking()
            .Where(u => cedulas.Contains(u.cedula))
            .Select(u => new { u.cedula, u.firstName, u.surName })
            .ToListAsync(ct);

        foreach (var r in rows)
            if (int.TryParse(r.cedula, out var ced))
                result[ced] = $"{r.firstName} {r.surName}".Trim();

        return result;
    }

    /// <summary>Deja constancia del cambio en la bitácora del caso (alimenta la línea de tiempo).</summary>
    private void RegistrarEventoTarea(long ticketId, string texto, DateTime now) =>
        _ctx.TicketNotas.Add(new TicketNota
        {
            TicketId   = ticketId,
            UserId     = _currentUser.UserId,
            Nota       = texto,
            TipoEvento = TicketNotaEventos.Tarea,
            EsInterna  = false,
            CreatedAt  = now
        });

    // ───────────────────────────── LISTAR ─────────────────────────────

    public async Task<IReadOnlyList<TicketTareaDto>> GetByTicketAsync(long ticketId, CancellationToken ct)
    {
        var caso = await CargarCasoAsync(ticketId, ct);
        if (caso is null || !await PuedeVerAsync(caso, ct))
            return Array.Empty<TicketTareaDto>();

        return await ProyectarTareasAsync(ticketId, ct);
    }

    /// <summary>
    /// Proyecta las tareas vivas del caso con sus horas registradas y su cantidad de subtareas.
    /// Las sumas y los conteos se resuelven como subconsultas agregadas en la BD.
    /// </summary>
    private async Task<List<TicketTareaDto>> ProyectarTareasAsync(long ticketId, CancellationToken ct)
    {
        var rows = await _ctx.TicketTareas.AsNoTracking()
            .Where(t => t.TicketId == ticketId && t.DeletedAt == null)
            .OrderBy(t => t.Estado).ThenBy(t => t.Orden).ThenBy(t => t.Id)
            .Select(t => new
            {
                t.Id, t.TicketId, t.Codigo, t.Tipo, t.Estado, t.Prioridad, t.Titulo, t.Descripcion,
                t.AsignadoUserGuid, t.ParentTareaId, t.Orden, t.HorasEstimadas,
                t.FechaInicioPlan, t.FechaFinPlan, t.FechaInicioReal, t.FechaFinReal,
                t.Etiquetas, t.CreatedAt, t.CreatedByUserId,
                HorasRegistradas = t.Tiempos.Where(w => w.DeletedAt == null).Sum(w => (decimal?)w.Horas) ?? 0m,
                Subtareas = _ctx.TicketTareas.Count(s => s.ParentTareaId == t.Id && s.DeletedAt == null)
            })
            .ToListAsync(ct);

        var nombres = await NombresPorGuidAsync(
            rows.Where(r => r.AsignadoUserGuid.HasValue).Select(r => r.AsignadoUserGuid!.Value), ct);
        var autores = await NombresPorCedulaAsync(rows.Select(r => r.CreatedByUserId), ct);

        return rows.Select(r => new TicketTareaDto(
            r.Id, r.TicketId, r.Codigo, r.Tipo, r.Estado, r.Prioridad, r.Titulo, r.Descripcion,
            r.AsignadoUserGuid,
            r.AsignadoUserGuid.HasValue ? nombres.GetValueOrDefault(r.AsignadoUserGuid.Value) : null,
            r.ParentTareaId, r.Orden, r.HorasEstimadas, r.HorasRegistradas,
            r.FechaInicioPlan, r.FechaFinPlan, r.FechaInicioReal, r.FechaFinReal,
            r.Etiquetas, r.CreatedAt, autores.GetValueOrDefault(r.CreatedByUserId),
            r.Subtareas)).ToList();
    }

    // ───────────────────────────── CREAR ─────────────────────────────

    public async Task<TicketTareaDto?> CreateAsync(long ticketId, CreateTicketTareaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo))
            throw new InvalidOperationException("El título de la tarea es requerido.");
        if (req.HorasEstimadas is < 0)
            throw new InvalidOperationException("Las horas estimadas no pueden ser negativas.");
        if (req.FechaInicioPlan is { } ini && req.FechaFinPlan is { } fin && fin < ini)
            throw new InvalidOperationException("La fecha de fin planificada no puede ser anterior a la de inicio.");

        var caso = await CargarParaEscrituraAsync(ticketId, ct);
        if (caso is null) return null;

        if (req.ParentTareaId is { } padreId)
        {
            var padreValido = await _ctx.TicketTareas.AsNoTracking()
                .AnyAsync(t => t.Id == padreId && t.TicketId == ticketId && t.DeletedAt == null, ct);
            if (!padreValido)
                throw new InvalidOperationException("La tarea padre no existe en este caso.");
        }

        var estado = TicketTareaCalculos.NormalizarEstado(req.Estado);
        var now = DateTime.UtcNow;

        // Códigos ya emitidos, incluidos los de tareas borradas: el correlativo no se reutiliza.
        var codigos = await _ctx.TicketTareas.AsNoTracking()
            .Where(t => t.TicketId == ticketId)
            .Select(t => t.Codigo)
            .ToListAsync(ct);

        // Entra al final de su columna.
        var orden = await _ctx.TicketTareas.AsNoTracking()
            .CountAsync(t => t.TicketId == ticketId && t.DeletedAt == null && t.Estado == estado, ct);

        var (inicioReal, finReal) = TicketTareaCalculos.SellarFechasReales(estado, null, null, now);

        var entity = new TicketTarea
        {
            TicketId         = ticketId,
            Codigo           = TicketTareaCalculos.GenerarCodigoTarea(
                                   caso.Codigo, ticketId, TicketTareaCalculos.SiguienteConsecutivo(codigos)),
            Tipo             = TicketTareaCalculos.NormalizarTipo(req.Tipo),
            Estado           = estado,
            Prioridad        = TicketTareaCalculos.NormalizarPrioridad(req.Prioridad),
            Titulo           = req.Titulo.Trim(),
            Descripcion      = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            AsignadoUserGuid = req.AsignadoUserGuid,
            ParentTareaId    = req.ParentTareaId,
            Orden            = orden,
            HorasEstimadas   = req.HorasEstimadas,
            FechaInicioPlan  = req.FechaInicioPlan,
            FechaFinPlan     = req.FechaFinPlan,
            FechaInicioReal  = inicioReal,
            FechaFinReal     = finReal,
            Etiquetas        = string.IsNullOrWhiteSpace(req.Etiquetas) ? null : req.Etiquetas.Trim(),
            CompanyId        = caso.CompanyId,
            CreatedByUserId  = _currentUser.UserId,
            CreatedAt        = now
        };

        _ctx.TicketTareas.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        // Sin nota de sistema: el alta de la tarea ya la deriva la línea de tiempo de la propia
        // fila (TicketTimelineCalculos). Anotarla acá además duplicaría el evento en pantalla.
        var tareas = await ProyectarTareasAsync(ticketId, ct);
        return tareas.FirstOrDefault(t => t.Id == entity.Id);
    }

    // ───────────────────────────── EDITAR ─────────────────────────────

    public async Task<TicketTareaDto?> UpdateAsync(
        long ticketId, long tareaId, UpdateTicketTareaRequest req, CancellationToken ct)
    {
        var caso = await CargarParaEscrituraAsync(ticketId, ct);
        if (caso is null) return null;

        var tarea = await _ctx.TicketTareas
            .FirstOrDefaultAsync(t => t.Id == tareaId && t.TicketId == ticketId && t.DeletedAt == null, ct);
        if (tarea is null) return null;

        if (req.HorasEstimadas is < 0)
            throw new InvalidOperationException("Las horas estimadas no pueden ser negativas.");

        var now = DateTime.UtcNow;
        var estadoAnterior = tarea.Estado;

        if (!string.IsNullOrWhiteSpace(req.Titulo)) tarea.Titulo = req.Titulo.Trim();
        if (req.Descripcion is not null)
            tarea.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        if (req.Etiquetas is not null)
            tarea.Etiquetas = string.IsNullOrWhiteSpace(req.Etiquetas) ? null : req.Etiquetas.Trim();
        if (!string.IsNullOrWhiteSpace(req.Tipo))      tarea.Tipo = TicketTareaCalculos.NormalizarTipo(req.Tipo);
        if (!string.IsNullOrWhiteSpace(req.Prioridad)) tarea.Prioridad = TicketTareaCalculos.NormalizarPrioridad(req.Prioridad);
        if (req.HorasEstimadas is not null)            tarea.HorasEstimadas = req.HorasEstimadas;
        if (req.FechaInicioPlan is not null)           tarea.FechaInicioPlan = req.FechaInicioPlan;
        if (req.FechaFinPlan is not null)              tarea.FechaFinPlan = req.FechaFinPlan;

        if (tarea.FechaInicioPlan is { } ini && tarea.FechaFinPlan is { } fin && fin < ini)
            throw new InvalidOperationException("La fecha de fin planificada no puede ser anterior a la de inicio.");

        // Quitar el responsable necesita un flag: null significa "no tocar" en un patch parcial.
        if (req.QuitarAsignado) tarea.AsignadoUserGuid = null;
        else if (req.AsignadoUserGuid is { } nuevo && nuevo != Guid.Empty) tarea.AsignadoUserGuid = nuevo;

        if (!string.IsNullOrWhiteSpace(req.Estado))
        {
            var estadoNuevo = TicketTareaCalculos.NormalizarEstado(req.Estado);
            if (!estadoNuevo.Equals(tarea.Estado, StringComparison.OrdinalIgnoreCase))
            {
                // Al cambiar de columna por edición se manda al final de la nueva.
                tarea.Orden = await _ctx.TicketTareas.AsNoTracking()
                    .CountAsync(t => t.TicketId == ticketId && t.DeletedAt == null &&
                                     t.Estado == estadoNuevo && t.Id != tareaId, ct);
                tarea.Estado = estadoNuevo;
            }
        }

        var (inicioReal, finReal) = TicketTareaCalculos.SellarFechasReales(
            tarea.Estado, tarea.FechaInicioReal, tarea.FechaFinReal, now);
        tarea.FechaInicioReal = inicioReal;
        tarea.FechaFinReal = finReal;

        tarea.UpdatedByUserId = _currentUser.UserId;
        tarea.UpdatedAt = now;

        if (!estadoAnterior.Equals(tarea.Estado, StringComparison.OrdinalIgnoreCase))
            RegistrarEventoTarea(ticketId,
                $"Tarea {tarea.Codigo}: {estadoAnterior} → {tarea.Estado}.", now);

        await _ctx.SaveChangesAsync(ct);

        var tareas = await ProyectarTareasAsync(ticketId, ct);
        return tareas.FirstOrDefault(t => t.Id == tareaId);
    }

    // ───────────────────────────── MOVER (drag & drop) ─────────────────────────────

    public async Task<IReadOnlyList<TicketTareaDto>> MoverAsync(
        long ticketId, long tareaId, MoverTicketTareaRequest req, CancellationToken ct)
    {
        if (!TicketTareaEstados.EsValido(req.Estado))
            throw new InvalidOperationException("Columna inválida para la tarea.");

        var caso = await CargarParaEscrituraAsync(ticketId, ct);
        if (caso is null) return Array.Empty<TicketTareaDto>();

        var destino = req.Estado.ToUpperInvariant();
        var now = DateTime.UtcNow;

        var tareas = await _ctx.TicketTareas
            .Where(t => t.TicketId == ticketId && t.DeletedAt == null)
            .ToListAsync(ct);

        var movida = tareas.FirstOrDefault(t => t.Id == tareaId);
        if (movida is null) return Array.Empty<TicketTareaDto>();

        var estadoAnterior = movida.Estado;

        var cambios = TicketTareaCalculos.Reordenar(
            tareas.Select(t => new TicketTareaCalculos.Posicion(t.Id, t.Estado, t.Orden)),
            tareaId, destino, req.Indice);

        foreach (var c in cambios)
        {
            var t = tareas.First(x => x.Id == c.Id);
            t.Orden = c.Orden;
            if (t.Id == tareaId) t.Estado = c.Estado;
            t.UpdatedByUserId = _currentUser.UserId;
            t.UpdatedAt = now;
        }

        // Cuando la columna destino es la misma y no hubo reacomodo, igual hay que sellar el estado.
        movida.Estado = destino;

        var (inicioReal, finReal) = TicketTareaCalculos.SellarFechasReales(
            destino, movida.FechaInicioReal, movida.FechaFinReal, now);
        movida.FechaInicioReal = inicioReal;
        movida.FechaFinReal = finReal;

        if (!estadoAnterior.Equals(destino, StringComparison.OrdinalIgnoreCase))
            RegistrarEventoTarea(ticketId, $"Tarea {movida.Codigo}: {estadoAnterior} → {destino}.", now);

        await _ctx.SaveChangesAsync(ct);
        return await ProyectarTareasAsync(ticketId, ct);
    }

    // ───────────────────────────── BORRAR (lógico) ─────────────────────────────

    public async Task<bool> DeleteAsync(long ticketId, long tareaId, CancellationToken ct)
    {
        var caso = await CargarParaEscrituraAsync(ticketId, ct);
        if (caso is null) return false;

        var tarea = await _ctx.TicketTareas
            .FirstOrDefaultAsync(t => t.Id == tareaId && t.TicketId == ticketId && t.DeletedAt == null, ct);
        if (tarea is null) return false;

        var conSubtareas = await _ctx.TicketTareas.AsNoTracking()
            .AnyAsync(t => t.ParentTareaId == tareaId && t.DeletedAt == null, ct);
        if (conSubtareas)
            throw new InvalidOperationException("La tarea tiene subtareas activas: eliminalas primero.");

        var now = DateTime.UtcNow;
        tarea.DeletedAt = now;
        tarea.UpdatedByUserId = _currentUser.UserId;
        tarea.UpdatedAt = now;

        // Compactar la columna: si quedan huecos, el tablero se ve barajado en la próxima carga.
        var restantes = await _ctx.TicketTareas
            .Where(t => t.TicketId == ticketId && t.DeletedAt == null &&
                        t.Estado == tarea.Estado && t.Id != tareaId)
            .OrderBy(t => t.Orden).ToListAsync(ct);
        for (var i = 0; i < restantes.Count; i++)
            if (restantes[i].Orden != i) restantes[i].Orden = i;

        RegistrarEventoTarea(ticketId, $"Tarea eliminada: {tarea.Codigo} · {tarea.Titulo}.", now);

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
