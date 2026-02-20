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

    public LoteReproductoraAveEngordeFilterDataService(
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

    public async Task<LoteReproductoraAveEngordeFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        // Cargar en secuencia para no usar el mismo DbContext en paralelo (evita 500).
        var farms = (await _farmService.GetAllAsync(userId: null, companyId: null).ConfigureAwait(false)).ToList();
        var nucleos = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesDetail = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
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
