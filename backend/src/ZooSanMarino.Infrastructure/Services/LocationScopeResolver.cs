using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Resuelve el alcance granular de ubicación del usuario ACTUAL por granja (registrado Scoped =
/// caché por request). Costo cero para usuarios sin restricciones: una sola query liviana de las
/// granjas con restrict_locations = true; solo para esas se cargan grants + catálogos y se computa
/// el cierre (lógica pura en <see cref="UserLocationScopeCalculos"/>).
/// Sin usuario en contexto (jobs internos) ⇒ Global, igual que el scoping por granja existente.
/// </summary>
public class LocationScopeResolver : ILocationScopeResolver
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    private HashSet<int>? _restrictedFarmIds;
    private readonly Dictionary<int, UserLocationScopeCalculos.LocationScope> _cache = new();

    public LocationScopeResolver(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    private async Task<HashSet<int>> GetRestrictedFarmIdsAsync()
    {
        if (_restrictedFarmIds is not null) return _restrictedFarmIds;

        var userGuid = _current.UserGuid;
        if (!userGuid.HasValue)
            return _restrictedFarmIds = new HashSet<int>();

        var ids = await _ctx.UserFarms.AsNoTracking()
            .Where(uf => uf.UserId == userGuid.Value && uf.RestrictLocations)
            .Select(uf => uf.FarmId)
            .ToListAsync();
        return _restrictedFarmIds = ids.ToHashSet();
    }

    public async Task<UserLocationScopeCalculos.LocationScope> GetScopeAsync(int farmId)
    {
        if (_cache.TryGetValue(farmId, out var cached)) return cached;

        var restricted = await GetRestrictedFarmIdsAsync();
        if (!restricted.Contains(farmId))
            return _cache[farmId] = UserLocationScopeCalculos.LocationScope.Global;

        var userGuid = _current.UserGuid!.Value;

        var grants = await _ctx.UserFarmScopes.AsNoTracking()
            .Where(s => s.UserId == userGuid && s.FarmId == farmId)
            .Select(s => new UserLocationScopeCalculos.ScopeGrant(s.NucleoId, s.GalponId, s.LoteId))
            .ToListAsync();

        var nucleos = await _ctx.Nucleos.AsNoTracking()
            .Where(n => n.GranjaId == farmId && n.DeletedAt == null)
            .Select(n => n.NucleoId)
            .ToListAsync();

        var galpones = await _ctx.Galpones.AsNoTracking()
            .Where(g => g.GranjaId == farmId && g.DeletedAt == null)
            .Select(g => new UserLocationScopeCalculos.GalponUbicacion(g.GalponId, g.NucleoId))
            .ToListAsync();

        var lotes = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.GranjaId == farmId && l.DeletedAt == null && l.LoteId != null)
            .Select(l => new UserLocationScopeCalculos.LoteUbicacion(l.LoteId!.Value, l.NucleoId, l.GalponId))
            .ToListAsync();

        var scope = UserLocationScopeCalculos.ComputeScope(true, grants, nucleos, galpones, lotes);
        return _cache[farmId] = scope;
    }

    public async Task<IReadOnlyDictionary<int, UserLocationScopeCalculos.LocationScope>> GetRestrictedScopesAsync(
        IEnumerable<int> farmIds)
    {
        var restricted = await GetRestrictedFarmIdsAsync();
        var result = new Dictionary<int, UserLocationScopeCalculos.LocationScope>();
        foreach (var farmId in farmIds.Distinct())
        {
            if (restricted.Contains(farmId))
                result[farmId] = await GetScopeAsync(farmId);
        }
        return result;
    }

    public async Task<IReadOnlyDictionary<int, UserLocationScopeCalculos.LocationScope>> GetAllRestrictedScopesAsync()
    {
        var restricted = await GetRestrictedFarmIdsAsync();
        var result = new Dictionary<int, UserLocationScopeCalculos.LocationScope>();
        foreach (var farmId in restricted)
            result[farmId] = await GetScopeAsync(farmId);
        return result;
    }

    public async Task<bool> PermiteLoteAsync(int loteId)
    {
        var granjaId = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId)
            .Select(l => (int?)l.GranjaId)
            .FirstOrDefaultAsync();

        // Lote inexistente: no bloquear aquí; el flujo normal responde su propio 404.
        if (granjaId is null) return true;

        var scope = await GetScopeAsync(granjaId.Value);
        return scope.PermiteLote(loteId);
    }
}
