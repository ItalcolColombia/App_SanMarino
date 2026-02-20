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

    public SeguimientoAvesEngordeFilterDataService(
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        ILoteAveEngordeService loteAveEngordeService)
    {
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _loteAveEngordeService = loteAveEngordeService;
    }

    public async Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        // Secuencial para evitar uso concurrente del mismo DbContext (no thread-safe).
        var farms = (await _farmService.GetAllAsync(userId: null, companyId: null).ConfigureAwait(false)).ToList();
        var nucleos = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesDetail = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var lotesDetail = (await _loteAveEngordeService.GetAllAsync().ConfigureAwait(false)).ToList();

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
