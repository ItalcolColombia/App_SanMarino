using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// ItalJira — historias (épicas) del área de desarrollo: el nivel que agrupa tareas y casos.
/// </summary>
/// <remarks>
/// Archivo ANCLA del partial: campos, constructor, permisos, identidad y CRUD. Las vistas agregadas
/// (backlog, tablero y roadmap) viven en <c>Funciones/HistoriaService.Backlog.cs</c>.
///
/// <b>Alcance:</b> ItalJira NO filtra por empresa. Es deliberado y espeja lo que ya hace la bandeja
/// de gestión de tickets (<c>AplicarFiltroTablero</c> solo filtra empresa si el filtro lo pide):
/// el equipo de desarrollo es uno solo y trabaja los casos de todas las empresas, así que atarlo a
/// la empresa activa haría desaparecer la mitad del backlog al cambiar de empresa. La puerta es el
/// PERMISO (<c>tickets.gestionar</c> / <c>tickets.admin</c>), no el tenant.
///
/// <b>Horas:</b> la historia nunca registra horas propias — las agrega de sus tareas y casos. Si
/// pudiera registrarlas, el mismo esfuerzo se contaría dos veces en el panel.
/// </remarks>
public partial class HistoriaService : IHistoriaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;

    public HistoriaService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    // ───────────────────────────── Permisos ─────────────────────────────

    private bool EsSuperAdmin() =>
        _currentUser.Permissions.Contains("tickets.admin", StringComparer.OrdinalIgnoreCase);

    /// <summary>ItalJira es del área de desarrollo: gestor del módulo o administrador global.</summary>
    private bool PuedeGestionar() =>
        EsSuperAdmin() ||
        _currentUser.Permissions.Contains("tickets.gestionar", StringComparer.OrdinalIgnoreCase);

    private void Exigir()
    {
        if (!PuedeGestionar())
            throw new InvalidOperationException("No tenés permisos para gestionar ItalJira.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// Expone <see cref="PuedeGestionar"/> sin duplicar la regla: quien la necesite desde otro
    /// módulo pregunta acá en vez de volver a escribir el <c>Permissions.Contains(...)</c>.
    /// </remarks>
    public bool PuedeGestionarItalJira() => PuedeGestionar();

    // ───────────────────────────── Identidad ─────────────────────────────

    private async Task<Dictionary<Guid, string>> NombresPorGuidAsync(
        IEnumerable<Guid> guids, CancellationToken ct)
    {
        var ids = guids.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return await _ctx.Set<User>().AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.firstName} {u.surName}".Trim(), ct);
    }

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

    // ───────────────────────────── LISTAR ─────────────────────────────

    public async Task<IReadOnlyList<HistoriaDto>> GetAllAsync(ItalJiraFiltro filtro, CancellationToken ct)
    {
        if (!PuedeGestionar()) return Array.Empty<HistoriaDto>();
        return await ProyectarHistoriasAsync(AplicarFiltro(filtro), ct);
    }

    public async Task<HistoriaDetalleDto?> GetByIdAsync(long id, CancellationToken ct)
    {
        if (!PuedeGestionar()) return null;

        var historias = await ProyectarHistoriasAsync(
            _ctx.Historias.AsNoTracking().Where(h => h.Id == id), ct);
        var historia = historias.FirstOrDefault();
        if (historia is null) return null;

        var tareas = await ProyectarTareasDeHistoriasAsync(new[] { id }, ct);
        var casos  = await ProyectarCasosAsync(_ctx.Tickets.AsNoTracking().Where(t => t.HistoriaId == id), ct);

        return new HistoriaDetalleDto(
            historia,
            tareas.TryGetValue(id, out var lista) ? lista : Array.Empty<TicketTareaDto>(),
            casos);
    }

    /// <summary>Universo de historias vivas con el filtro común de las vistas de ItalJira.</summary>
    private IQueryable<Historia> AplicarFiltro(ItalJiraFiltro filtro)
    {
        var query = _ctx.Historias.AsNoTracking().Where(h => h.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            var estado = HistoriaCalculos.NormalizarEstado(filtro.Estado);
            query = query.Where(h => h.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Prioridad))
        {
            var prioridad = HistoriaCalculos.NormalizarPrioridad(filtro.Prioridad);
            query = query.Where(h => h.Prioridad == prioridad);
        }

        if (filtro.ResponsableUserGuid is { } responsable && responsable != Guid.Empty)
            query = query.Where(h => h.ResponsableUserGuid == responsable);

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            query = query.Where(h =>
                EF.Functions.ILike(h.Titulo, $"%{texto}%") ||
                (h.Codigo != null && EF.Functions.ILike(h.Codigo, $"%{texto}%")));
        }

        if (!filtro.IncluirTerminadas)
            query = query.Where(h => h.Estado != HistoriaEstados.Listo);

        return query;
    }

    // ───────────────────────────── CREAR ─────────────────────────────

    public async Task<HistoriaDto> CreateAsync(CreateHistoriaRequest req, CancellationToken ct)
    {
        Exigir();

        if (string.IsNullOrWhiteSpace(req.Titulo))
            throw new InvalidOperationException("El título de la historia es requerido.");
        if (req.HorasEstimadas is < 0)
            throw new InvalidOperationException("Las horas estimadas no pueden ser negativas.");
        if (req.FechaInicioPlan is { } ini && req.FechaFinPlan is { } fin && fin < ini)
            throw new InvalidOperationException("La fecha de fin planificada no puede ser anterior a la de inicio.");

        var now = DateTime.UtcNow;
        var anio = now.Year;
        var estado = HistoriaCalculos.NormalizarEstado(req.Estado);

        // Códigos ya emitidos INCLUIDOS los de historias borradas: el correlativo no se reutiliza.
        var codigos = await _ctx.Historias.AsNoTracking()
            .Where(h => h.Codigo != null)
            .Select(h => h.Codigo)
            .ToListAsync(ct);

        var orden = await _ctx.Historias.AsNoTracking()
            .CountAsync(h => h.DeletedAt == null && h.Estado == estado, ct);

        var (inicioReal, finReal) = HistoriaCalculos.SellarFechasReales(estado, null, null, now);

        var entity = new Historia
        {
            Codigo              = HistoriaCalculos.GenerarCodigo(
                                      anio, HistoriaCalculos.SiguienteConsecutivo(codigos, anio)),
            PaisId              = _currentUser.PaisId ?? 0,
            Titulo              = req.Titulo.Trim(),
            Descripcion         = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Estado              = estado,
            Prioridad           = HistoriaCalculos.NormalizarPrioridad(req.Prioridad),
            ResponsableUserGuid = req.ResponsableUserGuid,
            Orden               = orden,
            HorasEstimadas      = req.HorasEstimadas,
            FechaInicioPlan     = req.FechaInicioPlan,
            FechaFinPlan        = req.FechaFinPlan,
            FechaInicioReal     = inicioReal,
            FechaFinReal        = finReal,
            Etiquetas           = string.IsNullOrWhiteSpace(req.Etiquetas) ? null : req.Etiquetas.Trim(),
            CompanyId           = _currentUser.CompanyId,
            CreatedByUserId     = _currentUser.UserId,
            CreatedAt           = now
        };

        _ctx.Historias.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        var creada = await ProyectarHistoriasAsync(
            _ctx.Historias.AsNoTracking().Where(h => h.Id == entity.Id), ct);
        return creada[0];
    }

    // ───────────────────────────── EDITAR ─────────────────────────────

    public async Task<HistoriaDto?> UpdateAsync(long id, UpdateHistoriaRequest req, CancellationToken ct)
    {
        Exigir();

        var historia = await _ctx.Historias.FirstOrDefaultAsync(h => h.Id == id && h.DeletedAt == null, ct);
        if (historia is null) return null;

        if (req.HorasEstimadas is < 0)
            throw new InvalidOperationException("Las horas estimadas no pueden ser negativas.");

        var now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(req.Titulo)) historia.Titulo = req.Titulo.Trim();
        if (req.Descripcion is not null)
            historia.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        if (req.Etiquetas is not null)
            historia.Etiquetas = string.IsNullOrWhiteSpace(req.Etiquetas) ? null : req.Etiquetas.Trim();
        if (!string.IsNullOrWhiteSpace(req.Prioridad))
            historia.Prioridad = HistoriaCalculos.NormalizarPrioridad(req.Prioridad);
        if (req.HorasEstimadas is not null)  historia.HorasEstimadas = req.HorasEstimadas;
        if (req.FechaInicioPlan is not null) historia.FechaInicioPlan = req.FechaInicioPlan;
        if (req.FechaFinPlan is not null)    historia.FechaFinPlan = req.FechaFinPlan;

        if (historia.FechaInicioPlan is { } ini && historia.FechaFinPlan is { } fin && fin < ini)
            throw new InvalidOperationException("La fecha de fin planificada no puede ser anterior a la de inicio.");

        if (req.QuitarResponsable) historia.ResponsableUserGuid = null;
        else if (req.ResponsableUserGuid is { } nuevo && nuevo != Guid.Empty)
            historia.ResponsableUserGuid = nuevo;

        if (!string.IsNullOrWhiteSpace(req.Estado))
        {
            var estadoNuevo = HistoriaCalculos.NormalizarEstado(req.Estado);
            if (!estadoNuevo.Equals(historia.Estado, StringComparison.OrdinalIgnoreCase))
            {
                historia.Orden = await _ctx.Historias.AsNoTracking()
                    .CountAsync(h => h.DeletedAt == null && h.Estado == estadoNuevo && h.Id != id, ct);
                historia.Estado = estadoNuevo;
            }
        }

        var (inicioReal, finReal) = HistoriaCalculos.SellarFechasReales(
            historia.Estado, historia.FechaInicioReal, historia.FechaFinReal, now);
        historia.FechaInicioReal = inicioReal;
        historia.FechaFinReal = finReal;

        historia.UpdatedByUserId = _currentUser.UserId;
        historia.UpdatedAt = now;

        await _ctx.SaveChangesAsync(ct);

        var actualizada = await ProyectarHistoriasAsync(
            _ctx.Historias.AsNoTracking().Where(h => h.Id == id), ct);
        return actualizada.FirstOrDefault();
    }

    // ───────────────────────────── MOVER (drag & drop) ─────────────────────────────

    public async Task<IReadOnlyList<HistoriaDto>> MoverAsync(
        long id, MoverHistoriaRequest req, CancellationToken ct)
    {
        Exigir();

        if (!HistoriaEstados.EsValido(req.Estado))
            throw new InvalidOperationException("Columna inválida para la historia.");

        var historias = await _ctx.Historias.Where(h => h.DeletedAt == null).ToListAsync(ct);
        var movida = historias.FirstOrDefault(h => h.Id == id);
        if (movida is null) return Array.Empty<HistoriaDto>();

        var destino = req.Estado.ToUpperInvariant();
        var now = DateTime.UtcNow;

        // Se reutiliza el reordenamiento de las tarjetas: opera sobre (Id, Estado, Orden), no sabe
        // si la fila es tarea o historia. Una sola fórmula para las dos alturas del tablero.
        var cambios = TicketTareaCalculos.Reordenar(
            historias.Select(h => new TicketTareaCalculos.Posicion(h.Id, h.Estado, h.Orden)),
            id, destino, req.Indice);

        foreach (var c in cambios)
        {
            var h = historias.First(x => x.Id == c.Id);
            h.Orden = c.Orden;
            if (h.Id == id) h.Estado = c.Estado;
            h.UpdatedByUserId = _currentUser.UserId;
            h.UpdatedAt = now;
        }

        movida.Estado = destino;

        var (inicioReal, finReal) = HistoriaCalculos.SellarFechasReales(
            destino, movida.FechaInicioReal, movida.FechaFinReal, now);
        movida.FechaInicioReal = inicioReal;
        movida.FechaFinReal = finReal;

        await _ctx.SaveChangesAsync(ct);

        return await ProyectarHistoriasAsync(_ctx.Historias.AsNoTracking(), ct);
    }

    // ───────────────────────────── BORRAR (lógico) ─────────────────────────────

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        Exigir();

        var historia = await _ctx.Historias.FirstOrDefaultAsync(h => h.Id == id && h.DeletedAt == null, ct);
        if (historia is null) return false;

        var now = DateTime.UtcNow;
        historia.DeletedAt = now;
        historia.UpdatedByUserId = _currentUser.UserId;
        historia.UpdatedAt = now;

        // El trabajo NO se borra: vuelve a la bandeja «sin historia». Borrar una épica es una
        // decisión de organización, no de datos — las tareas y los casos siguen siendo reales.
        var tareas = await _ctx.TicketTareas.Where(t => t.HistoriaId == id).ToListAsync(ct);
        foreach (var t in tareas)
        {
            t.HistoriaId = null;
            t.UpdatedByUserId = _currentUser.UserId;
            t.UpdatedAt = now;
        }

        var casos = await _ctx.Tickets.Where(t => t.HistoriaId == id).ToListAsync(ct);
        foreach (var c in casos)
        {
            c.HistoriaId = null;
            c.UpdatedByUserId = _currentUser.UserId;
            c.UpdatedAt = now;
        }

        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    // ───────────────────────────── AGRUPAR TRABAJO ─────────────────────────────

    public async Task<bool> AsignarCasoAsync(long ticketId, AsignarAHistoriaRequest req, CancellationToken ct)
    {
        Exigir();

        var caso = await _ctx.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.DeletedAt == null, ct);
        if (caso is null) return false;

        await ValidarHistoriaDestinoAsync(req.HistoriaId, ct);

        // Agrupar NO toca el estado del caso: su máquina de estados sigue siendo del resolutor.
        caso.HistoriaId = req.HistoriaId;
        caso.UpdatedByUserId = _currentUser.UserId;
        caso.UpdatedAt = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AsignarTareaAsync(long tareaId, AsignarAHistoriaRequest req, CancellationToken ct)
    {
        Exigir();

        var tarea = await _ctx.TicketTareas.FirstOrDefaultAsync(t => t.Id == tareaId && t.DeletedAt == null, ct);
        if (tarea is null) return false;

        if (tarea.ParentTareaId is not null)
            throw new InvalidOperationException(
                "Una subtarea sigue a su tarea padre: mové la tarea padre para cambiarla de historia.");

        await ValidarHistoriaDestinoAsync(req.HistoriaId, ct);

        var now = DateTime.UtcNow;
        tarea.HistoriaId = req.HistoriaId;
        tarea.UpdatedByUserId = _currentUser.UserId;
        tarea.UpdatedAt = now;

        // Las subtareas viajan con su madre, o el árbol quedaría con hijos en otra historia.
        var subtareas = await _ctx.TicketTareas.Where(t => t.ParentTareaId == tareaId).ToListAsync(ct);
        foreach (var s in subtareas)
        {
            s.HistoriaId = req.HistoriaId;
            s.UpdatedByUserId = _currentUser.UserId;
            s.UpdatedAt = now;
        }

        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    private async Task ValidarHistoriaDestinoAsync(long? historiaId, CancellationToken ct)
    {
        if (historiaId is not { } hid) return;

        var existe = await _ctx.Historias.AsNoTracking()
            .AnyAsync(h => h.Id == hid && h.DeletedAt == null, ct);
        if (!existe)
            throw new InvalidOperationException("La historia indicada no existe.");
    }
}
