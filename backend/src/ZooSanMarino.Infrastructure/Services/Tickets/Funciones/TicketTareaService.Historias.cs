using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// ItalJira — trabajo que NO nace de un caso: tareas colgadas de una historia (o sueltas) y sus
/// subtareas/bugs.
/// </summary>
/// <remarks>
/// Vive en el MISMO servicio que las tareas del caso a propósito: <c>ticket_tareas</c> tiene un
/// único escritor. Así el reordenamiento (<see cref="TicketTareaCalculos.Reordenar"/>), el sellado
/// de fechas reales y la proyección a DTO son los mismos en las dos vistas — si divergieran, el
/// tablero del caso y el de ItalJira mostrarían números distintos para la misma tarea.
///
/// La diferencia real está en la VISIBILIDAD: una tarea de caso hereda los permisos del caso; una
/// tarea de ItalJira no tiene caso del que heredar, así que exige el permiso del módulo de gestión.
/// </remarks>
public partial class TicketTareaService
{
    /// <summary>
    /// Puerta de ItalJira: es el módulo del área de desarrollo, no de los usuarios finales.
    /// Mismos permisos que ya protegen tablero, roadmap y panel.
    /// </summary>
    private bool PuedeGestionarItalJira() =>
        EsSuperAdmin() ||
        _currentUser.Permissions.Contains("tickets.gestionar", StringComparer.OrdinalIgnoreCase);

    private void ExigirGestionItalJira()
    {
        if (!PuedeGestionarItalJira())
            throw new InvalidOperationException("No tenés permisos para gestionar el trabajo de ItalJira.");
    }

    // ───────────────────────────── LISTAR ─────────────────────────────

    public Task<IReadOnlyList<TicketTareaDto>> GetPorHistoriaAsync(long historiaId, CancellationToken ct)
    {
        if (!PuedeGestionarItalJira())
            return Task.FromResult<IReadOnlyList<TicketTareaDto>>(Array.Empty<TicketTareaDto>());

        return ProyectarComoListaAsync(
            _ctx.TicketTareas.AsNoTracking().Where(t => t.HistoriaId == historiaId), ct);
    }

    /// <summary>
    /// Bandeja de tareas sueltas: sin historia y sin caso. Devuelve el ÁRBOL completo (raíces y sus
    /// subtareas), no solo las raíces: cuando se borra una historia su trabajo cae acá, y filtrar
    /// por <c>ParentTareaId == null</c> dejaría las subtareas invisibles en todas las pantallas.
    /// El front las anida con <c>armarArbolTareas</c>.
    /// </summary>
    public Task<IReadOnlyList<TicketTareaDto>> GetSinAgruparAsync(CancellationToken ct)
    {
        if (!PuedeGestionarItalJira())
            return Task.FromResult<IReadOnlyList<TicketTareaDto>>(Array.Empty<TicketTareaDto>());

        return ProyectarComoListaAsync(
            _ctx.TicketTareas.AsNoTracking()
                .Where(t => t.HistoriaId == null && t.TicketId == null), ct);
    }

    private async Task<IReadOnlyList<TicketTareaDto>> ProyectarComoListaAsync(
        IQueryable<TicketTarea> universo, CancellationToken ct) =>
        await ProyectarTareasAsync(universo, ct);

    // ───────────────────────────── CREAR ─────────────────────────────

