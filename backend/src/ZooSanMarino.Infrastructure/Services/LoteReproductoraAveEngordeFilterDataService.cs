// src/ZooSanMarino.Infrastructure/Services/LoteReproductoraAveEngordeFilterDataService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Galpones;
using ZooSanMarino.Application.DTOs.LoteAveEngorde;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

public class LoteReproductoraAveEngordeFilterDataService : ILoteReproductoraAveEngordeFilterDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILoteAveEngordeService _loteAveEngordeService;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;

    public LoteReproductoraAveEngordeFilterDataService(
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
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    public async Task<LoteReproductoraAveEngordeFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        if (!_current.UserGuid.HasValue)
            throw new UnauthorizedAccessException("Sesión inválida. Inicie sesión de nuevo.");
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var farms = (await _farmService.GetAllAsync(_current.UserGuid, companyId).ConfigureAwait(false)).ToList();
        var allowedFarmIds = farms.Select(f => f.Id).ToHashSet();
        var nucleos = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).Where(n => allowedFarmIds.Contains(n.GranjaId)).ToList();
        var galponesDetail = (await _galponService.GetAllAsync().ConfigureAwait(false)).Where(g => allowedFarmIds.Contains(g.GranjaId)).ToList();
        var lotesDetail = (await _loteAveEngordeService.GetAllAsync().ConfigureAwait(false)).ToList();

        var galpones = galponesDetail
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId))
            .ToList();

        var lotesAveEngorde = lotesDetail
            .Select(l => new LoteAveEngordeFilterItemDto(
                l.LoteAveEngordeId,
                l.LoteNombre,
                l.GranjaId,
                l.NucleoId,
                l.GalponId))
            .ToList();

        return new LoteReproductoraAveEngordeFilterDataDto(
            Farms: farms,
            Nucleos: nucleos,
            Galpones: galpones,
            LotesAveEngorde: lotesAveEngorde
        );
    }
}
