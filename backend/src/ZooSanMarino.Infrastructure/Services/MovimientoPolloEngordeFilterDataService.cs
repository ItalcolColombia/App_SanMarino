using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Galpones;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Catálogo único para filtros en cascada: granjas asignadas al usuario, núcleos/galpones acotados y lotes Ave Engorde.
/// </summary>
public sealed class MovimientoPolloEngordeFilterDataService : IMovimientoPolloEngordeFilterDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILoteAveEngordeService _loteAveEngordeService;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;

    public MovimientoPolloEngordeFilterDataService(
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        ILoteAveEngordeService loteAveEngordeService,
        ICurrentUser current,
        ICompanyResolver companyResolver)
    {
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _loteAveEngordeService = loteAveEngordeService;
        _current = current;
        _companyResolver = companyResolver;
    }

    private async Task<int> GetEffectiveCompanyIdAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim()).ConfigureAwait(false);
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    /// <inheritdoc />
    public async Task<MovimientoPolloEngordeFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        if (!_current.UserGuid.HasValue)
            throw new UnauthorizedAccessException("Sesión inválida. Inicie sesión de nuevo.");

        var companyId = await GetEffectiveCompanyIdAsync(ct).ConfigureAwait(false);
        var farms = (await _farmService
            .GetAssignedFarmsForCompanyAsync(_current.UserGuid.Value, companyId, _current.PaisId)
            .ConfigureAwait(false)).ToList();

        var farmIdList = farms.Select(f => f.Id).ToList();
        List<NucleoDto> nucleos;
        List<GalponDetailDto> galpones;
        if (farmIdList.Count == 0)
        {
            nucleos = [];
            galpones = [];
        }
        else
        {
            nucleos = (await _nucleoService.GetByFarmIdsForCompanyAsync(farmIdList, companyId, ct).ConfigureAwait(false)).ToList();
            galpones = (await _galponService.GetByFarmIdsForCompanyAsync(farmIdList, companyId, ct).ConfigureAwait(false)).ToList();
        }

        var farmIdsSet = farmIdList.ToHashSet();
        var lotesAe = (await _loteAveEngordeService.GetAllAsync().ConfigureAwait(false))
            .Where(l => farmIdsSet.Contains(l.GranjaId))
            .ToList();

        return new MovimientoPolloEngordeFilterDataDto(farms, nucleos, galpones, lotesAe);
    }
}
