// src/ZooSanMarino.Infrastructure/Services/Tickets/TicketAsignablesConsulta.cs
// Una sola fórmula de «quién puede recibir un ticket» (desplegable, crear, transferir y ver).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Quién ATIENDE un ticket de (tipo, país) de una empresa. La usan el desplegable «Asignar a»
/// (<see cref="TicketPerfilService"/>), la validación al crear y al transferir y la visibilidad del
/// caso (<see cref="TicketService"/>, <see cref="TicketTareaService"/>).
/// </summary>
/// <remarks>
/// <para>
/// Antes del 19-sep-2026 eran tres implementaciones: el desplegable filtraba la fila por empresa, la
/// validación de <c>CreateAsync</c> no (la API aceptaba asignar a quien el desplegable no ofrecía) y
/// la visibilidad no miraba la empresa (un resolutor veía los casos de su tipo de cualquier empresa).
/// Ahora la fila aplica si es GLOBAL o si es de la empresa del ticket
/// (<see cref="TicketResolutorAlcanceCalculos.Aplica"/>), en los tres caminos.
/// </para>
/// <para>
/// La membresía de un rol NO se filtra por empresa (igual que antes): el rol es de su empresa y una
/// fila de rol en otra empresa solo la puede crear el admin global, a propósito.
/// </para>
/// </remarks>
internal static class TicketAsignablesConsulta
{
    /// <summary>Asignables de (tipo, país) para los tickets de <paramref name="companyId"/>, sin repetidos.</summary>
    public static async Task<IReadOnlyList<AsignableDto>> ListarAsync(
        ZooSanMarinoContext ctx, string tipo, int? paisId, int companyId, CancellationToken ct)
    {
        var t = tipo.ToUpperInvariant();

        // 1. Resolutores directos por usuario.
        var directos = await ctx.TicketResolutores.AsNoTracking()
            .Where(r => r.Activo && r.Tipo == t &&
                        (r.Alcance == TicketAlcance.Global || r.CompanyId == companyId) &&
                        (r.PaisId == null || r.PaisId == paisId))
            .OrderBy(r => r.Id)
            .Select(r => new { r.UserId, r.Alcance, r.CompanyId })
            .ToListAsync(ct);

        // 2. Resolutores por rol (se leen en vivo: no hace falta copiar la plantilla al usuario).
        var porRol = await ctx.TicketResolutorRoles.AsNoTracking()
            .Where(r => r.Activo && r.Tipo == t &&
                        (r.Alcance == TicketAlcance.Global || r.CompanyId == companyId) &&
                        (r.PaisId == null || r.PaisId == paisId))
            .OrderBy(r => r.Id)
            .Select(r => new { r.RoleId, r.Alcance, r.CompanyId })
            .ToListAsync(ct);

        var roleIds = porRol.Select(r => r.RoleId).Distinct().ToList();
        var miembros = roleIds.Count == 0
            ? new List<(int RoleId, Guid UserId)>()
            : (await ctx.UserRoles.AsNoTracking()
                    .Where(ur => roleIds.Contains(ur.RoleId))
                    .Select(ur => new { ur.RoleId, ur.UserId })
                    .ToListAsync(ct))
                .Select(m => (m.RoleId, m.UserId)).ToList();

        var userIds = directos.Select(d => d.UserId)
            .Concat(miembros.Select(m => m.UserId))
            .Distinct().ToList();
        if (userIds.Count == 0) return Array.Empty<AsignableDto>();

        var nombres = await ctx.Set<User>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.firstName, u.surName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.firstName} {u.surName}".Trim(), ct);

        var empresaIds = directos.Select(d => d.CompanyId).Concat(porRol.Select(r => r.CompanyId)).Distinct().ToList();
        var empresas = await ctx.Companies.AsNoTracking()
            .Where(c => empresaIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        string Etiqueta(string alcance, int filaCompanyId) =>
            TicketResolutorAlcanceCalculos.Etiqueta(alcance, empresas.GetValueOrDefault(filaCompanyId));

        var result = new List<AsignableDto>();
        var agregados = new HashSet<Guid>();

        foreach (var d in directos)
        {
            if (!nombres.TryGetValue(d.UserId, out var nombre) || !agregados.Add(d.UserId)) continue;
            result.Add(new AsignableDto(d.UserId, nombre, Etiqueta(d.Alcance, d.CompanyId)));
        }

        foreach (var rr in porRol)
        {
            foreach (var m in miembros.Where(m => m.RoleId == rr.RoleId))
            {
                if (!nombres.TryGetValue(m.UserId, out var nombre) || !agregados.Add(m.UserId)) continue;
                result.Add(new AsignableDto(m.UserId, nombre, Etiqueta(rr.Alcance, rr.CompanyId)));
            }
        }

        return result;
    }

    /// <summary>¿<paramref name="userGuid"/> puede recibir un ticket de (tipo, país) de la empresa?</summary>
    public static async Task<bool> EsAsignableAsync(
        ZooSanMarinoContext ctx, Guid? userGuid, string tipo, int? paisId, int companyId, CancellationToken ct)
    {
        if (userGuid is not { } guid || guid == Guid.Empty) return false;
        var asignables = await ListarAsync(ctx, tipo, paisId, companyId, ct);
        return asignables.Any(a => a.UserId == guid);
    }

    /// <summary>
    /// ¿Tiene un perfil de resolutor DIRECTO que aplique a los tickets de (tipo, país) de la empresa?
    /// Es la regla de visibilidad del caso para quien no es creador, solicitante ni asignado (antes no
    /// miraba la empresa).
    /// </summary>
    public static Task<bool> EsResolutorDirectoAsync(
        ZooSanMarinoContext ctx, Guid userGuid, string tipo, int paisId, int companyId, CancellationToken ct) =>
        ctx.TicketResolutores.AsNoTracking()
            .AnyAsync(r => r.UserId == userGuid && r.Activo && r.Tipo == tipo &&
                           (r.Alcance == TicketAlcance.Global || r.CompanyId == companyId) &&
                           (r.PaisId == null || r.PaisId == paisId), ct);
}
