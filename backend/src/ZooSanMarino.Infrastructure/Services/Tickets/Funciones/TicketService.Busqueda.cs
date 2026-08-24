// src/ZooSanMarino.Infrastructure/Services/Tickets/Funciones/TicketService.Busqueda.cs
// Listados: bandeja de asignados a mi, busqueda por rol (mis tickets/gestion/admin), paginado
// y el enriquecido de filas con identidad (nombre+rol), pais y metricas derivadas.
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Correos;
using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class TicketService
{
    public async Task<PagedResult<TicketListItemDto>> GetAsignadosAsync(TicketSearchRequest req, CancellationToken ct)
    {
        var userGuid = _currentUser.UserGuid;
        if (!userGuid.HasValue)
            return new PagedResult<TicketListItemDto> { Page=1,PageSize=req.PageSize,Total=0,Items=Array.Empty<TicketListItemDto>() };

        // Sin filtro de empresa: el resolutor es global y debe ver todos sus tickets
        // independientemente de en qué subsidiaria se originaron.
        var query = _ctx.Tickets.AsNoTracking()
            .Where(x => x.AssignedToUserGuid == userGuid.Value && x.DeletedAt == null);
        return await PageAsync(ApplyFilters(query, req), req, ct);
    }

    public async Task<PagedResult<TicketListItemDto>> SearchMisTicketsAsync(TicketSearchRequest req, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var miGuid = _currentUser.UserGuid;

        // "Mis solicitudes" incluye los casos que otro registró A MI NOMBRE: si no, el usuario
        // recibiría el correo de un caso suyo que no puede ver ni cerrar.
        var query = BaseQuery(companyId).Where(x =>
            x.CreatedByUserId == _currentUser.UserId ||
            (x.SolicitanteUserId != null && x.SolicitanteUserId == _currentUser.UserId) ||
            (miGuid != null && x.SolicitanteUserGuid == miGuid));

        return await PageAsync(ApplyFilters(query, req), req, ct);
    }

    public async Task<PagedResult<TicketListItemDto>> SearchGestionAsync(TicketSearchRequest req, CancellationToken ct)
    {
        var userGuid = _currentUser.UserGuid;
        if (!userGuid.HasValue)
            return new PagedResult<TicketListItemDto> { Page=1,PageSize=req.PageSize,Total=0,Items=Array.Empty<TicketListItemDto>() };

        // Bandeja personal: solo tickets asignados explícitamente a mí.
        var query = _ctx.Tickets.AsNoTracking()
            .Where(x => x.AssignedToUserGuid == userGuid.Value && x.DeletedAt == null);
        return await PageAsync(ApplyFilters(query, req), req, ct);
    }

    /// <summary>
    /// Construye el predicado de la bandeja por perfil de resolutor:
    /// <c>tipo ∈ tiposGlobales OR (tipo,país) ∈ paresPais</c>. Un solo parámetro para que EF lo traduzca.
    /// </summary>
    private static Expression<Func<Ticket, bool>> BuildResolutorPredicate(
        List<string> tiposGlobales, List<(string Tipo, int PaisId)> paresPais)
    {
        var x = Expression.Parameter(typeof(Ticket), "x");
        Expression body = Expression.Constant(false);

        var tipoProp = Expression.Property(x, nameof(Ticket.Tipo));
        var paisProp = Expression.Property(x, nameof(Ticket.PaisId));

        if (tiposGlobales.Count > 0)
        {
            // tiposGlobales.Contains(x.Tipo)  →  x.Tipo IN (...)
            var contains = Expression.Call(
                typeof(Enumerable), nameof(Enumerable.Contains), new[] { typeof(string) },
                Expression.Constant(tiposGlobales), tipoProp);
            body = Expression.OrElse(body, contains);
        }

        foreach (var (tipo, pais) in paresPais)
        {
            var tipoEq = Expression.Equal(tipoProp, Expression.Constant(tipo));
            var paisEq = Expression.Equal(paisProp, Expression.Constant(pais));
            body = Expression.OrElse(body, Expression.AndAlso(tipoEq, paisEq));
        }

        return Expression.Lambda<Func<Ticket, bool>>(body, x);
    }

    public async Task<PagedResult<TicketListItemDto>> SearchAdminAsync(TicketSearchRequest req, CancellationToken ct)
    {
        // Admin global: todos los tickets de todas las empresas/países, sin filtro implícito.
        var query = _ctx.Tickets.AsNoTracking().Where(x => x.DeletedAt == null);
        if (req.PaisId.HasValue)
            query = query.Where(x => x.PaisId == req.PaisId.Value);
        return await PageAsync(ApplyFilters(query, req), req, ct);
    }

    public async Task<IReadOnlyList<ResolutorListItemDto>> GetResolutoresAdminAsync(CancellationToken ct)
    {
        var guids = await _ctx.Tickets.AsNoTracking()
            .Where(x => x.AssignedToUserGuid != null && x.DeletedAt == null)
            .Select(x => x.AssignedToUserGuid!.Value)
            .Distinct()
            .ToListAsync(ct);

        if (guids.Count == 0) return Array.Empty<ResolutorListItemDto>();

        var users = await _ctx.Set<User>().AsNoTracking()
            .Where(u => guids.Contains(u.Id))
            .Select(u => new { u.Id, u.firstName, u.surName })
            .ToListAsync(ct);

        return users.Select(u => new ResolutorListItemDto(u.Id, $"{u.firstName} {u.surName}".Trim()))
                    .OrderBy(r => r.Nombre)
                    .ToList();
    }

    private IQueryable<Ticket> BaseQuery(int companyId) =>
        _ctx.Tickets.AsNoTracking().Where(x => x.CompanyId == companyId && x.DeletedAt == null);

    private static IQueryable<Ticket> ApplyFilters(IQueryable<Ticket> query, TicketSearchRequest req)
    {
        if (req.Anio.HasValue)
            query = query.Where(x => x.CreatedAt.Year == req.Anio.Value);

        if (!string.IsNullOrWhiteSpace(req.Estado))
        {
            var e = req.Estado.ToUpperInvariant();
            query = query.Where(x => x.Estado == e);
        }

        if (!string.IsNullOrWhiteSpace(req.Tipo))
        {
            var t = req.Tipo.ToUpperInvariant();
            query = query.Where(x => x.Tipo == t);
        }

        if (req.AssignedToGuid.HasValue)
            query = query.Where(x => x.AssignedToUserGuid == req.AssignedToGuid.Value);

        if (!string.IsNullOrWhiteSpace(req.Prioridad))
        {
            var p = req.Prioridad.ToUpperInvariant();
            query = query.Where(x => x.Prioridad == p);
        }

        // Búsqueda libre: se resuelve en la BD (ILIKE), nunca trayendo todo a memoria.
        if (!string.IsNullOrWhiteSpace(req.Texto))
        {
            var texto = $"%{req.Texto.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Titulo, texto) ||
                EF.Functions.ILike(x.Descripcion, texto) ||
                (x.Codigo != null && EF.Functions.ILike(x.Codigo, texto)));
        }

        return query;
    }

    /// <summary>Fila intermedia del listado (incluye CompanyId y Guids para resolver nombres/roles).</summary>
    private sealed record TicketRow(
        long Id, string? Codigo, string Titulo, string Tipo, string Estado, int PaisId, int CompanyId,
        int CreatedByUserId, int? AssignedToUserId, Guid? CreatedByUserGuid, Guid? AssignedToUserGuid,
        DateTime CreatedAt, int ImgCount, int NotaCount,
        // Gestión tipo tablero
        string Prioridad, int OrdenTablero, DateTime? FechaLimite, DateTime? FechaSolucion,
        DateOnly? FechaInicioPlan, DateOnly? FechaFinPlan, decimal? HorasEstimadas,
        decimal HorasRegistradas, int CantidadTareas, int TareasListas,
        Guid? SolicitanteUserGuid, int? SolicitanteUserId);

    private async Task<PagedResult<TicketListItemDto>> PageAsync(
        IQueryable<Ticket> query, TicketSearchRequest req, CancellationToken ct)
    {
        var page = req.Page < 1 ? 1 : req.Page;
        var size = req.PageSize is < 1 or > 100 ? 20 : req.PageSize;

        var total = await query.LongCountAsync(ct);

        var rows = await ProyectarFilasAsync(
            query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * size).Take(size), ct);

        return new PagedResult<TicketListItemDto>
        {
            Page = page, PageSize = size, Total = total, Items = await MapearItemsAsync(rows, ct)
        };
    }

    /// <summary>
    /// Proyecta las filas del listado. Contadores y sumas de tareas/tiempos se traducen a
    /// subconsultas agregadas ⇒ la BD filtra y agrupa, y NO se materializa <c>imagen_base64</c>.
    /// </summary>
    private static Task<List<TicketRow>> ProyectarFilasAsync(IQueryable<Ticket> query, CancellationToken ct) =>
        query.Select(x => new TicketRow(
                x.Id, x.Codigo, x.Titulo, x.Tipo, x.Estado, x.PaisId, x.CompanyId,
                x.CreatedByUserId, x.AssignedToUserId, x.CreatedByUserGuid, x.AssignedToUserGuid,
                x.CreatedAt, x.Imagenes.Count, x.Notas.Count,
                x.Prioridad, x.OrdenTablero, x.FechaLimite, x.FechaSolucion,
                x.FechaInicioPlan, x.FechaFinPlan, x.HorasEstimadas,
                x.Tiempos.Where(t => t.DeletedAt == null).Sum(t => (decimal?)t.Horas) ?? 0m,
                x.Tareas.Count(t => t.DeletedAt == null),
                x.Tareas.Count(t => t.DeletedAt == null && t.Estado == TicketTareaEstados.Listo),
                x.SolicitanteUserGuid, x.SolicitanteUserId))
            .ToListAsync(ct);

    /// <summary>Enriquece las filas con identidad (nombre + rol), país y métricas derivadas.</summary>
    private async Task<List<TicketListItemDto>> MapearItemsAsync(List<TicketRow> rows, CancellationToken ct)
    {
        var refs = new List<(Guid Guid, int CompanyId)>();
        foreach (var r in rows)
        {
            if (r.CreatedByUserGuid.HasValue) refs.Add((r.CreatedByUserGuid.Value, r.CompanyId));
            if (r.AssignedToUserGuid.HasValue) refs.Add((r.AssignedToUserGuid.Value, r.CompanyId));
            if (r.SolicitanteUserGuid.HasValue) refs.Add((r.SolicitanteUserGuid.Value, r.CompanyId));
        }
        var (users, roles) = await BuildUserInfoAsync(refs, ct);
        var paises = await BuildPaisMapAsync(rows.Select(r => r.PaisId), ct);
        var ahora = DateTime.UtcNow;

        return rows.Select(r =>
        {
            var creadoPor = NombreDe(users, r.CreatedByUserGuid);
            var solicitante = r.SolicitanteUserGuid.HasValue
                ? NombreDe(users, r.SolicitanteUserGuid)
                : creadoPor;

            return new TicketListItemDto(
                r.Id, r.Codigo, r.Titulo, r.Tipo, r.Estado, r.PaisId,
                r.CreatedByUserId, r.AssignedToUserId, r.CreatedAt, r.ImgCount, r.NotaCount,
                creadoPor, RolDe(roles, r.CreatedByUserGuid, r.CompanyId),
                NombreDe(users, r.AssignedToUserGuid), RolDe(roles, r.AssignedToUserGuid, r.CompanyId),
                paises.GetValueOrDefault(r.PaisId),
                r.Prioridad, r.OrdenTablero, r.FechaLimite, r.FechaInicioPlan, r.FechaFinPlan,
                r.HorasEstimadas, r.HorasRegistradas, r.CantidadTareas, r.TareasListas,
                TicketMetricasCalculos.PorcentajeAvanceTareas(r.CantidadTareas, r.TareasListas),
                TicketMetricasCalculos.EstadoSla(r.FechaLimite, r.FechaSolucion, ahora),
                TicketMetricasCalculos.HorasParaVencer(r.FechaLimite, r.FechaSolucion, ahora),
                solicitante,
                r.SolicitanteUserGuid.HasValue);
        }).ToList();
    }

    /// <summary>
    /// Dado un conjunto de (Guid de usuario, empresa), devuelve nombre + email por Guid
    /// y el nombre de rol por (Guid, empresa). El rol es el que el usuario tiene en la empresa del ticket.
    /// </summary>
    private async Task<(Dictionary<Guid, (string Nombre, string? Email)> Users, Dictionary<(Guid, int), string> Roles)>
        BuildUserInfoAsync(IReadOnlyCollection<(Guid Guid, int CompanyId)> refs, CancellationToken ct)
    {
        var users = new Dictionary<Guid, (string, string?)>();
        var roles = new Dictionary<(Guid, int), string>();
        if (refs.Count == 0) return (users, roles);

        var guids = refs.Select(r => r.Guid).Distinct().ToList();
        var companyIds = refs.Select(r => r.CompanyId).Distinct().ToList();

        var rows = await _ctx.Set<User>().AsNoTracking()
            .Where(u => guids.Contains(u.Id))
            .Select(u => new
            {
                u.Id, u.firstName, u.surName,
                Email = u.UserLogins.Select(ul => ul.Login.email).FirstOrDefault()
            })
            .ToListAsync(ct);
        foreach (var u in rows)
            users[u.Id] = ($"{u.firstName} {u.surName}".Trim(), u.Email);

        var roleRows = await _ctx.Set<UserRole>().AsNoTracking()
            .Where(ur => guids.Contains(ur.UserId) && companyIds.Contains(ur.CompanyId))
            .Select(ur => new { ur.UserId, ur.CompanyId, RoleName = ur.Role.Name })
            .ToListAsync(ct);
        foreach (var r in roleRows)
            roles.TryAdd((r.UserId, r.CompanyId), r.RoleName);

        return (users, roles);
    }

    /// <summary>
    /// Resuelve nombre + rol + email de los autores de notas, identificados por su cédula numérica
    /// (<c>TicketNota.UserId</c> guarda <c>ICurrentUser.UserId</c>, que es la cédula).
    /// </summary>
    private async Task<Dictionary<int, (string? Nombre, string? Rol, string? Email)>> BuildNotaUserInfoAsync(
        List<int> userIds, int companyId, CancellationToken ct)
    {
        var result = new Dictionary<int, (string?, string?, string?)>();
        if (userIds.Count == 0) return result;

        var cedulas = userIds.Select(id => id.ToString()).Distinct().ToList();
        var users = await _ctx.Set<User>().AsNoTracking()
            .Where(u => cedulas.Contains(u.cedula))
            .Select(u => new
            {
                u.cedula, u.firstName, u.surName,
                Rol = u.UserRoles.Where(ur => ur.CompanyId == companyId)
                                 .Select(ur => ur.Role.Name).FirstOrDefault(),
                Email = u.UserLogins.Select(ul => ul.Login.email).FirstOrDefault()
            })
            .ToListAsync(ct);

        foreach (var u in users)
            if (int.TryParse(u.cedula, out var cid))
                result[cid] = ($"{u.firstName} {u.surName}".Trim(), u.Rol, u.Email);

        return result;
    }

    /// <summary>Mapea paisId → nombre del país (catálogo <c>Pais</c>).</summary>
    private async Task<Dictionary<int, string>> BuildPaisMapAsync(IEnumerable<int> paisIds, CancellationToken ct)
    {
        var ids = paisIds.Where(p => p > 0).Distinct().ToList();
        if (ids.Count == 0) return new();
        return await _ctx.Set<Pais>().AsNoTracking()
            .Where(p => ids.Contains(p.PaisId))
            .ToDictionaryAsync(p => p.PaisId, p => p.PaisNombre, ct);
    }

    /// <summary>Nombre + rol + email del usuario actual en una empresa (para respuestas inmediatas de notas).</summary>
    private async Task<(string? Nombre, string? Rol, string? Email)> ResolveCurrentUserNombreRolAsync(int companyId, CancellationToken ct)
    {
        if (!_currentUser.UserGuid.HasValue) return (null, null, null);
        var g = _currentUser.UserGuid.Value;
        var u = await _ctx.Set<User>().AsNoTracking()
            .Where(x => x.Id == g)
            .Select(x => new
            {
                x.firstName, x.surName,
                Rol = x.UserRoles.Where(ur => ur.CompanyId == companyId)
                                 .Select(ur => ur.Role.Name).FirstOrDefault(),
                Email = x.UserLogins.Select(ul => ul.Login.email).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);
        return u is null ? (null, null, null) : ($"{u.firstName} {u.surName}".Trim(), u.Rol, u.Email);
    }

    private static string? NombreDe(Dictionary<Guid, (string Nombre, string? Email)> map, Guid? g)
        => g.HasValue && map.TryGetValue(g.Value, out var v) ? v.Nombre : null;

    private static string? EmailDe(Dictionary<Guid, (string Nombre, string? Email)> map, Guid? g)
        => g.HasValue && map.TryGetValue(g.Value, out var v) ? v.Email : null;

    private static string? RolDe(Dictionary<(Guid, int), string> map, Guid? g, int companyId)
        => g.HasValue && map.TryGetValue((g.Value, companyId), out var r) ? r : null;
}
