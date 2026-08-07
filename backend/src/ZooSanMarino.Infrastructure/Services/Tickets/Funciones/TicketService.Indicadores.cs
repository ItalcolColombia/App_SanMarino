using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Panel de control del administrador: indicadores del conjunto filtrado y el reporte detallado
/// que alimenta la descarga a Excel.
/// </summary>
/// <remarks>
/// La BD filtra y proyecta (incluidos los agregados de tareas y tiempos como subconsultas);
/// el reparto y los promedios los hace <see cref="TicketIndicadoresCalculos"/>, que es puro y
/// está cubierto por tests. La proyección es deliberadamente flaca —sin título, descripción ni
/// base64— para que traer TODO el conjunto filtrado siga siendo barato.
/// </remarks>
public partial class TicketService
{
    /// <summary>Tope de filas del reporte detallado: más que esto no se abre cómodo en Excel.</summary>
    private const int MaxFilasReporte = 5000;

    // ───────────────────────────── INDICADORES ─────────────────────────────

    public async Task<TicketIndicadoresDto> GetIndicadoresAsync(TicketTableroFiltro filtro, CancellationToken ct)
    {
        var filas = await ProyectarFilasIndicadorAsync(AplicarFiltroTablero(filtro), ct);
        return ConstruirIndicadores(filas);
    }

    /// <summary>Proyección flaca de los casos del filtro, con los agregados resueltos en la BD.</summary>
    private async Task<List<TicketIndicadoresCalculos.FilaCaso>> ProyectarFilasIndicadorAsync(
        IQueryable<Ticket> query, CancellationToken ct)
    {
        var crudas = await query
            .Select(x => new
            {
                x.Id, x.PaisId, x.CompanyId, x.Tipo, x.Estado, x.Prioridad,
                x.AssignedToUserGuid, x.CreatedAt, x.FechaPrimeraApertura,
                x.FechaSolucion, x.FechaCierreSolicitante, x.FechaLimite, x.HorasEstimadas,
                HorasRegistradas = x.Tiempos.Where(t => t.DeletedAt == null).Sum(t => (decimal?)t.Horas) ?? 0m,
                TareasTotal = x.Tareas.Count(t => t.DeletedAt == null),
                TareasListas = x.Tareas.Count(t => t.DeletedAt == null && t.Estado == TicketTareaEstados.Listo),
            })
            .ToListAsync(ct);

        var paises = await BuildPaisMapAsync(crudas.Select(c => c.PaisId), ct);
        var empresas = await BuildEmpresaMapAsync(crudas.Select(c => c.CompanyId), ct);
        var refs = crudas.Where(c => c.AssignedToUserGuid.HasValue)
            .Select(c => (c.AssignedToUserGuid!.Value, c.CompanyId)).ToList();
        var (users, _) = await BuildUserInfoAsync(refs, ct);

        return crudas.Select(c => new TicketIndicadoresCalculos.FilaCaso(
            c.Id, c.PaisId, paises.GetValueOrDefault(c.PaisId),
            c.CompanyId, empresas.GetValueOrDefault(c.CompanyId),
            c.Tipo, c.Estado, c.Prioridad,
            c.AssignedToUserGuid, NombreDe(users, c.AssignedToUserGuid),
            c.CreatedAt, c.FechaPrimeraApertura, c.FechaSolucion, c.FechaCierreSolicitante,
            c.FechaLimite, c.HorasEstimadas, c.HorasRegistradas,
            c.TareasTotal, c.TareasListas)).ToList();
    }

