// src/ZooSanMarino.Infrastructure/Services/ReporteDiarioCostosPostura/ReporteDiarioCostosPosturaService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.ReporteDiarioCostosPostura;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio DELGADO del Reporte Diario Área de Costos de POSTURA: resuelve alcance
/// (empresa efectiva + granjas asignadas + alcance granular por ubicación), delega la
/// agregación diaria a <c>fn_reporte_diario_costos_postura</c> y consolida con
/// <see cref="ReporteDiarioCostosPosturaCalculos"/> (puro).
///
/// La clasificación de huevo NO se hace en SQL a propósito: su único dueño es
/// <c>ReporteDiarioCostosPosturaCalculos.ClasificarHuevo</c>, que está testeado.
/// </summary>
public class ReporteDiarioCostosPosturaService : IReporteDiarioCostosPosturaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly IFarmService _farmService;
    private readonly ILocationScopeResolver _scopeResolver;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public ReporteDiarioCostosPosturaService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        IFarmService farmService,
        ILocationScopeResolver scopeResolver)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _farmService = farmService;
        _scopeResolver = scopeResolver;
    }

    public async Task<ReporteDiarioCostosPosturaReporteDto> GenerarAsync(
        ReporteDiarioCostosPosturaRequest request,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        if (!_current.UserGuid.HasValue)
            throw new UnauthorizedAccessException("Sesión inválida. Inicie sesión de nuevo.");

        // ── Alcance de granjas: SIEMPRE las asignadas al usuario (fail-closed) ──
        var farms = (await _farmService.GetAllAsync(_current.UserGuid, companyId)).ToList();

        var granjaIds = farms.Select(f => f.Id).ToList();
        if (request.GranjaId.HasValue)
        {
            var granja = farms.FirstOrDefault(f => f.Id == request.GranjaId.Value)
                ?? throw new InvalidOperationException(
                    "La granja no existe, no pertenece a la compañía o no está asignada a su usuario.");
            granjaIds = new List<int> { granja.Id };
        }

        // Sin granjas visibles ⇒ reporte vacío. Jamás "todas" (el NULL de la fn es para scripts).
        if (granjaIds.Count == 0)
            return Vacio(request);

        var fase = ReporteDiarioCostosPosturaCalculos.NormalizarFase(request.Fase);
        var regional = string.IsNullOrWhiteSpace(request.Regional) ? null : request.Regional.Trim();

        var rows = await _ctx.Database
            .SqlQueryRaw<ReporteDiarioCostosPosturaRow>(
                "SELECT * FROM fn_reporte_diario_costos_postura({0}::int, {1}::int[], {2}::text, {3}::int, {4}::text, {5}::date, {6}::date)",
                companyId,
                granjaIds.ToArray(),
                (object?)regional ?? DBNull.Value,
                (object?)request.LotePosturaBaseId ?? DBNull.Value,
                (object?)fase ?? DBNull.Value,
                (object?)request.FechaDesde?.Date ?? DBNull.Value,
                (object?)request.FechaHasta?.Date ?? DBNull.Value)
            .ToListAsync(ct);

        // ── Alcance granular por ubicación: en granjas restringidas se recortan los lotes
        //    no visibles del usuario. Granja no restringida ⇒ el reporte sale idéntico.
        var restringidos = await _scopeResolver.GetRestrictedScopesAsync(granjaIds);
        if (restringidos.Count > 0)
        {
            rows = rows
                .Where(r => UserLocationScopeCalculos.LotePermitido(restringidos, r.GranjaId, r.LoteId))
                .ToList();
        }

        var filas = rows.Select(MapearFila).ToList();

        var lotes = filas
            .GroupBy(f => f.LoteId)
            .Select(g => new ReporteDiarioCostosPosturaLoteDto(
                g.Key,
                g.First().LoteNombre,
                g.First().GalponId,
                g.First().GalponNombre,
                g.First().LoteGalpon,
                g.First().GranjaId,
                g.First().GranjaNombre,
                g.First().LotePosturaBaseId,
                g.First().LoteBaseNombre))
            .OrderBy(l => l.GranjaNombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.LoteNombre, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ReporteDiarioCostosPosturaReporteDto(
            FiltrosAplicados: request,
            FechaDesdeEfectiva: request.FechaDesde?.Date ?? (filas.Count > 0 ? filas.Min(f => f.Fecha) : null),
            FechaHastaEfectiva: request.FechaHasta?.Date ?? (filas.Count > 0 ? filas.Max(f => f.Fecha) : null),
            Fases: ReporteDiarioCostosPosturaCalculos.FasesPresentes(filas),
            Lotes: lotes,
            Filas: filas,
            Totales: ReporteDiarioCostosPosturaCalculos.ConstruirTotales(filas));
    }

    /// <summary>Fila cruda de la fn → DTO: clasifica el huevo (D1) y explota el json de alimentos (D4).</summary>
    private static ReporteDiarioCostosPosturaFilaDto MapearFila(ReporteDiarioCostosPosturaRow r)
    {
        var loteNombre = r.LoteNombre ?? string.Empty;
        var galponNombre = r.GalponNombre ?? string.Empty;

        var huevo = ReporteDiarioCostosPosturaCalculos.ClasificarHuevo(
            new HuevoCrudo(
                Tot: r.HuevoTot,
                Inc: r.HuevoInc,
                Limpio: r.HuevoLimpio,
                Tratado: r.HuevoTratado,
                Sucio: r.HuevoSucio,
                Deforme: r.HuevoDeforme,
                Blanco: r.HuevoBlanco,
                DobleYema: r.HuevoDobleYema,
                Piso: r.HuevoPiso,
                Pequeno: r.HuevoPequeno,
                Roto: r.HuevoRoto,
                Desecho: r.HuevoDesecho,
                Otro: r.HuevoOtro),
            venta: r.HuevoVenta,
            trasladoPlanta: r.HuevoTrasladoPlanta);

        return new ReporteDiarioCostosPosturaFilaDto(
            Fecha: r.Fecha,
            Fase: r.Fase,
            LoteId: r.LoteId,
            LoteNombre: loteNombre,
            GalponId: r.GalponId ?? string.Empty,
            GalponNombre: galponNombre,
            LoteGalpon: ReporteDiarioCostosPosturaCalculos.EtiquetaLoteGalpon(loteNombre, galponNombre),
            NucleoId: r.NucleoId ?? string.Empty,
            GranjaId: r.GranjaId,
            GranjaNombre: r.GranjaNombre ?? string.Empty,
            Regional: r.Regional ?? string.Empty,
            LotePosturaBaseId: r.LotePosturaBaseId,
            LoteBaseNombre: r.LoteBaseNombre ?? string.Empty,
            EdadDias: r.EdadDias,
            Semana: r.Semana,
            MortalidadH: r.MortalidadH,
            MortalidadM: r.MortalidadM,
            SeleccionH: r.SeleccionH,
            SeleccionM: r.SeleccionM,
            ErrorSexajeH: r.ErrorSexajeH,
            ErrorSexajeM: r.ErrorSexajeM,
            VentaAvesH: r.VentaAvesH,
            VentaAvesM: r.VentaAvesM,
            ConsumoKgH: r.ConsumoKgH,
            ConsumoKgM: r.ConsumoKgM,
            Alimentos: ParseAlimentos(r.Alimentos),
            Huevo: huevo);
    }

    /// <summary>JSON inválido o vacío ⇒ lista vacía (nunca rompe el reporte).</summary>
    private static IReadOnlyList<ReporteDiarioCostosPosturaAlimentoDto> ParseAlimentos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<ReporteDiarioCostosPosturaAlimentoDto>();
        try
        {
            return JsonSerializer.Deserialize<List<ReporteDiarioCostosPosturaAlimentoDto>>(json, JsonOpts)
                ?? (IReadOnlyList<ReporteDiarioCostosPosturaAlimentoDto>)Array.Empty<ReporteDiarioCostosPosturaAlimentoDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<ReporteDiarioCostosPosturaAlimentoDto>();
        }
    }

    private static ReporteDiarioCostosPosturaReporteDto Vacio(ReporteDiarioCostosPosturaRequest request)
    {
        var filas = Array.Empty<ReporteDiarioCostosPosturaFilaDto>();
        return new ReporteDiarioCostosPosturaReporteDto(
            FiltrosAplicados: request,
            FechaDesdeEfectiva: request.FechaDesde?.Date,
            FechaHastaEfectiva: request.FechaHasta?.Date,
            Fases: Array.Empty<string>(),
            Lotes: Array.Empty<ReporteDiarioCostosPosturaLoteDto>(),
            Filas: filas,
            Totales: ReporteDiarioCostosPosturaCalculos.ConstruirTotales(filas));
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
}
