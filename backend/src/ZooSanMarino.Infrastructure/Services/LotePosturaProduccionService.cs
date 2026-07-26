using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Lotes;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using FarmLiteDto = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LotePosturaProduccionService : ILotePosturaProduccionService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly IUserPermissionService _userPermissionService;
    private readonly IUserFarmService _userFarmService;
    private readonly ILocationScopeResolver _scopeResolver;

    public LotePosturaProduccionService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        IUserPermissionService userPermissionService,
        IUserFarmService userFarmService,
        ILocationScopeResolver scopeResolver)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _userPermissionService = userPermissionService;
        _userFarmService = userFarmService;
        _scopeResolver = scopeResolver;
    }

    /// <summary>
    /// Filtro de alcance granular (user_farms.restrict_locations + user_farm_scopes), componible en
    /// SQL. Filas con LoteId (FK a lotes, heredado del levante) se deciden por lote permitido;
    /// filas legacy sin LoteId caen al galpón/núcleo visible. Granjas no restringidas pasan intactas.
    /// <paramref name="paraDestino"/> = true lo omite (selección de DESTINO en traslados).
    /// </summary>
    private async Task<IQueryable<LotePosturaProduccion>> AplicarScopeUbicacionAsync(
        IQueryable<LotePosturaProduccion> q, bool paraDestino = false)
    {
        if (paraDestino) return q;
        var restringidos = await _scopeResolver.GetAllRestrictedScopesAsync();
        if (restringidos.Count == 0) return q;

        var granjasRestringidas = restringidos.Keys.ToList();
        var lotesPermitidos = restringidos.SelectMany(kv => kv.Value.LotesPermitidos).ToList();
        var galponesVisibles = restringidos.SelectMany(kv => kv.Value.GalponesVisibles).Distinct().ToList();
        var clavesNucleo = restringidos
            .SelectMany(kv => kv.Value.NucleosVisibles.Select(n => kv.Key + "|" + n))
            .ToList();

        return q.Where(l => !granjasRestringidas.Contains(l.GranjaId)
            || (l.LoteId != null && lotesPermitidos.Contains(l.LoteId.Value))
            || (l.LoteId == null && l.GalponId != null && l.GalponId != "" && galponesVisibles.Contains(l.GalponId))
            || (l.LoteId == null && (l.GalponId == null || l.GalponId == "") && l.NucleoId != null &&
                clavesNucleo.Contains(l.GranjaId.ToString() + "|" + l.NucleoId)));
    }

    private async Task<int> GetEffectiveCompanyIdAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    private async Task<bool> IsUserAdminOrAdministratorAsync(CancellationToken ct = default)
    {
        var userIdGuid = _current.UserGuid;
        if (!userIdGuid.HasValue) return false;

        var userRoles = await _ctx.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userIdGuid.Value)
            .Select(ur => ur.Role!.Name)
            .ToListAsync(ct);

        return userRoles.Any(role =>
            !string.IsNullOrWhiteSpace(role) &&
            (role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
             role.Equals("administrador", StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<bool> IsSuperAdminAsync(CancellationToken ct = default)
    {
        var userIdGuid = _current.UserGuid;
        if (!userIdGuid.HasValue) return false;

        var userEmail = await _ctx.UserLogins
            .AsNoTracking()
            .Include(ul => ul.Login)
            .Where(ul => ul.UserId == userIdGuid.Value)
            .Select(ul => ul.Login!.email)
            .FirstOrDefaultAsync(ct);

        return userEmail?.ToLower() == "moiesbbuga@gmail.com";
    }

    private async Task<List<int>?> GetAllowedFarmIdsForCurrentUserAsync(CancellationToken ct = default)
    {
        var userIdGuid = _current.UserGuid;
        if (!userIdGuid.HasValue) return null;

        var accessible = await _userFarmService.GetUserAccessibleFarmsAsync(userIdGuid.Value);
        return accessible.Select(x => x.FarmId).Distinct().ToList();
    }

    /// <summary>
    /// Obtiene todos los lotes postura producción de la empresa en sesión,
    /// filtrados por granjas a las que el usuario tiene permiso.
    /// </summary>
    public async Task<IEnumerable<LotePosturaProduccionDetailDto>> GetAllAsync(CancellationToken ct = default, bool paraDestino = false)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);

        var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
        var allCountriesCount = await _ctx.Set<Pais>().CountAsync(ct);
        var isAdmin = assignedCountries.Count() >= allCountriesCount ||
                     await IsUserAdminOrAdministratorAsync(ct) ||
                     await IsSuperAdminAsync(ct);

        var q = _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null);

        if (!isAdmin)
        {
            var allowedFarmIds = await GetAllowedFarmIdsForCurrentUserAsync(ct);
            if (allowedFarmIds != null && allowedFarmIds.Count > 0)
                q = q.Where(l => allowedFarmIds.Contains(l.GranjaId));
            else
                q = q.Where(_ => false);
        }

        // Alcance granular núcleo/galpón/lote — aplica incluso a admin (restricción explícita
        // gana al bypass de rol). Omitido al elegir DESTINO de traslados.
        q = await AplicarScopeUbicacionAsync(q, paraDestino);

        q = q.OrderBy(l => l.LotePosturaProduccionId);
        return await ProjectToDetail(q).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LotePosturaProduccionDetailDto>> GetByLoteIdAsync(int loteId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);

        var allowedFarmIds = (List<int>?)null;
        var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
        var allCountriesCount = await _ctx.Set<Pais>().CountAsync(ct);
        var isAdmin = assignedCountries.Count() >= allCountriesCount ||
                     await IsUserAdminOrAdministratorAsync(ct) ||
                     await IsSuperAdminAsync(ct);
        if (!isAdmin)
            allowedFarmIds = await GetAllowedFarmIdsForCurrentUserAsync(ct);

        var levanteIds = await _ctx.LotePosturaLevante
            .AsNoTracking()
            .Where(lv => lv.CompanyId == companyId && lv.DeletedAt == null && lv.LoteId == loteId)
            .Select(lv => lv.LotePosturaLevanteId ?? 0)
            .ToListAsync(ct);

        if (levanteIds.Count == 0)
            return Enumerable.Empty<LotePosturaProduccionDetailDto>();

        var q = _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && levanteIds.Contains(l.LotePosturaLevanteId ?? 0));

        if (allowedFarmIds != null && allowedFarmIds.Count > 0)
            q = q.Where(l => allowedFarmIds.Contains(l.GranjaId));
        else if (!isAdmin)
            return Enumerable.Empty<LotePosturaProduccionDetailDto>();

        // Alcance granular: acceso directo por loteId también respeta el scope (fail-closed → vacío)
        q = await AplicarScopeUbicacionAsync(q);

        q = q.OrderBy(l => l.LotePosturaProduccionId);
        return await ProjectToDetail(q).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<LotePosturaProduccionDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var q = _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LotePosturaProduccionId == id);
        return await ProjectToDetail(q).FirstOrDefaultAsync(ct);
    }

    private static IQueryable<LotePosturaProduccionDetailDto> ProjectToDetail(
        IQueryable<LotePosturaProduccion> q)
    {
        return q
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Select(l => new LotePosturaProduccionDetailDto(
                l.LotePosturaProduccionId ?? 0,
                l.LoteNombre,
                l.GranjaId,
                l.NucleoId,
                l.GalponId,
                l.Regional,
                l.FechaEncaset,
                l.HembrasL,
                l.MachosL,
                l.PesoInicialH,
                l.PesoInicialM,
                l.Raza,
                l.Linea,
                l.Tecnico,
                l.AvesEncasetadas,
                l.EdadInicial,
                l.LoteErp,
                l.PaisId,
                l.PaisNombre,
                l.EmpresaNombre,
                l.CompanyId,
                l.CreatedAt,
                l.FechaInicioProduccion,
                l.HembrasInicialesProd,
                l.MachosInicialesProd,
                l.LotePosturaLevanteId,
                l.AvesHInicial,
                l.AvesMInicial,
                l.AvesHActual,
                l.AvesMActual,
                l.Estado,
                l.Etapa,
                l.Edad,
                l.EstadoCierre,
                new FarmLiteDto(
                    l.Farm.Id,
                    l.Farm.Name,
                    l.Farm.RegionalId,
                    l.Farm.DepartamentoId,
                    l.Farm.MunicipioId,
                    l.Farm.ClienteId,
                    l.Farm.Zona,
                    l.Farm.CertificadoGab,
                    l.Farm.Latitud,
                    l.Farm.Longitud
                ),
                l.Nucleo == null
                    ? null
                    : new NucleoLiteDto(
                        l.Nucleo.NucleoId,
                        l.Nucleo.NucleoNombre ?? l.Nucleo.NucleoId,
                        l.Nucleo.GranjaId
                    ),
                l.Galpon == null
                    ? null
                    : new GalponLiteDto(
                        l.Galpon.GalponId,
                        l.Galpon.GalponNombre ?? l.Galpon.GalponId,
                        l.Galpon.NucleoId ?? "",
                        l.Galpon.GranjaId
                    ),
                // Feature 14 — orden posicional (LoteId + 4 acumulados producción)
                l.LoteId,
                l.ProduccionTrasladoIngresoHembras,
                l.ProduccionTrasladoIngresoMachos,
                l.ProduccionTrasladoSalidaHembras,
                l.ProduccionTrasladoSalidaMachos
            ));
    }
}
