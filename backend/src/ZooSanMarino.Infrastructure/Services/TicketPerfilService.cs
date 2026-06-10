using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio de perfiles de atención del módulo de tickets.
/// Gestiona resolutores por (usuario|rol, tipo, país/global) y nivel del solicitante.
/// </summary>
public class TicketPerfilService : ITicketPerfilService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    public TicketPerfilService(ZooSanMarinoContext ctx, ICurrentUser currentUser, ICompanyResolver companyResolver)
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

    // ─────────────── Tipos permitidos + asignables (para crear ticket) ───────────────

    public async Task<IReadOnlyList<TipoPermitidoDto>> GetTiposPermitidosAsync(CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var paisId = _currentUser.PaisId;
        var userGuid = _currentUser.UserGuid;

        // 1) Nivel del usuario actual — si tiene tickets.gestionar o tickets.admin → IMPLEMENTADOR
        string nivel = NivelTicket.Normal;
        var perms = _currentUser.Permissions;
        if (perms.Contains("tickets.gestionar", StringComparer.OrdinalIgnoreCase) ||
            perms.Contains("tickets.admin", StringComparer.OrdinalIgnoreCase))
        {
            nivel = NivelTicket.Implementador;
        }
        else if (userGuid.HasValue)
        {
            var perfil = await _ctx.TicketPerfilesUsuario.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userGuid.Value && p.CompanyId == companyId && p.Activo, ct);
            if (perfil != null) nivel = perfil.Nivel;
        }

        var tiposDelNivel = NivelTicket.GetTiposPermitidos(nivel);

        // 2) Para cada tipo del nivel, buscar asignables (país == paisId OR global)
        var result = new List<TipoPermitidoDto>();
        foreach (var tipo in tiposDelNivel)
        {
            var asignables = await GetAsignablesInternalAsync(tipo, paisId, companyId, ct);
            if (asignables.Count > 0)
                result.Add(new TipoPermitidoDto(tipo, TipoLabel(tipo), asignables));
        }
        return result;
    }

    public async Task<IReadOnlyList<AsignableDto>> GetAsignablesAsync(string tipo, int? paisId, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        return await GetAsignablesInternalAsync(tipo, paisId, companyId, ct);
    }

    private async Task<IReadOnlyList<AsignableDto>> GetAsignablesInternalAsync(
        string tipo, int? paisId, int companyId, CancellationToken ct)
    {
        // Resolutores válidos: tipo coincide Y (pais_id == paisId OR pais_id IS NULL) Y activo.
        // Sin filtro de company: los resolutores son globales (equipo central de soporte).
        var query = _ctx.TicketResolutores.AsNoTracking()
            .Where(r => r.Activo &&
                        r.Tipo == tipo.ToUpperInvariant() &&
                        (r.PaisId == null || r.PaisId == paisId));

        var resolutores = await query.Select(r => new { r.UserId, r.PaisId }).ToListAsync(ct);

        var result = new List<AsignableDto>();
        foreach (var r in resolutores)
        {
            var user = await _ctx.Set<User>().AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
            if (user == null) continue;
            var nombre = $"{user.firstName} {user.surName}".Trim();
            var paisLabel = r.PaisId == null ? "Global" : $"País #{r.PaisId}";
            result.Add(new AsignableDto(r.UserId, nombre, paisLabel));
        }
        return result;
    }

    // ─────────────── Perfil de usuario ───────────────

    public async Task<TicketPerfilDto> GetPerfilUsuarioAsync(Guid userId, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        var perfil = await _ctx.TicketPerfilesUsuario.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.CompanyId == companyId, ct);

        var resolutores = await _ctx.TicketResolutores.AsNoTracking()
            .Where(r => r.UserId == userId && r.CompanyId == companyId)
            .Select(r => new ResolutorItemDto(r.Id, r.Tipo, r.PaisId, r.Activo))
            .ToListAsync(ct);

        return new TicketPerfilDto(userId, perfil?.Nivel ?? NivelTicket.Normal, resolutores);
    }

    public async Task<TicketPerfilDto> UpsertPerfilUsuarioAsync(Guid userId, UpsertTicketPerfilRequest req, CancellationToken ct)
    {
        if (!NivelTicket.Todos.Contains(req.Nivel))
            throw new InvalidOperationException($"Nivel inválido: {req.Nivel}. Use NORMAL o IMPLEMENTADOR.");

        var companyId = await GetEffectiveCompanyIdAsync();
        var now = DateTime.UtcNow;

        // Upsert nivel
        var perfil = await _ctx.TicketPerfilesUsuario
            .FirstOrDefaultAsync(p => p.UserId == userId && p.CompanyId == companyId, ct);
        if (perfil == null)
        {
            _ctx.TicketPerfilesUsuario.Add(new TicketPerfilUsuario { UserId = userId, CompanyId = companyId, Nivel = req.Nivel.ToUpperInvariant(), CreatedAt = now });
        }
        else
        {
            perfil.Nivel = req.Nivel.ToUpperInvariant();
            perfil.UpdatedAt = now;
        }

        // Desactivar todos los resolutores actuales del usuario
        var existing = await _ctx.TicketResolutores
            .Where(r => r.UserId == userId && r.CompanyId == companyId)
            .ToListAsync(ct);
        foreach (var e in existing) e.Activo = false;

        // Upsert con los nuevos
        foreach (var item in req.Resolutores)
        {
            if (!TicketTipos.EsValido(item.Tipo)) continue;
            var tipo = item.Tipo.ToUpperInvariant();
            var found = existing.FirstOrDefault(r => r.Tipo == tipo && r.PaisId == item.PaisId);
            if (found != null) { found.Activo = true; found.UpdatedAt = now; }
            else _ctx.TicketResolutores.Add(new TicketResolutor { UserId = userId, Tipo = tipo, PaisId = item.PaisId, CompanyId = companyId, CreatedAt = now });
        }

        await _ctx.SaveChangesAsync(ct);
        return await GetPerfilUsuarioAsync(userId, ct);
    }

    // ─────────────── Perfil de rol (defaults) ───────────────

    public async Task<TicketResolutorRolDto> GetPerfilRolAsync(int roleId, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var items = await _ctx.TicketResolutorRoles.AsNoTracking()
            .Where(r => r.RoleId == roleId && r.CompanyId == companyId)
            .Select(r => new ResolutorItemDto(r.Id, r.Tipo, r.PaisId, r.Activo))
            .ToListAsync(ct);
        return new TicketResolutorRolDto(roleId, items);
    }

    public async Task<TicketResolutorRolDto> UpsertPerfilRolAsync(int roleId, UpsertTicketResolutorRolRequest req, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var now = DateTime.UtcNow;

        var existing = await _ctx.TicketResolutorRoles
            .Where(r => r.RoleId == roleId && r.CompanyId == companyId)
            .ToListAsync(ct);
        foreach (var e in existing) e.Activo = false;

        foreach (var item in req.Resolutores)
        {
            if (!TicketTipos.EsValido(item.Tipo)) continue;
            var tipo = item.Tipo.ToUpperInvariant();
            var found = existing.FirstOrDefault(r => r.Tipo == tipo && r.PaisId == item.PaisId);
            if (found != null) { found.Activo = true; found.UpdatedAt = now; }
            else _ctx.TicketResolutorRoles.Add(new TicketResolutorRol { RoleId = roleId, Tipo = tipo, PaisId = item.PaisId, CompanyId = companyId, CreatedAt = now });
        }

        await _ctx.SaveChangesAsync(ct);
        return await GetPerfilRolAsync(roleId, ct);
    }

    public async Task SeedPerfilDesdeRolAsync(Guid userId, int roleId, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var defaults = await _ctx.TicketResolutorRoles.AsNoTracking()
            .Where(r => r.RoleId == roleId && r.CompanyId == companyId && r.Activo)
            .ToListAsync(ct);

        if (!defaults.Any()) return;
        var now = DateTime.UtcNow;

        foreach (var d in defaults)
        {
            var exists = await _ctx.TicketResolutores
                .AnyAsync(r => r.UserId == userId && r.Tipo == d.Tipo && r.PaisId == d.PaisId && r.CompanyId == companyId, ct);
            if (!exists)
                _ctx.TicketResolutores.Add(new TicketResolutor { UserId = userId, Tipo = d.Tipo, PaisId = d.PaisId, CompanyId = companyId, CreatedAt = now });
        }
        await _ctx.SaveChangesAsync(ct);
    }

    private static string TipoLabel(string tipo) => tipo switch
    {
        "SOPORTE" => "Soporte", "DESARROLLO" => "Desarrollo",
        "REQUERIMIENTO" => "Requerimiento", "DUDAS" => "Dudas",
        _ => tipo
    };
}