    public async Task<TicketTareaDto> CrearTareaItalJiraAsync(
        CreateTicketTareaRequest req, CancellationToken ct)
    {
        ExigirGestionItalJira();

        if (string.IsNullOrWhiteSpace(req.Titulo))
            throw new InvalidOperationException("El título de la tarea es requerido.");
        if (req.HorasEstimadas is < 0)
            throw new InvalidOperationException("Las horas estimadas no pueden ser negativas.");
        if (req.FechaInicioPlan is { } ini && req.FechaFinPlan is { } fin && fin < ini)
            throw new InvalidOperationException("La fecha de fin planificada no puede ser anterior a la de inicio.");

        // El padre manda: una subtarea hereda la historia y el caso de su tarea padre, para que el
        // árbol no pueda quedar con un hijo colgando de otra historia que su madre.
        long? historiaId = req.HistoriaId;
        long? ticketId = null;

        if (req.ParentTareaId is { } padreId)
        {
            var padre = await _ctx.TicketTareas.AsNoTracking()
                .Where(t => t.Id == padreId && t.DeletedAt == null)
                .Select(t => new { t.Id, t.HistoriaId, t.TicketId })
                .FirstOrDefaultAsync(ct);
            if (padre is null)
                throw new InvalidOperationException("La tarea padre no existe.");

            historiaId = padre.HistoriaId;
            ticketId   = padre.TicketId;
        }
        else if (historiaId is { } hid)
        {
            var existe = await _ctx.Historias.AsNoTracking()
                .AnyAsync(h => h.Id == hid && h.DeletedAt == null, ct);
            if (!existe)
                throw new InvalidOperationException("La historia indicada no existe.");
        }

        var estado = TicketTareaCalculos.NormalizarEstado(req.Estado);
        var now = DateTime.UtcNow;

        // El correlativo se cuenta dentro del universo de la tarea (historia o bandeja suelta),
        // igual que en un caso se cuenta dentro del caso.
        var codigos = await _ctx.TicketTareas.AsNoTracking()
            .Where(t => t.HistoriaId == historiaId && t.TicketId == null)
            .Select(t => t.Codigo)
            .ToListAsync(ct);

        var orden = await _ctx.TicketTareas.AsNoTracking()
            .CountAsync(t => t.HistoriaId == historiaId && t.TicketId == ticketId &&
                             t.DeletedAt == null && t.Estado == estado, ct);

        var (inicioReal, finReal) = TicketTareaCalculos.SellarFechasReales(estado, null, null, now);

        var entity = new TicketTarea
        {
            TicketId         = ticketId,
            HistoriaId       = historiaId,
            Codigo           = await GenerarCodigoItalJiraAsync(historiaId, codigos, ct),
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
            CompanyId        = _currentUser.CompanyId,
            CreatedByUserId  = _currentUser.UserId,
            CreatedAt        = now
        };

        _ctx.TicketTareas.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        var creada = await ProyectarUnaAsync(entity.Id, ct);
        return creada ?? throw new InvalidOperationException("No se pudo leer la tarea recién creada.");
    }

    /// <summary>
    /// Código de una tarea de ItalJira: <c>{codigoHistoria}-T{n}</c>, o <c>ITJ-T{n}</c> global
    /// cuando la tarea todavía no pertenece a ninguna historia.
    /// </summary>
    private async Task<string> GenerarCodigoItalJiraAsync(
        long? historiaId, List<string?> codigosDelUniverso, CancellationToken ct)
    {
        var consecutivo = TicketTareaCalculos.SiguienteConsecutivo(codigosDelUniverso);

        if (historiaId is not { } hid)
            return $"ITJ-T{consecutivo}";

        var codigoHistoria = await _ctx.Historias.AsNoTracking()
            .Where(h => h.Id == hid)
            .Select(h => h.Codigo)
            .FirstOrDefaultAsync(ct);

        var baseCodigo = string.IsNullOrWhiteSpace(codigoHistoria) ? $"HIS-{hid}" : codigoHistoria!.Trim();
        return $"{baseCodigo}-T{consecutivo}";
    }

    // ───────────────────────────── EDITAR ─────────────────────────────

