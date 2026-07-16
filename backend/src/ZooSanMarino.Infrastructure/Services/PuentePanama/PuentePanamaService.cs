// ANCLA del puente ZooPanamaPollo → pollo engorde (partial class).
// Campos, ctor, resolución de empresa efectiva, carga de guía genética y auditoría.
// La orquestación (recorrido + upserts) vive en Funciones/PuentePanamaService.Sincronizar.cs.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.PuentePanama;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class PuentePanamaService : IPuentePanamaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly IConfiguration _config;
    private readonly IPuentePanamaApiClient _api;
    private readonly IMigracionRepository _repo;
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILoteAveEngordeService _loteAveEngordeService;
    private readonly ISeguimientoAvesEngordeService _seguimientoEngordeService;
    private readonly ILoteReproductoraAveEngordeService _loteReproService;
    private readonly ISeguimientoDiarioLoteReproductoraService _seguimientoReproService;
    private readonly IGuiaGeneticaEcuadorService _guiaGeneticaService;

    public PuentePanamaService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        IConfiguration config,
        IPuentePanamaApiClient api,
        IMigracionRepository repo,
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        ILoteAveEngordeService loteAveEngordeService,
        ISeguimientoAvesEngordeService seguimientoEngordeService,
        ILoteReproductoraAveEngordeService loteReproService,
        ISeguimientoDiarioLoteReproductoraService seguimientoReproService,
        IGuiaGeneticaEcuadorService guiaGeneticaService)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _config = config;
        _api = api;
        _repo = repo;
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _loteAveEngordeService = loteAveEngordeService;
        _seguimientoEngordeService = seguimientoEngordeService;
        _loteReproService = loteReproService;
        _seguimientoReproService = seguimientoReproService;
        _guiaGeneticaService = guiaGeneticaService;
    }

    /// <summary>Empresa efectiva: header de empresa activa o, en su defecto, la del usuario (mismo patrón que MigracionService).</summary>
    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var cid = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName);
            if (cid.HasValue) return cid.Value;
        }
        return _current.CompanyId;
    }

    /// <summary>
    /// Combinaciones válidas "raza|anio" de la guía genética de la empresa (clásica + Ecuador activa).
    /// Mismo criterio que LoteAveEngordeService.ExisteGuiaGeneticaRazaAnioAsync.
    /// </summary>
    private async Task<HashSet<string>> CargarRazaAnioGuiaAsync(int companyId, CancellationToken ct)
    {
        var clasica = await _ctx.ProduccionAvicolaRaw.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.DeletedAt == null && p.Raza != null && p.AnioGuia != null)
            .Select(p => new { p.Raza, p.AnioGuia })
            .Distinct().ToListAsync(ct);

        var ecuador = await _ctx.GuiaGeneticaEcuadorHeader.AsNoTracking()
            .Where(h => h.CompanyId == companyId && h.DeletedAt == null && h.Estado == "active")
            .Select(h => new { h.Raza, h.AnioGuia })
            .Distinct().ToListAsync(ct);

        var set = new HashSet<string>();
        foreach (var x in clasica)
            if (!string.IsNullOrWhiteSpace(x.Raza) && !string.IsNullOrWhiteSpace(x.AnioGuia))
                set.Add(Clave(x.Raza!, x.AnioGuia!.Trim()));
        foreach (var x in ecuador)
            if (!string.IsNullOrWhiteSpace(x.Raza))
                set.Add(Clave(x.Raza, x.AnioGuia.ToString()));
        return set;
    }

    private static string Clave(string raza, string anio) => $"{raza.Trim().ToLowerInvariant()}|{anio}";

    /// <summary>Tipo con el que el puente audita sus corridas en migracion_masiva.</summary>
    internal const string TipoAuditoria = "SincronizacionPanamaEngorde";

    /// <summary>
    /// Persiste la auditoría de la corrida (reutiliza la tabla migracion_masiva). Además del contrato
    /// clásico (contadores + errores_json), guarda en detalle_json el ResultadoSincronizacionDto podado
    /// (todos los contadores + lotes con novedad + mensajes) para el historial enriquecido, y devuelve
    /// el id de auditoría en <c>r.AuditoriaId</c> ("corrida #id" en el front).
    /// </summary>
    private async Task RegistrarAuditoriaAsync(int companyId, ResultadoSincronizacionDto r, CancellationToken ct)
    {
        var registro = await _repo.RegistrarAsync(new MigracionMasiva
        {
            CompanyId = companyId,
            Tipo = TipoAuditoria,
            NombreArchivo = $"ZooPanamaPollo · año {(r.Anio?.ToString() ?? "todos")}",
            FilasTotales = r.LotesEnAnio,
            FilasProcesadas = r.LotesNuevos,
            FilasError = r.LotesConError,
            FilasOmitidas = r.LotesOmitidos,
            DuracionMs = r.DuracionMs,
            FueDryRun = r.DryRun,
            Estado = r.Estado,
            ErroresJson = r.Mensajes.Count > 0 ? JsonSerializer.Serialize(r.Mensajes) : null,
            DetalleJson = JsonSerializer.Serialize(PuentePanamaCalculos.PodarDetalleParaHistorial(r)),
            FechaProceso = DateTime.UtcNow,
            CreatedByUserId = _current.UserId
        }, ct);
        r.AuditoriaId = registro.Id;
    }
}
