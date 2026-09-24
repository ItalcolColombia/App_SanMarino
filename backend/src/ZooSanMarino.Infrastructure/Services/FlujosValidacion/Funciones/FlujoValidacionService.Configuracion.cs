// Catálogo de procesos + CRUD de borradores/versiones (plan §9.1, §12 F1-F2).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.FlujosValidacion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class FlujoValidacionService
{
    private const string PermisoVer = "flujos_validacion.ver";
    private const string PermisoGestionar = "flujos_validacion.gestionar";

    /// <summary>
    /// Fail-closed: el administrador interno solo opera la empresa activa; el Super Admin/admin
    /// global puede elegir cualquier empresa (plan §7.1). Exige además el permiso de gestión.
    /// </summary>
    private void AutorizarGestionDeEmpresa(int companyId)
    {
        if (!_current.Permissions.Contains(PermisoGestionar))
            throw new UnauthorizedAccessException($"No tiene el permiso '{PermisoGestionar}'.");
        if (!_current.EsAdminEmpresas && companyId != CompanyIdActiva)
            throw new UnauthorizedAccessException("No puede configurar flujos de otra empresa.");
    }

    private void AutorizarLecturaDeEmpresa(int companyId)
    {
        if (!_current.Permissions.Contains(PermisoVer) && !_current.Permissions.Contains(PermisoGestionar))
            throw new UnauthorizedAccessException($"No tiene el permiso '{PermisoVer}'.");
        if (!_current.EsAdminEmpresas && companyId != CompanyIdActiva)
            throw new UnauthorizedAccessException("No puede ver flujos de otra empresa.");
    }

    public async Task<IReadOnlyList<ProcesoValidacionDto>> ListarProcesosAsync(int companyId, CancellationToken ct = default)
    {
        AutorizarLecturaDeEmpresa(companyId);

        var procesos = await _ctx.ValidacionProcesos.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Orden)
            .ToListAsync(ct);

        var publicados = await _ctx.ValidacionFlujos.AsNoTracking()
            .Where(f => f.CompanyId == companyId && f.Estado == EstadoFlujoValidacion.Publicado)
            .Select(f => f.ProcesoId)
            .ToListAsync(ct);
        var publicadosSet = publicados.ToHashSet();

        return procesos.Select(p => new ProcesoValidacionDto(
            p.Id, p.Key, p.Nombre, p.Descripcion, p.MenuRoute, publicadosSet.Contains(p.Id))).ToList();
    }

    public async Task<FlujoValidacionDto?> ObtenerFlujoVigenteAsync(int companyId, string procesoKey, CancellationToken ct = default)
    {
        AutorizarLecturaDeEmpresa(companyId);

        var proceso = await ObtenerProcesoPorKeyAsync(procesoKey, ct);
        var flujo = await _ctx.ValidacionFlujos.AsNoTracking()
            .Include(f => f.Pasos).ThenInclude(p => p.Asignados)
            .Where(f => f.CompanyId == companyId && f.ProcesoId == proceso.Id
                     && f.Estado != EstadoFlujoValidacion.Retirado)
            .OrderByDescending(f => f.Version)
            .FirstOrDefaultAsync(ct);

        return flujo is null ? null : await ProyectarFlujoAsync(flujo, proceso.Key, ct);
    }

    public async Task<FlujoValidacionDto> CrearBorradorAsync(CrearBorradorFlujoCommand cmd, CancellationToken ct = default)
    {
        AutorizarGestionDeEmpresa(cmd.CompanyId);
        var proceso = await ObtenerProcesoPorKeyAsync(cmd.ProcesoKey, ct);

        var siguienteVersion = 1 + (await _ctx.ValidacionFlujos
            .Where(f => f.CompanyId == cmd.CompanyId && f.ProcesoId == proceso.Id)
            .Select(f => (int?)f.Version).MaxAsync(ct) ?? 0);

        var flujo = new ValidacionFlujo
        {
            CompanyId = cmd.CompanyId,
            ProcesoId = proceso.Id,
            Version = siguienteVersion,
            Nombre = cmd.Nombre,
            Estado = EstadoFlujoValidacion.Borrador,
            PlazoTotalHoras = cmd.PlazoTotalHoras,
            RequierePersonasDistintas = cmd.RequierePersonasDistintas,
            PermiteAprobacionCreador = cmd.PermiteAprobacionCreador,
            CreatedByUserId = UserIdActual,
        };
        AplicarPasos(flujo, cmd.Pasos);

        _ctx.ValidacionFlujos.Add(flujo);
        await _ctx.SaveChangesAsync(ct);

        return (await ProyectarFlujoAsync(flujo, proceso.Key, ct))!;
    }

    public async Task<FlujoValidacionDto> ActualizarBorradorAsync(int flujoId, ActualizarBorradorFlujoCommand cmd, CancellationToken ct = default)
    {
        var flujo = await _ctx.ValidacionFlujos.Include(f => f.Pasos).ThenInclude(p => p.Asignados)
            .Include(f => f.Proceso)
            .FirstOrDefaultAsync(f => f.Id == flujoId, ct)
            ?? throw new InvalidOperationException("El flujo no existe.");
        AutorizarGestionDeEmpresa(flujo.CompanyId);

        if (flujo.Estado != EstadoFlujoValidacion.Borrador)
            throw new InvalidOperationException("Solo un flujo en BORRADOR admite edición directa; publicado es inmutable.");

        flujo.Nombre = cmd.Nombre;
        flujo.PlazoTotalHoras = cmd.PlazoTotalHoras;
        flujo.RequierePersonasDistintas = cmd.RequierePersonasDistintas;
        flujo.PermiteAprobacionCreador = cmd.PermiteAprobacionCreador;
        flujo.UpdatedAt = DateTime.UtcNow;

        _ctx.ValidacionFlujoPasos.RemoveRange(flujo.Pasos);
        flujo.Pasos.Clear();
        AplicarPasos(flujo, cmd.Pasos);

        await _ctx.SaveChangesAsync(ct);
        return (await ProyectarFlujoAsync(flujo, flujo.Proceso.Key, ct))!;
    }

    /// <summary>Clona un flujo (publicado o retirado) a un nuevo BORRADOR versión N+1 (plan §5.1).</summary>
    public async Task<FlujoValidacionDto> ClonarAsync(int flujoId, CancellationToken ct = default)
    {
        var origen = await _ctx.ValidacionFlujos.AsNoTracking()
            .Include(f => f.Pasos).ThenInclude(p => p.Asignados)
            .Include(f => f.Proceso)
            .FirstOrDefaultAsync(f => f.Id == flujoId, ct)
            ?? throw new InvalidOperationException("El flujo no existe.");
        AutorizarGestionDeEmpresa(origen.CompanyId);

        var siguienteVersion = 1 + await _ctx.ValidacionFlujos
            .Where(f => f.CompanyId == origen.CompanyId && f.ProcesoId == origen.ProcesoId)
            .MaxAsync(f => (int?)f.Version, ct) ?? 1;

        var clon = new ValidacionFlujo
        {
            CompanyId = origen.CompanyId,
            ProcesoId = origen.ProcesoId,
            Version = siguienteVersion,
            Nombre = origen.Nombre,
            Estado = EstadoFlujoValidacion.Borrador,
            PlazoTotalHoras = origen.PlazoTotalHoras,
            RequierePersonasDistintas = origen.RequierePersonasDistintas,
            PermiteAprobacionCreador = origen.PermiteAprobacionCreador,
            CreatedByUserId = UserIdActual,
        };
        foreach (var paso in origen.Pasos.OrderBy(p => p.Orden))
        {
            var nuevoPaso = new ValidacionFlujoPaso
            {
                Orden = paso.Orden,
                Nombre = paso.Nombre,
                AprobacionesRequeridas = paso.AprobacionesRequeridas,
                Descripcion = paso.Descripcion,
            };
            foreach (var asignado in paso.Asignados)
            {
                nuevoPaso.Asignados.Add(new ValidacionFlujoAsignado
                {
                    Tipo = asignado.Tipo,
                    RoleId = asignado.RoleId,
                    UserId = asignado.UserId,
                    RolFiltroOrigenId = asignado.RolFiltroOrigenId,
                });
            }
            clon.Pasos.Add(nuevoPaso);
        }

        _ctx.ValidacionFlujos.Add(clon);
        await _ctx.SaveChangesAsync(ct);
        return (await ProyectarFlujoAsync(clon, origen.Proceso.Key, ct))!;
    }

    public async Task<IReadOnlyList<AsignableFlujoDto>> ListarAsignablesAsync(int companyId, CancellationToken ct = default)
    {
        AutorizarLecturaDeEmpresa(companyId);

        var roles = await _ctx.UserRoles.AsNoTracking()
            .Where(ur => ur.CompanyId == companyId)
            .Select(ur => ur.RoleId).Distinct()
            .Join(_ctx.Roles.AsNoTracking(), id => id, r => r.Id, (id, r) => new { r.Id, r.Name })
            .ToListAsync(ct);

        var usuarios = await _ctx.UserRoles.AsNoTracking()
            .Where(ur => ur.CompanyId == companyId)
            .Select(ur => ur.UserId).Distinct()
            .Join(_ctx.Users.AsNoTracking().Where(u => u.IsActive), id => id, u => u.Id,
                (id, u) => new { u.Id, u.firstName, u.surName })
            .ToListAsync(ct);

        var resultado = new List<AsignableFlujoDto>();
        resultado.AddRange(roles.Select(r => new AsignableFlujoDto(TipoAsignadoFlujo.Rol, r.Id, null, r.Name)));
        resultado.AddRange(usuarios.Select(u =>
            new AsignableFlujoDto(TipoAsignadoFlujo.Usuario, null, u.Id, $"{u.firstName} {u.surName}".Trim())));
        return resultado;
    }

    private static void AplicarPasos(ValidacionFlujo flujo, IReadOnlyList<FlujoPasoDto> pasos)
    {
        foreach (var pasoDto in pasos)
        {
            var paso = new ValidacionFlujoPaso
            {
                Orden = pasoDto.Orden,
                Nombre = pasoDto.Nombre,
                AprobacionesRequeridas = Math.Max(1, pasoDto.AprobacionesRequeridas),
                Descripcion = pasoDto.Descripcion,
            };
            foreach (var a in pasoDto.Asignados)
            {
                paso.Asignados.Add(new ValidacionFlujoAsignado
                {
                    Tipo = a.Tipo,
                    RoleId = a.RoleId,
                    UserId = a.UserId,
                });
            }
            flujo.Pasos.Add(paso);
        }
    }

    private async Task<FlujoValidacionDto?> ProyectarFlujoAsync(ValidacionFlujo flujo, string procesoKey, CancellationToken ct)
    {
        // Resuelve nombres legibles de rol/usuario para los asignados de cada paso.
        var roleIds = flujo.Pasos.SelectMany(p => p.Asignados).Where(a => a.RoleId.HasValue).Select(a => a.RoleId!.Value).Distinct().ToList();
        var userIds = flujo.Pasos.SelectMany(p => p.Asignados).Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value).Distinct().ToList();

        var rolesPorId = await _ctx.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var usuariosPorId = await _ctx.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.firstName} {u.surName}".Trim(), ct);

        var pasosDto = flujo.Pasos.OrderBy(p => p.Orden).Select(p => new FlujoPasoDto(
            p.Id, p.Orden, p.Nombre, p.Descripcion, p.AprobacionesRequeridas,
            p.Asignados.Select(a => new FlujoAsignadoDto(
                a.Id, a.Tipo, a.RoleId, a.UserId,
                a.Tipo == TipoAsignadoFlujo.Rol
                    ? rolesPorId.GetValueOrDefault(a.RoleId ?? 0, "(rol eliminado)")
                    : usuariosPorId.GetValueOrDefault(a.UserId ?? Guid.Empty, "(usuario eliminado)")
            )).ToList()
        )).ToList();

        return new FlujoValidacionDto(
            flujo.Id, flujo.CompanyId, flujo.ProcesoId, procesoKey, flujo.Version, flujo.Nombre,
            flujo.Estado, flujo.PlazoTotalHoras, flujo.RequierePersonasDistintas, flujo.PermiteAprobacionCreador,
            flujo.CreatedAt, flujo.PublishedAt, flujo.RetiredAt, pasosDto);
    }
}