    public async Task<TicketTareaDto?> ActualizarTareaAsync(
        long tareaId, UpdateTicketTareaRequest req, CancellationToken ct)
    {
        var tarea = await CargarTareaParaEscrituraAsync(tareaId, ct);
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

        if (req.QuitarAsignado) tarea.AsignadoUserGuid = null;
        else if (req.AsignadoUserGuid is { } nuevo && nuevo != Guid.Empty) tarea.AsignadoUserGuid = nuevo;

        if (!string.IsNullOrWhiteSpace(req.Estado))
        {
            var estadoNuevo = TicketTareaCalculos.NormalizarEstado(req.Estado);
            if (!estadoNuevo.Equals(tarea.Estado, StringComparison.OrdinalIgnoreCase))
            {
                tarea.Orden = await UniversoDe(tarea)
                    .CountAsync(t => t.DeletedAt == null && t.Estado == estadoNuevo && t.Id != tareaId, ct);
                tarea.Estado = estadoNuevo;
            }
        }

        var (inicioReal, finReal) = TicketTareaCalculos.SellarFechasReales(
            tarea.Estado, tarea.FechaInicioReal, tarea.FechaFinReal, now);
        tarea.FechaInicioReal = inicioReal;
        tarea.FechaFinReal = finReal;

        tarea.UpdatedByUserId = _currentUser.UserId;
        tarea.UpdatedAt = now;

        // Si la tarea pertenece a un caso, el cambio de columna sigue quedando en su bitácora.
        if (tarea.TicketId is { } casoId && !estadoAnterior.Equals(tarea.Estado, StringComparison.OrdinalIgnoreCase))
            RegistrarEventoTarea(casoId, $"Tarea {tarea.Codigo}: {estadoAnterior} → {tarea.Estado}.", now);

        await _ctx.SaveChangesAsync(ct);
        return await ProyectarUnaAsync(tareaId, ct);
    }

    // ───────────────────────────── MOVER ─────────────────────────────

    public async Task<IReadOnlyList<TicketTareaDto>> MoverTareaAsync(
        long tareaId, MoverTicketTareaRequest req, CancellationToken ct)
    {
        if (!TicketTareaEstados.EsValido(req.Estado))
            throw new InvalidOperationException("Columna inválida para la tarea.");

        var movida = await CargarTareaParaEscrituraAsync(tareaId, ct);
        if (movida is null) return Array.Empty<TicketTareaDto>();

        var destino = req.Estado.ToUpperInvariant();
        var now = DateTime.UtcNow;
        var estadoAnterior = movida.Estado;

        // El universo del reordenamiento es el conjunto de tarjetas que se ven juntas en el tablero.
        var tareas = await UniversoDe(movida).Where(t => t.DeletedAt == null).ToListAsync(ct);

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

        movida.Estado = destino;

        var (inicioReal, finReal) = TicketTareaCalculos.SellarFechasReales(
            destino, movida.FechaInicioReal, movida.FechaFinReal, now);
        movida.FechaInicioReal = inicioReal;
        movida.FechaFinReal = finReal;

        if (movida.TicketId is { } casoId && !estadoAnterior.Equals(destino, StringComparison.OrdinalIgnoreCase))
            RegistrarEventoTarea(casoId, $"Tarea {movida.Codigo}: {estadoAnterior} → {destino}.", now);

        await _ctx.SaveChangesAsync(ct);
        return await ProyectarTareasAsync(UniversoDe(movida).AsNoTracking(), ct);
    }

    // ───────────────────────────── BORRAR (lógico) ─────────────────────────────