    /// <summary>Arma el DTO del panel delegando TODO el cálculo en la clase pura.</summary>
    private static TicketIndicadoresDto ConstruirIndicadores(List<TicketIndicadoresCalculos.FilaCaso> filas)
    {
        var ahora = DateTime.UtcNow;
        var r = TicketIndicadoresCalculos.CalcularResumen(filas, ahora);

        return new TicketIndicadoresDto(
            new TicketResumenIndicadoresDto(
                r.Total, r.Abiertos, r.EnCurso, r.Solucionados, r.Cerrados, r.Suspendidos,
                r.Vencidos, r.PorVencer, r.SinAsignar,
                r.TareasTotal, r.TareasListas, r.TareasPendientes,
                r.HorasEstimadas, r.HorasRegistradas,
                r.PromedioPrimeraRespuesta, r.PromedioResolucion, r.PromedioConfirmacionCierre,
                r.Efectividad, r.PorcentajeResueltos, r.AvanceTareas,
                r.ConCompromiso, r.CompromisoCumplido),

            TicketIndicadoresCalculos.PorPais(filas, ahora).Select(AGrupo).ToList(),
            TicketIndicadoresCalculos.PorEmpresa(filas, ahora).Select(AGrupo).ToList(),

            TicketIndicadoresCalculos.PorEstado(filas, ahora)
                .Select(c => ACategoria(c, TicketTimelineCalculos.EtiquetaEstado(c.Clave))).ToList(),

            TicketIndicadoresCalculos.PorCategoria(filas, f => f.Tipo, ahora)
                .Select(c => ACategoria(c, c.Clave)).ToList(),

            TicketIndicadoresCalculos.PorCategoria(filas, f => f.Prioridad, ahora)
                .Select(c => ACategoria(c, c.Clave)).ToList(),

            TicketIndicadoresCalculos.PorResponsable(filas, ahora)
                .Select(x => new TicketIndicadorResponsableDto(
                    x.Guid, x.Nombre, x.Asignados, x.Resueltos, x.Vencidos,
                    x.HorasRegistradas, x.TareasListas, x.PromedioResolucion))
                .ToList());
    }

    private static TicketIndicadorCategoriaDto ACategoria(
        TicketIndicadoresCalculos.FilaCategoria c, string label) =>
        new(c.Clave, label, c.Total, c.Resueltos, c.Vencidos, c.PromedioResolucion);

    private static TicketIndicadorGrupoDto AGrupo(TicketIndicadoresCalculos.FilaGrupo g) =>
        new(g.Id, g.Nombre, g.Total, g.Abiertos, g.EnCurso, g.Resueltos,
            g.Vencidos, g.HorasRegistradas, g.AvanceTareas, g.PromedioResolucion, g.Efectividad);

    // ───────────────────────────── REPORTE DETALLADO ─────────────────────────────

    public async Task<TicketReporteDto> GetReporteAsync(TicketTableroFiltro filtro, CancellationToken ct)
    {
        var query = AplicarFiltroTablero(filtro);

        // Los indicadores se calculan sobre el conjunto COMPLETO, aunque el detalle se recorte.
        var filasIndicador = await ProyectarFilasIndicadorAsync(query, ct);
        var indicadores = ConstruirIndicadores(filasIndicador);

        var casosCrudos = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(MaxFilasReporte)
            .Select(x => new
            {
                x.Id, x.Codigo, x.PaisId, x.CompanyId, x.Tipo, x.Estado, x.Prioridad, x.Titulo,
                x.CreatedByUserGuid, x.CreatedByUserId, x.SolicitanteUserGuid, x.SolicitanteUserId,
                x.AssignedToUserGuid, x.AssignedToUserId,
                x.CreatedAt, x.FechaPrimeraApertura, x.FechaSolucion, x.FechaCierreSolicitante,
                x.FechaLimite, x.FechaInicioPlan, x.FechaFinPlan,
                x.HorasEstimadas, x.SolucionDescripcion,
                HorasRegistradas = x.Tiempos.Where(t => t.DeletedAt == null).Sum(t => (decimal?)t.Horas) ?? 0m,
                TareasTotal = x.Tareas.Count(t => t.DeletedAt == null),
                TareasListas = x.Tareas.Count(t => t.DeletedAt == null && t.Estado == TicketTareaEstados.Listo),
            })
            .ToListAsync(ct);

        var ids = casosCrudos.Select(c => c.Id).ToList();

        // Identidad: guids (creador / solicitante / responsable) y cédulas de respaldo.
        var refs = new List<(Guid, int)>();
        foreach (var c in casosCrudos)
        {
            if (c.CreatedByUserGuid.HasValue)    refs.Add((c.CreatedByUserGuid.Value, c.CompanyId));
            if (c.SolicitanteUserGuid.HasValue)  refs.Add((c.SolicitanteUserGuid.Value, c.CompanyId));
            if (c.AssignedToUserGuid.HasValue)   refs.Add((c.AssignedToUserGuid.Value, c.CompanyId));
        }
        var (users, _) = await BuildUserInfoAsync(refs, ct);
        var paises = await BuildPaisMapAsync(casosCrudos.Select(c => c.PaisId), ct);
        var empresas = await BuildEmpresaMapAsync(casosCrudos.Select(c => c.CompanyId), ct);

        var ahora = DateTime.UtcNow;
        var casos = casosCrudos.Select(c =>
        {
            var solicitanteGuid = c.SolicitanteUserGuid ?? c.CreatedByUserGuid;
            return new TicketReporteCasoDto(
                c.Id, c.Codigo, paises.GetValueOrDefault(c.PaisId), empresas.GetValueOrDefault(c.CompanyId),
                c.Tipo, c.Estado, c.Prioridad, c.Titulo,
                NombreDe(users, solicitanteGuid), EmailDe(users, solicitanteGuid),
                NombreDe(users, c.CreatedByUserGuid), NombreDe(users, c.AssignedToUserGuid),
                c.CreatedAt, c.FechaPrimeraApertura, c.FechaSolucion, c.FechaCierreSolicitante,
                c.FechaLimite,
                TicketMetricasCalculos.EstadoSla(c.FechaLimite, c.FechaSolucion, ahora),
                TicketMetricasCalculos.HorasPrimeraRespuesta(c.CreatedAt, c.FechaPrimeraApertura),
                TicketMetricasCalculos.HorasResolucion(c.CreatedAt, c.FechaSolucion, ahora),
                c.FechaInicioPlan, c.FechaFinPlan,
                c.HorasEstimadas, c.HorasRegistradas,
                TicketMetricasCalculos.DesvioHoras(c.HorasEstimadas, c.HorasRegistradas),
                c.TareasTotal, c.TareasListas,
                TicketMetricasCalculos.PorcentajeAvanceTareas(c.TareasTotal, c.TareasListas),
                c.SolucionDescripcion);
        }).ToList();

        var tareas = await ProyectarTareasReporteAsync(ids, paises, ct);
        var tiempos = await ProyectarTiemposReporteAsync(ids, paises, ct);

        return new TicketReporteDto(indicadores, casos, tareas, tiempos,
            DescribirFiltros(filtro, paises, empresas));
    }

