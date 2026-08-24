// src/ZooSanMarino.Infrastructure/Services/Tickets/Funciones/TicketService.Detalle.cs
// Detalle de un ticket, con la validacion de quien puede verlo.
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
    public async Task<TicketDetailDto?> GetByIdAsync(long id, CancellationToken ct)
    {
        // Cross-company: el ticket se busca por id (los resolutores son globales).
        // La autorización se valida por visibilidad, no por empresa activa.
        var meta = await _ctx.Tickets.AsNoTracking()
            .Where(x => x.Id == id && x.DeletedAt == null)
            .Select(x => new { x.PaisId, x.Tipo, x.CreatedByUserId, x.CreatedByUserGuid, x.AssignedToUserGuid,
                               x.SolicitanteUserGuid, x.SolicitanteUserId })
            .FirstOrDefaultAsync(ct);
        if (meta is null) return null;

        if (!await PuedeVerTicketAsync(meta.PaisId, meta.Tipo, meta.CreatedByUserId,
                                       meta.CreatedByUserGuid, meta.AssignedToUserGuid,
                                       meta.SolicitanteUserGuid, meta.SolicitanteUserId, ct))
            return null;   // 404: no revela existencia de tickets ajenos

        return await GetByIdInternalAsync(id, ct);
    }

    /// <summary>
    /// Reglas de visibilidad de un ticket: lo ve su creador, el solicitante a cuyo nombre se
    /// registró, su asignado, un resolutor cuyo perfil matchea (tipo, país), o cualquiera con
    /// <c>tickets.admin</c>.
    /// </summary>
    private async Task<bool> PuedeVerTicketAsync(int paisId, string tipo, int createdByUserId,
        Guid? createdByGuid, Guid? assignedGuid, Guid? solicitanteGuid, int? solicitanteUserId,
        CancellationToken ct)
    {
        if (createdByUserId != 0 && createdByUserId == _currentUser.UserId) return true;
        if (solicitanteUserId is { } sol && sol != 0 && sol == _currentUser.UserId) return true;

        var userGuid = _currentUser.UserGuid;
        if (userGuid.HasValue)
        {
            if (createdByGuid == userGuid.Value) return true;   // creador (guid)
            if (assignedGuid == userGuid.Value) return true;    // asignado
            if (solicitanteGuid == userGuid.Value) return true; // solicitante delegado
        }

        if (EsSuperAdmin())
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
                // Solicitante delegado + gestión tipo tablero
                x.SolicitanteUserGuid, x.SolicitanteUserId,
                x.Prioridad, x.OrdenTablero, x.FechaLimite, x.FechaInicioPlan, x.FechaFinPlan,
                x.HorasEstimadas,
                HorasRegistradas = x.Tiempos.Where(t => t.DeletedAt == null).Sum(t => (decimal?)t.Horas) ?? 0m,
                Tareas = x.Tareas.Where(t => t.DeletedAt == null)
                    .OrderBy(t => t.Estado).ThenBy(t => t.Orden)
                    .Select(t => new
                    {
                        t.Id, t.Codigo, t.Tipo, t.Estado, t.Prioridad, t.Titulo, t.Descripcion,
                        t.AsignadoUserGuid, t.ParentTareaId, t.Orden, t.HorasEstimadas,
                        t.FechaInicioPlan, t.FechaFinPlan, t.FechaInicioReal, t.FechaFinReal,
                        t.Etiquetas, t.CreatedAt, t.CreatedByUserId,
                        HorasRegistradas = t.Tiempos.Where(w => w.DeletedAt == null).Sum(w => (decimal?)w.Horas) ?? 0m
                    })
                    .ToList(),
                Notas = x.Notas.OrderBy(n => n.CreatedAt)
                    .Select(n => new { n.Id, n.UserId, n.Nota, n.EstadoResultante, n.EsInterna, n.TipoEvento, n.CreatedAt })
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

        // Identidad por Guid (creador/asignado/solicitante/responsables de tareas) — rol en la empresa del ticket.
        var refs = new List<(Guid Guid, int CompanyId)>();
        if (t.CreatedByUserGuid.HasValue) refs.Add((t.CreatedByUserGuid.Value, t.CompanyId));
        if (t.AssignedToUserGuid.HasValue) refs.Add((t.AssignedToUserGuid.Value, t.CompanyId));
        if (t.SolicitanteUserGuid.HasValue) refs.Add((t.SolicitanteUserGuid.Value, t.CompanyId));
        foreach (var tarea in t.Tareas)
            if (tarea.AsignadoUserGuid.HasValue) refs.Add((tarea.AsignadoUserGuid.Value, t.CompanyId));
        var (users, roles) = await BuildUserInfoAsync(refs, ct);

        // Identidad por cédula (autores de notas + adjuntos + fallback de creador/asignado sin Guid,
        // para tickets antiguos creados antes de poblar created_by_user_guid).
        var cedulaIds = t.Notas.Select(n => n.UserId)
            .Concat(t.Adjuntos.Select(a => a.CreatedByUserId))
            .Concat(t.Tareas.Select(x => x.CreatedByUserId))
            .Append(t.CreatedByUserId)
            .Append(t.AssignedToUserId ?? 0)
            .Append(t.SolicitanteUserId ?? 0)
            .Where(uid => uid != 0).Distinct().ToList();
        var cedInfo = await BuildNotaUserInfoAsync(cedulaIds, t.CompanyId, ct);

        var paisNombre = (await BuildPaisMapAsync(new[] { t.PaisId }, ct)).GetValueOrDefault(t.PaisId);

        var miUserId = _currentUser.UserId;
        var notasDto = t.Notas.Select(n =>
        {
            cedInfo.TryGetValue(n.UserId, out var info);
            return new TicketNotaDto(n.Id, n.UserId, n.Nota, n.EstadoResultante, n.EsInterna, n.CreatedAt,
                info.Nombre, info.Rol, info.Email, EsMio: n.UserId != 0 && n.UserId == miUserId,
                TipoEvento: n.TipoEvento);
        }).ToList();

        var soyCreador = (t.CreatedByUserId != 0 && t.CreatedByUserId == miUserId)
                         || (_currentUser.UserGuid.HasValue && t.CreatedByUserGuid == _currentUser.UserGuid.Value);

        // Solicitante efectivo: el delegado si existe, el creador si no. Es quien puede cerrar/reabrir.
        var hayDelegado = t.SolicitanteUserGuid.HasValue || t.SolicitanteUserId.HasValue;
        var soySolicitante = hayDelegado
            ? (t.SolicitanteUserId is { } solCed && solCed != 0 && solCed == miUserId)
              || (_currentUser.UserGuid.HasValue && t.SolicitanteUserGuid == _currentUser.UserGuid.Value)
            : soyCreador;

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
        var solicitante = hayDelegado
            ? Resolver(t.SolicitanteUserGuid, t.SolicitanteUserId ?? 0)
            : creador;

        var adjuntosDto = t.Adjuntos.Select(a =>
        {
            cedInfo.TryGetValue(a.CreatedByUserId, out var u);
            return new TicketAdjuntoDto(a.Id, a.Tipo, a.FileName, a.ContentType, a.SizeBytes,
                a.Url, a.Titulo, a.CreatedByUserId, a.CreatedAt, u.Nombre);
        }).ToList();

        var subtareasPorPadre = t.Tareas
            .Where(x => x.ParentTareaId.HasValue)
            .GroupBy(x => x.ParentTareaId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var tareasDto = t.Tareas.Select(x =>
        {
            cedInfo.TryGetValue(x.CreatedByUserId, out var autor);
            return new TicketTareaDto(
                x.Id, t.Id, x.Codigo, x.Tipo, x.Estado, x.Prioridad, x.Titulo, x.Descripcion,
                x.AsignadoUserGuid, NombreDe(users, x.AsignadoUserGuid), x.ParentTareaId, x.Orden,
                x.HorasEstimadas, x.HorasRegistradas,
                x.FechaInicioPlan, x.FechaFinPlan, x.FechaInicioReal, x.FechaFinReal,
                x.Etiquetas, x.CreatedAt, autor.Nombre,
                subtareasPorPadre.GetValueOrDefault(x.Id));
        }).ToList();

        var metricas = ConstruirMetricas(
            t.CreatedAt, t.FechaPrimeraApertura, t.FechaSolucion, t.FechaCierreSolicitante,
            t.FechaLimite, t.Estado, t.HorasEstimadas, t.HorasRegistradas,
            t.Tareas.Count, t.Tareas.Count(x => TicketTareaEstados.EsTerminal(x.Estado)),
            t.Notas.Where(n => !string.IsNullOrWhiteSpace(n.EstadoResultante))
                   .Select(n => new TicketMetricasCalculos.CambioEstado(n.EstadoResultante!, n.CreatedAt)));

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
            t.Notificados,
            t.SolicitanteUserGuid,
            solicitante.Nombre,
            solicitante.Rol,
            solicitante.Email,
            RegistradoPorTercero: hayDelegado,
            SoySolicitante: soySolicitante,
            t.Prioridad,
            t.OrdenTablero,
            t.FechaLimite,
            t.FechaInicioPlan,
            t.FechaFinPlan,
            t.HorasEstimadas,
            t.HorasRegistradas,
            tareasDto,
            metricas,
            t.AssignedToUserGuid);
    }
}
