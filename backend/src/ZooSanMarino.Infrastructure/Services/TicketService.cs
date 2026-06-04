using Microsoft.EntityFrameworkCore;
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

    public TicketService(ZooSanMarinoContext ctx, ICurrentUser currentUser, ICompanyResolver companyResolver)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

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

        var entity = new Ticket
        {
            CompanyId       = companyId,
            PaisId          = _currentUser.PaisId ?? 0,
            Tipo            = req.Tipo.ToUpperInvariant(),
            Estado          = TicketEstados.Abierto,
            Titulo          = req.Titulo.Trim(),
            Descripcion     = req.Descripcion.Trim(),
            CreatedByUserId = _currentUser.UserId,
            CreatedAt       = now,
            Status          = "A"
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

        return (await GetByIdInternalAsync(entity.Id, companyId, ct))!;
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
        var companyId = await GetEffectiveCompanyIdAsync();
        var query = BaseQuery(companyId);
        // El país se inyecta del contexto del resolutor, no del query.
        if (_currentUser.PaisId.HasValue)
            query = query.Where(x => x.PaisId == _currentUser.PaisId.Value);
        return await PageAsync(ApplyFilters(query, req), req, ct);
    }

    public async Task<PagedResult<TicketListItemDto>> SearchAdminAsync(TicketSearchRequest req, CancellationToken ct)
    {
        // Super admin: global dentro de la empresa (todos los países), con filtros opcionales.
        var companyId = req.CompanyId ?? await GetEffectiveCompanyIdAsync();
        var query = BaseQuery(companyId);
        if (req.PaisId.HasValue)
            query = query.Where(x => x.PaisId == req.PaisId.Value);
        return await PageAsync(ApplyFilters(query, req), req, ct);
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

        return query;
    }

    private static async Task<PagedResult<TicketListItemDto>> PageAsync(
        IQueryable<Ticket> query, TicketSearchRequest req, CancellationToken ct)
    {
        var page = req.Page < 1 ? 1 : req.Page;
        var size = req.PageSize is < 1 or > 100 ? 20 : req.PageSize;

        var total = await query.LongCountAsync(ct);

        // Proyección: Imagenes.Count / Notas.Count se traducen a subconsultas COUNT,
        // por lo que NO se materializa imagen_base64 en los listados.
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new TicketListItemDto(
                x.Id, x.Codigo, x.Titulo, x.Tipo, x.Estado, x.PaisId,
                x.CreatedByUserId, x.AssignedToUserId, x.CreatedAt,
                x.Imagenes.Count, x.Notas.Count))
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>
        {
            Page = page, PageSize = size, Total = total, Items = items
        };
    }

    // ───────────────────────────── DETALLE ─────────────────────────────
    public async Task<TicketDetailDto?> GetByIdAsync(long id, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        return await GetByIdInternalAsync(id, companyId, ct);
    }

    private async Task<TicketDetailDto?> GetByIdInternalAsync(long id, int companyId, CancellationToken ct) =>
        await _ctx.Tickets.AsNoTracking()
            .Where(x => x.Id == id && x.CompanyId == companyId && x.DeletedAt == null)
            .Select(x => new TicketDetailDto(
                x.Id, x.Codigo, x.Titulo, x.Tipo, x.Estado, x.Descripcion, x.PaisId,
                x.CreatedByUserId, x.AssignedToUserId, x.CreatedAt, x.FechaPrimeraApertura, x.FechaSolucion,
                x.Notas.OrderBy(n => n.CreatedAt)
                    .Select(n => new TicketNotaDto(n.Id, n.UserId, n.Nota, n.EstadoResultante, n.EsInterna, n.CreatedAt))
                    .ToList(),
                // Solo metadata — NO imagen_base64.
                x.Imagenes.OrderBy(i => i.CreatedAt)
                    .Select(i => new TicketImagenMetaDto(i.Id, i.FileName, i.ContentType, i.SizeBytes, i.CreatedAt))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

    // ───────────────────────────── IMÁGENES ─────────────────────────────
    public async Task<IReadOnlyList<TicketImagenMetaDto>> GetImagenesMetaAsync(long ticketId, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        return await _ctx.TicketImagenes.AsNoTracking()
            .Where(i => i.TicketId == ticketId && i.Ticket!.CompanyId == companyId && i.Ticket.DeletedAt == null)
            .OrderBy(i => i.CreatedAt)
            .Select(i => new TicketImagenMetaDto(i.Id, i.FileName, i.ContentType, i.SizeBytes, i.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<TicketImagenDto?> GetImagenAsync(long ticketId, long imagenId, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        return await _ctx.TicketImagenes.AsNoTracking()
            .Where(i => i.Id == imagenId && i.TicketId == ticketId
                        && i.Ticket!.CompanyId == companyId && i.Ticket.DeletedAt == null)
            .Select(i => new TicketImagenDto(i.Id, i.ImagenBase64, i.ContentType, i.FileName))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> AddImagenesAsync(long ticketId, AddTicketImagenesRequest req, CancellationToken ct)
    {
        if (req.Imagenes is null || req.Imagenes.Count == 0) return 0;

        var companyId = await GetEffectiveCompanyIdAsync();
        var exists = await _ctx.Tickets.AsNoTracking()
            .AnyAsync(x => x.Id == ticketId && x.CompanyId == companyId && x.DeletedAt == null, ct);
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

        var companyId = await GetEffectiveCompanyIdAsync();
        var exists = await _ctx.Tickets.AsNoTracking()
            .AnyAsync(x => x.Id == ticketId && x.CompanyId == companyId && x.DeletedAt == null, ct);
        if (!exists) return null;

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

        return new TicketNotaDto(nota.Id, nota.UserId, nota.Nota, nota.EstadoResultante, nota.EsInterna, nota.CreatedAt);
    }

    // ───────────────────────────── GESTIÓN (resolutor) ─────────────────────────────
    public async Task<TicketDetailDto?> TomarAsync(long id, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var ticket = await _ctx.Tickets
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.DeletedAt == null, ct);
        if (ticket is null) return null;

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

        return await GetByIdInternalAsync(id, companyId, ct);
    }

    public async Task<TicketDetailDto?> CambiarEstadoAsync(long id, CambiarEstadoTicketRequest req, CancellationToken ct)
    {
        if (!TicketEstados.EsValido(req.Estado))
            throw new InvalidOperationException("Estado inválido.");
        var nuevo = req.Estado.ToUpperInvariant();

        var companyId = await GetEffectiveCompanyIdAsync();
        var ticket = await _ctx.Tickets
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.DeletedAt == null, ct);
        if (ticket is null) return null;

        if (!string.Equals(ticket.Estado, nuevo, StringComparison.OrdinalIgnoreCase) &&
            !TicketEstados.PuedeTransicionar(ticket.Estado, nuevo))
            throw new InvalidOperationException($"Transición inválida: {ticket.Estado} → {nuevo}.");

        var now = DateTime.UtcNow;
        ticket.Estado = nuevo;
        if (nuevo == TicketEstados.Solucionado) ticket.FechaSolucion ??= now;
        ticket.UpdatedByUserId = _currentUser.UserId;
        ticket.UpdatedAt = now;

        _ctx.TicketNotas.Add(new TicketNota
        {
            TicketId         = ticket.Id,
            UserId           = _currentUser.UserId,
            Nota             = string.IsNullOrWhiteSpace(req.Nota) ? $"Estado cambiado a {nuevo}." : req.Nota.Trim(),
            EstadoResultante = nuevo,
            CreatedAt        = now
        });
        await _ctx.SaveChangesAsync(ct);

        return await GetByIdInternalAsync(id, companyId, ct);
    }

    // ───────────────────────────── DELETE (lógico) ─────────────────────────────
    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var ticket = await _ctx.Tickets
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.DeletedAt == null, ct);
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
