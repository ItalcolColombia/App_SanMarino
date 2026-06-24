using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio delgado: delega TODO el cálculo a fn_informe_semanal_pollo_engorde(...).
/// Sólo arma parámetros, mapea filas y arma el CONSOLIDADO por semana.
/// </summary>
public class InformeSemanalPolloEngordeService : IInformeSemanalPolloEngordeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;

    public InformeSemanalPolloEngordeService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    public async Task<InformeSemanalReporteDto> GenerarAsync(InformeSemanalRequest request, CancellationToken ct = default)
    {
        var pCompany = new NpgsqlParameter("p_company_id", NpgsqlDbType.Integer) { Value = _currentUser.CompanyId };
        var pGranjas = new NpgsqlParameter("p_granja_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer)
        {
            Value = (request.GranjaIds is { Length: > 0 }) ? request.GranjaIds : (object)DBNull.Value
        };
        var pNucleo = new NpgsqlParameter("p_nucleo_id", NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(request.NucleoId) ? DBNull.Value : request.NucleoId
        };
        var pGalpon = new NpgsqlParameter("p_galpon_id", NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(request.GalponId) ? DBNull.Value : request.GalponId
        };
        var pLote = new NpgsqlParameter("p_lote_id", NpgsqlDbType.Integer)
        {
            Value = request.LoteId.HasValue ? request.LoteId.Value : (object)DBNull.Value
        };
        var pDesde = new NpgsqlParameter("p_fecha_desde", NpgsqlDbType.Date)
        {
            Value = request.FechaDesde.HasValue ? request.FechaDesde.Value : (object)DBNull.Value
        };
        var pHasta = new NpgsqlParameter("p_fecha_hasta", NpgsqlDbType.Date)
        {
            Value = request.FechaHasta.HasValue ? request.FechaHasta.Value : (object)DBNull.Value
        };

        const string sql =
            "SELECT * FROM public.fn_informe_semanal_pollo_engorde(" +
            "@p_company_id, @p_granja_ids, @p_nucleo_id, @p_galpon_id, @p_lote_id, @p_fecha_desde, @p_fecha_hasta)";

        var rows = await _ctx.Database
            .SqlQueryRaw<InformeSemanalRow>(sql, pCompany, pGranjas, pNucleo, pGalpon, pLote, pDesde, pHasta)
            .ToListAsync(ct);

        var grupos = rows
            .GroupBy(r => r.Semana)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var filas = g
                    .OrderBy(r => r.GranjaNombre)
                    .ThenBy(r => r.LoteNombre)
                    .Select(r => r.ToDto())
                    .ToList();
                return new InformeSemanalGrupoSemanaDto(g.Key, filas, Consolidar(g.Key, filas));
            })
            .ToList();

        return new InformeSemanalReporteDto(request, rows.Count, grupos);
    }

    /// <summary>CONSOLIDADO de la semana: AVES = suma; tasas/pesos/consumo = promedio (espejo del Excel).</summary>
    private static InformeSemanalConsolidadoDto Consolidar(int semana, IReadOnlyList<InformeSemanalFilaDto> filas) => new(
        Semana: semana,
        CantidadLotes: filas.Count,
        AvesTotales: filas.Sum(f => f.AvesEncasetadas),
        ConsumoRealGAveProm: Avg(filas.Select(f => (decimal?)f.ConsumoRealGAve)) ?? 0m,
        PesoRealGProm: Avg(filas.Select(f => f.PesoRealG)),
        GananciaRealGProm: Avg(filas.Select(f => f.GananciaRealG)),
        ConversionRealProm: Avg(filas.Select(f => f.ConversionReal)),
        MortNaturalPctProm: Avg(filas.Select(f => (decimal?)f.MortNaturalPct)) ?? 0m,
        SeleccionPctProm: Avg(filas.Select(f => (decimal?)f.SeleccionPct)) ?? 0m,
        MortalidadTotalPctProm: Avg(filas.Select(f => (decimal?)f.MortalidadTotalPct)) ?? 0m,
        ConsumoSemanaKgTotal: filas.Sum(f => f.ConsumoSemanaKg),
        VentasKgTotal: filas.Sum(f => f.VentasKg),
        VentasUnidTotal: filas.Sum(f => f.VentasUnid),
        ConsumoTablaGProm: Avg(filas.Select(f => f.ConsumoTablaG)),
        PesoTablaGProm: Avg(filas.Select(f => f.PesoTablaG)),
        GananciaTablaGProm: Avg(filas.Select(f => f.GananciaTablaG)),
        ConversionTablaProm: Avg(filas.Select(f => f.ConversionTabla)),
        MortalidadTablaPctProm: Avg(filas.Select(f => f.MortalidadTablaPct)),
        PctConsumoProm: Avg(filas.Select(f => f.PctConsumo)),
        PctPesoProm: Avg(filas.Select(f => f.PctPeso)),
        PctConversionProm: Avg(filas.Select(f => f.PctConversion))
    );

    private static decimal? Avg(IEnumerable<decimal?> values)
    {
        var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return list.Count == 0 ? (decimal?)null : list.Average();
    }
}
