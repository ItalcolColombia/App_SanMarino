// Preflight + publicación + retiro de un flujo (plan §5.1, §8.6, casos 6-10).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.FlujosValidacion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class FlujoValidacionService
{
    public async Task<PreflightPublicacionDto> PreflightPublicacionAsync(int flujoId, CancellationToken ct = default)
    {
        var flujo = await _ctx.ValidacionFlujos.AsNoTracking()
            .Include(f => f.Pasos).ThenInclude(p => p.Asignados)
            .FirstOrDefaultAsync(f => f.Id == flujoId, ct)
            ?? throw new InvalidOperationException("El flujo no existe.");
        AutorizarGestionDeEmpresa(flujo.CompanyId);

        var problemas = new List<string>(await ValidarPreflightAsync(flujo, ct));
        return new PreflightPublicacionDto(problemas.Count == 0, problemas);
    }

    private async Task<List<string>> ValidarPreflightAsync(ValidacionFlujo flujo, CancellationToken ct)
    {
        var topologia = flujo.Pasos
            .Select(p => new FlujoValidacionCalculos.PasoTopologia(p.Orden, p.Asignados.Count > 0))
            .ToList();
        var problemas = FlujoValidacionCalculos.ValidarTopologiaParaPublicar(topologia).ToList();

        // Rol/usuario ajeno o inactivo (casos 7 del plan): el rol debe tener al menos una asignación
        // vigente en user_roles para ESTA empresa, y el usuario debe existir, estar activo y asignado.
        var roleIds = flujo.Pasos.SelectMany(p => p.Asignados).Where(a => a.RoleId.HasValue).Select(a => a.RoleId!.Value).Distinct().ToList();
        var userIds = flujo.Pasos.SelectMany(p => p.Asignados).Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value).Distinct().ToList();

        var rolesVigentesEnEmpresa = await _ctx.UserRoles.AsNoTracking()
            .Where(ur => ur.CompanyId == flujo.CompanyId && roleIds.Contains(ur.RoleId))
            .Select(ur => ur.RoleId).Distinct().ToListAsync(ct);
        foreach (var roleId in roleIds.Except(rolesVigentesEnEmpresa))
        {
            var nombreRol = await _ctx.Roles.AsNoTracking().Where(r => r.Id == roleId)
                .Select(r => r.Name).FirstOrDefaultAsync(ct) ?? $"#{roleId}";
            problemas.Add($"El rol '{nombreRol}' no tiene ningún usuario activo asignado en esta empresa.");
        }

        var usuariosActivosAsignados = await _ctx.UserRoles.AsNoTracking()
            .Where(ur => ur.CompanyId == flujo.CompanyId && userIds.Contains(ur.UserId))
            .Join(_ctx.Users.AsNoTracking().Where(u => u.IsActive), ur => ur.UserId, u => u.Id, (ur, u) => u.Id)
            .Distinct().ToListAsync(ct);
        foreach (var userId in userIds.Except(usuariosActivosAsignados))
        {
            problemas.Add($"El usuario {userId} no está activo o no está asignado a esta empresa.");
        }

        return problemas;
    }

    public async Task<FlujoValidacionDto> PublicarAsync(int flujoId, CancellationToken ct = default)
    {
        var flujo = await _ctx.ValidacionFlujos
            .Include(f => f.Pasos).ThenInclude(p => p.Asignados)
            .Include(f => f.Proceso)
            .FirstOrDefaultAsync(f => f.Id == flujoId, ct)
            ?? throw new InvalidOperationException("El flujo no existe.");
        AutorizarGestionDeEmpresa(flujo.CompanyId);

        if (flujo.Estado != EstadoFlujoValidacion.Borrador)
            throw new InvalidOperationException("Solo un BORRADOR puede publicarse.");

        var problemas = await ValidarPreflightAsync(flujo, ct);
        if (problemas.Count > 0)
            throw new InvalidOperationException("No se puede publicar: " + string.Join(" | ", problemas));

        // Retira la versión PUBLICADO anterior (si existe): el índice único parcial exige que solo
        // haya una vigente por empresa/proceso. Las instancias en curso conservan su FlujoId, así que
        // retirar no las afecta (plan §5.1).
        var anteriorPublicado = await _ctx.ValidacionFlujos
            .Where(f => f.CompanyId == flujo.CompanyId && f.ProcesoId == flujo.ProcesoId
                     && f.Estado == EstadoFlujoValidacion.Publicado)
            .FirstOrDefaultAsync(ct);
        if (anteriorPublicado is not null)
        {
            anteriorPublicado.Estado = EstadoFlujoValidacion.Retirado;
            anteriorPublicado.RetiredAt = DateTime.UtcNow;
        }

        flujo.Estado = EstadoFlujoValidacion.Publicado;
        flujo.PublishedAt = DateTime.UtcNow;
        flujo.PublishedByUserId = UserIdActual;

        await _ctx.SaveChangesAsync(ct);
        return (await ProyectarFlujoAsync(flujo, flujo.Proceso.Key, ct))!;
    }

    public async Task<FlujoValidacionDto> RetirarAsync(int flujoId, CancellationToken ct = default)
    {
        var flujo = await _ctx.ValidacionFlujos
            .Include(f => f.Pasos).ThenInclude(p => p.Asignados)
            .Include(f => f.Proceso)
            .FirstOrDefaultAsync(f => f.Id == flujoId, ct)
            ?? throw new InvalidOperationException("El flujo no existe.");
        AutorizarGestionDeEmpresa(flujo.CompanyId);

        if (flujo.Estado != EstadoFlujoValidacion.Publicado)
            throw new InvalidOperationException("Solo un flujo PUBLICADO puede retirarse.");

        flujo.Estado = EstadoFlujoValidacion.Retirado;
        flujo.RetiredAt = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
        return (await ProyectarFlujoAsync(flujo, flujo.Proceso.Key, ct))!;
    }
}
