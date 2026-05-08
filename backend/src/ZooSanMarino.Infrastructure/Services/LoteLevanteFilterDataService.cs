// src/ZooSanMarino.Infrastructure/Services/LoteLevanteFilterDataService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Orquesta en una sola llamada los datos para los filtros del módulo Seguimiento Diario de Levante.
/// Incluye lotes de lote_postura_levante (abiertos y cerrados), usando lote_id (FK a lotes) para compatibilidad con seguimiento_diario.
/// Las granjas se filtran por las asignadas al usuario (UserFarms).
/// </summary>
public class LoteLevanteFilterDataService : ILoteLevanteFilterDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILotePosturaLevanteService _lotePosturaLevanteService;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;

    public LoteLevanteFilterDataService(
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        ILotePosturaLevanteService lotePosturaLevanteService,
        ICurrentUser current,
        ICompanyResolver companyResolver)
    {
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _lotePosturaLevanteService = lotePosturaLevanteService;
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
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var farms = (await _farmService.GetAllAsync(userId: _current.UserGuid, companyId: companyId).ConfigureAwait(false)).ToList();
        var nucleos = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesDetail = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var levantesDetail = (await _lotePosturaLevanteService.GetAllAsync(ct).ConfigureAwait(false)).ToList();

        var galpones = galponesDetail
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId))
            .ToList();

        // Solo incluir levantes con LoteId (FK a lotes) para que seguimiento_diario.lote_id coincida
        var lotes = levantesDetail
            .Where(l => l.LoteId.HasValue)
            .Select(l => new LoteFilterItemDto(
                l.LoteId!.Value,
                l.LoteNombre,
                l.GranjaId,
                l.NucleoId,
                l.GalponId))
            .ToList();

        return new LoteReproductoraFilterDataDto(
            Farms: farms,
            Nucleos: nucleos,
            Galpones: galpones,
            Lotes: lotes,
            LotesBase: Array.Empty<LoteBaseFilterItemDto>()
        );
    }
}
