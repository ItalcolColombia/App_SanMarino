using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Registro de tiempos (worklog) del caso y de sus tareas: cuántas horas dedicó quién y en qué día.
/// </summary>
/// <remarks>
/// Partial de <see cref="TicketTareaService"/> — misma clase, mismo namespace plano.
/// El borrado es lógico para no alterar totales ya reportados.
/// </remarks>
public partial class TicketTareaService
{
    /// <summary>Tope por registro: más de una jornada en una sola imputación es un error de tipeo.</summary>
    private const decimal MaxHorasPorRegistro = 24m;

    // ───────────────────────────── LISTAR ─────────────────────────────

    public async Task<IReadOnlyList<TicketTiempoDto>> GetTiemposAsync(long ticketId, CancellationToken ct)
    {
        var caso = await CargarCasoAsync(ticketId, ct);
        if (caso is null || !await PuedeVerAsync(caso, ct))
            return Array.Empty<TicketTiempoDto>();

        return await ProyectarTiemposAsync(ticketId, ct);
    }

    private async Task<List<TicketTiempoDto>> ProyectarTiemposAsync(long ticketId, CancellationToken ct)
    {
        var rows = await _ctx.TicketTiempos.AsNoTracking()
            .Where(w => w.TicketId == ticketId && w.DeletedAt == null)
            .OrderByDescending(w => w.Fecha).ThenByDescending(w => w.Id)
            .Select(w => new
            {
                w.Id, w.TicketId, w.TareaId, w.UserId, w.UserGuid,
                w.Fecha, w.Horas, w.Descripcion, w.CreatedAt,
                TareaTitulo = w.Tarea != null ? w.Tarea.Titulo : null
            })
            .ToListAsync(ct);

        var porGuid = await NombresPorGuidAsync(
            rows.Where(r => r.UserGuid.HasValue).Select(r => r.UserGuid!.Value), ct);
        var porCedula = await NombresPorCedulaAsync(rows.Select(r => r.UserId), ct);

        return rows.Select(r => new TicketTiempoDto(
            r.Id, r.TicketId, r.TareaId, r.TareaTitulo, r.UserId, r.UserGuid,
            (r.UserGuid.HasValue ? porGuid.GetValueOrDefault(r.UserGuid.Value) : null)
                ?? porCedula.GetValueOrDefault(r.UserId),
            r.Fecha, r.Horas, r.Descripcion, r.CreatedAt)).ToList();
    }

    // ───────────────────────────── REGISTRAR ─────────────────────────────

    public async Task<TicketTiempoDto?> AddTiempoAsync(
        long ticketId, CreateTicketTiempoRequest req, CancellationToken ct)
    {
        if (req.Horas <= 0)
            throw new InvalidOperationException("Las horas deben ser mayores a cero.");
        if (req.Horas > MaxHorasPorRegistro)
            throw new InvalidOperationException($"Un registro no puede superar {MaxHorasPorRegistro:0} horas.");

        var caso = await CargarParaEscrituraAsync(ticketId, ct);
        if (caso is null) return null;

        if (req.TareaId is { } tareaId)
        {
            var tareaValida = await _ctx.TicketTareas.AsNoTracking()
                .AnyAsync(t => t.Id == tareaId && t.TicketId == ticketId && t.DeletedAt == null, ct);
            if (!tareaValida)
                throw new InvalidOperationException("La tarea indicada no existe en este caso.");
        }

        var now = DateTime.UtcNow;
        var fecha = req.Fecha ?? DateOnly.FromDateTime(now);
        if (fecha > DateOnly.FromDateTime(now.AddDays(1)))
            throw new InvalidOperationException("No se puede registrar tiempo en una fecha futura.");

        var entity = new TicketTiempo
        {
            TicketId    = ticketId,
            TareaId     = req.TareaId,
            UserGuid    = _currentUser.UserGuid,
            UserId      = _currentUser.UserId,
            Fecha       = fecha,
            Horas       = Math.Round(req.Horas, 2),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            CreatedAt   = now
        };

        _ctx.TicketTiempos.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        var tiempos = await ProyectarTiemposAsync(ticketId, ct);
        return tiempos.FirstOrDefault(t => t.Id == entity.Id);
    }

    // ───────────────────────────── ELIMINAR (lógico) ─────────────────────────────

    public async Task<bool> DeleteTiempoAsync(long ticketId, long tiempoId, CancellationToken ct)
    {
        var caso = await CargarParaEscrituraAsync(ticketId, ct);
        if (caso is null) return false;

        var tiempo = await _ctx.TicketTiempos
            .FirstOrDefaultAsync(w => w.Id == tiempoId && w.TicketId == ticketId && w.DeletedAt == null, ct);
        if (tiempo is null) return false;

        // Cada quien borra lo suyo; el administrador puede corregir cualquier registro.
        var esMio = (_currentUser.UserGuid.HasValue && tiempo.UserGuid == _currentUser.UserGuid.Value)
                    || (tiempo.UserId != 0 && tiempo.UserId == _currentUser.UserId);
        if (!esMio && !EsSuperAdmin())
            throw new InvalidOperationException("Solo podés eliminar los registros de tiempo que cargaste vos.");

        tiempo.DeletedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    // ───────────────────────────── RESUMEN ─────────────────────────────

    public async Task<TicketResumenTiemposDto?> GetResumenTiemposAsync(long ticketId, CancellationToken ct)
    {
        var caso = await CargarCasoAsync(ticketId, ct);
        if (caso is null || !await PuedeVerAsync(caso, ct)) return null;

        var estimadas = await _ctx.Tickets.AsNoTracking()
            .Where(x => x.Id == ticketId)
            .Select(x => x.HorasEstimadas)
            .FirstOrDefaultAsync(ct);

        // La agrupación por persona la hace la BD, no el backend.
        var porPersona = await _ctx.TicketTiempos.AsNoTracking()
            .Where(w => w.TicketId == ticketId && w.DeletedAt == null)
            .GroupBy(w => new { w.UserGuid, w.UserId })
            .Select(g => new { g.Key.UserGuid, g.Key.UserId, Horas = g.Sum(w => w.Horas) })
            .ToListAsync(ct);

        var porGuid = await NombresPorGuidAsync(
            porPersona.Where(p => p.UserGuid.HasValue).Select(p => p.UserGuid!.Value), ct);
        var porCedula = await NombresPorCedulaAsync(porPersona.Select(p => p.UserId), ct);

        var total = porPersona.Sum(p => p.Horas);

        return new TicketResumenTiemposDto(
            HorasRegistradas: total,
            HorasEstimadas: estimadas,
            DesvioHoras: TicketMetricasCalculos.DesvioHoras(estimadas, total),
            PorPersona: porPersona
                .Select(p => new TicketTiempoPorPersonaDto(
                    p.UserGuid,
                    (p.UserGuid.HasValue ? porGuid.GetValueOrDefault(p.UserGuid.Value) : null)
                        ?? porCedula.GetValueOrDefault(p.UserId),
                    p.Horas))
                .OrderByDescending(p => p.Horas)
                .ToList());
    }
}
