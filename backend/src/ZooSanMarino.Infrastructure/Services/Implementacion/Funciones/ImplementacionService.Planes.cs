// Implementacion/Funciones/ImplementacionService.Planes.cs
// CRUD de planes de implementación (cronogramas) de la empresa activa.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ImplementacionService
{
    public async Task<List<ImplementacionPlanDto>> GetPlanesAsync(CancellationToken ct = default)
    {
        var paisId = _current.PaisId;

        var filas = await _ctx.ImplementacionPlanes.AsNoTracking()
            .Where(p => p.CompanyId == _current.CompanyId && p.DeletedAt == null)
            .Where(p => paisId == null || p.PaisId == null || p.PaisId == paisId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                Plan        = p,
                Total       = p.Tareas.Count(t => t.DeletedAt == null),
                Completadas = p.Tareas.Count(t => t.DeletedAt == null && t.Estado == ImplementacionCalculos.TareaCompletada),
                Confirmadas = p.Tareas.Count(t => t.DeletedAt == null && t.Estado == ImplementacionCalculos.TareaConfirmada)
            })
            .ToListAsync(ct);

        return filas.Select(f => MapPlan(f.Plan, f.Total, f.Completadas, f.Confirmadas)).ToList();
    }

    public async Task<ImplementacionPlanDetalleDto?> GetPlanDetalleAsync(int planId, CancellationToken ct = default)
    {
        var plan = await _ctx.ImplementacionPlanes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId && p.CompanyId == _current.CompanyId && p.DeletedAt == null, ct);
        if (plan is null) return null;

        var tareas = await _ctx.ImplementacionTareas.AsNoTracking()
            .Include(t => t.Role)
            .Include(t => t.AsignadoUser)
            .Include(t => t.CompletadaPorUser)
            .Include(t => t.ConfirmadaPorUser)
            .Where(t => t.PlanId == planId && t.DeletedAt == null)
            .OrderBy(t => t.Orden).ThenBy(t => t.Id)
            .ToListAsync(ct);

        var hoy = DateTime.UtcNow;
        var completadas = tareas.Count(t => t.Estado == ImplementacionCalculos.TareaCompletada);
        var confirmadas = tareas.Count(t => t.Estado == ImplementacionCalculos.TareaConfirmada);

        return new ImplementacionPlanDetalleDto(
            MapPlan(plan, tareas.Count, completadas, confirmadas),
            tareas.Select(t => MapTarea(t, hoy)).ToList());
    }

    public async Task<ImplementacionPlanDto> CreatePlanAsync(ImplementacionPlanCreateRequest req, CancellationToken ct = default)
    {
        var nombre = TextoRequerido(req.Nombre, "El nombre del plan", 200);
        ValidarRangoFechas(req.FechaInicio, req.FechaFin);

        var plan = new ImplementacionPlan
        {
            CompanyId       = _current.CompanyId,
            PaisId          = _current.PaisId,
            Nombre          = nombre,
            Descripcion     = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            FechaInicio     = req.FechaInicio?.Date,
            FechaFin        = req.FechaFin?.Date,
            Estado          = ImplementacionCalculos.PlanBorrador,
            CreatedByUserId = _current.UserId
        };

        if (req.UsarPlantilla)
        {
            foreach (var pt in ImplementacionCalculos.PlantillaPorDefecto())
            {
                plan.Tareas.Add(new ImplementacionTarea
                {
                    CompanyId       = _current.CompanyId,
                    Categoria       = pt.Categoria,
                    Titulo          = pt.Titulo,
                    Orden           = pt.Orden,
                    Estado          = ImplementacionCalculos.TareaPendiente,
                    CreatedByUserId = _current.UserId
                });
            }
        }

        _ctx.ImplementacionPlanes.Add(plan);
        await _ctx.SaveChangesAsync(ct);

        return MapPlan(plan, plan.Tareas.Count, 0, 0);
    }

    public async Task<ImplementacionPlanDto?> UpdatePlanAsync(int planId, ImplementacionPlanUpdateRequest req, CancellationToken ct = default)
    {
        var plan = await GetPlanScopedAsync(planId, ct);
        if (plan is null) return null;

        plan.Nombre      = TextoRequerido(req.Nombre, "El nombre del plan", 200);
        ValidarRangoFechas(req.FechaInicio, req.FechaFin);
        plan.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        plan.FechaInicio = req.FechaInicio?.Date;
        plan.FechaFin    = req.FechaFin?.Date;
        plan.UpdatedByUserId = _current.UserId;

        // "cancelado" es el único estado manual; cualquier otro valor explícito reactiva el plan
        // y el estado real se rederiva de las tareas.
        var estadoBase = req.Estado == ImplementacionCalculos.PlanCancelado
            ? ImplementacionCalculos.PlanCancelado
            : string.IsNullOrWhiteSpace(req.Estado) ? plan.Estado : ImplementacionCalculos.PlanEnProgreso;

        var conteos = await _ctx.ImplementacionTareas
            .Where(t => t.PlanId == planId && t.DeletedAt == null)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total       = g.Count(),
                Completadas = g.Count(t => t.Estado == ImplementacionCalculos.TareaCompletada),
                Confirmadas = g.Count(t => t.Estado == ImplementacionCalculos.TareaConfirmada)
            })
            .FirstOrDefaultAsync(ct);

        var total       = conteos?.Total       ?? 0;
        var completadas = conteos?.Completadas ?? 0;
        var confirmadas = conteos?.Confirmadas ?? 0;

        plan.Estado = ImplementacionCalculos.DeterminarEstadoPlan(
            estadoBase, total, confirmadas, completadas + confirmadas);

        await _ctx.SaveChangesAsync(ct);
        return MapPlan(plan, total, completadas, confirmadas);
    }

    public async Task<bool> DeletePlanAsync(int planId, CancellationToken ct = default)
    {
        var plan = await GetPlanScopedAsync(planId, ct);
        if (plan is null) return false;

        plan.DeletedAt = DateTime.UtcNow;
        plan.UpdatedByUserId = _current.UserId;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    private static ImplementacionPlanDto MapPlan(ImplementacionPlan p, int total, int completadas, int confirmadas)
    {
        var r = ImplementacionCalculos.CalcularResumen(total, completadas, confirmadas);
        return new ImplementacionPlanDto(
            p.Id, p.CompanyId, p.PaisId, p.Nombre, p.Descripcion,
            p.FechaInicio, p.FechaFin, p.Estado,
            r.TotalTareas, r.Completadas, r.Confirmadas,
            r.PorcentajeAvance, r.PorcentajeConfirmado,
            p.CreatedAt);
    }
}
