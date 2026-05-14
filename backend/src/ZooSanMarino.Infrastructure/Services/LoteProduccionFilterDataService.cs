// src/ZooSanMarino.Infrastructure/Services/LoteProduccionFilterDataService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Lotes;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.Interfaces;
using GalponDtos = ZooSanMarino.Application.DTOs.Galpones;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Orquesta en una sola llamada los datos para los filtros del Reporte Técnico de Producción.
///
/// Cascada de filtros:
///   Granja → Núcleo → Galpón → Lote Base (lote_postura_base) → Lote Producción (lote_postura_produccion)
///
/// Resolución de LotePosturaBaseId:
///   lote_postura_produccion.LotePosturaLevanteId → lote_postura_levante.LoteId → lote.lote_postura_base_id → lote_postura_base
/// </summary>
public class LoteProduccionFilterDataService : ILoteProduccionFilterDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILotePosturaProduccionService _lotePosturaProduccionService;
    private readonly ILotePosturaLevanteService _lotePosturaLevanteService;
    private readonly ILoteService _loteService;
    private readonly ILotePosturaBaseService _lotePosturaBaseService;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;

    public LoteProduccionFilterDataService(
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        ILotePosturaProduccionService lotePosturaProduccionService,
        ILotePosturaLevanteService lotePosturaLevanteService,
        ILoteService loteService,
        ILotePosturaBaseService lotePosturaBaseService,
        ICurrentUser current,
        ICompanyResolver companyResolver)
    {
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _lotePosturaProduccionService = lotePosturaProduccionService;
        _lotePosturaLevanteService = lotePosturaLevanteService;
        _loteService = loteService;
        _lotePosturaBaseService = lotePosturaBaseService;
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

    public async Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);

        var farms = (await _farmService.GetAllAsync(userId: _current.UserGuid, companyId: companyId).ConfigureAwait(false)).ToList();
        var farmIds = farms.Select(f => f.Id).ToHashSet();

        var nucleos = (await _nucleoService.GetAllAsync().ConfigureAwait(false))
            .Where(n => farmIds.Contains(n.GranjaId))
            .ToList();

        var galponesDetail = (await _galponService.GetAllAsync().ConfigureAwait(false))
            .Where(g => farmIds.Contains(g.GranjaId))
            .ToList();

        var galpones = galponesDetail
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId))
            .ToList();

        var lppAll = (await _lotePosturaProduccionService.GetAllAsync(ct).ConfigureAwait(false))
            .Where(l => farmIds.Contains(l.GranjaId))
            .ToList();

        // Mapa lplId → LoteId: lote_postura_levante.LotePosturaLevanteId → lote_postura_levante.LoteId
        Dictionary<int, int?> lplIdToLoteId;
        try
        {
            var todosLpl = (await _lotePosturaLevanteService.GetAllAsync(ct).ConfigureAwait(false)).ToList();
            lplIdToLoteId = todosLpl
                .Where(l => l.LotePosturaLevanteId > 0)
                .ToDictionary(l => l.LotePosturaLevanteId, l => l.LoteId);
        }
        catch
        {
            lplIdToLoteId = new Dictionary<int, int?>();
        }

        // Mapa loteId → lotePosturaBaseId: lote.LoteId → lote.LotePosturaBaseId
        Dictionary<int, int?> loteIdToBaseId;
        try
        {
            var todosLotes = (await _loteService.GetAllAsync().ConfigureAwait(false)).ToList();
            loteIdToBaseId = todosLotes
                .Where(l => l.LoteId > 0)
                .ToDictionary(l => l.LoteId, l => l.LotePosturaBaseId);
        }
        catch
        {
            loteIdToBaseId = new Dictionary<int, int?>();
        }

        // LoteFilterItemDto con LotePosturaBaseId resuelto vía cadena LPP → LPL → Lote → LoteBase
        var lotes = lppAll
            .Select(l =>
            {
                int? baseId = null;
                if (l.LotePosturaLevanteId.HasValue
                    && lplIdToLoteId.TryGetValue(l.LotePosturaLevanteId.Value, out var loteId)
                    && loteId.HasValue
                    && loteIdToBaseId.TryGetValue(loteId.Value, out var bid))
                {
                    baseId = bid;
                }

                return new LoteFilterItemDto(
                    LoteId: l.LotePosturaProduccionId,
                    LoteNombre: l.LoteNombre,
                    GranjaId: l.GranjaId,
                    NucleoId: l.NucleoId,
                    GalponId: l.GalponId,
                    LotePosturaBaseId: baseId);
            })
            .ToList();

        // LotesBase: únicos referenciados por las producciones accesibles
        var baseIdsAccesibles = lotes
            .Where(l => l.LotePosturaBaseId.HasValue)
            .Select(l => l.LotePosturaBaseId!.Value)
            .ToHashSet();

        List<LoteBaseFilterItemDto> lotesBase;
        try
        {
            var todosLotesBase = (await _lotePosturaBaseService.GetAllAsync().ConfigureAwait(false)).ToList();
            lotesBase = todosLotesBase
                .Where(lb => baseIdsAccesibles.Contains(lb.LotePosturaBaseId))
                .Select(lb => new LoteBaseFilterItemDto(lb.LotePosturaBaseId, lb.LoteNombre, lb.CodigoErp))
                .ToList();
        }
        catch
        {
            lotesBase = new List<LoteBaseFilterItemDto>();
        }

        return new LoteReproductoraFilterDataDto(
            Farms: farms,
            Nucleos: nucleos,
            Galpones: galpones,
            Lotes: lotes,
            LotesBase: lotesBase
        );
    }
}
