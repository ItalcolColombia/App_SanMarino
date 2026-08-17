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

        // Alcance granular: solo se resuelve para granjas RESTRINGIDAS (diccionario vacío = sin cambios)
        var visibles = await ResolverLotesVisiblesPorGranjaRestringidaAsync(granjas, ct);

        // 🔴 Los dos alias NO son cosmética: sin ellos el endpoint REVIENTA en runtime.
        // La función devuelve `total_tardio_1_semana` / `total_tardio_2_mas_semanas`, pero la
        // convención snake_case de EF traduce TotalTardio1Semana → `total_tardio1semana` (no mete
        // guión bajo después de un dígito) y SqlQueryRaw exige esa columna exacta: "The required
        // column 'total_tardio1semana' was not present". Se aliasa acá —y no se renombra la fn ni
        // el DTO— porque el DTO viaja al front y la fn la comparten reportes ya desplegados.
        const string sql =
            "SELECT c.*, " +
            "c.total_tardio_1_semana AS total_tardio1semana, " +
            "c.total_tardio_2_mas_semanas AS total_tardio2mas_semanas " +
            "FROM public.fn_vacunacion_cumplimiento_lote(" +
            "@p_company_id, @p_pais_id, @p_granja_ids, @p_nucleo_id, @p_galpon_id, @p_lote_ids, @p_linea_productiva, @p_fecha_desde, @p_fecha_hasta) c";

        var rows = await _ctx.Database
            .SqlQueryRaw<VacunacionCumplimientoLoteRow>(sql, BuildReporteParams(req, granjas))
            .ToListAsync(ct);

        if (visibles.Count > 0)
            rows = rows.Where(r => FilaVisible(visibles, r.GranjaId, r.LoteId)).ToList();

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

        // Alcance granular: solo se resuelve para granjas RESTRINGIDAS (diccionario vacío = sin cambios)
        var visibles = await ResolverLotesVisiblesPorGranjaRestringidaAsync(granjas, ct);

        const string sql =
            "SELECT * FROM public.fn_vacunacion_cumplimiento_detalle(" +
            "@p_company_id, @p_pais_id, @p_granja_ids, @p_nucleo_id, @p_galpon_id, @p_lote_ids, @p_linea_productiva, @p_fecha_desde, @p_fecha_hasta)";

        var rows = await _ctx.Database
            .SqlQueryRaw<VacunacionCumplimientoDetalleRow>(sql, BuildReporteParams(req, granjas))
            .ToListAsync(ct);

        if (visibles.Count > 0)
            rows = rows.Where(r => FilaVisible(visibles, r.GranjaId, r.LoteId)).ToList();

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