    private async Task<List<TicketReporteTareaDto>> ProyectarTareasReporteAsync(
        List<long> ticketIds, Dictionary<int, string> paises, CancellationToken ct)
    {
        if (ticketIds.Count == 0) return new List<TicketReporteTareaDto>();

        var rows = await _ctx.TicketTareas.AsNoTracking()
            .Where(t => ticketIds.Contains(t.TicketId) && t.DeletedAt == null)
            .OrderBy(t => t.TicketId).ThenBy(t => t.Id)
            .Take(MaxFilasReporte)
            .Select(t => new
            {
                t.Codigo, t.Tipo, t.Estado, t.Prioridad, t.Titulo, t.AsignadoUserGuid,
                t.HorasEstimadas, t.FechaInicioPlan, t.FechaFinPlan,
                t.FechaInicioReal, t.FechaFinReal, t.CreatedAt,
                CodigoCaso = t.Ticket!.Codigo, TituloCaso = t.Ticket!.Titulo,
                PaisId = t.Ticket!.PaisId, CompanyId = t.Ticket!.CompanyId,
                HorasRegistradas = t.Tiempos.Where(w => w.DeletedAt == null).Sum(w => (decimal?)w.Horas) ?? 0m,
            })
            .ToListAsync(ct);

        var refs = rows.Where(r => r.AsignadoUserGuid.HasValue)
            .Select(r => (r.AsignadoUserGuid!.Value, r.CompanyId)).ToList();
        var (users, _) = await BuildUserInfoAsync(refs, ct);

        return rows.Select(r => new TicketReporteTareaDto(
            r.CodigoCaso, r.TituloCaso, paises.GetValueOrDefault(r.PaisId),
            r.Codigo, r.Tipo, r.Estado, r.Prioridad, r.Titulo,
            NombreDe(users, r.AsignadoUserGuid), r.HorasEstimadas, r.HorasRegistradas,
            r.FechaInicioPlan, r.FechaFinPlan, r.FechaInicioReal, r.FechaFinReal, r.CreatedAt)).ToList();
    }

