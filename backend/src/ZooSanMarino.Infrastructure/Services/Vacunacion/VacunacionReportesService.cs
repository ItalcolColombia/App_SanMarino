// Vacunacion/VacunacionReportesService.cs
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>Cumplimiento de vacunación: envoltorio C# de fn_vacunacion_cumplimiento_lote (backend/sql/).</summary>
public sealed class VacunacionReportesService : IVacunacionReportesService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;

    public VacunacionReportesService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<List<VacunacionCumplimientoLoteDto>> GetCumplimientoAsync(
        VacunacionCumplimientoFiltroRequest req, CancellationToken ct = default)
    {
        var pCompany = new NpgsqlParameter("p_company_id", NpgsqlDbType.Integer) { Value = _currentUser.CompanyId };
        var pPais = new NpgsqlParameter("p_pais_id", NpgsqlDbType.Integer)
        {
            Value = _currentUser.PaisId.HasValue ? _currentUser.PaisId.Value : DBNull.Value
        };
        var pGranjas = new NpgsqlParameter("p_granja_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer)
        {
            Value = (req.GranjaIds is { Count: > 0 }) ? req.GranjaIds.ToArray() : (object)DBNull.Value
        };
        var pNucleo = new NpgsqlParameter("p_nucleo_id", NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(req.NucleoId) ? DBNull.Value : req.NucleoId
        };
        var pGalpon = new NpgsqlParameter("p_galpon_id", NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(req.GalponId) ? DBNull.Value : req.GalponId
        };
        var pLotes = new NpgsqlParameter("p_lote_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer)
        {
            Value = (req.LoteIds is { Count: > 0 }) ? req.LoteIds.ToArray() : (object)DBNull.Value
        };
        var pLinea = new NpgsqlParameter("p_linea_productiva", NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(req.LineaProductiva) ? DBNull.Value : req.LineaProductiva
        };
        var pDesde = new NpgsqlParameter("p_fecha_desde", NpgsqlDbType.Date)
        {
            Value = req.FechaDesde.HasValue ? req.FechaDesde.Value.Date : DBNull.Value
        };
        var pHasta = new NpgsqlParameter("p_fecha_hasta", NpgsqlDbType.Date)
        {
            Value = req.FechaHasta.HasValue ? req.FechaHasta.Value.Date : DBNull.Value
        };

        const string sql =
            "SELECT * FROM public.fn_vacunacion_cumplimiento_lote(" +
            "@p_company_id, @p_pais_id, @p_granja_ids, @p_nucleo_id, @p_galpon_id, @p_lote_ids, @p_linea_productiva, @p_fecha_desde, @p_fecha_hasta)";

        var rows = await _ctx.Database
            .SqlQueryRaw<VacunacionCumplimientoLoteRow>(sql, pCompany, pPais, pGranjas, pNucleo, pGalpon, pLotes, pLinea, pDesde, pHasta)
            .ToListAsync(ct);

        return rows.Select(r => new VacunacionCumplimientoLoteDto(
            r.LoteId, r.LoteNombre ?? "", r.LineaProductiva ?? "",
            r.GranjaId, r.GranjaNombre,
            r.TotalProgramadas, r.TotalATiempo, r.TotalTardio1Semana, r.TotalTardio2MasSemanas,
            r.TotalNoAplicado, r.TotalPendiente,
            r.PorcentajeATiempo ?? 0, r.PorcentajeTardio ?? 0, r.PorcentajeNoAplicado ?? 0,
            r.PromedioDiasAtraso
        )).ToList();
    }
}
