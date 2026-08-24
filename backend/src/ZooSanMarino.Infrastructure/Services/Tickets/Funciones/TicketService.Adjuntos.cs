// src/ZooSanMarino.Infrastructure/Services/Tickets/Funciones/TicketService.Adjuntos.cs
// Imagenes, notas y adjuntos (documentos + links) de un ticket.
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
}
