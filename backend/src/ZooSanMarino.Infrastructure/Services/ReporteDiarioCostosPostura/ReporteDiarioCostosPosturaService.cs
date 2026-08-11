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

        // ── El LOTE BASE manda sobre la granja ────────────────────────────────
        // Trasladar un lote completo a otra granja crea un lote NUEVO allá (historial_traslado_lote),
        // así que un lote base puede tener el levante en una granja y la producción en otra. Si el
        // usuario eligió un lote base, la granja (y la regional) pasan a ser el punto de entrada y el
        // reporte sigue al lote base a donde haya vivido — SIEMPRE dentro de las granjas asignadas,
        // que es lo que conserva el fail-closed.
        var seguirLoteBase = request.LotePosturaBaseId.HasValue;

        var granjaIds = farms.Select(f => f.Id).ToList();
        if (request.GranjaId.HasValue)
        {
            var granja = farms.FirstOrDefault(f => f.Id == request.GranjaId.Value)
                ?? throw new InvalidOperationException(
                    "La granja no existe, no pertenece a la compañía o no está asignada a su usuario.");
            if (!seguirLoteBase)
                granjaIds = new List<int> { granja.Id };
        }

        // Sin granjas visibles ⇒ reporte vacío. Jamás "todas" (el NULL de la fn es para scripts).
        if (granjaIds.Count == 0)
            return Vacio(request);

        var fase = ReporteDiarioCostosPosturaCalculos.NormalizarFase(request.Fase);
        var regional = seguirLoteBase || string.IsNullOrWhiteSpace(request.Regional)
            ? null
            : request.Regional!.Trim();

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

        // ── Traslape levante/producción ───────────────────────────────────────
        // Un día que quedó registrado en las dos etapas con consumo o bajas se contaría dos veces
        // (K345: 14 días de 2025 con ~16.952 kg duplicados). La regla la escribe
        // CorteEtapaPosturaCalculos; acá solo se aplica: las filas siguen visibles, la de levante
        // deja de sumar.
        var filas = ReporteDiarioCostosPosturaCalculos.MarcarDuplicados(
            rows.Select(MapearFila).ToList());

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
            Totales: ReporteDiarioCostosPosturaCalculos.ConstruirTotales(filas),
            Ubicaciones: ReporteDiarioCostosPosturaCalculos.Ubicaciones(filas),
            DiasDuplicados: ReporteDiarioCostosPosturaCalculos.ContarDuplicados(filas),
            TotalesExcluidos: ReporteDiarioCostosPosturaCalculos.TotalesExcluidos(filas),
            // Solo se avisa si la expansión REALMENTE trajo otra granja: pedir NIZA III y recibir
            // únicamente NIZA III no es una expansión que valga la pena contar.
            AlcanceExpandidoPorLoteBase: seguirLoteBase
                && request.GranjaId.HasValue
                && filas.Any(f => f.GranjaId != request.GranjaId.Value));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReporteDiarioCostosPosturaLoteBaseOpcionDto>> LotesBaseAsync(
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        if (!_current.UserGuid.HasValue)
            throw new UnauthorizedAccessException("Sesión inválida. Inicie sesión de nuevo.");

        var farms = (await _farmService.GetAllAsync(_current.UserGuid, companyId)).ToList();
        var granjaIds = farms.Select(f => f.Id).ToList();
        if (granjaIds.Count == 0)
            return Array.Empty<ReporteDiarioCostosPosturaLoteBaseOpcionDto>();

        // El catálogo se arma por DÓNDE ESTÁN LOS LOTES, no por lote_postura_base.farm_id: si el lote
        // se traslada, la base tiene que seguir apareciendo bajo la granja donde se hizo el levante.
        // El filtro por empresa y granjas asignadas va a la BD; solo se agrupa en memoria (el universo
        // ya viene recortado a los lotes de postura del usuario).
        var crudo = await (
            from l in _ctx.Lotes
            join b in _ctx.LotePosturaBases on l.LotePosturaBaseId equals (int?)b.LotePosturaBaseId
            join f in _ctx.Farms on l.GranjaId equals f.Id
            where l.CompanyId == companyId
               && l.DeletedAt == null
               && b.DeletedAt == null
               && f.DeletedAt == null
               && granjaIds.Contains(l.GranjaId)
            select new
            {
                b.LotePosturaBaseId,
                BaseNombre = b.LoteNombre,
                l.GranjaId,
                GranjaNombre = f.Name,
                l.LoteId
            }).ToListAsync(ct);

        // Alcance granular por ubicación: en granjas restringidas se recortan los lotes no visibles.
        var restringidos = await _scopeResolver.GetRestrictedScopesAsync(granjaIds);
        if (restringidos.Count > 0)
        {
            crudo = crudo
                .Where(x => UserLocationScopeCalculos.LotePermitido(restringidos, x.GranjaId, x.LoteId))
                .ToList();
        }

        return crudo
            .GroupBy(x => (x.LotePosturaBaseId, x.BaseNombre))
            .Select(g => new ReporteDiarioCostosPosturaLoteBaseOpcionDto(
                LotePosturaBaseId: g.Key.LotePosturaBaseId,
                LoteNombre: g.Key.BaseNombre ?? string.Empty,
                GranjaIds: g.Select(x => x.GranjaId).Distinct().OrderBy(id => id).ToList(),
                GranjaNombres: g.Select(x => x.GranjaNombre ?? string.Empty)
                                .Where(n => n.Length > 0)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                                .ToList(),
                Lotes: g.Select(x => x.LoteId).Distinct().Count()))
            .OrderBy(o => o.LoteNombre, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
