// src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeFilterDataService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Galpones;
using ZooSanMarino.Application.DTOs.LoteAveEngorde;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Filter-data para Seguimiento Diario Aves de Engorde: Granja, Núcleo, Galpón y Lotes = lotes de lote_ave_engorde.
/// El front recibe la misma estructura (LoteReproductoraFilterDataDto) con LoteId = lote_ave_engorde_id.
/// </summary>
public class SeguimientoAvesEngordeFilterDataService : ISeguimientoAvesEngordeFilterDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILoteAveEngordeService _loteAveEngordeService;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;

    public SeguimientoAvesEngordeFilterDataService(
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

    public async Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        if (!_current.UserGuid.HasValue)
            throw new UnauthorizedAccessException("Sesión inválida. Inicie sesión de nuevo.");
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        // Granjas: solo IDs en user_farms para el usuario, cruzados con farms de la empresa activa (sin GetAllAsync ni lógica por rol).
        var assignedFarmIds = await _farmService.GetAssignedFarmIdsForUserAsync(_current.UserGuid.Value, ct).ConfigureAwait(false);
        var farms = (await _farmService
            .GetFarmDtosByIdsInCompanyAsync(assignedFarmIds, companyId, ct)
            .ConfigureAwait(false)).ToList();
        var allowedFarmIds = farms.Select(f => f.Id).ToHashSet();
        var nucleos = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).Where(n => allowedFarmIds.Contains(n.GranjaId)).ToList();
        var galponesDetail = (await _galponService.GetAllAsync().ConfigureAwait(false)).Where(g => allowedFarmIds.Contains(g.GranjaId)).ToList();
        var lotesDetail = (await _loteAveEngordeService.GetAllAsync().ConfigureAwait(false))
            .Where(l => allowedFarmIds.Contains(l.GranjaId))
            .ToList();

        var galpones = galponesDetail
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId))
            .ToList();

        // Lotes = lotes de lote_ave_engorde, mapeados a LoteFilterItemDto (LoteId = lote_ave_engorde_id)
        var lotes = lotesDetail
            .Select(l => new LoteFilterItemDto(
                l.LoteAveEngordeId,
                l.LoteNombre,
                l.GranjaId,
                l.NucleoId,
                l.GalponId,
                l.LoteErp))
            .ToList();

        return new LoteReproductoraFilterDataDto(
            Farms: farms,
            Nucleos: nucleos,
            Galpones: galpones,
            Lotes: lotes
        );
    }
}
