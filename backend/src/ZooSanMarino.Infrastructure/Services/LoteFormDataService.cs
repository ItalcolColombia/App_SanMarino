// src/ZooSanMarino.Infrastructure/Services/LoteFormDataService.cs
using ZooSanMarino.Application.DTOs.Lotes;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Orquesta en una sola llamada los catálogos necesarios para el modal de crear/editar lote.
/// </summary>
public class LoteFormDataService : ILoteFormDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly IUserService _userService;
    private readonly ICompanyService _companyService;
    private readonly IGuiaGeneticaService _guiaGeneticaService;

    public LoteFormDataService(
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        IUserService userService,
        ICompanyService companyService,
        IGuiaGeneticaService guiaGeneticaService)
    {
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _userService = userService;
        _companyService = companyService;
        _guiaGeneticaService = guiaGeneticaService;
    }

    /// <summary>
    /// Carga secuencial para evitar uso concurrente del mismo DbContext (EF Core no permite varias operaciones simultáneas en una misma instancia).
    /// </summary>
    public async Task<LoteFormDataDto> GetFormDataAsync(CancellationToken ct = default)
    {
        var farms = (await _farmService.GetAllAsync(userId: null, companyId: null).ConfigureAwait(false)).ToList();
        var nucleos = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galpones = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var tecnicos = await _userService.GetUsersAsync().ConfigureAwait(false);
        var companies = (await _companyService.GetAllAsync().ConfigureAwait(false)).ToList();
        var razas = (await _guiaGeneticaService.ObtenerRazasDisponiblesAsync().ConfigureAwait(false)).ToList();

        return new LoteFormDataDto(
            Farms: farms,
            Nucleos: nucleos,
            Galpones: galpones,
            Tecnicos: tecnicos,
            Companies: companies,
            Razas: razas
        );
    }
}
