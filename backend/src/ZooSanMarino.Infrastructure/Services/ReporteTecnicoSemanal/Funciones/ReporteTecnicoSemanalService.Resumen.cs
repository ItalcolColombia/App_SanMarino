// Informe RA Pesadas — concern RESUMEN SEMANAL (hoja 1 del archivo).
// Una fila por lote para UNA semana calendario, para las dos etapas.
//
// La agregación entera vive en la BD (fn_resumen_semanal_ra_pesadas_levante /
// _produccion): son ~70 lotes × 25 semanas y llamar la fn por-lote desde acá
// colgaría el endpoint. Este service solo resuelve empresa, recorta por alcance
// y arma la respuesta.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoSemanalService
{
    public async Task<ResumenSemanalRaPesadasLevanteResponse> GenerarResumenLevanteAsync(
        ResumenSemanalRaPesadasRequest request, CancellationToken ct = default)
    {
        var companyId = await ResolveCompanyIdAsync();
        ValidarSemana(request);

        var granjaIds = request.GranjaIds is { Count: > 0 } ? request.GranjaIds.ToArray() : null;

        var filas = await _ctx.Database
            .SqlQueryRaw<ResumenSemanalLevanteRow>(
                "SELECT * FROM public.fn_resumen_semanal_ra_pesadas_levante(" +
                "{0}::int, {1}::int, {2}::int, {3}::int[], {4}::text, {5}::boolean)",
                companyId, request.Anio, request.SemanaAnio,
                (object?)granjaIds ?? DBNull.Value,
                (object?)request.Regional ?? DBNull.Value,
                request.ExcluirTrasladados)
            .ToListAsync(ct);

        filas = await RecortarPorAlcanceAsync(filas, f => f.GranjaId, f => f.LoteId);

        // La participación viene calculada sobre TODAS las filas de la fn; si el
        // alcance recortó lotes hay que recalcularla o los ponderados salen mal.
        ResumenSemanalRaPesadasCalculos.RecalcularParticipacionLevante(filas);

        var (inicio, fin) = ResumenSemanalRaPesadasCalculos.RangoSemanaExcel(request.Anio, request.SemanaAnio);

        return new ResumenSemanalRaPesadasLevanteResponse(
            Anio: request.Anio,
            SemanaAnio: request.SemanaAnio,
            FechaInicioSemana: inicio.ToDateTime(TimeOnly.MinValue),
            FechaFinSemana: fin.ToDateTime(TimeOnly.MinValue),
            Filas: filas,
            Totales: ResumenSemanalRaPesadasCalculos.TotalesLevante(filas));
    }

    public async Task<ResumenSemanalRaPesadasProduccionResponse> GenerarResumenProduccionAsync(
        ResumenSemanalRaPesadasRequest request, CancellationToken ct = default)
    {
        var companyId = await ResolveCompanyIdAsync();
        ValidarSemana(request);

        var granjaIds = request.GranjaIds is { Count: > 0 } ? request.GranjaIds.ToArray() : null;

        var filas = await _ctx.Database
            .SqlQueryRaw<ResumenSemanalProduccionRow>(
                "SELECT * FROM public.fn_resumen_semanal_ra_pesadas_produccion(" +
                "{0}::int, {1}::int, {2}::int, {3}::int[], {4}::text, {5}::text)",
                companyId, request.Anio, request.SemanaAnio,
                (object?)granjaIds ?? DBNull.Value,
                (object?)request.Regional ?? DBNull.Value,
                (object?)request.Ciclo ?? DBNull.Value)
            .ToListAsync(ct);

        filas = await RecortarPorAlcanceAsync(filas, f => f.GranjaId, f => f.LoteId);

        ResumenSemanalRaPesadasCalculos.RecalcularParticipacionProduccion(filas);

        var (inicio, fin) = ResumenSemanalRaPesadasCalculos.RangoSemanaExcel(request.Anio, request.SemanaAnio);

        return new ResumenSemanalRaPesadasProduccionResponse(
            Anio: request.Anio,
            SemanaAnio: request.SemanaAnio,
            FechaInicioSemana: inicio.ToDateTime(TimeOnly.MinValue),
            FechaFinSemana: fin.ToDateTime(TimeOnly.MinValue),
            Filas: filas,
            Totales: ResumenSemanalRaPesadasCalculos.TotalesProduccion(filas));
    }

    /// <summary>
    /// Curva consolidada del año: TODOS los lotes a lo largo de todas las edades.
    /// Reusa las MISMAS fns del Resumen con la semana en NULL (= todas) y pliega
    /// por edad en cálculo puro. El filtrado por empresa/granja/regional sigue
    /// haciéndose en la BD; en memoria solo queda el group-by final, que trabaja
    /// sobre un conjunto ya recortado.
    /// </summary>
    public async Task<CurvaConsolidadaResponse> GenerarCurvaConsolidadaAsync(
        CurvaConsolidadaRequest request, CancellationToken ct = default)
    {
        var companyId = await ResolveCompanyIdAsync();
        if (request.Anio is < 2000 or > 2100)
            throw new ArgumentException($"Año fuera de rango: {request.Anio}.");

        var etapa = ResumenSemanalRaPesadasCalculos.NormalizarEtapa(request.Etapa)
            ?? throw new ArgumentException("Etapa inválida. Use 'levante' o 'produccion'.");

        var granjaIds = request.GranjaIds is { Count: > 0 } ? request.GranjaIds.ToArray() : null;

        if (etapa == ResumenSemanalRaPesadasCalculos.EtapaLevante)
        {
            var filas = await _ctx.Database
                .SqlQueryRaw<ResumenSemanalLevanteRow>(
                    "SELECT * FROM public.fn_resumen_semanal_ra_pesadas_levante(" +
                    "{0}::int, {1}::int, NULL::int, {2}::int[], {3}::text, {4}::boolean)",
                    companyId, request.Anio,
                    (object?)granjaIds ?? DBNull.Value,
                    (object?)request.Regional ?? DBNull.Value,
                    request.ExcluirTrasladados)
                .ToListAsync(ct);

            filas = await RecortarPorAlcanceAsync(filas, f => f.GranjaId, f => f.LoteId);

            return new CurvaConsolidadaResponse(
                request.Anio, etapa,
                filas.Select(f => f.LoteId).Distinct().Count(),
                ResumenSemanalRaPesadasCalculos.ConsolidarPorEdadLevante(filas));
        }

        var filasProd = await _ctx.Database
            .SqlQueryRaw<ResumenSemanalProduccionRow>(
                "SELECT * FROM public.fn_resumen_semanal_ra_pesadas_produccion(" +
                "{0}::int, {1}::int, NULL::int, {2}::int[], {3}::text, {4}::text)",
                companyId, request.Anio,
                (object?)granjaIds ?? DBNull.Value,
                (object?)request.Regional ?? DBNull.Value,
                (object?)request.Ciclo ?? DBNull.Value)
            .ToListAsync(ct);

        filasProd = await RecortarPorAlcanceAsync(filasProd, f => f.GranjaId, f => f.LoteId);

        return new CurvaConsolidadaResponse(
            request.Anio, etapa,
            filasProd.Select(f => f.LotePosturaProduccionId).Distinct().Count(),
            ResumenSemanalRaPesadasCalculos.ConsolidarPorEdadProduccion(filasProd));
    }

    private static void ValidarSemana(ResumenSemanalRaPesadasRequest request)
    {
        if (request.Anio is < 2000 or > 2100)
            throw new ArgumentException($"Año fuera de rango: {request.Anio}.");
        // WEEKNUM de Excel llega a 54 en años que arrancan en sábado y son bisiestos.
        if (request.SemanaAnio is < 1 or > 54)
            throw new ArgumentException($"Semana del año fuera de rango: {request.SemanaAnio}. Debe estar entre 1 y 54.");
    }

    /// <summary>
    /// Recorta filas que el usuario no puede ver (alcance granular granja/lote).
    /// Sin restricciones no toca la lista.
    /// </summary>
    private async Task<List<T>> RecortarPorAlcanceAsync<T>(
        List<T> filas, Func<T, int> granjaId, Func<T, int?> loteId)
    {
        var restringidos = await _scopeResolver.GetAllRestrictedScopesAsync();
        if (restringidos.Count == 0) return filas;

        return filas
            .Where(f => UserLocationScopeCalculos.LotePermitido(restringidos, granjaId(f), loteId(f)))
            .ToList();
    }
}
