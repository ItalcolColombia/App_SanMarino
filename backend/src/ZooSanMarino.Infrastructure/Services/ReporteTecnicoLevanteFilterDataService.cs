using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Lotes;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Orquesta en una sola llamada los datos para los filtros del módulo Reporte Técnico Levante.
/// Lotes provienen de lote_postura_levante. LoteId en cada item = lotePosturaLevanteId (para reportes y seguimiento_diario).
/// </summary>
public class ReporteTecnicoLevanteFilterDataService : IReporteTecnicoLevanteFilterDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILotePosturaLevanteService _lotePosturaLevanteService;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;

    public ReporteTecnicoLevanteFilterDataService(
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

        // Para Reporte Técnico: LoteId = lotePosturaLevanteId (el frontend usa esto para llamar al reporte; seguimiento_diario usa lote_postura_levante_id)
        var lotes = levantesDetail
            .Where(l => l.LotePosturaLevanteId > 0)
            .Select(l => new LoteFilterItemDto(
                l.LotePosturaLevanteId, // LoteId en respuesta = lotePosturaLevanteId
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
