// src/ZooSanMarino.Infrastructure/Services/Tickets/Funciones/TicketService.Estado.cs
// Ciclo de vida: tomar, cambiar estado, cierre por el solicitante, notificados y delete
// logico. El cuerpo del aviso de "solucionado" vive en TicketEmailTemplates.Solucionado
// junto al resto de las notificaciones de tickets: antes era el unico correo que se
// maquetaba a mano, sin logo ni pie, y quedaba fuera de cualquier cambio de diseno.
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
    /// <summary>True si el usuario actual tiene el permiso de administración global del módulo.</summary>
    private bool EsSuperAdmin() =>
        _currentUser.Permissions.Contains("tickets.admin", StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True si el usuario actual es EL SOLICITANTE del caso — el delegado cuando existe, el creador
    /// cuando no. Es la regla que decide quién NO puede gestionar el caso y quién sí puede cerrarlo.
    /// </summary>
    /// <remarks>
    /// Cuando el admin registra un caso a nombre de otro, el admin es el creador pero NO el
    /// solicitante: por eso sí puede gestionarlo, que es justamente para lo que delegó.
    /// </remarks>
    private bool EsSolicitante(Ticket t)
    {
        if (t.SolicitanteUserGuid.HasValue || t.SolicitanteUserId.HasValue)
            return (t.SolicitanteUserId is { } ced && ced != 0 && ced == _currentUser.UserId)
                || (_currentUser.UserGuid.HasValue && t.SolicitanteUserGuid == _currentUser.UserGuid.Value);

        return (t.CreatedByUserId != 0 && t.CreatedByUserId == _currentUser.UserId)
            || (_currentUser.UserGuid.HasValue && t.CreatedByUserGuid == _currentUser.UserGuid.Value);
    }

    /// <summary>Guid del solicitante efectivo (delegado si lo hay; si no, el creador).</summary>
    private static Guid? SolicitanteGuidDe(Ticket t) => t.SolicitanteUserGuid ?? t.CreatedByUserGuid;

    /// <summary>Cédula del solicitante efectivo (delegado si lo hay; si no, el creador).</summary>
    private static int SolicitanteCedulaDe(Ticket t) => t.SolicitanteUserId ?? t.CreatedByUserId;

    public async Task<TicketDetailDto?> TomarAsync(long id, CancellationToken ct)
    {
        var ticket = await _ctx.Tickets
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (ticket is null) return null;

        // El solicitante NO gestiona su propio ticket — ni siquiera el admin: la gestión la hace
        // el equipo que atiende. El admin sigue gestionando tickets ajenos (ahí EsSolicitante es falso),
        // incluidos los que él mismo registró A NOMBRE DE otro usuario.
        if (EsSolicitante(ticket))
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
        // Aplica también al admin cuando él es el solicitante: la gestión la hace el equipo que
        // atiende (sobre casos ajenos, donde EsSolicitante es falso, el admin gestiona normalmente).
        if (EsSolicitante(ticket))
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

            // Notificar la solución al solicitante por correo (cola asíncrona). Si el caso se
            // registró a nombre de otro usuario, el correo va a ESE usuario, no a quien lo tipeó.
            var (email, nombreSol) = await ResolveSolicitanteEmailAsync(
                SolicitanteGuidDe(ticket), SolicitanteCedulaDe(ticket), ct);
            if (!string.IsNullOrWhiteSpace(email))
            {
                try
                {
                    await _emailQueue.EnqueueEmailAsync(
                        email!,
                        $"[{ticket.Codigo}] Tu ticket fue solucionado",
                        TicketEmailTemplates.Solucionado(ticket, nombreSol,
                            _logoUrl, _brandName, BrandLine, _applicationUrl, _logoSecundarioUrl),
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

    public async Task<TicketDetailDto?> ConfirmarCierreAsync(long id, ConfirmarCierreRequest req, CancellationToken ct)
    {
        var ticket = await _ctx.Tickets
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (ticket is null) return null;

        if (!EsSolicitante(ticket))
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
                SolicitanteGuidDe(ticket), SolicitanteCedulaDe(ticket), ct);
            if (!string.IsNullOrWhiteSpace(solicitanteEmail))
                destinatarios[solicitanteEmail!] = solicitanteNombre;

            // Con solicitante delegado, quien registró el caso también recibe el cierre: es quien
            // lo está siguiendo operativamente.
            if (ticket.SolicitanteUserGuid.HasValue)
            {
                var (registradorEmail, registradorNombre) = await ResolveSolicitanteEmailAsync(
                    ticket.CreatedByUserGuid, ticket.CreatedByUserId, ct);
                if (!string.IsNullOrWhiteSpace(registradorEmail))
                    destinatarios.TryAdd(registradorEmail!, registradorNombre);
            }

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
                        _logoUrl, _brandName, BrandLine, _applicationUrl, _logoSecundarioUrl);
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
