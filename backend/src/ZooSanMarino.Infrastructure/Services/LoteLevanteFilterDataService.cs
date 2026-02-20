// src/ZooSanMarino.Infrastructure/Services/LoteLevanteFilterDataService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Orquesta en una sola llamada los datos para los filtros del módulo Seguimiento Diario de Levante.
/// Solo incluye lotes en fase Levante (Fase == "Levante") para no mezclar con Producción.
/// </summary>
public class LoteLevanteFilterDataService : ILoteLevanteFilterDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILoteService _loteService;

    public LoteLevanteFilterDataService(
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
        var lotesDetail = (await _loteService.GetLotesLevanteAsync().ConfigureAwait(false)).ToList();

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