    public async Task<bool> EliminarTareaAsync(long tareaId, CancellationToken ct)
    {
        var tarea = await CargarTareaParaEscrituraAsync(tareaId, ct);
        if (tarea is null) return false;

        var conSubtareas = await _ctx.TicketTareas.AsNoTracking()
            .AnyAsync(t => t.ParentTareaId == tareaId && t.DeletedAt == null, ct);
        if (conSubtareas)
            throw new InvalidOperationException("La tarea tiene subtareas activas: eliminalas primero.");

        var now = DateTime.UtcNow;
        tarea.DeletedAt = now;
        tarea.UpdatedByUserId = _currentUser.UserId;
        tarea.UpdatedAt = now;

        // Compactar la columna para que el tablero no se vea barajado en la próxima carga.
        var restantes = await UniversoDe(tarea)
            .Where(t => t.DeletedAt == null && t.Estado == tarea.Estado && t.Id != tareaId)
            .OrderBy(t => t.Orden).ToListAsync(ct);
        for (var i = 0; i < restantes.Count; i++)
            if (restantes[i].Orden != i) restantes[i].Orden = i;

        if (tarea.TicketId is { } casoId)
            RegistrarEventoTarea(casoId, $"Tarea eliminada: {tarea.Codigo} · {tarea.Titulo}.", now);

        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    // ───────────────────────────── TIEMPO SOBRE UNA TAREA ─────────────────────────────

    public async Task<TicketTiempoDto?> AddTiempoTareaAsync(
        long tareaId, CreateTicketTiempoRequest req, CancellationToken ct)
    {
        if (req.Horas <= 0)
            throw new InvalidOperationException("Las horas deben ser mayores a cero.");
        if (req.Horas > MaxHorasPorRegistro)
            throw new InvalidOperationException($"Un registro no puede superar {MaxHorasPorRegistro:0} horas.");

        var tarea = await CargarTareaParaEscrituraAsync(tareaId, ct);
        if (tarea is null) return null;

        var now = DateTime.UtcNow;
        var fecha = req.Fecha ?? DateOnly.FromDateTime(now);
        if (fecha > DateOnly.FromDateTime(now.AddDays(1)))
            throw new InvalidOperationException("No se puede registrar tiempo en una fecha futura.");

        var entity = new TicketTiempo
        {
            TicketId    = tarea.TicketId,     // null cuando la tarea nació en ItalJira
            TareaId     = tarea.Id,
            UserGuid    = _currentUser.UserGuid,
            UserId      = _currentUser.UserId,
            Fecha       = fecha,
            Horas       = Math.Round(req.Horas, 2),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            CreatedAt   = now
        };

        _ctx.TicketTiempos.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        var nombre = _currentUser.UserGuid.HasValue
            ? (await NombresPorGuidAsync(new[] { _currentUser.UserGuid.Value }, ct))
                .GetValueOrDefault(_currentUser.UserGuid.Value)
            : null;

        return new TicketTiempoDto(
            entity.Id, entity.TicketId, entity.TareaId, tarea.Titulo,
            entity.UserId, entity.UserGuid, nombre,
            entity.Fecha, entity.Horas, entity.Descripcion, entity.CreatedAt);
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    /// <summary>
    /// Carga la tarea exigiendo permiso de escritura. Si pertenece a un caso, el permiso lo decide
    /// el caso (regla histórica del módulo); si no, lo decide el permiso de gestión de ItalJira.
    /// </summary>
    private async Task<TicketTarea?> CargarTareaParaEscrituraAsync(long tareaId, CancellationToken ct)
    {
        var tarea = await _ctx.TicketTareas
            .FirstOrDefaultAsync(t => t.Id == tareaId && t.DeletedAt == null, ct);
        if (tarea is null) return null;

        if (tarea.TicketId is { } ticketId)
        {
            var caso = await CargarCasoAsync(ticketId, ct);
            if (caso is null) return null;
            if (!PuedeGestionar(caso))
                throw new InvalidOperationException("No tenés permisos para gestionar las tareas de este caso.");
            return tarea;
        }

        ExigirGestionItalJira();
        return tarea;
    }

    /// <summary>
    /// Conjunto de tarjetas que comparten tablero con la tarea dada: las de su caso, o las de su
    /// historia, o la bandeja de sueltas. Define el universo del reordenamiento.
    /// </summary>
    private IQueryable<TicketTarea> UniversoDe(TicketTarea tarea) => tarea switch
    {
        { TicketId: { } ticketId }   => _ctx.TicketTareas.Where(t => t.TicketId == ticketId),
        { HistoriaId: { } historia } => _ctx.TicketTareas.Where(t => t.HistoriaId == historia && t.TicketId == null),
        _                            => _ctx.TicketTareas.Where(t => t.HistoriaId == null && t.TicketId == null),
    };

    /// <summary>Proyecta una sola tarea por id, con la misma fórmula que usan las listas.</summary>
    private async Task<TicketTareaDto?> ProyectarUnaAsync(long tareaId, CancellationToken ct)
    {
        var filas = await ProyectarTareasAsync(
            _ctx.TicketTareas.AsNoTracking().Where(t => t.Id == tareaId), ct);
        return filas.FirstOrDefault();
    }
}
