// Implementacion/Funciones/ImplementacionService.Tareas.cs
// Checklist: CRUD de tareas + flujo de doble check (completar por el gestor → confirmar por el asignado).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ImplementacionService
{
    public async Task<ImplementacionTareaDto?> CreateTareaAsync(int planId, ImplementacionTareaCreateRequest req, CancellationToken ct = default)
    {
        var plan = await GetPlanScopedAsync(planId, ct);
        if (plan is null) return null;
        if (plan.Estado == ImplementacionCalculos.PlanCancelado)
            throw new InvalidOperationException("El plan está cancelado; reactivalo para agregar tareas.");

        var categoria = TextoRequerido(req.Categoria, "La categoría", 100);
        var titulo    = TextoRequerido(req.Titulo, "El título", 300);
        await ValidarAsignacionAsync(req.RoleId, req.AsignadoUserId, ct);

        var orden = req.Orden ?? (await _ctx.ImplementacionTareas
            .Where(t => t.PlanId == planId && t.DeletedAt == null)
            .MaxAsync(t => (int?)t.Orden, ct) ?? 0) + 1;

        var tarea = new ImplementacionTarea
        {
            PlanId          = planId,
            CompanyId       = _current.CompanyId,
            Categoria       = categoria,
            Titulo          = titulo,
            Descripcion     = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Orden           = orden,
            FechaProgramada = req.FechaProgramada?.Date,
            RoleId          = req.RoleId,
            AsignadoUserId  = req.AsignadoUserId,
            Estado          = ImplementacionCalculos.TareaPendiente,
            CreatedByUserId = _current.UserId
        };

        _ctx.ImplementacionTareas.Add(tarea);
        await _ctx.SaveChangesAsync(ct);
        await RecalcularEstadoPlanAsync(planId, ct);
        return await ReloadTareaDtoAsync(tarea.Id, ct);
    }

    public async Task<ImplementacionTareaDto?> UpdateTareaAsync(int tareaId, ImplementacionTareaUpdateRequest req, CancellationToken ct = default)
    {
        var tarea = await GetTareaScopedAsync(tareaId, ct);
        if (tarea is null) return null;
        if (tarea.Estado == ImplementacionCalculos.TareaConfirmada)
            throw new InvalidOperationException("No se puede editar una tarea ya confirmada; reabrila primero.");

        tarea.Categoria       = TextoRequerido(req.Categoria, "La categoría", 100);
        tarea.Titulo          = TextoRequerido(req.Titulo, "El título", 300);
        await ValidarAsignacionAsync(req.RoleId, req.AsignadoUserId, ct);
        tarea.Descripcion     = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        if (req.Orden.HasValue) tarea.Orden = req.Orden.Value;
        tarea.FechaProgramada = req.FechaProgramada?.Date;
        tarea.RoleId          = req.RoleId;
        tarea.AsignadoUserId  = req.AsignadoUserId;
        tarea.UpdatedByUserId = _current.UserId;

        await _ctx.SaveChangesAsync(ct);
        return await ReloadTareaDtoAsync(tareaId, ct);
    }

    public async Task<bool> DeleteTareaAsync(int tareaId, CancellationToken ct = default)
    {
        var tarea = await GetTareaScopedAsync(tareaId, ct);
        if (tarea is null) return false;

        tarea.DeletedAt = DateTime.UtcNow;
        tarea.UpdatedByUserId = _current.UserId;
        await _ctx.SaveChangesAsync(ct);
        await RecalcularEstadoPlanAsync(tarea.PlanId, ct);
        return true;
    }

    /// <summary>Check del gestor: pendiente → completada, con fecha y usuario que marcó.</summary>
    public async Task<ImplementacionTareaDto?> CompletarTareaAsync(int tareaId, CancellationToken ct = default)
    {
        var tarea = await GetTareaScopedAsync(tareaId, ct);
        if (tarea is null) return null;
        if (tarea.Plan.Estado == ImplementacionCalculos.PlanCancelado)
            throw new InvalidOperationException("El plan está cancelado; no se pueden completar tareas.");
        if (tarea.Estado != ImplementacionCalculos.TareaPendiente)
            throw new InvalidOperationException("La tarea ya fue marcada como completada.");

        tarea.Estado               = ImplementacionCalculos.TareaCompletada;
        tarea.FechaCompletada      = DateTime.UtcNow;
        tarea.CompletadaPorUserId  = _current.UserGuid;
        tarea.UpdatedByUserId      = _current.UserId;

        await _ctx.SaveChangesAsync(ct);
        await RecalcularEstadoPlanAsync(tarea.PlanId, ct);
        return await ReloadTareaDtoAsync(tareaId, ct);
    }

    /// <summary>
    /// Confirmación del usuario asignado (desde "Mis tareas"): completada → confirmada.
    /// Lanza <see cref="UnauthorizedAccessException"/> si quien llama no es el asignado.
    /// </summary>
    public async Task<ImplementacionTareaDto?> ConfirmarTareaAsync(int tareaId, ImplementacionConfirmarRequest req, CancellationToken ct = default)
    {
        var tarea = await GetTareaScopedAsync(tareaId, ct);
        if (tarea is null) return null;
        if (tarea.Plan.Estado == ImplementacionCalculos.PlanCancelado)
            throw new InvalidOperationException("El plan está cancelado; no se pueden confirmar tareas.");
        if (tarea.Estado == ImplementacionCalculos.TareaConfirmada)
            throw new InvalidOperationException("La tarea ya fue confirmada.");
        if (tarea.Estado == ImplementacionCalculos.TareaPendiente)
            throw new InvalidOperationException("La tarea aún no fue marcada como completada por el gestor.");

        if (!ImplementacionCalculos.PuedeConfirmar(tarea.Estado, tarea.AsignadoUserId, _current.UserGuid))
            throw new UnauthorizedAccessException("Solo el usuario asignado puede confirmar esta tarea.");

        tarea.Estado              = ImplementacionCalculos.TareaConfirmada;
        tarea.FechaConfirmada     = DateTime.UtcNow;
        tarea.ConfirmadaPorUserId = _current.UserGuid;
        if (!string.IsNullOrWhiteSpace(req.Observaciones))
            tarea.Observaciones = req.Observaciones.Trim();
        tarea.UpdatedByUserId = _current.UserId;

        await _ctx.SaveChangesAsync(ct);
        await RecalcularEstadoPlanAsync(tarea.PlanId, ct);
        return await ReloadTareaDtoAsync(tareaId, ct);
    }

    /// <summary>Deshace el check (corrige errores): limpia fechas/usuarios y vuelve a pendiente.</summary>
    public async Task<ImplementacionTareaDto?> ReabrirTareaAsync(int tareaId, CancellationToken ct = default)
    {
        var tarea = await GetTareaScopedAsync(tareaId, ct);
        if (tarea is null) return null;
        if (tarea.Estado == ImplementacionCalculos.TareaPendiente)
            throw new InvalidOperationException("La tarea ya está pendiente.");

        tarea.Estado              = ImplementacionCalculos.TareaPendiente;
        tarea.FechaCompletada     = null;
        tarea.CompletadaPorUserId = null;
        tarea.FechaConfirmada     = null;
        tarea.ConfirmadaPorUserId = null;
        tarea.UpdatedByUserId     = _current.UserId;

        await _ctx.SaveChangesAsync(ct);
        await RecalcularEstadoPlanAsync(tarea.PlanId, ct);
        return await ReloadTareaDtoAsync(tareaId, ct);
    }
}