    private async Task<List<TicketReporteTiempoDto>> ProyectarTiemposReporteAsync(
        List<long> ticketIds, Dictionary<int, string> paises, CancellationToken ct)
    {
        if (ticketIds.Count == 0) return new List<TicketReporteTiempoDto>();

        var rows = await _ctx.TicketTiempos.AsNoTracking()
            .Where(w => ticketIds.Contains(w.TicketId) && w.DeletedAt == null)
            .OrderByDescending(w => w.Fecha).ThenBy(w => w.Id)
            .Take(MaxFilasReporte)
            .Select(w => new
            {
                w.UserGuid, w.UserId, w.Fecha, w.Horas, w.Descripcion,
                Tarea = w.Tarea != null ? w.Tarea.Titulo : null,
                CodigoCaso = w.Ticket!.Codigo, TituloCaso = w.Ticket!.Titulo,
                PaisId = w.Ticket!.PaisId, CompanyId = w.Ticket!.CompanyId,
            })
            .ToListAsync(ct);

        var refs = rows.Where(r => r.UserGuid.HasValue)
            .Select(r => (r.UserGuid!.Value, r.CompanyId)).ToList();
        var (users, _) = await BuildUserInfoAsync(refs, ct);
        var cedInfo = await BuildNotaUserInfoAsync(
            rows.Select(r => r.UserId).Where(x => x != 0).Distinct().ToList(),
            rows.Count > 0 ? rows[0].CompanyId : 0, ct);

        return rows.Select(r => new TicketReporteTiempoDto(
            r.CodigoCaso, r.TituloCaso, paises.GetValueOrDefault(r.PaisId), r.Tarea,
            NombreDe(users, r.UserGuid) ?? (cedInfo.TryGetValue(r.UserId, out var i) ? i.Nombre : null),
            r.Fecha, r.Horas, r.Descripcion)).ToList();
    }

    /// <summary>Mapea companyId → nombre, para la columna «Empresa» del reporte.</summary>
    private async Task<Dictionary<int, string>> BuildEmpresaMapAsync(IEnumerable<int> companyIds, CancellationToken ct)
    {
        var ids = companyIds.Where(c => c > 0).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, string>();
        return await _ctx.Companies.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }

    /// <summary>Filtros en texto, para que el Excel diga sobre qué recorte se sacó.</summary>
    private static List<string> DescribirFiltros(
        TicketTableroFiltro f, Dictionary<int, string> paises, Dictionary<int, string> empresas)
    {
        var partes = new List<string>();

        if (f.Desde.HasValue || f.Hasta.HasValue)
            partes.Add($"Creados entre {f.Desde?.ToString("dd/MM/yyyy") ?? "el inicio"} y {f.Hasta?.ToString("dd/MM/yyyy") ?? "hoy"}");
        else if (f.Anio.HasValue)
            partes.Add($"Año {f.Anio}");

        if (f.PaisIds is { Count: > 0 })
            partes.Add("Países: " + string.Join(", ", f.PaisIds.Select(p => paises.GetValueOrDefault(p, $"#{p}"))));
        else if (f.PaisId.HasValue)
            partes.Add("País: " + paises.GetValueOrDefault(f.PaisId.Value, $"#{f.PaisId}"));

        if (f.CompanyIds is { Count: > 0 })
            partes.Add("Empresas: " + string.Join(", ", f.CompanyIds.Select(c => empresas.GetValueOrDefault(c, $"#{c}"))));
        else if (f.CompanyId.HasValue)
            partes.Add("Empresa: " + empresas.GetValueOrDefault(f.CompanyId.Value, $"#{f.CompanyId}"));

        if (!string.IsNullOrWhiteSpace(f.Tipo))      partes.Add($"Tipo: {f.Tipo}");
        if (!string.IsNullOrWhiteSpace(f.Estado))    partes.Add($"Estado: {f.Estado}");
        if (!string.IsNullOrWhiteSpace(f.Prioridad)) partes.Add($"Prioridad: {f.Prioridad}");
        if (!string.IsNullOrWhiteSpace(f.EstadoSla)) partes.Add($"SLA: {f.EstadoSla}");
        if (!string.IsNullOrWhiteSpace(f.Texto))     partes.Add($"Búsqueda: «{f.Texto}»");
        if (f.AssignedToGuid.HasValue)               partes.Add("Filtrado por responsable");

        if (partes.Count == 0) partes.Add("Sin filtros: todos los casos visibles");
        return partes;
    }
}
