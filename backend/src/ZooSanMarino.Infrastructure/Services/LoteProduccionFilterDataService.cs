// src/ZooSanMarino.Infrastructure/Services/LoteProduccionFilterDataService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.Interfaces;
using GalponDtos = ZooSanMarino.Application.DTOs.Galpones;
using LoteDtos = ZooSanMarino.Application.DTOs.Lotes;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Orquesta en una sola llamada los datos para los filtros del módulo Seguimiento Diario de Producción.
/// Lotes provienen de lote_postura_produccion (abiertos o cerrados) de la empresa.
/// Las granjas se filtran por las asignadas al usuario (UserFarms) y pertenecientes a la empresa activa.
/// </summary>
public class LoteProduccionFilterDataService : ILoteProduccionFilterDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILotePosturaProduccionService _lotePosturaProduccionService;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;

    public LoteProduccionFilterDataService(
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        ILotePosturaProduccionService lotePosturaProduccionService,
        ICurrentUser current,
        ICompanyResolver companyResolver)
    {
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _lotePosturaProduccionService = lotePosturaProduccionService;
        _current = current;
        _companyResolver = companyResolver;
    }

    private async Task<int?> GetEffectiveCompanyIdAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId > 0 ? _current.CompanyId : null;
    }

    public async Task<SeguimientoProduccionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var farms = (await _farmService.GetAllAsync(userId: _current.UserGuid, companyId: companyId).ConfigureAwait(false)).ToList();
        var allowedFarmIds = farms.Select(f => f.Id).ToHashSet();

        var nucleosAll = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var nucleos = allowedFarmIds.Count > 0
            ? nucleosAll.Where(n => allowedFarmIds.Contains(n.GranjaId)).ToList()
            : new List<NucleoDto>();

        var galponesDetailAll = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesDetail = allowedFarmIds.Count > 0
            ? galponesDetailAll.Where(g => allowedFarmIds.Contains(g.GranjaId)).ToList()
            : new List<GalponDtos.GalponDetailDto>();

        var galpones = galponesDetail
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId))
            .ToList();

        var lppAll = (await _lotePosturaProduccionService.GetAllAsync(ct).ConfigureAwait(false)).ToList();
        var lppFiltered = allowedFarmIds.Count > 0
            ? lppAll.Where(l => allowedFarmIds.Contains(l.GranjaId)).ToList()
            : new List<LoteDtos.LotePosturaProduccionDetailDto>();

        var lotes = lppFiltered
            .Select(l => new LotePosturaProduccionFilterItemDto(
                l.LotePosturaProduccionId,
                l.LoteNombre,
                l.GranjaId,
                l.NucleoId,
                l.GalponId,
                l.AvesHInicial,
                l.AvesMInicial,
                l.AvesHActual,
                l.AvesMActual,
                l.EstadoCierre,
                l.FechaEncaset))
            .ToList();

        return new SeguimientoProduccionFilterDataDto(
            Farms: farms,
            Nucleos: nucleos,
            Galpones: galpones,
            Lotes: lotes
        );
    }
}
