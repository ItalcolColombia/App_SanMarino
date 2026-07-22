// Implementacion/ImplementacionService.cs
// Partial 'ancla': campos, ctor, helpers compartidos y la interfaz. La lógica por concern vive en Funciones/.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ImplementacionService : IImplementacionService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    public ImplementacionService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    private static string NombreCompleto(User? u)
        => u is null ? "" : $"{u.firstName} {u.surName}".Trim();

    /// <summary>Primer correo de login del usuario (requiere UserLogins→Login cargados; null si no hay).</summary>
    private static string? EmailDe(User? u)
        => u?.UserLogins?
            .Select(ul => ul.Login?.email)
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));

    private static ImplementacionFirmaDto MapFirma(ImplementacionTareaFirma f) => new(
        f.Id,
        f.TareaId,
        f.UserId,
        NombreCompleto(f.User),
        f.User?.cedula ?? "",
        EmailDe(f.User),
        f.Estado,
        f.FirmaTexto,
        f.Nota,
        f.FechaRespuesta);

    private static ImplementacionTareaDto MapTarea(ImplementacionTarea t, DateTime hoy) => new(
        t.Id,
        t.PlanId,
        t.Categoria,
        t.Titulo,
        t.Descripcion,
        t.Orden,
        t.FechaProgramada,
        t.RoleId,
        t.Role?.Name,
        t.AsignadoUserId,
        t.AsignadoUser is null ? null : NombreCompleto(t.AsignadoUser),
        t.Estado,
        ImplementacionCalculos.EsTareaVencida(t.FechaProgramada, hoy, t.Estado),
        t.FechaCompletada,
        t.CompletadaPorUser is null ? null : NombreCompleto(t.CompletadaPorUser),
        t.FechaConfirmada,
        t.ConfirmadaPorUser is null ? null : NombreCompleto(t.ConfirmadaPorUser),
        t.Observaciones,
        (t.Firmas ?? Enumerable.Empty<ImplementacionTareaFirma>())
            .Where(f => f.DeletedAt == null)
            .OrderBy(f => f.Id)
            .Select(MapFirma)
            .ToList());

    /// <summary>Nombre completo + correo de un usuario por Guid (una consulta; (null, null) si no existe).</summary>
    private async Task<(string? Nombre, string? Email)> NombreYEmailAsync(Guid? userId, CancellationToken ct)
    {
        if (userId is null) return (null, null);
        var fila = await _ctx.Users.AsNoTracking()
            .Where(u => u.Id == userId.Value)
            .Select(u => new
            {
                Nombre = u.firstName + " " + u.surName,
                Email  = u.UserLogins.Select(ul => ul.Login.email).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);
        return fila is null ? (null, null) : (fila.Nombre.Trim(), fila.Email);
    }

    /// <summary>Plan de la empresa activa (tracked para mutaciones); null si no existe o no es de la empresa.</summary>
    private Task<ImplementacionPlan?> GetPlanScopedAsync(int planId, CancellationToken ct)
        => _ctx.ImplementacionPlanes
            .Where(p => p.Id == planId && p.CompanyId == _current.CompanyId && p.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

    /// <summary>Tarea de la empresa activa con su plan (tracked); null si no existe o el plan está borrado.</summary>
    private Task<ImplementacionTarea?> GetTareaScopedAsync(int tareaId, CancellationToken ct)
        => _ctx.ImplementacionTareas
            .Include(t => t.Plan)
            .Where(t => t.Id == tareaId && t.CompanyId == _current.CompanyId
                        && t.DeletedAt == null && t.Plan.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

    /// <summary>Recarga la tarea con sus navegaciones de nombres (incl. firmas con correo) y la mapea a DTO.</summary>
    private async Task<ImplementacionTareaDto?> ReloadTareaDtoAsync(int tareaId, CancellationToken ct)
    {
        var t = await _ctx.ImplementacionTareas.AsNoTracking()
            .Include(x => x.Role)
            .Include(x => x.AsignadoUser)
            .Include(x => x.CompletadaPorUser)
            .Include(x => x.ConfirmadaPorUser)
            .Include(x => x.Firmas.Where(f => f.DeletedAt == null))
                .ThenInclude(f => f.User)
                .ThenInclude(u => u.UserLogins)
                .ThenInclude(ul => ul.Login)
            .FirstOrDefaultAsync(x => x.Id == tareaId, ct);
        return t is null ? null : MapTarea(t, DateTime.UtcNow);
    }

    /// <summary>
    /// Rederiva el estado del plan desde los conteos reales de tareas (llamar DESPUÉS de persistir
    /// el cambio de la tarea). La agregación corre en la BD.
    /// </summary>
    private async Task RecalcularEstadoPlanAsync(int planId, CancellationToken ct)
    {
        var plan = await _ctx.ImplementacionPlanes
            .FirstOrDefaultAsync(p => p.Id == planId && p.DeletedAt == null, ct);
        if (plan is null) return;

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

        var nuevoEstado = ImplementacionCalculos.DeterminarEstadoPlan(
            plan.Estado, total, confirmadas, completadas + confirmadas);
        if (nuevoEstado != plan.Estado)
        {
            plan.Estado = nuevoEstado;
            plan.UpdatedByUserId = _current.UserId;
            await _ctx.SaveChangesAsync(ct);
        }
    }

    /// <summary>Valida rol existente y usuario perteneciente a la empresa activa (fail-closed).</summary>
    private async Task ValidarAsignacionAsync(int? roleId, Guid? asignadoUserId, CancellationToken ct)
    {
        if (roleId.HasValue &&
            !await _ctx.Roles.AnyAsync(r => r.Id == roleId.Value, ct))
            throw new InvalidOperationException("El rol responsable indicado no existe.");

        await ValidarUsuarioDeEmpresaAsync(asignadoUserId, "El usuario asignado", ct);
    }

    /// <summary>Valida que el usuario (si viene) pertenezca a la empresa activa (fail-closed).</summary>
    private async Task ValidarUsuarioDeEmpresaAsync(Guid? userId, string etiqueta, CancellationToken ct)
    {
        if (userId.HasValue &&
            !await _ctx.UserCompanies.AnyAsync(
                uc => uc.UserId == userId.Value && uc.CompanyId == _current.CompanyId, ct))
            throw new InvalidOperationException($"{etiqueta} no pertenece a la empresa activa.");
    }

    private static string TextoRequerido(string? valor, string campo, int maxLen)
    {
        var v = (valor ?? "").Trim();
        if (v.Length == 0) throw new InvalidOperationException($"{campo} es obligatorio.");
        if (v.Length > maxLen) throw new InvalidOperationException($"{campo} supera el máximo de {maxLen} caracteres.");
        return v;
    }

    private static void ValidarRangoFechas(DateTime? inicio, DateTime? fin)
    {
        if (inicio.HasValue && fin.HasValue && fin.Value.Date < inicio.Value.Date)
            throw new InvalidOperationException("La fecha fin no puede ser anterior a la fecha inicio.");
    }
}
