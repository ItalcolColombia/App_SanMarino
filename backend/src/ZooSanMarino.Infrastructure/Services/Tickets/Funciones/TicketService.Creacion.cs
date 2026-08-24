// src/ZooSanMarino.Infrastructure/Services/Tickets/Funciones/TicketService.Creacion.cs
// Creacion de tickets y su transferencia (requerimiento -> desarrollo).
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

        // Solicitante delegado ("a nombre de"): privilegio exclusivo de tickets.admin. Sin este
        // campo el caso queda idéntico a como se creaba antes (solicitante = creador).
        Guid? solicitanteGuid = null;
        int?  solicitanteCedula = null;
        if (req.SolicitanteUserGuid is { } delegado && delegado != Guid.Empty)
        {
            if (!EsSuperAdmin())
                throw new InvalidOperationException("Solo el administrador de tickets puede registrar un caso a nombre de otro usuario.");

            var destino = await _ctx.Set<User>().AsNoTracking()
                .Where(u => u.Id == delegado)
                .Select(u => new
                {
                    u.Id, u.cedula,
                    Empresas = u.UserCompanies.Select(uc => uc.CompanyId).ToList()
                })
                .FirstOrDefaultAsync(ct);
            if (destino is null)
                throw new InvalidOperationException("El usuario indicado como solicitante no existe.");

            // Delegar en uno mismo es redundante: se ignora y el caso queda como propio.
            if (!_currentUser.UserGuid.HasValue || destino.Id != _currentUser.UserGuid.Value)
            {
                solicitanteGuid = destino.Id;
                solicitanteCedula = int.TryParse(destino.cedula, out var ced) ? ced : null;

                // El caso pertenece a la empresa del SOLICITANTE: si quedara en la empresa activa
                // del admin, "Mis solicitudes" del solicitante (que filtra por su empresa efectiva)
                // no lo mostraría nunca. Se respeta la empresa activa si el solicitante también
                // pertenece a ella; si no, se cae a la suya.
                if (destino.Empresas.Count > 0 && !destino.Empresas.Contains(companyId))
                    companyId = destino.Empresas[0];
            }
        }

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
            SolicitanteUserGuid  = solicitanteGuid,
            SolicitanteUserId    = solicitanteCedula,
            Prioridad            = TicketTareaCalculos.NormalizarPrioridad(req.Prioridad),
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

        // Deja rastro en la bitácora de que el caso lo registró un tercero: sin esta nota, el
        // solicitante vería un caso suyo que nunca creó.
        if (solicitanteGuid.HasValue)
        {
            var nombreSolicitante = await ResolveNombrePorGuidAsync(solicitanteGuid, ct);
            var nombreRegistrador = await ResolveNombrePorGuidAsync(_currentUser.UserGuid, ct);
            _ctx.TicketNotas.Add(new TicketNota
            {
                TicketId   = entity.Id,
                UserId     = _currentUser.UserId,
                Nota       = $"Caso registrado por {nombreRegistrador ?? "el administrador"} a nombre de {nombreSolicitante ?? "otro usuario"}.",
                TipoEvento = TicketNotaEventos.Solicitante,
                CreatedAt  = now
            });
        }

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
                _logoUrl, _brandName, BrandLine, _applicationUrl, _logoSecundarioUrl);

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
                    _logoUrl, _brandName, BrandLine, _applicationUrl, _logoSecundarioUrl);
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
}
