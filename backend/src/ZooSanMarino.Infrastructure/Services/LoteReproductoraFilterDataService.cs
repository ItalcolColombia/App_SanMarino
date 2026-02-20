// src/ZooSanMarino.Infrastructure/Services/LoteReproductoraFilterDataService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Orquesta en una sola llamada los datos para los filtros del módulo Lote Reproductora.
/// Carga secuencial para evitar uso concurrente del mismo DbContext.
/// </summary>
public class LoteReproductoraFilterDataService : ILoteReproductoraFilterDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILoteService _loteService;

    public LoteReproductoraFilterDataService(
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        ILoteService loteService)
    {
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _loteService = loteService;
    }

    public async Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        var farms = (await _farmService.GetAllAsync(userId: null, companyId: null).ConfigureAwait(false)).ToList();
        var nucleos = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesDetail = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var lotesDetail = (await _loteService.GetAllAsync().ConfigureAwait(false)).ToList();

        var galpones = galponesDetail
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId))
            .ToList();

        var lotes = lotesDetail
            .Select(l => new LoteFilterItemDto(
                l.LoteId,
                l.LoteNombre,
                l.GranjaId,
                l.NucleoId,
                l.GalponId))
            .ToList();

        return new LoteReproductoraFilterDataDto(
            Farms: farms,
            Nucleos: nucleos,
            Galpones: galpones,
            Lotes: lotes
        );
    }
}
