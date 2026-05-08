using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Lotes;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Orquesta en una sola llamada los datos para los filtros del módulo Reporte Técnico Levante.
///
/// Cascada de filtros:
///   Granja → Núcleo → Galpón → Lote Base (lote_postura_base) → Sublotes (lote_postura_levante)
///
/// Resolución de LotePosturaBase:
///   lote_postura_levante.LoteId → lote.lote_postura_base_id → lote_postura_base
///
/// lote_postura_base NO tiene GranjaId; la ubicación se resuelve a través de
/// lote_postura_levante (que sí tiene GranjaId/GalponId/NucleoId).
/// El frontend filtra client-side usando el set de lotePosturaBaseId presentes
/// en los levantes del galpón seleccionado.
/// </summary>
public class ReporteTecnicoLevanteFilterDataService : IReporteTecnicoLevanteFilterDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILotePosturaLevanteService _lotePosturaLevanteService;
    private readonly ILoteService _loteService;
    private readonly ILotePosturaBaseService _lotePosturaBaseService;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;

    public ReporteTecnicoLevanteFilterDataService(
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        ILotePosturaLevanteService lotePosturaLevanteService,
        ILoteService loteService,
        ILotePosturaBaseService lotePosturaBaseService,
        ICurrentUser current,
        ICompanyResolver companyResolver)
    {
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _lotePosturaLevanteService = lotePosturaLevanteService;
        _loteService = loteService;
        _lotePosturaBaseService = lotePosturaBaseService;
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

        // Granjas accesibles para este usuario/empresa — fuente de verdad para el filtrado
        var farms = (await _farmService.GetAllAsync(userId: _current.UserGuid, companyId: companyId).ConfigureAwait(false)).ToList();
        var farmIds = farms.Select(f => f.Id).ToHashSet();

        // Filtrar nucleos, galpones y levantes a las granjas accesibles
        var nucleos = (await _nucleoService.GetAllAsync().ConfigureAwait(false))
            .Where(n => farmIds.Contains(n.GranjaId))
            .ToList();

        var galponesDetail = (await _galponService.GetAllAsync().ConfigureAwait(false))
            .Where(g => farmIds.Contains(g.GranjaId))
            .ToList();

        var levantesDetail = (await _lotePosturaLevanteService.GetAllAsync(ct).ConfigureAwait(false))
            .Where(l => farmIds.Contains(l.GranjaId))
            .ToList();

        // Mapa loteId → lotePosturaBaseId (join: lote_postura_levante → lote → lote_postura_base)
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

        // LoteFilterItemDto: todos los lotes de levante con su LotePosturaBaseId resuelto
        var galpones = galponesDetail
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId))
            .ToList();

        var lotes = levantesDetail
            .Where(l => l.LotePosturaLevanteId > 0)
            .Select(l =>
            {
                int? baseId = null;
                if (l.LoteId.HasValue && loteIdToBaseId.TryGetValue(l.LoteId.Value, out var bid))
                    baseId = bid;

                return new LoteFilterItemDto(
                    l.LotePosturaLevanteId,
                    l.LoteNombre,
                    l.GranjaId,
                    l.NucleoId,
                    l.GalponId,
                    LotePosturaLevantePadreId: l.LotePosturaLevantePadreId,
                    LotePosturaBaseId: baseId);
            })
            .ToList();

        // LoteBaseFilterItemDto: registros únicos de lote_postura_base referenciados
        // por los levantes accesibles (companyId ya está implícito en _lotePosturaBaseService)
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
