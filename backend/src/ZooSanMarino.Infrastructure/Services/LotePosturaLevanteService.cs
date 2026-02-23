using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Lotes;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using FarmLiteDto = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LotePosturaLevanteService : ILotePosturaLevanteService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly IUserPermissionService _userPermissionService;
    private readonly IUserFarmService _userFarmService;

    public LotePosturaLevanteService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        IUserPermissionService userPermissionService,
        IUserFarmService userFarmService)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _userPermissionService = userPermissionService;
        _userFarmService = userFarmService;
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

    private const int SemanaCierre = 26;

    /// <summary>
    /// Cierra lotes que llegaron a semana 26 y crea lotes producción H/M.
    /// Ej: QLK345 → QLK345-H (hembras) y QLK345-M (machos).
    /// </summary>
    private async Task ProcessarCierresPendientesAsync(CancellationToken ct = default)
    {
        var allAbiertos = await _ctx.LotePosturaLevante
            .Where(l => l.DeletedAt == null &&
                        (l.EstadoCierre == "Abierto" || l.EstadoCierre == null))
            .ToListAsync(ct);

        var pendientes = allAbiertos.Where(l =>
        {
            var edadSemanas = l.Edad ?? (l.FechaEncaset.HasValue
                ? (int)((DateTime.UtcNow.Date - l.FechaEncaset.Value.Date).TotalDays / 7)
                : 0);
            return edadSemanas >= SemanaCierre;
        }).ToList();

        if (pendientes.Count == 0) return;

        var now = DateTime.UtcNow;
        var userId = _current.UserId;

        foreach (var lev in pendientes)
        {
            lev.EstadoCierre = "Cerrado";
            lev.UpdatedByUserId = userId;
            lev.UpdatedAt = now;

            var avesH = lev.AvesHActual ?? 0;
            var avesM = lev.AvesMActual ?? 0;

            var baseNombre = (lev.LoteNombre ?? "").Trim();
            if (string.IsNullOrEmpty(baseNombre)) baseNombre = $"Lote-{lev.LotePosturaLevanteId}";

            if (avesH > 0)
            {
                var prodH = CrearLoteProduccion(lev, $"{baseNombre}-H", avesH, 0, now, userId);
                _ctx.LotePosturaProduccion.Add(prodH);
            }
            if (avesM > 0)
            {
                var prodM = CrearLoteProduccion(lev, $"{baseNombre}-M", 0, avesM, now, userId);
                _ctx.LotePosturaProduccion.Add(prodM);
            }

            if (avesH == 0 && avesM == 0)
            {
                var prodH = CrearLoteProduccion(lev, $"{baseNombre}-H", 0, 0, now, userId);
                var prodM = CrearLoteProduccion(lev, $"{baseNombre}-M", 0, 0, now, userId);
                _ctx.LotePosturaProduccion.Add(prodH);
                _ctx.LotePosturaProduccion.Add(prodM);
            }
        }

        await _ctx.SaveChangesAsync(ct);
    }

    private static LotePosturaProduccion CrearLoteProduccion(
        LotePosturaLevante lev, string nombre, int avesH, int avesM, DateTime now, int userId)
    {
        return new LotePosturaProduccion
        {
            LoteNombre = nombre,
            GranjaId = lev.GranjaId,
            NucleoId = lev.NucleoId,
            GalponId = lev.GalponId,
            Regional = lev.Regional,
            FechaEncaset = lev.FechaEncaset,
            HembrasL = lev.HembrasL,
            MachosL = lev.MachosL,
            PesoInicialH = lev.PesoInicialH,
            PesoInicialM = lev.PesoInicialM,
            UnifH = lev.UnifH,
            UnifM = lev.UnifM,
            MortCajaH = lev.MortCajaH,
            MortCajaM = lev.MortCajaM,
            Raza = lev.Raza,
            AnoTablaGenetica = lev.AnoTablaGenetica,
            Linea = lev.Linea,
            TipoLinea = lev.TipoLinea,
            CodigoGuiaGenetica = lev.CodigoGuiaGenetica,
            LineaGeneticaId = lev.LineaGeneticaId,
            Tecnico = lev.Tecnico,
            Mixtas = lev.Mixtas,
            PesoMixto = lev.PesoMixto,
            AvesEncasetadas = lev.AvesEncasetadas,
            EdadInicial = lev.EdadInicial,
            LoteErp = lev.LoteErp,
            EstadoTraslado = lev.EstadoTraslado,
            PaisId = lev.PaisId,
            PaisNombre = lev.PaisNombre,
            EmpresaNombre = lev.EmpresaNombre,
            FechaInicioProduccion = now,
            HembrasInicialesProd = avesH,
            MachosInicialesProd = avesM,
            LotePosturaLevanteId = lev.LotePosturaLevanteId,
            AvesHInicial = avesH,
            AvesMInicial = avesM,
            AvesHActual = avesH,
            AvesMActual = avesM,
            EmpresaId = lev.CompanyId,
            UsuarioId = userId,
            Estado = "Produccion",
            Etapa = "Produccion",
            Edad = lev.Edad,
            EstadoCierre = "Abierta",
            CompanyId = lev.CompanyId,
            CreatedByUserId = userId,
            CreatedAt = now
        };
    }

    /// <summary>
    /// Obtiene los lotes postura levante de la empresa en sesión, filtrados por:
    /// - Company (empresa activa)
    /// - Granjas a las que el usuario tiene permiso (UserFarms + granjas por empresa)
    /// - Excluye eliminados (DeletedAt). Muestra abiertos y cerrados.
    /// </summary>
    public async Task<IEnumerable<LotePosturaLevanteDetailDto>> GetAllAsync(CancellationToken ct = default)
    {
        await ProcessarCierresPendientesAsync(ct);

        var companyId = await GetEffectiveCompanyIdAsync(ct);

        var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
        var allCountriesCount = await _ctx.Set<Pais>().CountAsync(ct);
        var isAdmin = assignedCountries.Count() >= allCountriesCount ||
                     await IsUserAdminOrAdministratorAsync(ct) ||
                     await IsSuperAdminAsync(ct);

        var q = _ctx.LotePosturaLevante
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null);

        if (!isAdmin)
        {
            var allowedFarmIds = await GetAllowedFarmIdsForCurrentUserAsync(ct);
            if (allowedFarmIds != null && allowedFarmIds.Count > 0)
                q = q.Where(l => allowedFarmIds.Contains(l.GranjaId));
            else
                q = q.Where(_ => false); // Sin granjas asignadas → lista vacía
        }

        q = q.OrderBy(l => l.LotePosturaLevanteId);
        return await ProjectToDetail(q).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LotePosturaLevanteDetailDto>> GetByLoteIdAsync(int loteId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var q = _ctx.LotePosturaLevante
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LoteId == loteId)
            .OrderBy(l => l.LotePosturaLevanteId);
        return await ProjectToDetail(q).ToListAsync(ct);
    }

    private static IQueryable<LotePosturaLevanteDetailDto> ProjectToDetail(
        IQueryable<LotePosturaLevante> q)
    {
        return q
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Select(l => new LotePosturaLevanteDetailDto(
                l.LotePosturaLevanteId ?? 0,
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
                l.UnifH,
                l.UnifM,
                l.MortCajaH,
                l.MortCajaM,
                l.Raza,
                l.AnoTablaGenetica,
                l.Linea,
                l.TipoLinea,
                l.CodigoGuiaGenetica,
                l.LineaGeneticaId,
                l.Tecnico,
                l.Mixtas,
                l.PesoMixto,
                l.AvesEncasetadas,
                l.EdadInicial,
                l.LoteErp,
                l.EstadoTraslado,
                l.PaisId,
                l.PaisNombre,
                l.EmpresaNombre,
                l.CompanyId,
                l.CreatedAt,
                l.LoteId,
                l.LotePadreId,
                l.LotePosturaLevantePadreId,
                l.AvesHInicial,
                l.AvesMInicial,
                l.AvesHActual,
                l.AvesMActual,
                l.EmpresaId,
                l.UsuarioId,
                l.Estado,
                l.Etapa,
                l.Edad,
                l.EstadoCierre,
                new FarmLiteDto(
                    l.Farm.Id,
                    l.Farm.Name,
                    l.Farm.RegionalId,
                    l.Farm.DepartamentoId,
                    l.Farm.MunicipioId
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
                    )
            ));
    }
}
