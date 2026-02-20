// Filter-data para Seguimiento Diario Lote Reproductora: Granja, Núcleo, Galpón, Lotes ave engorde y Lotes reproductora.
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Galpones;
using ZooSanMarino.Application.DTOs.LoteAveEngorde;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoDiarioLoteReproductoraFilterDataService : ISeguimientoDiarioLoteReproductoraFilterDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILoteAveEngordeService _loteAveEngordeService;
    private readonly ILoteReproductoraAveEngordeService _loteReproductoraService;

    public SeguimientoDiarioLoteReproductoraFilterDataService(
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        ILoteAveEngordeService loteAveEngordeService,
        ILoteReproductoraAveEngordeService loteReproductoraService)
    {
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _loteAveEngordeService = loteAveEngordeService;
        _loteReproductoraService = loteReproductoraService;
    }

    public async Task<SeguimientoDiarioLoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        var farms = (await _farmService.GetAllAsync(userId: null, companyId: null).ConfigureAwait(false)).ToList();
        var nucleos = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesDetail = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var lotesAveEngordeDetail = (await _loteAveEngordeService.GetAllAsync().ConfigureAwait(false)).ToList();
        var lotesReproductora = (await _loteReproductoraService.GetAllAsync(null).ConfigureAwait(false)).ToList();

        var galpones = galponesDetail
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId))
            .ToList();

        var lotes = lotesAveEngordeDetail
            .Select(l => new LoteFilterItemDto(
                l.LoteAveEngordeId,
                l.LoteNombre,
                l.GranjaId,
                l.NucleoId,
                l.GalponId))
            .ToList();

        var lotesReproductoraItems = lotesReproductora
            .Select(l => new LoteReproductoraSeguimientoFilterItemDto(
                l.Id,
                l.NombreLote,
                l.LoteAveEngordeId))
            .ToList();

        return new SeguimientoDiarioLoteReproductoraFilterDataDto(
            Farms: farms,
            Nucleos: nucleos,
            Galpones: galpones,
            Lotes: lotes,
            LotesReproductora: lotesReproductoraItems
        );
    }
}
