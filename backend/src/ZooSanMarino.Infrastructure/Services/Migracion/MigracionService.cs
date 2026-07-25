// src/ZooSanMarino.Infrastructure/Services/Migracion/MigracionService.cs
// ANCLA del módulo de Migraciones Masivas (partial class).
// Contiene: campos, ctor, helpers de scoping y la declaración de la interfaz.
// Las operaciones (validar/importar/plantilla/elegibles) viven en Funciones/*.cs.
using System.Text.Json;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService : IMigracionService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly IMigracionRepository _repo;
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILoteAveEngordeService _loteAveEngordeService;
    private readonly ISeguimientoAvesEngordeService _seguimientoEngordeService;
    private readonly ISeguimientoDiarioLoteReproductoraService _seguimientoReproductoraService;

    static MigracionService()
    {
        // EPPlus 8+ requiere fijar la licencia (uso no comercial), igual que ExcelImportService.
        OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("ZooSanMarino");
    }

    public MigracionService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        IMigracionRepository repo,
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        ILoteAveEngordeService loteAveEngordeService,
        ISeguimientoAvesEngordeService seguimientoEngordeService,
        ISeguimientoDiarioLoteReproductoraService seguimientoReproductoraService)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _repo = repo;
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _loteAveEngordeService = loteAveEngordeService;
        _seguimientoEngordeService = seguimientoEngordeService;
        _seguimientoReproductoraService = seguimientoReproductoraService;
    }

    /// <summary>
    /// Empresa efectiva: se resuelve del header de empresa activa (validado por el middleware,
    /// con bypass de superadmin) o, en su defecto, de la compañía del usuario. Mismo patrón que
    /// el resto de servicios del backend — la selección de empresa del admin viaja por el header,
    /// no por el body, para no saltarse la validación de pertenencia.
    /// </summary>
    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var cid = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName);
            if (cid.HasValue) return cid.Value;
        }
        return _current.CompanyId;
    }

    public IReadOnlyList<TipoMigracionInfoDto> GetTipos() => TipoMigracionCatalogo.Todos;

    /// <summary>Historial paginado de auditoría de la empresa activa (opcionalmente por tipo).</summary>
    public async Task<MigracionHistorialPagedDto> GetHistorialAsync(string? tipo, int page, int pageSize, bool incluirValidaciones, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var (items, total) = await _repo.GetHistorialAsync(companyId, tipo, page, pageSize, incluirValidaciones, ct);

        var dtos = items.Select(x => new MigracionHistorialDto(
            x.Id, x.Tipo, x.NombreArchivo,
            x.FilasTotales, x.FilasProcesadas, x.FilasError,
            x.Estado, x.FechaProceso, x.CreatedByUserId,
            x.FilasOmitidas, x.DuracionMs, x.FueDryRun,
            TieneErrores: x.ErroresJson is not null)).ToList();

        var pageClamped = page < 1 ? 1 : page;
        var pageSizeClamped = pageSize < 1 ? 20 : Math.Min(pageSize, 100);
        return new MigracionHistorialPagedDto(dtos, total, pageClamped, pageSizeClamped);
    }

    /// <summary>Errores/advertencias de una corrida puntual del historial. Null si no existe o no es de la empresa activa.</summary>
    public async Task<IReadOnlyList<MigracionErrorDto>?> GetErroresAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var registro = await _repo.GetPorIdAsync(id, companyId, ct);
        if (registro is null) return null;
        if (string.IsNullOrWhiteSpace(registro.ErroresJson)) return Array.Empty<MigracionErrorDto>();
        return JsonSerializer.Deserialize<List<MigracionErrorDto>>(registro.ErroresJson) ?? new List<MigracionErrorDto>();
    }
}
