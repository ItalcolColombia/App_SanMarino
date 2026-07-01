using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Adaptador del módulo de tickets. Calca el patrón de <c>LesionService</c>:
/// resuelve la empresa efectiva con <see cref="ICompanyResolver"/> y toma país/usuario
/// de <see cref="ICurrentUser"/>. Ningún listado materializa <c>imagen_base64</c>.
/// </summary>
public class TicketService : ITicketService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;
    private readonly IEmailQueueService _emailQueue;
    private readonly IConfiguration _configuration;
    private readonly string _logoUrl;
    private readonly string _brandName;
    private readonly string _brandTagline;
    private readonly string _applicationUrl;

    public TicketService(ZooSanMarinoContext ctx, ICurrentUser currentUser,
        ICompanyResolver companyResolver, IEmailQueueService emailQueue, IConfiguration configuration)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
        _emailQueue = emailQueue;
        _configuration = configuration;
        _applicationUrl = _configuration["Email:ApplicationUrl"] ?? "http://localhost:4200";
        _brandName = _configuration["Email:BrandName"] ?? "ItalGranja";
        _brandTagline = _configuration["Email:Tagline"] ?? "Gestión de granjas avícolas · Italcol";
        _logoUrl = _configuration["Email:LogoUrl"] ?? $"{_applicationUrl}/assets/brand/logo_intalfoods_zootenico.png";
    }

    private string BrandLine => $"{_brandName} · {_brandTagline}";

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var cid = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName);
            if (cid.HasValue) return cid.Value;
        }
        return _currentUser.CompanyId;
    }

    // ───────────────────────────── CREATE ─────────────────────────────
    public async Task<TicketDetailDto> CreateAsync(CreateTicketRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo))
            throw new InvalidOperationException("El título es requerido.");
        if (string.IsNullOrWhiteSpace(req.Descripcion))
            throw new InvalidOperationException("La descripción es requerida.");
        if (!TicketTipos.EsValido(req.Tipo))
            throw new InvalidOperationException("Tipo inválido. Use: SOPORTE, DESARROLLO, REQUERIMIENTO o DUDAS.");

        var companyId = await GetEffectiveCompanyIdAsync();
        var now = DateTime.UtcNow;

        // Validar que el resolutor sea asignable para (tipo, país): directo O por rol.
        var paisId = _currentUser.PaisId;
        var tipoUpper = req.Tipo.ToUpperInvariant();

        var resolutorValido = await _ctx.TicketResolutores.AsNoTracking()
            .AnyAsync(r => r.UserId == req.AssignedToUserGuid &&
                           r.Tipo == tipoUpper &&
                           r.Activo &&
                           (r.PaisId == null || r.PaisId == paisId), ct);

        if (!resolutorValido)
        {
            // Verificar si es resolutor por rol.
            var roleIds = await _ctx.TicketResolutorRoles.AsNoTracking()
                .Where(r => r.Activo && r.Tipo == tipoUpper &&
                            (r.PaisId == null || r.PaisId == paisId))
                .Select(r => r.RoleId)
                .ToListAsync(ct);

            // Sin filtro de company: los resolutores por rol son globales. Espeja a
            // TicketPerfilService.GetAsignablesInternalAsync para que el resolutor ofrecido en el
            // dropdown (admin con rol en la empresa central) también valide al crear en otra empresa.
            if (roleIds.Count > 0)
                resolutorValido = await _ctx.UserRoles.AsNoTracking()
                    .AnyAsync(ur => ur.UserId == req.AssignedToUserGuid &&
                                    roleIds.Contains(ur.RoleId), ct);
        }

        if (!resolutorValido)
            throw new InvalidOperationException("El resolutor seleccionado no está disponible para este tipo y país.");

        var entity = new Ticket
        {
            CompanyId            = companyId,
            PaisId               = paisId ?? 0,
            Tipo                 = req.Tipo.ToUpperInvariant(),
            Estado               = TicketEstados.Abierto,
            Titulo               = req.Titulo.Trim(),
            Descripcion          = req.Descripcion.Trim(),
            CreatedByUserId      = _currentUser.UserId,
            CreatedByUserGuid    = _currentUser.UserGuid,
            AssignedToUserGuid   = req.AssignedToUserGuid,
            CreatedAt            = now,
            Status               = "A"
        };

        if (req.Imagenes is { Count: > 0 })
        {
            foreach (var img in req.Imagenes)
            {
                if (string.IsNullOrWhiteSpace(img.Base64)) continue;
                entity.Imagenes.Add(new TicketImagen
                {
                    ImagenBase64 = img.Base64,
                    FileName     = img.FileName,
                    ContentType  = img.ContentType,
                    SizeBytes    = img.SizeBytes,
                    CreatedAt    = now
                });
            }
        }

        _ctx.Tickets.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        // Código legible una vez disponible el Id (libre de colisiones).
        entity.Codigo = $"TK-{now:yyyy}-{entity.Id:D6}";
        await _ctx.SaveChangesAsync(ct);

        // Notificados (copiados): resolver email + nombre por cada Guid recibido. Se omite
        // silenciosamente cualquier Guid sin email (Email es requerido en TicketNotificado).
        List<TicketNotificado> notificadosPersistidos = new();
        if (req.NotificarUserGuids is { Count: > 0 })
        {
            var guids = req.NotificarUserGuids.Distinct().ToList();
            var infos = await _ctx.Set<User>().AsNoTracking()
                .Where(u => guids.Contains(u.Id))
                .Select(u => new
                {
                    u.Id, u.firstName, u.surName, u.cedula,
                    Email = u.UserLogins.Select(ul => ul.Login.email).FirstOrDefault()
                })
                .ToListAsync(ct);

            foreach (var info in infos)
            {
                if (string.IsNullOrWhiteSpace(info.Email)) continue;
                var notificado = new TicketNotificado
                {
                    TicketId        = entity.Id,
                    UserGuid        = info.Id,
                    Cedula          = info.cedula,
                    Email           = info.Email!,
                    Nombre          = $"{info.firstName} {info.surName}".Trim(),
                    CreatedAt       = now,
                    CreatedByUserId = _currentUser.UserId
                };
                _ctx.TicketNotificados.Add(notificado);
                notificadosPersistidos.Add(notificado);
            }

            if (notificadosPersistidos.Count > 0)
                await _ctx.SaveChangesAsync(ct);
        }

        // Encolar correo "ticket_creado" a cada notificado. No bloquea la creación del ticket.
        if (notificadosPersistidos.Count > 0)
        {
            var (_, creadorNombre) = await ResolveSolicitanteEmailAsync(entity.CreatedByUserGuid, entity.CreatedByUserId, ct);
            var asignadoNombre = await ResolveNombrePorGuidAsync(entity.AssignedToUserGuid, ct);
            var body = TicketEmailTemplates.Creado(entity, creadorNombre, asignadoNombre,
                _logoUrl, _brandName, BrandLine, _applicationUrl);

            foreach (var notificado in notificadosPersistidos)
            {
                try
                {
                    await _emailQueue.EnqueueEmailAsync(
                        notificado.Email,
                        $"[{entity.Codigo}] Nuevo ticket: {entity.Titulo}",
                        body,
                        "ticket_creado",
                        $"{{\"ticketId\":{entity.Id},\"codigo\":\"{entity.Codigo}\"}}");
                }
                catch { /* si la cola falla, no bloquea la creación */ }
            }
        }

        return (await GetByIdInternalAsync(entity.Id, ct))!;
    }

    // ─────────────────── BANDEJA ASIGNADOS A MÍ ───────────────────────────
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

    // ─────────────────── TRANSFERIR (REQUERIMIENTO → DESARROLLO) ──────────
    public async Task<TicketDetailDto?> TransferirAsync(long id, TransferirTicketRequest req, CancellationToken ct)
    {
        // Cross-company: el ticket se ubica por id (resolutores globales).
        var ticket = await _ctx.Tickets
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (ticket is null) return null;

        if (ticket.Tipo != TicketTipos.Requerimiento)
            throw new InvalidOperationException("Solo se pueden transferir tickets de tipo REQUERIMIENTO.");

        // Validar que el nuevo asignado sea resolutor de DESARROLLO en el país del ticket.
        // Sin filtro de company: los resolutores son globales.
        var resolutorValido = await _ctx.TicketResolutores.AsNoTracking()
            .AnyAsync(r => r.UserId == req.NuevoAsignadoGuid &&
                           r.Tipo == TicketTipos.Desarrollo &&
                           r.Activo &&
                           (r.PaisId == null || r.PaisId == ticket.PaisId), ct);
        if (!resolutorValido)
            throw new InvalidOperationException("El usuario destino no es resolutor de DESARROLLO en este país.");

        var now = DateTime.UtcNow;
        ticket.Tipo                = TicketTipos.Desarrollo;
        ticket.Estado              = TicketEstados.Transferido;
        ticket.AssignedToUserGuid  = req.NuevoAsignadoGuid;
        ticket.AssignedToUserId    = null;   // reset int legacy
        ticket.UpdatedByUserId     = _currentUser.UserId;
        ticket.UpdatedAt           = now;

        var nota = string.IsNullOrWhiteSpace(req.Nota)
            ? "Ticket transferido a Desarrollo."
            : req.Nota.Trim();

        _ctx.TicketNotas.Add(new TicketNota
        {
            TicketId         = ticket.Id,
            UserId           = _currentUser.UserId,
            Nota             = nota,
            EstadoResultante = TicketEstados.Transferido,
            CreatedAt        = now
        });

        await _ctx.SaveChangesAsync(ct);

        // Notificar al nuevo resolutor que le asignaron un ticket. No bloquea la transferencia.
        try
        {
            var (nuevoEmail, nuevoNombre) = await ResolveSolicitanteEmailAsync(req.NuevoAsignadoGuid, 0, ct);
            if (!string.IsNullOrWhiteSpace(nuevoEmail))
            {
                var asignadorNombre = await ResolveNombrePorGuidAsync(_currentUser.UserGuid, ct);
                var body = TicketEmailTemplates.Asignado(ticket, nuevoNombre, asignadorNombre,
                    _logoUrl, _brandName, BrandLine, _applicationUrl);
                await _emailQueue.EnqueueEmailAsync(
                    nuevoEmail!,
                    $"[{ticket.Codigo}] Te transfirieron un ticket",
                    body,
                    "ticket_transferido",
                    $"{{\"ticketId\":{ticket.Id},\"codigo\":\"{ticket.Codigo}\"}}");
            }
        }
        catch { /* si la cola falla, no bloquea la transferencia */ }

        return await GetByIdInternalAsync(id, ct);
    }

    // ───────────────────────────── LISTADOS ─────────────────────────────
    public async Task<PagedResult<TicketListItemDto>> SearchMisTicketsAsync(TicketSearchRequest req, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var query = BaseQuery(companyId).Where(x => x.CreatedByUserId == _currentUser.UserId);
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

        return query;
    }

    /// <summary>Fila intermedia del listado (incluye CompanyId y Guids para resolver nombres/roles).</summary>
    private sealed record TicketRow(
        long Id, string? Codigo, string Titulo, string Tipo, string Estado, int PaisId, int CompanyId,
        int CreatedByUserId, int? AssignedToUserId, Guid? CreatedByUserGuid, Guid? AssignedToUserGuid,
        DateTime CreatedAt, int ImgCount, int NotaCount);

    private async Task<PagedResult<TicketListItemDto>> PageAsync(
        IQueryable<Ticket> query, TicketSearchRequest req, CancellationToken ct)
    {
        var page = req.Page < 1 ? 1 : req.Page;
        var size = req.PageSize is < 1 or > 100 ? 20 : req.PageSize;

        var total = await query.LongCountAsync(ct);

        // Proyección: Imagenes.Count / Notas.Count se traducen a subconsultas COUNT,
        // por lo que NO se materializa imagen_base64 en los listados.
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new TicketRow(
                x.Id, x.Codigo, x.Titulo, x.Tipo, x.Estado, x.PaisId, x.CompanyId,
                x.CreatedByUserId, x.AssignedToUserId, x.CreatedByUserGuid, x.AssignedToUserGuid,
                x.CreatedAt, x.Imagenes.Count, x.Notas.Count))
            .ToListAsync(ct);

        // Enriquecer con nombre completo + rol (en la empresa de cada ticket) + país.
        var refs = new List<(Guid Guid, int CompanyId)>();
        foreach (var r in rows)
        {
            if (r.CreatedByUserGuid.HasValue) refs.Add((r.CreatedByUserGuid.Value, r.CompanyId));
            if (r.AssignedToUserGuid.HasValue) refs.Add((r.AssignedToUserGuid.Value, r.CompanyId));
        }
        var (users, roles) = await BuildUserInfoAsync(refs, ct);
        var paises = await BuildPaisMapAsync(rows.Select(r => r.PaisId), ct);

        var items = rows.Select(r => new TicketListItemDto(
            r.Id, r.Codigo, r.Titulo, r.Tipo, r.Estado, r.PaisId,
            r.CreatedByUserId, r.AssignedToUserId, r.CreatedAt, r.ImgCount, r.NotaCount,
            NombreDe(users, r.CreatedByUserGuid),  RolDe(roles, r.CreatedByUserGuid, r.CompanyId),
            NombreDe(users, r.AssignedToUserGuid), RolDe(roles, r.AssignedToUserGuid, r.CompanyId),
            paises.GetValueOrDefault(r.PaisId)))
            .ToList();

        return new PagedResult<TicketListItemDto>
        {
            Page = page, PageSize = size, Total = total, Items = items
        };
    }

    // ───────────────────────── Resolución de identidad (nombre + rol) ─────────────────────────

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

    // ───────────────────────────── DETALLE ─────────────────────────────
    public async Task<TicketDetailDto?> GetByIdAsync(long id, CancellationToken ct)
    {
        // Cross-company: el ticket se busca por id (los resolutores son globales).
        // La autorización se valida por visibilidad, no por empresa activa.
        var meta = await _ctx.Tickets.AsNoTracking()
            .Where(x => x.Id == id && x.DeletedAt == null)
            .Select(x => new { x.PaisId, x.Tipo, x.CreatedByUserId, x.CreatedByUserGuid, x.AssignedToUserGuid })
            .FirstOrDefaultAsync(ct);
        if (meta is null) return null;

        if (!await PuedeVerTicketAsync(meta.PaisId, meta.Tipo, meta.CreatedByUserId,
                                       meta.CreatedByUserGuid, meta.AssignedToUserGuid, ct))
            return null;   // 404: no revela existencia de tickets ajenos

        return await GetByIdInternalAsync(id, ct);
    }

    /// <summary>
    /// Reglas de visibilidad de un ticket: lo ve su creador, su asignado, un resolutor
    /// cuyo perfil matchea (tipo, país), o cualquiera con <c>tickets.admin</c>.
    /// </summary>
    private async Task<bool> PuedeVerTicketAsync(int paisId, string tipo, int createdByUserId,
        Guid? createdByGuid, Guid? assignedGuid, CancellationToken ct)
    {
        if (createdByUserId != 0 && createdByUserId == _currentUser.UserId) return true;

        var userGuid = _currentUser.UserGuid;
        if (userGuid.HasValue)
        {
            if (createdByGuid == userGuid.Value) return true;   // creador (guid)
            if (assignedGuid == userGuid.Value) return true;    // asignado
        }

        if (_currentUser.Permissions.Contains("tickets.admin", StringComparer.OrdinalIgnoreCase))
            return true;

        if (userGuid.HasValue)
        {
            var esResolutor = await _ctx.TicketResolutores.AsNoTracking()
                .AnyAsync(r => r.UserId == userGuid.Value && r.Activo &&
                               r.Tipo == tipo && (r.PaisId == null || r.PaisId == paisId), ct);
            if (esResolutor) return true;
        }

        return false;
    }

    private async Task<TicketDetailDto?> GetByIdInternalAsync(long id, CancellationToken ct)
    {
        var t = await _ctx.Tickets.AsNoTracking()
            .Where(x => x.Id == id && x.DeletedAt == null)
            .Select(x => new
            {
                x.Id, x.Codigo, x.Titulo, x.Tipo, x.Estado, x.Descripcion, x.PaisId, x.CompanyId,
                x.CreatedByUserId, x.AssignedToUserId, x.CreatedByUserGuid, x.AssignedToUserGuid,
                x.CreatedAt, x.FechaPrimeraApertura, x.FechaSolucion,
                x.SolucionDescripcion, x.FechaCierreSolicitante,
                x.NotificadoCorreo, x.FechaNotificacionCorreo, x.CorreoNotificadoA,
                Notas = x.Notas.OrderBy(n => n.CreatedAt)
                    .Select(n => new { n.Id, n.UserId, n.Nota, n.EstadoResultante, n.EsInterna, n.CreatedAt })
                    .ToList(),
                // Solo metadata — NO imagen_base64.
                Imagenes = x.Imagenes.OrderBy(i => i.CreatedAt)
                    .Select(i => new TicketImagenMetaDto(i.Id, i.FileName, i.ContentType, i.SizeBytes, i.CreatedAt))
                    .ToList(),
                // Adjuntos — solo metadata (sin contenido_base64).
                Adjuntos = x.Adjuntos.OrderBy(a => a.CreatedAt)
                    .Select(a => new { a.Id, a.Tipo, a.FileName, a.ContentType, a.SizeBytes, a.Url, a.Titulo, a.CreatedByUserId, a.CreatedAt })
                    .ToList(),
                Notificados = x.Notificados.OrderBy(n => n.CreatedAt)
                    .Select(n => new TicketNotificadoDto(n.Id, n.UserGuid, n.Nombre, n.Email))
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (t is null) return null;

        // Identidad por Guid (creador/asignado) — rol en la empresa del ticket.
        var refs = new List<(Guid Guid, int CompanyId)>();
        if (t.CreatedByUserGuid.HasValue) refs.Add((t.CreatedByUserGuid.Value, t.CompanyId));
        if (t.AssignedToUserGuid.HasValue) refs.Add((t.AssignedToUserGuid.Value, t.CompanyId));
        var (users, roles) = await BuildUserInfoAsync(refs, ct);

        // Identidad por cédula (autores de notas + adjuntos + fallback de creador/asignado sin Guid,
        // para tickets antiguos creados antes de poblar created_by_user_guid).
        var cedulaIds = t.Notas.Select(n => n.UserId)
            .Concat(t.Adjuntos.Select(a => a.CreatedByUserId))
            .Append(t.CreatedByUserId)
            .Append(t.AssignedToUserId ?? 0)
            .Where(uid => uid != 0).Distinct().ToList();
        var cedInfo = await BuildNotaUserInfoAsync(cedulaIds, t.CompanyId, ct);

        var paisNombre = (await BuildPaisMapAsync(new[] { t.PaisId }, ct)).GetValueOrDefault(t.PaisId);

        var miUserId = _currentUser.UserId;
        var notasDto = t.Notas.Select(n =>
        {
            cedInfo.TryGetValue(n.UserId, out var info);
            return new TicketNotaDto(n.Id, n.UserId, n.Nota, n.EstadoResultante, n.EsInterna, n.CreatedAt,
                info.Nombre, info.Rol, info.Email, EsMio: n.UserId != 0 && n.UserId == miUserId);
        }).ToList();

        var soyCreador = (t.CreatedByUserId != 0 && t.CreatedByUserId == miUserId)
                         || (_currentUser.UserGuid.HasValue && t.CreatedByUserGuid == _currentUser.UserGuid.Value);

        // Resuelve nombre/rol/email: Guid primero; si no hay, cae a cédula.
        (string? Nombre, string? Rol, string? Email) Resolver(Guid? guid, int cedula)
        {
            if (guid.HasValue && users.TryGetValue(guid.Value, out var u))
                return (u.Nombre, RolDe(roles, guid, t.CompanyId), u.Email);
            if (cedula != 0 && cedInfo.TryGetValue(cedula, out var c))
                return (c.Nombre, c.Rol, c.Email);
            return (null, null, null);
        }

        var creador  = Resolver(t.CreatedByUserGuid, t.CreatedByUserId);
        var asignado = Resolver(t.AssignedToUserGuid, t.AssignedToUserId ?? 0);

        var adjuntosDto = t.Adjuntos.Select(a =>
        {
            cedInfo.TryGetValue(a.CreatedByUserId, out var u);
            return new TicketAdjuntoDto(a.Id, a.Tipo, a.FileName, a.ContentType, a.SizeBytes,
                a.Url, a.Titulo, a.CreatedByUserId, a.CreatedAt, u.Nombre);
        }).ToList();

        return new TicketDetailDto(
            t.Id, t.Codigo, t.Titulo, t.Tipo, t.Estado, t.Descripcion, t.PaisId,
            t.CreatedByUserId, t.AssignedToUserId, t.CreatedAt, t.FechaPrimeraApertura, t.FechaSolucion,
            notasDto, t.Imagenes,
            creador.Nombre,  creador.Rol,
            asignado.Nombre, asignado.Rol,
            paisNombre,
            creador.Email,
            asignado.Email,
            soyCreador,
            t.SolucionDescripcion,
            t.FechaCierreSolicitante,
            t.NotificadoCorreo,
            t.FechaNotificacionCorreo,
            t.CorreoNotificadoA,
            adjuntosDto,
            t.Notificados);
    }

    // ───────────────────────────── IMÁGENES ─────────────────────────────
    public async Task<IReadOnlyList<TicketImagenMetaDto>> GetImagenesMetaAsync(long ticketId, CancellationToken ct)
    {
        return await _ctx.TicketImagenes.AsNoTracking()
            .Where(i => i.TicketId == ticketId && i.Ticket!.DeletedAt == null)
            .OrderBy(i => i.CreatedAt)
            .Select(i => new TicketImagenMetaDto(i.Id, i.FileName, i.ContentType, i.SizeBytes, i.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<TicketImagenDto?> GetImagenAsync(long ticketId, long imagenId, CancellationToken ct)
    {
        return await _ctx.TicketImagenes.AsNoTracking()
            .Where(i => i.Id == imagenId && i.TicketId == ticketId && i.Ticket!.DeletedAt == null)
            .Select(i => new TicketImagenDto(i.Id, i.ImagenBase64, i.ContentType, i.FileName))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> AddImagenesAsync(long ticketId, AddTicketImagenesRequest req, CancellationToken ct)
    {
        if (req.Imagenes is null || req.Imagenes.Count == 0) return 0;

        var exists = await _ctx.Tickets.AsNoTracking()
            .AnyAsync(x => x.Id == ticketId && x.DeletedAt == null, ct);
        if (!exists) return 0;

        var now = DateTime.UtcNow;
        var added = 0;
        foreach (var img in req.Imagenes)
        {
            if (string.IsNullOrWhiteSpace(img.Base64)) continue;
            _ctx.TicketImagenes.Add(new TicketImagen
            {
                TicketId     = ticketId,
                ImagenBase64 = img.Base64,
                FileName     = img.FileName,
                ContentType  = img.ContentType,
                SizeBytes    = img.SizeBytes,
                CreatedAt    = now
            });
            added++;
        }

        if (added > 0) await _ctx.SaveChangesAsync(ct);
        return added;
    }

    // ───────────────────────────── NOTAS ─────────────────────────────
    public async Task<TicketNotaDto?> AddNotaAsync(long ticketId, CreateTicketNotaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nota))
            throw new InvalidOperationException("La nota no puede estar vacía.");

        // Cross-company: el ticket se ubica por id. Tomamos su empresa para resolver el rol del autor.
        var companyId = await _ctx.Tickets.AsNoTracking()
            .Where(x => x.Id == ticketId && x.DeletedAt == null)
            .Select(x => (int?)x.CompanyId)
            .FirstOrDefaultAsync(ct);
        if (companyId is null) return null;

        var nota = new TicketNota
        {
            TicketId  = ticketId,
            UserId    = _currentUser.UserId,
            Nota      = req.Nota.Trim(),
            EsInterna = req.EsInterna,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.TicketNotas.Add(nota);
        await _ctx.SaveChangesAsync(ct);

        var (nombre, rol, email) = await ResolveCurrentUserNombreRolAsync(companyId.Value, ct);
        return new TicketNotaDto(nota.Id, nota.UserId, nota.Nota, nota.EstadoResultante, nota.EsInterna, nota.CreatedAt,
            nombre, rol, email, EsMio: true);
    }

    // ───────────────────────────── ADJUNTOS (documentos + links) ─────────────────────────────
    public async Task<IReadOnlyList<TicketAdjuntoDto>> GetAdjuntosAsync(long ticketId, CancellationToken ct)
    {
        var ticketInfo = await _ctx.Tickets.AsNoTracking()
            .Where(x => x.Id == ticketId && x.DeletedAt == null)
            .Select(x => (int?)x.CompanyId)
            .FirstOrDefaultAsync(ct);
        if (ticketInfo is null) return Array.Empty<TicketAdjuntoDto>();

        var rows = await _ctx.TicketAdjuntos.AsNoTracking()
            .Where(a => a.TicketId == ticketId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, a.Tipo, a.FileName, a.ContentType, a.SizeBytes, a.Url, a.Titulo, a.CreatedByUserId, a.CreatedAt })
            .ToListAsync(ct);

        var info = await BuildNotaUserInfoAsync(
            rows.Select(r => r.CreatedByUserId).Where(x => x != 0).Distinct().ToList(), ticketInfo.Value, ct);

        return rows.Select(a =>
        {
            info.TryGetValue(a.CreatedByUserId, out var u);
            return new TicketAdjuntoDto(a.Id, a.Tipo, a.FileName, a.ContentType, a.SizeBytes,
                a.Url, a.Titulo, a.CreatedByUserId, a.CreatedAt, u.Nombre);
        }).ToList();
    }

    public async Task<TicketAdjuntoDto?> AddDocumentoAsync(long ticketId, AddTicketDocumentoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Base64))
            throw new InvalidOperationException("El archivo está vacío.");

        var exists = await _ctx.Tickets.AsNoTracking()
            .AnyAsync(x => x.Id == ticketId && x.DeletedAt == null, ct);
        if (!exists) return null;

        var entity = new TicketAdjunto
        {
            TicketId        = ticketId,
            Tipo            = TicketAdjuntoTipos.Archivo,
            ContenidoBase64 = req.Base64,
            FileName        = req.FileName,
            ContentType     = req.ContentType,
            SizeBytes       = req.SizeBytes,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt       = DateTime.UtcNow
        };
        _ctx.TicketAdjuntos.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return new TicketAdjuntoDto(entity.Id, entity.Tipo, entity.FileName, entity.ContentType,
            entity.SizeBytes, null, null, entity.CreatedByUserId, entity.CreatedAt);
    }

    public async Task<TicketAdjuntoDto?> AddLinkAsync(long ticketId, AddTicketLinkRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Url))
            throw new InvalidOperationException("La URL es requerida.");
        var url = req.Url.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La URL debe comenzar con http:// o https://");

        var exists = await _ctx.Tickets.AsNoTracking()
            .AnyAsync(x => x.Id == ticketId && x.DeletedAt == null, ct);
        if (!exists) return null;

        var entity = new TicketAdjunto
        {
            TicketId        = ticketId,
            Tipo            = TicketAdjuntoTipos.Link,
            Url             = url,
            Titulo          = string.IsNullOrWhiteSpace(req.Titulo) ? url : req.Titulo.Trim(),
            CreatedByUserId = _currentUser.UserId,
            CreatedAt       = DateTime.UtcNow
        };
        _ctx.TicketAdjuntos.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return new TicketAdjuntoDto(entity.Id, entity.Tipo, null, null, null,
            entity.Url, entity.Titulo, entity.CreatedByUserId, entity.CreatedAt);
    }

    public async Task<TicketDocumentoDto?> GetDocumentoAsync(long ticketId, long adjuntoId, CancellationToken ct)
    {
        return await _ctx.TicketAdjuntos.AsNoTracking()
            .Where(a => a.Id == adjuntoId && a.TicketId == ticketId
                        && a.Tipo == TicketAdjuntoTipos.Archivo
                        && a.Ticket!.DeletedAt == null)
            .Select(a => new TicketDocumentoDto(a.Id, a.ContenidoBase64!, a.ContentType, a.FileName))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> DeleteAdjuntoAsync(long ticketId, long adjuntoId, CancellationToken ct)
    {
        var adj = await _ctx.TicketAdjuntos
            .FirstOrDefaultAsync(a => a.Id == adjuntoId && a.TicketId == ticketId, ct);
        if (adj is null) return false;
        _ctx.TicketAdjuntos.Remove(adj);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    // ───────────────────────────── GESTIÓN (resolutor) ─────────────────────────────

    /// <summary>True si el usuario actual es el creador/solicitante del ticket.</summary>
    private bool EsCreador(Ticket t) =>
        (t.CreatedByUserId != 0 && t.CreatedByUserId == _currentUser.UserId)
        || (_currentUser.UserGuid.HasValue && t.CreatedByUserGuid == _currentUser.UserGuid.Value);

    public async Task<TicketDetailDto?> TomarAsync(long id, CancellationToken ct)
    {
        var ticket = await _ctx.Tickets
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (ticket is null) return null;

        // El solicitante NO gestiona su propio ticket — ni siquiera el admin: la gestión la hace
        // el equipo que atiende. El admin sigue gestionando tickets ajenos (ahí EsCreador es falso).
        if (EsCreador(ticket))
            throw new InvalidOperationException("Sos el solicitante de este ticket; lo toma y gestiona el equipo que atiende.");

        var now = DateTime.UtcNow;
        var cambio = false;

        if (ticket.Estado == TicketEstados.Abierto)
        {
            ticket.Estado = TicketEstados.EnAnalisis;
            ticket.FechaPrimeraApertura ??= now;
            cambio = true;
        }
        if (ticket.AssignedToUserId is null)
        {
            ticket.AssignedToUserId = _currentUser.UserId;
            cambio = true;
        }

        if (cambio)
        {
            ticket.UpdatedByUserId = _currentUser.UserId;
            ticket.UpdatedAt = now;
            _ctx.TicketNotas.Add(new TicketNota
            {
                TicketId         = ticket.Id,
                UserId           = _currentUser.UserId,
                Nota             = "Ticket tomado por el equipo de soporte.",
                EstadoResultante = ticket.Estado,
                CreatedAt        = now
            });
            await _ctx.SaveChangesAsync(ct);
        }

        return await GetByIdInternalAsync(id, ct);
    }

    public async Task<TicketDetailDto?> CambiarEstadoAsync(long id, CambiarEstadoTicketRequest req, CancellationToken ct)
    {
        if (!TicketEstados.EsValido(req.Estado))
            throw new InvalidOperationException("Estado inválido.");
        var nuevo = req.Estado.ToUpperInvariant();

        var ticket = await _ctx.Tickets
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (ticket is null) return null;

        // El solicitante NO gestiona su propio ticket, salvo REABRIR (SOLUCIONADO → EN_ANALISIS).
        // Aplica también al admin cuando es el creador: la gestión la hace el equipo que atiende
        // (sobre tickets ajenos, donde EsCreador es falso, el admin gestiona normalmente).
        if (EsCreador(ticket))
        {
            var esReapertura = string.Equals(ticket.Estado, TicketEstados.Solucionado, StringComparison.OrdinalIgnoreCase)
                               && nuevo == TicketEstados.EnAnalisis;
            if (!esReapertura)
                throw new InvalidOperationException("El solicitante no puede cambiar el estado de su propio ticket. Cuando esté SOLUCIONADO, podés 'Confirmar cierre' o 'Reabrir'.");
        }

        // El cierre definitivo lo confirma el solicitante (ConfirmarCierre), no la gestión.
        if (nuevo == TicketEstados.Cerrado)
            throw new InvalidOperationException("El cierre lo confirma el solicitante. Marcá SOLUCIONADO y el solicitante lo cerrará.");

        if (!string.Equals(ticket.Estado, nuevo, StringComparison.OrdinalIgnoreCase) &&
            !TicketEstados.PuedeTransicionar(ticket.Estado, nuevo))
            throw new InvalidOperationException($"Transición inválida: {ticket.Estado} → {nuevo}.");

        var now = DateTime.UtcNow;
        ticket.Estado = nuevo;

        if (nuevo == TicketEstados.Solucionado)
        {
            if (string.IsNullOrWhiteSpace(req.SolucionDescripcion))
                throw new InvalidOperationException("Indicá la descripción de la solución para marcar el ticket como SOLUCIONADO.");
            ticket.SolucionDescripcion = req.SolucionDescripcion.Trim();
            ticket.FechaSolucion ??= now;

            // Notificar la solución al solicitante por correo (cola asíncrona).
            var (email, nombreSol) = await ResolveSolicitanteEmailAsync(ticket.CreatedByUserGuid, ticket.CreatedByUserId, ct);
            if (!string.IsNullOrWhiteSpace(email))
            {
                try
                {
                    await _emailQueue.EnqueueEmailAsync(
                        email!,
                        $"[{ticket.Codigo}] Tu ticket fue solucionado",
                        BuildSolucionEmailBody(ticket, nombreSol),
                        "ticket_solucionado",
                        $"{{\"ticketId\":{ticket.Id},\"codigo\":\"{ticket.Codigo}\"}}");
                    ticket.NotificadoCorreo = true;
                    ticket.FechaNotificacionCorreo = now;
                    ticket.CorreoNotificadoA = email;
                }
                catch { /* si la cola falla, no bloquea el cambio de estado */ }
            }
        }

        ticket.UpdatedByUserId = _currentUser.UserId;
        ticket.UpdatedAt = now;

        var notaTexto = !string.IsNullOrWhiteSpace(req.Nota) ? req.Nota.Trim()
            : nuevo == TicketEstados.Solucionado && ticket.SolucionDescripcion is not null
                ? $"Solucionado: {ticket.SolucionDescripcion}"
                : $"Estado cambiado a {nuevo}.";

        _ctx.TicketNotas.Add(new TicketNota
        {
            TicketId         = ticket.Id,
            UserId           = _currentUser.UserId,
            Nota             = notaTexto,
            EstadoResultante = nuevo,
            CreatedAt        = now
        });
        await _ctx.SaveChangesAsync(ct);

        return await GetByIdInternalAsync(id, ct);
    }

    // ───────────────────────── CIERRE POR EL SOLICITANTE ─────────────────────────
    public async Task<TicketDetailDto?> ConfirmarCierreAsync(long id, ConfirmarCierreRequest req, CancellationToken ct)
    {
        var ticket = await _ctx.Tickets
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (ticket is null) return null;

        if (!EsCreador(ticket))
            throw new InvalidOperationException("Solo el solicitante puede confirmar el cierre del ticket.");
        if (!string.Equals(ticket.Estado, TicketEstados.Solucionado, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Solo se puede cerrar un ticket que está SOLUCIONADO.");

        var now = DateTime.UtcNow;
        ticket.Estado = TicketEstados.Cerrado;
        ticket.FechaCierreSolicitante = now;
        ticket.CerradoPorUserId = _currentUser.UserId;
        ticket.UpdatedByUserId = _currentUser.UserId;
        ticket.UpdatedAt = now;

        _ctx.TicketNotas.Add(new TicketNota
        {
            TicketId         = ticket.Id,
            UserId           = _currentUser.UserId,
            Nota             = string.IsNullOrWhiteSpace(req.Nota)
                ? "Cierre confirmado por el solicitante. Caso cerrado por ambas partes."
                : req.Nota.Trim(),
            EstadoResultante = TicketEstados.Cerrado,
            CreatedAt        = now
        });
        await _ctx.SaveChangesAsync(ct);

        // Notificar el cierre al solicitante + notificados (copiados): resumen de solución +
        // histórico de la bitácora pública (EsInterna == false). No bloquea el cierre.
        try
        {
            var notasPublicas = await _ctx.TicketNotas.AsNoTracking()
                .Where(n => n.TicketId == ticket.Id && !n.EsInterna)
                .OrderBy(n => n.CreatedAt)
                .Select(n => new { n.UserId, n.Nota, n.CreatedAt })
                .ToListAsync(ct);

            var autorInfo = await BuildNotaUserInfoAsync(
                notasPublicas.Select(n => n.UserId).Where(x => x != 0).Distinct().ToList(),
                ticket.CompanyId, ct);

            var notasResumen = notasPublicas.Select(n =>
            {
                autorInfo.TryGetValue(n.UserId, out var info);
                return new TicketEmailTemplates.NotaResumen(info.Nombre, n.CreatedAt, n.Nota);
            }).ToList();

            var destinatarios = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            var (solicitanteEmail, solicitanteNombre) = await ResolveSolicitanteEmailAsync(
                ticket.CreatedByUserGuid, ticket.CreatedByUserId, ct);
            if (!string.IsNullOrWhiteSpace(solicitanteEmail))
                destinatarios[solicitanteEmail!] = solicitanteNombre;

            var notificados = await _ctx.TicketNotificados.AsNoTracking()
                .Where(n => n.TicketId == ticket.Id)
                .Select(n => new { n.Email, n.Nombre })
                .ToListAsync(ct);
            foreach (var n in notificados)
                if (!string.IsNullOrWhiteSpace(n.Email))
                    destinatarios.TryAdd(n.Email, n.Nombre);

            foreach (var (email, nombre) in destinatarios)
            {
                try
                {
                    var body = TicketEmailTemplates.Cerrado(ticket, nombre, notasResumen,
                        _logoUrl, _brandName, BrandLine, _applicationUrl);
                    await _emailQueue.EnqueueEmailAsync(
                        email,
                        $"[{ticket.Codigo}] Ticket cerrado",
                        body,
                        "ticket_cerrado",
                        $"{{\"ticketId\":{ticket.Id},\"codigo\":\"{ticket.Codigo}\"}}");
                }
                catch { /* si la cola falla para un destinatario, se sigue con los demás */ }
            }
        }
        catch { /* si falla la resolución de destinatarios, no bloquea el cierre */ }

        return await GetByIdInternalAsync(id, ct);
    }

    /// <summary>Resuelve email + nombre del solicitante (Guid primero, cédula como fallback).</summary>
    private async Task<(string? Email, string? Nombre)> ResolveSolicitanteEmailAsync(Guid? guid, int cedula, CancellationToken ct)
    {
        if (guid.HasValue)
        {
            var u = await _ctx.Set<User>().AsNoTracking()
                .Where(x => x.Id == guid.Value)
                .Select(x => new { Email = x.UserLogins.Select(ul => ul.Login.email).FirstOrDefault(), x.firstName, x.surName })
                .FirstOrDefaultAsync(ct);
            if (u is not null && !string.IsNullOrWhiteSpace(u.Email))
                return (u.Email, $"{u.firstName} {u.surName}".Trim());
        }
        if (cedula != 0)
        {
            var ced = cedula.ToString();
            var u = await _ctx.Set<User>().AsNoTracking()
                .Where(x => x.cedula == ced)
                .Select(x => new { Email = x.UserLogins.Select(ul => ul.Login.email).FirstOrDefault(), x.firstName, x.surName })
                .FirstOrDefaultAsync(ct);
            if (u is not null)
                return (u.Email, $"{u.firstName} {u.surName}".Trim());
        }
        return (null, null);
    }

    /// <summary>Nombre completo de un usuario por Guid (helper liviano para los correos de tickets).</summary>
    private async Task<string?> ResolveNombrePorGuidAsync(Guid? guid, CancellationToken ct)
    {
        if (!guid.HasValue) return null;
        var u = await _ctx.Set<User>().AsNoTracking()
            .Where(x => x.Id == guid.Value)
            .Select(x => new { x.firstName, x.surName })
            .FirstOrDefaultAsync(ct);
        return u is null ? null : $"{u.firstName} {u.surName}".Trim();
    }

    // ───────────────────────────── NOTIFICADOS (copiados) ─────────────────────────────

    /// <summary>
    /// Usuarios de la empresa efectiva con email registrado, candidatos a ser notificados
    /// (copiados) al crear un ticket. Excluye al usuario actual.
    /// </summary>
    public async Task<IReadOnlyList<UsuarioNotificableDto>> GetNotificablesAsync(CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var currentGuid = _currentUser.UserGuid;

        var rows = await _ctx.Set<User>().AsNoTracking()
            .Where(u => u.UserRoles.Any(ur => ur.CompanyId == companyId))
            .Select(u => new
            {
                u.Id, u.firstName, u.surName,
                Email = u.UserLogins.Select(ul => ul.Login.email).FirstOrDefault(),
                Rol = u.UserRoles.Where(ur => ur.CompanyId == companyId)
                                 .Select(ur => ur.Role.Name).FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .Where(u => !currentGuid.HasValue || u.Id != currentGuid.Value)
            .Select(u => new UsuarioNotificableDto(u.Id, $"{u.firstName} {u.surName}".Trim(), u.Email!, u.Rol))
            .OrderBy(u => u.Nombre)
            .ToList();
    }

    private static string BuildSolucionEmailBody(Ticket t, string? nombreSolicitante)
    {
        var saludo = string.IsNullOrWhiteSpace(nombreSolicitante) ? "Hola" : $"Hola {nombreSolicitante}";
        var solucion = System.Net.WebUtility.HtmlEncode(t.SolucionDescripcion ?? "");
        var titulo = System.Net.WebUtility.HtmlEncode(t.Titulo);
        return $@"
<div style=""font-family:Arial,sans-serif;color:#1f2937;max-width:560px;margin:auto"">
  <div style=""background:#2d7a3e;color:#fff;padding:16px 20px;border-radius:10px 10px 0 0"">
    <h2 style=""margin:0;font-size:18px"">Tu ticket fue solucionado</h2>
  </div>
  <div style=""border:1px solid #e5e7eb;border-top:0;padding:20px;border-radius:0 0 10px 10px"">
    <p>{saludo},</p>
    <p>El ticket <strong>{t.Codigo}</strong> — “{titulo}” fue marcado como <strong>SOLUCIONADO</strong>.</p>
    <p style=""background:#f0fdf4;border-left:4px solid #2d7a3e;padding:10px 14px;border-radius:4px"">
      <strong>Solución:</strong><br/>{solucion}
    </p>
    <p>Ingresá a la plataforma para revisar la solución y, si estás conforme, <strong>confirmar el cierre</strong> del caso.</p>
    <p style=""color:#6b7280;font-size:13px"">Italcol · Gestión de tickets</p>
  </div>
</div>";
    }

    // ───────────────────────────── DELETE (lógico) ─────────────────────────────
    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        var ticket = await _ctx.Tickets
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (ticket is null) return false;

        var now = DateTime.UtcNow;
        ticket.DeletedAt = now;
        ticket.Status = "I";
        ticket.UpdatedByUserId = _currentUser.UserId;
        ticket.UpdatedAt = now;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
