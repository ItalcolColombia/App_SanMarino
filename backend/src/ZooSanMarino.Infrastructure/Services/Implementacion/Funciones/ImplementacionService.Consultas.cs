// Implementacion/Funciones/ImplementacionService.Consultas.cs
// "Mis tareas" del usuario actual + combos de asignación (usuarios y roles de la empresa activa).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ImplementacionService
{
    public async Task<List<ImplementacionMiTareaDto>> GetMisTareasAsync(CancellationToken ct = default)
    {
        var uid = _current.UserGuid;
        if (uid is null) return new List<ImplementacionMiTareaDto>();

        var filas = await _ctx.ImplementacionTareas.AsNoTracking()
            .Where(t => t.DeletedAt == null
                        && t.CompanyId == _current.CompanyId
                        && t.AsignadoUserId == uid
                        && t.Plan.DeletedAt == null
                        && t.Plan.Estado != ImplementacionCalculos.PlanCancelado)
            .OrderBy(t => t.Estado == ImplementacionCalculos.TareaConfirmada ? 1 : 0)
            .ThenBy(t => t.FechaProgramada == null)
            .ThenBy(t => t.FechaProgramada)
            .ThenBy(t => t.Orden)
            .Select(t => new
            {
                t.Id, t.PlanId, PlanNombre = t.Plan.Nombre,
                t.Categoria, t.Titulo, t.Descripcion,
                t.FechaProgramada, t.Estado,
                t.FechaCompletada,
                CompletadaPor = t.CompletadaPorUser == null
                    ? null
                    : t.CompletadaPorUser.firstName + " " + t.CompletadaPorUser.surName,
                t.FechaConfirmada, t.Observaciones
            })
            .ToListAsync(ct);

        var hoy = DateTime.UtcNow;
        return filas.Select(f => new ImplementacionMiTareaDto(
            f.Id, f.PlanId, f.PlanNombre, f.Categoria, f.Titulo, f.Descripcion,
            f.FechaProgramada, f.Estado,
            ImplementacionCalculos.EsTareaVencida(f.FechaProgramada, hoy, f.Estado),
            f.FechaCompletada, f.CompletadaPor, f.FechaConfirmada, f.Observaciones)).ToList();
    }

    public Task<List<ImplementacionUsuarioAsignableDto>> GetUsuariosAsignablesAsync(CancellationToken ct = default)
        => (from uc in _ctx.UserCompanies.AsNoTracking()
            where uc.CompanyId == _current.CompanyId
            join u in _ctx.Users.AsNoTracking() on uc.UserId equals u.Id
            where u.IsActive
            orderby u.firstName, u.surName
            select new ImplementacionUsuarioAsignableDto(u.Id, u.firstName + " " + u.surName, u.cedula))
           .ToListAsync(ct);

    public Task<List<ImplementacionRolAsignableDto>> GetRolesAsignablesAsync(CancellationToken ct = default)
        => (from rc in _ctx.RoleCompanies.AsNoTracking()
            where rc.CompanyId == _current.CompanyId
            join r in _ctx.Roles.AsNoTracking() on rc.RoleId equals r.Id
            orderby r.Name
            select new ImplementacionRolAsignableDto(r.Id, r.Name))
           .ToListAsync(ct);
}
