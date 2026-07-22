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
                Confirmadas = p.Tareas.Count(t => t.DeletedAt == null && t.Estado == ImplementacionCalculos.TareaConfirmada),
                ImplNombre  = p.ImplementadorUser == null ? null : p.ImplementadorUser.firstName + " " + p.ImplementadorUser.surName,
                ImplEmail   = p.ImplementadorUser == null ? null : p.ImplementadorUser.UserLogins.Select(ul => ul.Login.email).FirstOrDefault(),
                CreaNombre  = p.CreadoPorUser == null ? null : p.CreadoPorUser.firstName + " " + p.CreadoPorUser.surName,
                CreaEmail   = p.CreadoPorUser == null ? null : p.CreadoPorUser.UserLogins.Select(ul => ul.Login.email).FirstOrDefault()
            })
            .ToListAsync(ct);

        return filas.Select(f => MapPlan(
            f.Plan, f.Total, f.Completadas, f.Confirmadas,
            f.ImplNombre, f.ImplEmail, f.CreaNombre, f.CreaEmail)).ToList();
    }

    public async Task<ImplementacionPlanDetalleDto?> GetPlanDetalleAsync(int planId, CancellationToken ct = default)
    {
        var fila = await _ctx.ImplementacionPlanes.AsNoTracking()
            .Where(p => p.Id == planId && p.CompanyId == _current.CompanyId && p.DeletedAt == null)
            .Select(p => new
            {
                Plan       = p,
                ImplNombre = p.ImplementadorUser == null ? null : p.ImplementadorUser.firstName + " " + p.ImplementadorUser.surName,
                ImplEmail  = p.ImplementadorUser == null ? null : p.ImplementadorUser.UserLogins.Select(ul => ul.Login.email).FirstOrDefault(),
                CreaNombre = p.CreadoPorUser == null ? null : p.CreadoPorUser.firstName + " " + p.CreadoPorUser.surName,
                CreaEmail  = p.CreadoPorUser == null ? null : p.CreadoPorUser.UserLogins.Select(ul => ul.Login.email).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);
        if (fila is null) return null;

        var tareas = await _ctx.ImplementacionTareas.AsNoTracking()
            .Include(t => t.Role)
            .Include(t => t.AsignadoUser)
            .Include(t => t.CompletadaPorUser)
            .Include(t => t.ConfirmadaPorUser)
            .Include(t => t.Firmas.Where(f => f.DeletedAt == null))
                .ThenInclude(f => f.User)
                .ThenInclude(u => u.UserLogins)
                .ThenInclude(ul => ul.Login)
            .Where(t => t.PlanId == planId && t.DeletedAt == null)
            .OrderBy(t => t.Orden).ThenBy(t => t.Id)
            .ToListAsync(ct);

        var hoy = DateTime.UtcNow;
        var completadas = tareas.Count(t => t.Estado == ImplementacionCalculos.TareaCompletada);
        var confirmadas = tareas.Count(t => t.Estado == ImplementacionCalculos.TareaConfirmada);

        return new ImplementacionPlanDetalleDto(
            MapPlan(fila.Plan, tareas.Count, completadas, confirmadas,
                    fila.ImplNombre, fila.ImplEmail, fila.CreaNombre, fila.CreaEmail),
            tareas.Select(t => MapTarea(t, hoy)).ToList());
    }

    public async Task<ImplementacionPlanDto> CreatePlanAsync(ImplementacionPlanCreateRequest req, CancellationToken ct = default)
    {
        var nombre = TextoRequerido(req.Nombre, "El nombre del plan", 200);
        var tipo   = ImplementacionCalculos.NormalizarTipoPlan(req.Tipo);
        ValidarRangoFechas(req.FechaInicio, req.FechaFin);

        // Encargado: el elegido en "implementador diferente" o, por defecto, el mismo creador.
        await ValidarUsuarioDeEmpresaAsync(req.ImplementadorUserId, "El implementador elegido", ct);
        var implementadorId = req.ImplementadorUserId ?? _current.UserGuid;

        var plan = new ImplementacionPlan
        {
            CompanyId           = _current.CompanyId,
            PaisId              = _current.PaisId,
            Nombre              = nombre,
            Descripcion         = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Tipo                = tipo,
            FechaInicio         = req.FechaInicio?.Date,
            FechaFin            = req.FechaFin?.Date,
            Estado              = ImplementacionCalculos.PlanBorrador,
            ImplementadorUserId = implementadorId,
            CreadoPorUserGuid   = _current.UserGuid,
            CreatedByUserId     = _current.UserId
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

        var (implNombre, implEmail) = await NombreYEmailAsync(plan.ImplementadorUserId, ct);
        var (creaNombre, creaEmail) = await NombreYEmailAsync(plan.CreadoPorUserGuid, ct);
        return MapPlan(plan, plan.Tareas.Count, 0, 0, implNombre, implEmail, creaNombre, creaEmail);
    }

    public async Task<ImplementacionPlanDto?> UpdatePlanAsync(int planId, ImplementacionPlanUpdateRequest req, CancellationToken ct = default)
    {
        var plan = await GetPlanScopedAsync(planId, ct);
        if (plan is null) return null;

        plan.Nombre      = TextoRequerido(req.Nombre, "El nombre del plan", 200);
        plan.Tipo        = ImplementacionCalculos.NormalizarTipoPlan(req.Tipo);
        ValidarRangoFechas(req.FechaInicio, req.FechaFin);
        plan.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        plan.FechaInicio = req.FechaInicio?.Date;
        plan.FechaFin    = req.FechaFin?.Date;

        // Encargado: null en el request = "el creador" (si el plan es viejo y no guardó guid, queda null).
        await ValidarUsuarioDeEmpresaAsync(req.ImplementadorUserId, "El implementador elegido", ct);
        plan.ImplementadorUserId = req.ImplementadorUserId ?? plan.CreadoPorUserGuid;
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

        var (implNombre, implEmail) = await NombreYEmailAsync(plan.ImplementadorUserId, ct);
        var (creaNombre, creaEmail) = await NombreYEmailAsync(plan.CreadoPorUserGuid, ct);
        return MapPlan(plan, total, completadas, confirmadas, implNombre, implEmail, creaNombre, creaEmail);
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

    private static ImplementacionPlanDto MapPlan(
        ImplementacionPlan p, int total, int completadas, int confirmadas,
        string? implementadorNombre, string? implementadorEmail,
        string? creadoPorNombre, string? creadoPorEmail)
    {
        var r = ImplementacionCalculos.CalcularResumen(total, completadas, confirmadas);
        return new ImplementacionPlanDto(
            p.Id, p.CompanyId, p.PaisId, p.Nombre, p.Descripcion, p.Tipo,
            p.FechaInicio, p.FechaFin, p.Estado,
            p.ImplementadorUserId, implementadorNombre?.Trim(), implementadorEmail,
            p.CreadoPorUserGuid, creadoPorNombre?.Trim(), creadoPorEmail,
            r.TotalTareas, r.Completadas, r.Confirmadas,
            r.PorcentajeAvance, r.PorcentajeConfirmado,
            p.CreatedAt);
    }
}
