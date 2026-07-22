// src/ZooSanMarino.Infrastructure/Services/ReporteDiarioCostosEngordeService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.ReporteDiarioCostosEngorde;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio delgado del Reporte Diario Costos engorde: valida alcance (empresa efectiva +
/// granja asignada al usuario), delega la agregación diaria a fn_reporte_diario_costos_engorde
/// y consolida totales con ReporteDiarioCostosEngordeCalculos (puro).
/// </summary>
public class ReporteDiarioCostosEngordeService : IReporteDiarioCostosEngordeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly IFarmService _farmService;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public ReporteDiarioCostosEngordeService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        IFarmService farmService)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _farmService = farmService;
    }

    public async Task<ReporteDiarioCostosReporteDto> GenerarAsync(ReporteDiarioCostosRequest request, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        // Granja: debe estar asignada al usuario (mismo criterio que LoteAveEngordeService).
        if (!_current.UserGuid.HasValue)
            throw new UnauthorizedAccessException("Sesión inválida. Inicie sesión de nuevo.");
        var farms = await _farmService.GetAllAsync(_current.UserGuid, companyId);
        var granja = farms.FirstOrDefault(f => f.Id == request.GranjaId)
            ?? throw new InvalidOperationException("Granja no existe, no pertenece a la compañía o no está asignada a su usuario.");

        // Lote base opcional: validar empresa y traer nombre.
        string? loteBaseNombre = null;
        if (request.LoteBaseEngordeId.HasValue)
        {
            loteBaseNombre = await _ctx.LoteBaseEngorde
                .AsNoTracking()
                .Where(b => b.Id == request.LoteBaseEngordeId.Value && b.CompanyId == companyId && b.DeletedAt == null)
                .Select(b => b.Nombre)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("El lote base indicado no existe o no pertenece a la compañía.");
        }

        // Cabecera: lotes del alcance + galpones (columnas dinámicas). Mismo predicado que la fn.
        var lotes = await _ctx.LoteAveEngorde
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId
                     && l.GranjaId == request.GranjaId
                     && l.DeletedAt == null
                     && (request.LoteBaseEngordeId == null || l.LoteBaseEngordeId == request.LoteBaseEngordeId))
            .Select(l => new
            {
                Id = l.LoteAveEngordeId ?? 0,
                l.LoteNombre,
                GalponId = (l.GalponId ?? "").Trim(),
                GalponNombre = l.Galpon != null && l.Galpon.GalponNombre != null && l.Galpon.GalponNombre.Trim() != ""
                    ? l.Galpon.GalponNombre.Trim()
                    : ((l.GalponId ?? "").Trim() != "" ? (l.GalponId ?? "").Trim() : "Sin galpón"),
                l.FechaEncaset,
                l.EstadoOperativoLote
            })
            .OrderBy(l => l.FechaEncaset)
            .ThenBy(l => l.LoteNombre)
            .ToListAsync(ct);

        var lotesDto = lotes
            .Select(l => new ReporteDiarioCostosLoteDto(l.Id, l.LoteNombre, l.GalponId, l.GalponNombre, l.FechaEncaset, l.EstadoOperativoLote))
            .ToList();

        var galponesHeader = lotesDto
            .GroupBy(l => l.GalponId)
            .Select(g => new ReporteDiarioCostosGalponHeaderDto(
                g.Key,
                g.First().GalponNombre,
                g.Select(x => x.LoteNombre).Distinct().ToList()))
            .OrderBy(g => g.GalponNombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.GalponId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Agregación diaria en la BD (reusa fn_seguimiento_diario_engorde por lote).
        var rows = await _ctx.Database
            .SqlQueryRaw<ReporteDiarioCostosRow>(
                "SELECT * FROM fn_reporte_diario_costos_engorde({0}::int, {1}::int, {2}::int, {3}::date, {4}::date)",
                companyId,
                request.GranjaId,
                (object?)request.LoteBaseEngordeId ?? DBNull.Value,
                (object?)request.FechaInicio?.Date ?? DBNull.Value,
                (object?)request.FechaFin?.Date ?? DBNull.Value)
            .ToListAsync(ct);

        var filas = rows
            .OrderBy(r => r.Fecha)
            .Select(r => new ReporteDiarioCostosFilaDto(
                r.Fecha,
                r.ConsumoTotalKg,
                r.MortSelTotal,
                r.AvesVivasTotal,
                ParseJson<ReporteDiarioCostosAlimentoDto>(r.Alimentos),
                ParseJson<ReporteDiarioCostosGalponDiaDto>(r.Galpones)))
            .ToList();

        var totales = ReporteDiarioCostosEngordeCalculos.ConstruirTotales(filas);
        var (avesActuales, avesActualesTotal) = ReporteDiarioCostosEngordeCalculos.AvesVivasActuales(filas);

        return new ReporteDiarioCostosReporteDto(
            FiltrosAplicados: request,
            FechaInicioEfectiva: request.FechaInicio?.Date ?? (filas.Count > 0 ? filas[0].Fecha : null),
            FechaFinEfectiva: request.FechaFin?.Date ?? (filas.Count > 0 ? filas[^1].Fecha : null),
            GranjaId: granja.Id,
            GranjaNombre: granja.Name,
            LoteBaseEngordeId: request.LoteBaseEngordeId,
            LoteBaseNombre: loteBaseNombre,
            Lotes: lotesDto,
            Galpones: galponesHeader,
            AvesVivasActuales: avesActuales,
            AvesVivasActualesTotal: avesActualesTotal,
            Filas: filas,
            Totales: totales);
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    /// <summary>Deserializa el JSON (text) que arma la fn; inválido/vacío → lista vacía (no rompe el reporte).</summary>
    private static IReadOnlyList<T> ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<T>();
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOpts) ?? (IReadOnlyList<T>)Array.Empty<T>();
        }
        catch (JsonException)
        {
            return Array.Empty<T>();
        }
    }
}
