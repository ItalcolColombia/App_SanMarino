// src/ZooSanMarino.Infrastructure/Services/LoteFormDataService.cs
using ZooSanMarino.Application.DTOs.Lotes;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Orquesta en una sola llamada los catálogos necesarios para el modal de crear/editar lote.
/// Filtra granjas por las asignadas al usuario (UserFarms) y por la empresa activa.
/// </summary>
public class LoteFormDataService : ILoteFormDataService
{
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly IUserService _userService;
    private readonly ICompanyService _companyService;
    private readonly IGuiaGeneticaService _guiaGeneticaService;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;

    public LoteFormDataService(
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        IUserService userService,
        ICompanyService companyService,
        IGuiaGeneticaService guiaGeneticaService,
        ICurrentUser current,
        ICompanyResolver companyResolver)
    {
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _userService = userService;
        _companyService = companyService;
        _guiaGeneticaService = guiaGeneticaService;
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

    /// <summary>
    /// Carga secuencial para evitar uso concurrente del mismo DbContext (EF Core no permite varias operaciones simultáneas en una misma instancia).
    /// Granjas: solo las asignadas al usuario (UserFarms) y de la empresa activa.
    /// Núcleos y galpones: ya vienen filtrados por el servicio según permisos del usuario.
    /// </summary>
    public async Task<LoteFormDataDto> GetFormDataAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var farms = (await _farmService.GetAllAsync(userId: _current.UserGuid, companyId: companyId).ConfigureAwait(false)).ToList();
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
