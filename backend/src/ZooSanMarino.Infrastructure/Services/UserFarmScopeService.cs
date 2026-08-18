using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Administración del alcance granular usuario-granja (flag restrict_locations + user_farm_scopes).
/// Todas las validaciones son fail-closed: un item que no exista o no pertenezca a la granja aborta
/// el reemplazo completo sin persistir nada.
/// </summary>
public class UserFarmScopeService : IUserFarmScopeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    public UserFarmScopeService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    /// <summary>
    /// Gate de administración (fix QA A1): solo Super Admin o un rol con permisos de administración
    /// EN LA EMPRESA DE LA GRANJA (is_company_admin o admin/administrador) puede leer o cambiar el
    /// alcance de otros usuarios — sin esto, un usuario restringido podía quitarse la restricción
    /// con un PUT directo. Valida además que la granja exista (y de paso fija la empresa contra la
    /// que se exige el rol: no sirve ser admin de OTRA empresa). Fail-closed.
    /// </summary>
    private async Task EnsureCallerPuedeAdministrarAlcanceAsync(int farmId)
    {
        var guid = _current.UserGuid;
        if (!guid.HasValue)
            throw new UnauthorizedAccessException("Sesión inválida. Inicie sesión de nuevo.");

        var farmCompanyId = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == farmId)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync();
        if (farmCompanyId is null)
            throw new InvalidOperationException($"Granja con ID {farmId} no encontrada.");

        // Super Admin (mismo criterio, y ahora el mismo lector, que el resto del backend)
        if (await SuperAdminLookup.EsSuperAdminAsync(_ctx, guid)) return;

        var esAdminDeLaEmpresa = await _ctx.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == guid.Value && ur.CompanyId == farmCompanyId.Value)
            .AnyAsync(ur => ur.Role.IsCompanyAdmin ||
                            ur.Role.Name.ToLower() == "admin" ||
                            ur.Role.Name.ToLower() == "administrador");
        if (!esAdminDeLaEmpresa)
            throw new UnauthorizedAccessException(
                "Solo un Administrador de Empresa o Super Admin puede administrar el alcance de granjas.");
    }

    public async Task<UserFarmScopeConfigDto> GetScopeAsync(Guid userId, int farmId)
    {
        await EnsureCallerPuedeAdministrarAlcanceAsync(farmId);

        var userFarm = await _ctx.UserFarms.AsNoTracking()
            .Include(uf => uf.Farm)
            .FirstOrDefaultAsync(uf => uf.UserId == userId && uf.FarmId == farmId);

        if (userFarm is null)
            throw new InvalidOperationException("Asociación usuario-granja no encontrada.");

        var items = await BuildItemsWithNamesAsync(userId, farmId);
        return new UserFarmScopeConfigDto(userId, farmId, userFarm.Farm.Name, userFarm.RestrictLocations, items);
    }

    public async Task<UserFarmScopeConfigDto> ReplaceScopeAsync(
        Guid userId, int farmId, UpdateUserFarmScopeDto dto, Guid updatedByUserId)
    {
        await EnsureCallerPuedeAdministrarAlcanceAsync(farmId);

        var userFarm = await _ctx.UserFarms.AsNoTracking()
            .Include(uf => uf.Farm)
            .FirstOrDefaultAsync(uf => uf.UserId == userId && uf.FarmId == farmId);

        if (userFarm is null)
            throw new InvalidOperationException("Asociación usuario-granja no encontrada. Asigne primero la granja al usuario.");

        var items = dto.RestrictLocations ? (dto.Items ?? Array.Empty<UserFarmScopeItemDto>()) : Array.Empty<UserFarmScopeItemDto>();

        // Validación de forma (nivel + id del nivel) — lógica pura
        foreach (var item in items)
        {
            var error = UserLocationScopeCalculos.ValidarItem(item.Level, item.NucleoId, item.GalponId, item.LoteId);
            if (error is not null) throw new InvalidOperationException(error);
        }

        // Validación de pertenencia a la granja (fail-closed, todo-o-nada)
        var nucleoIds = items.Where(i => i.Level == UserLocationScopeCalculos.LevelNucleo).Select(i => i.NucleoId!).Distinct().ToList();
        var galponIds = items.Where(i => i.Level == UserLocationScopeCalculos.LevelGalpon).Select(i => i.GalponId!).Distinct().ToList();
        var loteIds   = items.Where(i => i.Level == UserLocationScopeCalculos.LevelLote).Select(i => i.LoteId!.Value).Distinct().ToList();

        if (nucleoIds.Count > 0)
        {
            var existentes = await _ctx.Nucleos.AsNoTracking()
                .Where(n => n.GranjaId == farmId && n.DeletedAt == null && nucleoIds.Contains(n.NucleoId))
                .Select(n => n.NucleoId).ToListAsync();
            var faltantes = nucleoIds.Except(existentes).ToList();
            if (faltantes.Count > 0)
                throw new InvalidOperationException($"Núcleo(s) inexistente(s) en la granja {farmId}: {string.Join(", ", faltantes)}.");
        }
        if (galponIds.Count > 0)
        {
            var existentes = await _ctx.Galpones.AsNoTracking()
                .Where(g => g.GranjaId == farmId && g.DeletedAt == null && galponIds.Contains(g.GalponId))
                .Select(g => g.GalponId).ToListAsync();
            var faltantes = galponIds.Except(existentes).ToList();
            if (faltantes.Count > 0)
                throw new InvalidOperationException($"Galpón(es) inexistente(s) en la granja {farmId}: {string.Join(", ", faltantes)}.");
        }
        if (loteIds.Count > 0)
        {
            var existentes = await _ctx.Lotes.AsNoTracking()
                .Where(l => l.GranjaId == farmId && l.DeletedAt == null && l.LoteId != null && loteIds.Contains(l.LoteId!.Value))
                .Select(l => l.LoteId!.Value).ToListAsync();
            var faltantes = loteIds.Except(existentes).ToList();
            if (faltantes.Count > 0)
                throw new InvalidOperationException($"Lote(s) inexistente(s) en la granja {farmId}: {string.Join(", ", faltantes)}.");
        }

        // Reemplazo transaccional SIN ChangeTracker (ExecuteDelete/Update + INSERT parametrizado):
        // evita la auditoría de SaveChanges del contexto, que al tocar user_farms sin el User
        // trackeado hace Attach() dentro del foreach de ChangeTracker.Entries() y revienta con
        // "Collection was modified" (bug latente del contexto, documentado aparte).
        var ahora = DateTime.UtcNow;
        await using (var tx = await _ctx.Database.BeginTransactionAsync())
        {
            await _ctx.UserFarmScopes
                .Where(s => s.UserId == userId && s.FarmId == farmId)
                .ExecuteDeleteAsync();

            await _ctx.UserFarms
                .Where(uf => uf.UserId == userId && uf.FarmId == farmId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RestrictLocations, dto.RestrictLocations));

            foreach (var nucleoId in nucleoIds)
                await _ctx.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO public.user_farm_scopes (user_id, farm_id, nucleo_id, created_at, created_by_user_id)
                       VALUES ({userId}, {farmId}, {nucleoId}, {ahora}, {updatedByUserId})");
            foreach (var galponId in galponIds)
                await _ctx.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO public.user_farm_scopes (user_id, farm_id, galpon_id, created_at, created_by_user_id)
                       VALUES ({userId}, {farmId}, {galponId}, {ahora}, {updatedByUserId})");
            foreach (var loteId in loteIds)
                await _ctx.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO public.user_farm_scopes (user_id, farm_id, lote_id, created_at, created_by_user_id)
                       VALUES ({userId}, {farmId}, {loteId}, {ahora}, {updatedByUserId})");

            await tx.CommitAsync();
        }

        var itemsOut = await BuildItemsWithNamesAsync(userId, farmId);
        return new UserFarmScopeConfigDto(userId, farmId, userFarm.Farm.Name, dto.RestrictLocations, itemsOut);
    }

    public async Task<FarmLocationsTreeDto> GetFarmLocationsTreeAsync(int farmId)
    {
        await EnsureCallerPuedeAdministrarAlcanceAsync(farmId);

        var farm = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == farmId && f.DeletedAt == null)
            .Select(f => new { f.Id, f.Name })
            .FirstOrDefaultAsync();
        if (farm is null)
            throw new InvalidOperationException($"Granja con ID {farmId} no encontrada.");

        var nucleos = await _ctx.Nucleos.AsNoTracking()
            .Where(n => n.GranjaId == farmId && n.DeletedAt == null)
            .OrderBy(n => n.NucleoNombre)
            .Select(n => new { n.NucleoId, n.NucleoNombre })
            .ToListAsync();

        var galpones = await _ctx.Galpones.AsNoTracking()
            .Where(g => g.GranjaId == farmId && g.DeletedAt == null)
            .OrderBy(g => g.GalponNombre)
            .Select(g => new { g.GalponId, g.GalponNombre, g.NucleoId })
            .ToListAsync();

        var lotes = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.GranjaId == farmId && l.DeletedAt == null && l.LoteId != null)
            .OrderBy(l => l.LoteNombre)
            .Select(l => new { LoteId = l.LoteId!.Value, l.LoteNombre, l.Fase, l.NucleoId, l.GalponId })
            .ToListAsync();

        var galponIdsValidos = galpones.Select(g => g.GalponId).ToHashSet();
        var nucleoIdsValidos = nucleos.Select(n => n.NucleoId).ToHashSet();

        var nucleoNodes = nucleos.Select(n =>
        {
            var galponesDelNucleo = galpones
                .Where(g => g.NucleoId == n.NucleoId)
                .Select(g => new FarmLocationGalponNodeDto(
                    g.GalponId,
                    g.GalponNombre,
                    lotes.Where(l => l.GalponId == g.GalponId)
                         .Select(l => new FarmLocationLoteNodeDto(l.LoteId, l.LoteNombre, l.Fase))
                         .ToArray()))
                .ToArray();

            var lotesSinGalpon = lotes
                .Where(l => l.NucleoId == n.NucleoId &&
                            (l.GalponId == null || !galponIdsValidos.Contains(l.GalponId)))
                .Select(l => new FarmLocationLoteNodeDto(l.LoteId, l.LoteNombre, l.Fase))
                .ToArray();

            return new FarmLocationNucleoNodeDto(n.NucleoId, n.NucleoNombre, galponesDelNucleo, lotesSinGalpon);
        }).ToArray();

        var lotesSinNucleo = lotes
            .Where(l => (l.NucleoId == null || !nucleoIdsValidos.Contains(l.NucleoId)) &&
                        (l.GalponId == null || !galponIdsValidos.Contains(l.GalponId)))
            .Select(l => new FarmLocationLoteNodeDto(l.LoteId, l.LoteNombre, l.Fase))
            .ToArray();

        return new FarmLocationsTreeDto(farm.Id, farm.Name, nucleoNodes, lotesSinNucleo);
    }

    /// <summary>Grants actuales con nombre display, resueltos contra los catálogos (referencias muertas se muestran sin nombre).</summary>
    private async Task<UserFarmScopeItemDto[]> BuildItemsWithNamesAsync(Guid userId, int farmId)
    {
        var scopes = await _ctx.UserFarmScopes.AsNoTracking()
            .Where(s => s.UserId == userId && s.FarmId == farmId)
            .OrderBy(s => s.Id)
            .ToListAsync();
        if (scopes.Count == 0) return Array.Empty<UserFarmScopeItemDto>();

        var nucleoIds = scopes.Where(s => s.NucleoId != null).Select(s => s.NucleoId!).Distinct().ToList();
        var galponIds = scopes.Where(s => s.GalponId != null).Select(s => s.GalponId!).Distinct().ToList();
        var loteIds   = scopes.Where(s => s.LoteId != null).Select(s => s.LoteId!.Value).Distinct().ToList();

        var nombresNucleo = nucleoIds.Count == 0 ? new Dictionary<string, string>() :
            await _ctx.Nucleos.AsNoTracking()
                .Where(n => n.GranjaId == farmId && nucleoIds.Contains(n.NucleoId))
                .ToDictionaryAsync(n => n.NucleoId, n => n.NucleoNombre);
        var nombresGalpon = galponIds.Count == 0 ? new Dictionary<string, string>() :
            await _ctx.Galpones.AsNoTracking()
                .Where(g => galponIds.Contains(g.GalponId))
                .ToDictionaryAsync(g => g.GalponId, g => g.GalponNombre);
        var nombresLote = loteIds.Count == 0 ? new Dictionary<int, string>() :
            await _ctx.Lotes.AsNoTracking()
                .Where(l => l.LoteId != null && loteIds.Contains(l.LoteId!.Value))
                .ToDictionaryAsync(l => l.LoteId!.Value, l => l.LoteNombre);

        return scopes.Select(s =>
        {
            if (s.NucleoId is not null)
                return new UserFarmScopeItemDto(
                    UserLocationScopeCalculos.LevelNucleo, s.NucleoId, null, null,
                    nombresNucleo.GetValueOrDefault(s.NucleoId));
            if (s.GalponId is not null)
                return new UserFarmScopeItemDto(
                    UserLocationScopeCalculos.LevelGalpon, null, s.GalponId, null,
                    nombresGalpon.GetValueOrDefault(s.GalponId));
            return new UserFarmScopeItemDto(
                UserLocationScopeCalculos.LevelLote, null, null, s.LoteId,
                s.LoteId.HasValue ? nombresLote.GetValueOrDefault(s.LoteId.Value) : null);
        }).ToArray();
    }
}
