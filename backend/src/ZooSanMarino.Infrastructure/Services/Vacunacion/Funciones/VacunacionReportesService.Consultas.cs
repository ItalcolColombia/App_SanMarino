// Vacunacion/Funciones/VacunacionReportesService.Consultas.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionReportesService
{
    /// <inheritdoc />
    public async Task<List<VacunacionCumplimientoLoteDto>> GetCumplimientoAsync(
        VacunacionCumplimientoFiltroRequest req, CancellationToken ct = default)
    {
        var granjas = await ResolverGranjasPermitidasAsync(req.GranjaIds, ct);

        const string sql =
            "SELECT * FROM public.fn_vacunacion_cumplimiento_lote(" +
            "@p_company_id, @p_pais_id, @p_granja_ids, @p_nucleo_id, @p_galpon_id, @p_lote_ids, @p_linea_productiva, @p_fecha_desde, @p_fecha_hasta)";

        var rows = await _ctx.Database
            .SqlQueryRaw<VacunacionCumplimientoLoteRow>(sql, BuildReporteParams(req, granjas))
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

    /// <inheritdoc />
    public async Task<List<VacunacionCumplimientoDetalleDto>> GetCumplimientoDetalleAsync(
        VacunacionCumplimientoFiltroRequest req, CancellationToken ct = default)
    {
        var granjas = await ResolverGranjasPermitidasAsync(req.GranjaIds, ct);

        const string sql =
            "SELECT * FROM public.fn_vacunacion_cumplimiento_detalle(" +
            "@p_company_id, @p_pais_id, @p_granja_ids, @p_nucleo_id, @p_galpon_id, @p_lote_ids, @p_linea_productiva, @p_fecha_desde, @p_fecha_hasta)";

        var rows = await _ctx.Database
            .SqlQueryRaw<VacunacionCumplimientoDetalleRow>(sql, BuildReporteParams(req, granjas))
            .ToListAsync(ct);

        return rows.Select(r => new VacunacionCumplimientoDetalleDto(
            r.ItemId, r.GranjaId, r.GranjaNombre,
            r.LoteId, r.LoteNombre, r.LineaProductiva ?? "",
            r.NucleoId, r.GalponId,
            r.VacunaNombre ?? "", r.UnidadObjetivo ?? "", r.ValorObjetivo,
            r.FechaObjetivoEfectiva, r.FechaInicioFranja, r.FechaFinFranja,
            r.Estado ?? "Pendiente", r.FechaAplicacion, r.DiasDesviacion, r.Incumplido,
            r.Motivo, r.AplicadoPor, r.RegistradoPor, r.Notas
        )).ToList();
    }
}
